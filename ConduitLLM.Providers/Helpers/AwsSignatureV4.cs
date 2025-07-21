using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace ConduitLLM.Providers.Helpers
{
    /// <summary>
    /// Implements AWS Signature Version 4 signing for HTTP requests.
    /// </summary>
    public static class AwsSignatureV4
    {
        private const string Algorithm = "AWS4-HMAC-SHA256";
        private const string DateFormat = "yyyyMMdd";
        private const string DateTimeFormat = "yyyyMMddTHHmmssZ";
        
        /// <summary>
        /// Signs an HTTP request with AWS Signature V4.
        /// </summary>
        /// <param name="request">The HTTP request to sign.</param>
        /// <param name="accessKey">AWS Access Key ID.</param>
        /// <param name="secretKey">AWS Secret Access Key.</param>
        /// <param name="region">AWS region (e.g., "us-east-1").</param>
        /// <param name="service">AWS service name (e.g., "bedrock").</param>
        public static void SignRequest(HttpRequestMessage request, string accessKey, string secretKey, string region, string service)
        {
            var now = DateTime.UtcNow;
            var dateStamp = now.ToString(DateFormat, CultureInfo.InvariantCulture);
            var dateTimeStamp = now.ToString(DateTimeFormat, CultureInfo.InvariantCulture);
            
            // Add required headers
            request.Headers.Add("X-Amz-Date", dateTimeStamp);
            
            // Get the request body
            string bodyContent = "";
            if (request.Content != null)
            {
                bodyContent = request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            }
            
            // Create canonical request
            var canonicalRequest = CreateCanonicalRequest(request, bodyContent);
            
            // Create string to sign
            var stringToSign = CreateStringToSign(canonicalRequest, dateTimeStamp, dateStamp, region, service);
            
            // Calculate signature
            var signature = CalculateSignature(stringToSign, secretKey, dateStamp, region, service);
            
            // Create authorization header
            var authorizationHeader = CreateAuthorizationHeader(
                accessKey, 
                signature, 
                dateStamp, 
                region, 
                service, 
                GetSignedHeaders(request));
            
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(Algorithm, authorizationHeader);
        }
        
        private static string CreateCanonicalRequest(HttpRequestMessage request, string bodyContent)
        {
            var method = request.Method.Method;
            var path = request.RequestUri?.AbsolutePath ?? "/";
            var query = request.RequestUri?.Query.TrimStart('?') ?? "";
            
            // Sort query parameters
            var queryParams = query.Split('&')
                .Where(p => !string.IsNullOrEmpty(p))
                .Select(p =>
                {
                    var parts = p.Split('=');
                    return new { Key = Uri.UnescapeDataString(parts[0]), Value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : "" };
                })
                .OrderBy(p => p.Key)
                .ThenBy(p => p.Value)
                .Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}");
            
            var canonicalQueryString = string.Join("&", queryParams);
            
            // Create canonical headers
            var headers = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in request.Headers)
            {
                headers[header.Key.ToLowerInvariant()] = string.Join(",", header.Value).Trim();
            }
            
            if (request.Content?.Headers != null)
            {
                foreach (var header in request.Content.Headers)
                {
                    headers[header.Key.ToLowerInvariant()] = string.Join(",", header.Value).Trim();
                }
            }
            
            // Host header is required
            if (!headers.ContainsKey("host"))
            {
                headers["host"] = request.RequestUri?.Host ?? "";
            }
            
            var canonicalHeaders = string.Join("\n", headers.Select(h => $"{h.Key}:{h.Value}"));
            var signedHeaders = string.Join(";", headers.Keys);
            
            // Hash the payload
            var payloadHash = ComputeSHA256Hash(bodyContent);
            
            var canonicalRequest = $"{method}\n{path}\n{canonicalQueryString}\n{canonicalHeaders}\n\n{signedHeaders}\n{payloadHash}";
            return canonicalRequest;
        }
        
        private static string CreateStringToSign(string canonicalRequest, string dateTimeStamp, string dateStamp, string region, string service)
        {
            var scope = $"{dateStamp}/{region}/{service}/aws4_request";
            var canonicalRequestHash = ComputeSHA256Hash(canonicalRequest);
            
            return $"{Algorithm}\n{dateTimeStamp}\n{scope}\n{canonicalRequestHash}";
        }
        
        private static string CalculateSignature(string stringToSign, string secretKey, string dateStamp, string region, string service)
        {
            var kSecret = Encoding.UTF8.GetBytes($"AWS4{secretKey}");
            var kDate = HmacSHA256(kSecret, Encoding.UTF8.GetBytes(dateStamp));
            var kRegion = HmacSHA256(kDate, Encoding.UTF8.GetBytes(region));
            var kService = HmacSHA256(kRegion, Encoding.UTF8.GetBytes(service));
            var kSigning = HmacSHA256(kService, Encoding.UTF8.GetBytes("aws4_request"));
            
            var signature = HmacSHA256(kSigning, Encoding.UTF8.GetBytes(stringToSign));
            return BytesToHex(signature);
        }
        
        private static string CreateAuthorizationHeader(string accessKey, string signature, string dateStamp, string region, string service, string signedHeaders)
        {
            var scope = $"{dateStamp}/{region}/{service}/aws4_request";
            return $"Credential={accessKey}/{scope}, SignedHeaders={signedHeaders}, Signature={signature}";
        }
        
        private static string GetSignedHeaders(HttpRequestMessage request)
        {
            var headers = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            
            foreach (var header in request.Headers)
            {
                headers.Add(header.Key.ToLowerInvariant());
            }
            
            if (request.Content?.Headers != null)
            {
                foreach (var header in request.Content.Headers)
                {
                    headers.Add(header.Key.ToLowerInvariant());
                }
            }
            
            // Host header is always included
            headers.Add("host");
            
            return string.Join(";", headers);
        }
        
        private static string ComputeSHA256Hash(string text)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(text));
                return BytesToHex(bytes);
            }
        }
        
        private static byte[] HmacSHA256(byte[] key, byte[] data)
        {
            using (var hmac = new HMACSHA256(key))
            {
                return hmac.ComputeHash(data);
            }
        }
        
        private static string BytesToHex(byte[] bytes)
        {
            return string.Concat(bytes.Select(b => b.ToString("x2")));
        }
    }
}