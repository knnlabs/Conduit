using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using ConduitLLM.Providers.Vertex;

using AwesomeAssertions;

using Moq;

using Xunit;

namespace ConduitLLM.Tests.Providers.Vertex;

[Trait("Category", "Unit")]
[Trait("Component", "Providers")]
public class GoogleServiceAccountTokenProviderTests : IDisposable
{
    public GoogleServiceAccountTokenProviderTests()
    {
        GoogleServiceAccountTokenProvider.ResetCacheForTests();
    }

    public void Dispose()
    {
        GoogleServiceAccountTokenProvider.ResetCacheForTests();
    }

    [Fact]
    public void CreateJwtAssertion_Should_Emit_And_Sign_The_Required_Google_Claims()
    {
        using var rsa = RSA.Create(2048);
        var serviceAccountJson = CreateServiceAccountJson(rsa);
        var issuedAt = new DateTimeOffset(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);

        var assertion = GoogleServiceAccountTokenProvider.CreateJwtAssertion(
            serviceAccountJson,
            issuedAt);

        var segments = assertion.Split('.');
        segments.Should().HaveCount(3);

        using var header = JsonDocument.Parse(Base64UrlDecode(segments[0]));
        header.RootElement.GetProperty("alg").GetString().Should().Be("RS256");
        header.RootElement.GetProperty("typ").GetString().Should().Be("JWT");
        header.RootElement.GetProperty("kid").GetString().Should().Be("test-key-id");

        using var claims = JsonDocument.Parse(Base64UrlDecode(segments[1]));
        claims.RootElement.GetProperty("iss").GetString()
            .Should().Be("vertex-test@example.iam.gserviceaccount.com");
        claims.RootElement.GetProperty("scope").GetString()
            .Should().Be(GoogleServiceAccountTokenProvider.CloudPlatformScope);
        claims.RootElement.GetProperty("aud").GetString()
            .Should().Be(GoogleServiceAccountTokenProvider.TokenEndpoint);
        claims.RootElement.GetProperty("iat").GetInt64()
            .Should().Be(issuedAt.ToUnixTimeSeconds());
        claims.RootElement.GetProperty("exp").GetInt64()
            .Should().Be(issuedAt.AddHours(1).ToUnixTimeSeconds());

        var signedData = Encoding.ASCII.GetBytes($"{segments[0]}.{segments[1]}");
        rsa.VerifyData(
                signedData,
                Base64UrlDecode(segments[2]),
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1)
            .Should().BeTrue();
    }

    [Fact]
    public async Task GetAccessTokenAsync_Should_Cache_By_Credential_And_Refresh_Before_Expiry()
    {
        using var rsa = RSA.Create(2048);
        var serviceAccountJson = CreateServiceAccountJson(rsa);
        var time = new MutableTimeProvider(
            new DateTimeOffset(2026, 7, 25, 12, 0, 0, TimeSpan.Zero));
        var tokenRequests = 0;
        string? postedForm = null;
        var handler = new CallbackHandler(async request =>
        {
            request.RequestUri.Should().Be(GoogleServiceAccountTokenProvider.TokenEndpoint);
            postedForm = await request.Content!.ReadAsStringAsync();
            tokenRequests++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""{"access_token":"token-{{tokenRequests}}","expires_in":3600,"token_type":"Bearer"}""",
                    Encoding.UTF8,
                    "application/json")
            };
        });
        var factory = CreateFactory(handler);

        var firstProvider = new GoogleServiceAccountTokenProvider(
            factory.Object,
            42,
            serviceAccountJson,
            time);
        var secondProvider = new GoogleServiceAccountTokenProvider(
            factory.Object,
            42,
            serviceAccountJson,
            time);

        (await firstProvider.GetAccessTokenAsync()).Should().Be("token-1");
        (await secondProvider.GetAccessTokenAsync()).Should().Be("token-1");
        tokenRequests.Should().Be(1);
        postedForm.Should().Contain(
            "grant_type=urn%3Aietf%3Aparams%3Aoauth%3Agrant-type%3Ajwt-bearer");
        postedForm.Should().Contain("&assertion=");

        time.Advance(TimeSpan.FromMinutes(55));

        (await secondProvider.GetAccessTokenAsync()).Should().Be("token-2");
        tokenRequests.Should().Be(2);
    }

    private static Mock<IHttpClientFactory> CreateFactory(HttpMessageHandler handler)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(value => value.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(handler, disposeHandler: false));
        return factory;
    }

    internal static string CreateServiceAccountJson(RSA rsa) =>
        JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["type"] = "service_account",
            ["client_email"] = "vertex-test@example.iam.gserviceaccount.com",
            ["private_key"] = rsa.ExportPkcs8PrivateKeyPem(),
            ["private_key_id"] = "test-key-id",
            ["token_uri"] = GoogleServiceAccountTokenProvider.TokenEndpoint
        });

    private static byte[] Base64UrlDecode(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + ((4 - base64.Length % 4) % 4), '=');
        return Convert.FromBase64String(base64);
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        internal void Advance(TimeSpan duration)
        {
            _utcNow = _utcNow.Add(duration);
        }
    }

    private sealed class CallbackHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> callback) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            callback(request);
    }
}
