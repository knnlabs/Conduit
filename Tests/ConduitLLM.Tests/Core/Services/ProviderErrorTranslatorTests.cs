using System.Net;
using System.Text.Json;

using ConduitLLM.Core.Configuration;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;

namespace ConduitLLM.Tests.Core.Services
{
    public class ProviderErrorTranslatorTests
    {
        private static ProviderErrorTranslator CreateTranslator(CustomerErrorMode mode)
            => new(new CustomerErrorOptions { Mode = mode });

        [Theory]
        [InlineData(HttpStatusCode.TooManyRequests, "rate-limited")]
        [InlineData(HttpStatusCode.ServiceUnavailable, "temporarily unavailable")]
        [InlineData(HttpStatusCode.BadGateway, "temporarily unavailable")]
        [InlineData(HttpStatusCode.InternalServerError, "temporarily unavailable")]
        [InlineData(HttpStatusCode.GatewayTimeout, "timed out")]
        [InlineData(HttpStatusCode.RequestTimeout, "timed out")]
        [InlineData(HttpStatusCode.NotFound, "was not accepted")]
        public void Translate_External_ReturnsClassifiedGenericMessage(HttpStatusCode status, string expectedFragment)
        {
            var translator = CreateTranslator(CustomerErrorMode.External);

            var result = translator.Translate(status, "raw provider text", "openai-prod");

            Assert.Contains(expectedFragment, result.Message);
            Assert.Null(result.Detail);
            Assert.DoesNotContain("openai-prod", result.Message);
            Assert.DoesNotContain("raw provider text", result.Message);
        }

