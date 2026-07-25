using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ConduitLLM.Providers.Authentication
{
    /// <summary>
    /// The AWS credential material used to produce a Signature Version 4 signature.
    /// </summary>
    /// <param name="AccessKeyId">The AWS access key ID (for example <c>AKIA…</c> or a temporary <c>ASIA…</c>).</param>
    /// <param name="SecretAccessKey">The AWS secret access key paired with <paramref name="AccessKeyId"/>.</param>
    /// <param name="SessionToken">The STS session token when the credentials are temporary; null for long-term keys.</param>
    public sealed record AwsSigV4Credentials(
        string AccessKeyId,
        string SecretAccessKey,
        string? SessionToken = null);

    /// <summary>
    /// Signs HTTP requests with AWS Signature Version 4, the request authentication scheme used by
    /// AWS services such as Bedrock. Implemented directly so provider adapters do not take a
    /// dependency on the AWS SDK for what is a pure request transformation.
    /// </summary>
    /// <remarks>
    /// The signature is computed over a canonical form of the request
    /// (https://docs.aws.amazon.com/IAM/latest/UserGuide/create-signed-request.html) and applied as
    /// the <c>Authorization</c> header together with <c>x-amz-date</c> and, for temporary
    /// credentials, <c>x-amz-security-token</c>. The headers included in the signature are
    /// <c>host</c>, <c>content-type</c> (when the request carries one), <c>x-amz-date</c>, and
    /// <c>x-amz-security-token</c> (when present) — the minimal set services require, kept small so
    /// intermediaries that add headers cannot invalidate the signature.
    /// </remarks>
    public static class AwsSigV4Signer
    {
        private const string Algorithm = "AWS4-HMAC-SHA256";

        /// <summary>
        /// Signs the request in place for the given service and region.
        /// </summary>
        /// <param name="request">The request to sign. Must have an absolute <see cref="HttpRequestMessage.RequestUri"/>.</param>
        /// <param name="payload">The exact request body bytes; use an empty array for bodyless requests.</param>
        /// <param name="credentials">The AWS credentials to sign with.</param>
        /// <param name="region">The AWS region forming the credential scope (for example <c>us-east-1</c>).</param>
        /// <param name="service">The signing service name (for example <c>bedrock-runtime</c>).</param>
        /// <param name="signingTime">The instant to stamp into <c>x-amz-date</c>; pass the current UTC time outside tests.</param>
        public static void Sign(
            HttpRequestMessage request,
            byte[] payload,
            AwsSigV4Credentials credentials,
            string region,
            string service,
            DateTimeOffset signingTime)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(payload);
            ArgumentNullException.ThrowIfNull(credentials);

            var uri = request.RequestUri
                ?? throw new ArgumentException("The request must have an absolute URI to be signed.", nameof(request));

            var amzDate = signingTime.UtcDateTime.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
            var dateStamp = signingTime.UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

            // The Host header is set explicitly so the value signed here is exactly what goes on
            // the wire rather than whatever HttpClient would derive at send time.
            request.Headers.Host = uri.Authority;
            request.Headers.Remove("x-amz-date");
            request.Headers.Add("x-amz-date", amzDate);
            request.Headers.Remove("x-amz-security-token");
            if (!string.IsNullOrEmpty(credentials.SessionToken))
            {
                request.Headers.Add("x-amz-security-token", credentials.SessionToken);
            }

            var signedHeaders = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["host"] = uri.Authority,
                ["x-amz-date"] = amzDate
            };
            var contentType = request.Content?.Headers.ContentType?.ToString();
            if (!string.IsNullOrEmpty(contentType))
            {
                signedHeaders["content-type"] = contentType;
            }
            if (!string.IsNullOrEmpty(credentials.SessionToken))
            {
                signedHeaders["x-amz-security-token"] = credentials.SessionToken;
            }

            var canonicalHeaders = string.Concat(signedHeaders.Select(header => $"{header.Key}:{header.Value.Trim()}\n"));
            var signedHeaderNames = string.Join(";", signedHeaders.Keys);
            var payloadHash = ToHex(SHA256.HashData(payload));

            var canonicalRequest = string.Join("\n",
                request.Method.Method,
                BuildCanonicalPath(uri),
                BuildCanonicalQuery(uri),
                canonicalHeaders,
                signedHeaderNames,
                payloadHash);

            var credentialScope = $"{dateStamp}/{region}/{service}/aws4_request";
            var stringToSign = string.Join("\n",
                Algorithm,
                amzDate,
                credentialScope,
                ToHex(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest))));

            var signingKey = HmacSha256(
                HmacSha256(
                    HmacSha256(
                        HmacSha256(Encoding.UTF8.GetBytes("AWS4" + credentials.SecretAccessKey), dateStamp),
                        region),
                    service),
                "aws4_request");
            var signature = ToHex(HmacSha256(signingKey, stringToSign));

            request.Headers.TryAddWithoutValidation(
                "Authorization",
                $"{Algorithm} Credential={credentials.AccessKeyId}/{credentialScope}, "
                + $"SignedHeaders={signedHeaderNames}, Signature={signature}");
        }

        /// <summary>
        /// Builds the canonical URI path. SigV4 for services other than S3 expects each path segment
        /// URI-encoded twice; <see cref="Uri.AbsolutePath"/> is the once-encoded wire form, so this
        /// encodes it once more, keeping <c>/</c> and unreserved characters literal (the same
        /// normalization botocore and the AWS SDKs apply).
        /// </summary>
        internal static string BuildCanonicalPath(Uri uri)
        {
            var path = uri.AbsolutePath;
            if (string.IsNullOrEmpty(path))
            {
                return "/";
            }

            var builder = new StringBuilder(path.Length);
            foreach (var b in Encoding.UTF8.GetBytes(path))
            {
                var c = (char)b;
                if (c == '/' || IsUnreserved(c))
                {
                    builder.Append(c);
                }
                else
                {
                    builder.Append('%').Append(((int)b).ToString("X2", CultureInfo.InvariantCulture));
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// Builds the canonical query string: parameters strictly percent-encoded and sorted by
        /// encoded name, then encoded value.
        /// </summary>
        internal static string BuildCanonicalQuery(Uri uri)
        {
            var query = uri.Query;
            if (string.IsNullOrEmpty(query) || query == "?")
            {
                return string.Empty;
            }

            var pairs = query.TrimStart('?')
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(pair =>
                {
                    var separator = pair.IndexOf('=');
                    var name = separator < 0 ? pair : pair[..separator];
                    var value = separator < 0 ? string.Empty : pair[(separator + 1)..];
                    return (Name: SigV4Encode(Uri.UnescapeDataString(name)),
                            Value: SigV4Encode(Uri.UnescapeDataString(value)));
                })
                .OrderBy(pair => pair.Name, StringComparer.Ordinal)
                .ThenBy(pair => pair.Value, StringComparer.Ordinal);

            return string.Join("&", pairs.Select(pair => $"{pair.Name}={pair.Value}"));
        }

        private static string SigV4Encode(string value)
        {
            var builder = new StringBuilder(value.Length);
            foreach (var b in Encoding.UTF8.GetBytes(value))
            {
                var c = (char)b;
                if (IsUnreserved(c))
                {
                    builder.Append(c);
                }
                else
                {
                    builder.Append('%').Append(((int)b).ToString("X2", CultureInfo.InvariantCulture));
                }
            }

            return builder.ToString();
        }

        private static bool IsUnreserved(char c) =>
            c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '.' or '_' or '~';

        private static byte[] HmacSha256(byte[] key, string data) =>
            HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(data));

        private static string ToHex(byte[] bytes) =>
            Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
