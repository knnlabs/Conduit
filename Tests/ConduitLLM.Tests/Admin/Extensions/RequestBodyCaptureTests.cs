using System.Text;

using ConduitLLM.Core.Extensions;

using Microsoft.AspNetCore.Http;

namespace ConduitLLM.Tests.Admin.Extensions
{
    [Trait("Category", "Unit")]
    [Trait("Component", "Admin")]
    public class RequestBodyCaptureTests
    {
        [Fact]
        public async Task CaptureAsync_ReturnsNull_WhenContextIsNull()
        {
            var result = await RequestBodyCapture.CaptureAsync(null);

            Assert.Null(result);
        }

        [Theory]
        [InlineData("GET")]
        [InlineData("HEAD")]
        [InlineData("OPTIONS")]
        public async Task CaptureAsync_ReturnsNull_ForNonMutationMethods(string method)
        {
            var context = CreateHttpContext(method, """{"name": "test"}""");

            var result = await RequestBodyCapture.CaptureAsync(context);

            Assert.Null(result);
        }

        [Theory]
        [InlineData("POST")]
        [InlineData("PUT")]
        [InlineData("PATCH")]
        [InlineData("DELETE")]
        public async Task CaptureAsync_ReturnsBody_ForMutationMethods(string method)
        {
            var context = CreateHttpContext(method, """{"name": "test"}""");

            var result = await RequestBodyCapture.CaptureAsync(context);

            Assert.NotNull(result);
            Assert.Contains("test", result);
        }

        [Fact]
        public async Task CaptureAsync_ReturnsNull_WhenBodyIsEmpty()
        {
            var context = CreateHttpContext("POST", "");

            var result = await RequestBodyCapture.CaptureAsync(context);

            Assert.Null(result);
        }

        [Fact]
        public async Task CaptureAsync_RedactsSensitiveFields_ApiKey()
        {
            var body = """{"name": "test", "apiKey": "sk-secret-123", "description": "hello"}""";
            var context = CreateHttpContext("POST", body);

            var result = await RequestBodyCapture.CaptureAsync(context);

            Assert.NotNull(result);
            Assert.DoesNotContain("sk-secret-123", result);
            Assert.Contains("[REDACTED]", result);
            Assert.Contains("test", result);
            Assert.Contains("hello", result);
        }

        [Fact]
        public async Task CaptureAsync_RedactsSensitiveFields_Password()
        {
            var body = """{"username": "admin", "password": "super-secret"}""";
            var context = CreateHttpContext("PUT", body);

            var result = await RequestBodyCapture.CaptureAsync(context);

            Assert.NotNull(result);
            Assert.DoesNotContain("super-secret", result);
            Assert.Contains("[REDACTED]", result);
            Assert.Contains("admin", result);
        }

        [Fact]
        public async Task CaptureAsync_RedactsSensitiveFields_Token()
        {
            var body = """{"token": "bearer-xyz-789", "data": "safe"}""";
            var context = CreateHttpContext("POST", body);

            var result = await RequestBodyCapture.CaptureAsync(context);

            Assert.NotNull(result);
            Assert.DoesNotContain("bearer-xyz-789", result);
            Assert.Contains("[REDACTED]", result);
            Assert.Contains("safe", result);
        }

        [Fact]
        public async Task CaptureAsync_RedactsSensitiveFields_Secret()
        {
            var body = """{"clientSecret": "abc123", "name": "test"}""";
            var context = CreateHttpContext("POST", body);

            var result = await RequestBodyCapture.CaptureAsync(context);

            Assert.NotNull(result);
            Assert.DoesNotContain("abc123", result);
            Assert.Contains("[REDACTED]", result);
        }

        [Fact]
        public async Task CaptureAsync_RedactsSensitiveFields_Credential()
        {
            var body = """{"credentialValue": "my-cred", "purpose": "testing"}""";
            var context = CreateHttpContext("POST", body);

            var result = await RequestBodyCapture.CaptureAsync(context);

            Assert.NotNull(result);
            Assert.DoesNotContain("my-cred", result);
            Assert.Contains("[REDACTED]", result);
        }

        [Fact]
        public async Task CaptureAsync_PreservesNonSensitiveFields()
        {
            var body = """{"name": "Model A", "description": "A test model", "costPerToken": 0.01}""";
            var context = CreateHttpContext("POST", body);

            var result = await RequestBodyCapture.CaptureAsync(context);

            Assert.NotNull(result);
            Assert.Contains("Model A", result);
            Assert.Contains("A test model", result);
            Assert.Contains("0.01", result);
        }

        [Fact]
        public async Task CaptureAsync_TruncatesLargeBody()
        {
            // Create a body larger than 4096 characters
            var largeValue = new string('x', 5000);
            var body = $$"""{"data": "{{largeValue}}"}""";
            var context = CreateHttpContext("POST", body);

            var result = await RequestBodyCapture.CaptureAsync(context);

            Assert.NotNull(result);
            // Result is bounded by both our 4096 truncation and LoggingSanitizer's 1000-char limit
            Assert.True(result.Length <= 1000, $"Expected max 1000 chars, got {result.Length}");
            Assert.True(result.Length < body.Length);
        }

        [Fact]
        public async Task CaptureAsync_HandlesMultipleSensitiveFields()
        {
            var body = """{"apiKey": "key1", "secret": "sec1", "authToken": "tok1", "name": "safe"}""";
            var context = CreateHttpContext("POST", body);

            var result = await RequestBodyCapture.CaptureAsync(context);

            Assert.NotNull(result);
            Assert.DoesNotContain("key1", result);
            Assert.DoesNotContain("sec1", result);
            Assert.DoesNotContain("tok1", result);
            Assert.Contains("safe", result);
        }

        [Fact]
        public async Task CaptureAsync_HandlesNonJsonBody()
        {
            var body = "this is plain text, not json";
            var context = CreateHttpContext("POST", body);

            var result = await RequestBodyCapture.CaptureAsync(context);

            Assert.NotNull(result);
            Assert.Contains("this is plain text", result);
        }

        [Fact]
        public async Task CaptureAsync_BodyCanBeReReadAfterCapture()
        {
            var originalBody = """{"name": "test"}""";
            var context = CreateHttpContext("POST", originalBody);

            await RequestBodyCapture.CaptureAsync(context);

            // Verify body stream is still readable
            context.Request.Body.Position = 0;
            using var reader = new StreamReader(context.Request.Body);
            var rereadBody = await reader.ReadToEndAsync();

            Assert.Equal(originalBody, rereadBody);
        }

        [Fact]
        public async Task CaptureAsync_CaseInsensitive_SensitiveFieldNames()
        {
            var body = """{"APIKEY": "val1", "Password": "val2", "AUTH_TOKEN": "val3"}""";
            var context = CreateHttpContext("POST", body);

            var result = await RequestBodyCapture.CaptureAsync(context);

            Assert.NotNull(result);
            Assert.DoesNotContain("val1", result);
            Assert.DoesNotContain("val2", result);
            Assert.DoesNotContain("val3", result);
        }

        private static HttpContext CreateHttpContext(string method, string body)
        {
            var context = new DefaultHttpContext();
            context.Request.Method = method;

            var bodyBytes = Encoding.UTF8.GetBytes(body);
            context.Request.Body = new MemoryStream(bodyBytes);
            context.Request.ContentLength = bodyBytes.Length;
            context.Request.ContentType = "application/json";

            return context;
        }
    }
}