        [Theory]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.PaymentRequired)]
        [InlineData(HttpStatusCode.Forbidden)]
        public void Translate_External_AuthAndBillingCollapseToOneMessage(HttpStatusCode status)
        {
            var translator = CreateTranslator(CustomerErrorMode.External);

            var result = translator.Translate(status, null, "openai-prod");

            Assert.Equal(
                "The model provider rejected the request due to an upstream configuration issue.",
                result.Message);
            Assert.Null(result.Detail);
        }

        [Fact]
        public void Translate_External_UnknownStatus_ReturnsGenericUpstreamMessage()
        {
            var translator = CreateTranslator(CustomerErrorMode.External);

            var result = translator.Translate(null, "something odd", null);

            Assert.Equal("An upstream provider error occurred. Please retry later.", result.Message);
            Assert.Equal(ProviderErrorType.Unknown, result.ErrorType);
        }

        [Fact]
        public void Translate_Internal_MessageNamesProviderStatusAndRawText()
        {
            var translator = CreateTranslator(CustomerErrorMode.Internal);

            var result = translator.Translate(
                HttpStatusCode.TooManyRequests, "Rate limit reached for gpt-4o", "openai-prod");

            Assert.Contains("openai-prod", result.Message);
            Assert.Contains("429", result.Message);
            Assert.Contains("Rate limit reached for gpt-4o", result.Message);
        }

        [Fact]
        public void Translate_Internal_DetailCarriesAllFields()
        {
            var translator = CreateTranslator(CustomerErrorMode.Internal);

            var result = translator.Translate(
                HttpStatusCode.TooManyRequests, "Rate limit reached", "openai-prod");

            Assert.NotNull(result.Detail);
            Assert.Equal("openai-prod", result.Detail!.Provider);
            Assert.Equal("rate_limit_exceeded", result.Detail.ErrorType);
            Assert.Equal(429, result.Detail.UpstreamStatus);
            Assert.Equal("Rate limit reached", result.Detail.RawMessage);
        }

        [Fact]
        public void Translate_Internal_DetailSerializesWithSnakeCasePropertyNames()
        {
            var translator = CreateTranslator(CustomerErrorMode.Internal);

            var result = translator.Translate(HttpStatusCode.Unauthorized, "bad key", "vertex");
            var json = JsonSerializer.Serialize(result.Detail);

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            Assert.Equal("vertex", root.GetProperty("provider").GetString());
            Assert.Equal("invalid_api_key", root.GetProperty("error_type").GetString());
            Assert.Equal(401, root.GetProperty("upstream_status").GetInt32());
            Assert.Equal("bad key", root.GetProperty("raw_message").GetString());
        }

        [Fact]
        public void Translate_Internal_NullProvider_MessageDegradesGracefully()
        {
            var translator = CreateTranslator(CustomerErrorMode.Internal);

            var result = translator.Translate(HttpStatusCode.ServiceUnavailable, "overloaded", null);

            Assert.StartsWith("The model provider returned HTTP 503", result.Message);
            Assert.Null(result.Detail!.Provider);
        }

        [Fact]
        public void Translate_Internal_RawMessageIsRedacted()
        {
            var translator = CreateTranslator(CustomerErrorMode.Internal);
            var raw = "failed fetching https://bucket.example.com/media?sig=secretvalue123";

            var result = translator.Translate(HttpStatusCode.BadGateway, raw, "minimax");

            Assert.DoesNotContain("secretvalue123", result.Detail!.RawMessage);
            Assert.DoesNotContain("secretvalue123", result.Message);
        }

        [Fact]
        public void Translate_Internal_LongRawMessageIsTruncated()
        {
            var translator = CreateTranslator(CustomerErrorMode.Internal);
            var raw = new string('x', 5000);

            var result = translator.Translate(HttpStatusCode.BadGateway, raw, null);

            Assert.True(result.Detail!.RawMessage!.Length < 2100);
        }

        [Fact]
        public void Translate_Exception_UsesInnerCommunicationExceptionAndProviderName()
        {
            var translator = CreateTranslator(CustomerErrorMode.Internal);
            var inner = new LLMCommunicationException(
                "API returned an error", HttpStatusCode.TooManyRequests, "slow down")
            { ProviderName = "openai-prod" };
            var outer = new LLMCommunicationException("wrapped", inner);

            var result = translator.Translate(outer);

            Assert.Equal(ProviderErrorType.RateLimitExceeded, result.ErrorType);
            Assert.Equal("openai-prod", result.Detail!.Provider);
            Assert.Equal(429, result.Detail.UpstreamStatus);
        }

        [Fact]
        public void Translate_Exception_NonProviderException_ClassifiesAndSanitizesExternally()
        {
            var translator = CreateTranslator(CustomerErrorMode.External);

            var result = translator.Translate(new HttpRequestException("connection refused to 10.0.0.5"));

            Assert.Equal(ProviderErrorType.NetworkError, result.ErrorType);
            Assert.DoesNotContain("10.0.0.5", result.Message);
        }

        [Fact]
        public void Translate_Exception_FallbackProviderNameUsedWhenExceptionHasNone()
        {
            var translator = CreateTranslator(CustomerErrorMode.Internal);
            var ex = new LLMCommunicationException("boom", HttpStatusCode.BadGateway, "body");

            var result = translator.Translate(ex, providerNameFallback: "replicate");

            Assert.Equal("replicate", result.Detail!.Provider);
        }

        [Theory]
        [InlineData(CustomerErrorMode.External)]
        [InlineData(CustomerErrorMode.Internal)]
        public void MapProviderError_StatusCodeAndErrorCodeMatchLegacyMapping(CustomerErrorMode mode)
        {
            var translator = CreateTranslator(mode);
            var statuses = new HttpStatusCode?[]
            {
                HttpStatusCode.TooManyRequests, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden,
                HttpStatusCode.RequestTimeout, HttpStatusCode.BadGateway, HttpStatusCode.ServiceUnavailable,
                HttpStatusCode.GatewayTimeout, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError, null
            };

            foreach (var status in statuses)
            {
                var ex = new LLMCommunicationException("provider blew up", status, "body");

                var mapping = translator.MapProviderError(ex);
                var legacy = ExceptionToResponseMapper.MapProviderCommunicationStatus(status, "ignored");

                Assert.Equal(legacy.StatusCode, mapping.StatusCode);
                Assert.Equal(legacy.ErrorCode, mapping.ErrorCode);
                Assert.Equal(legacy.OpenAIErrorType, mapping.OpenAIErrorType);
            }
        }

        [Fact]
        public void MapProviderError_External_NoDetailAndGenericMessage()
        {
            var translator = CreateTranslator(CustomerErrorMode.External);
            var ex = new LLMCommunicationException(
                "API returned an error: 429 - secret provider text", HttpStatusCode.TooManyRequests, "secret provider text");

            var mapping = translator.MapProviderError(ex);

            Assert.Null(mapping.ProviderDetail);
            Assert.DoesNotContain("secret provider text", mapping.ResponseMessage);
        }

        [Fact]
        public void MapProviderError_Internal_AttachesDetail()
        {
            var translator = CreateTranslator(CustomerErrorMode.Internal);
            var ex = new LLMCommunicationException("boom", HttpStatusCode.TooManyRequests, "slow down")
            { ProviderName = "groq" };

            var mapping = translator.MapProviderError(ex);

            Assert.NotNull(mapping.ProviderDetail);
            Assert.Equal("groq", mapping.ProviderDetail!.Provider);
        }
    }
}
