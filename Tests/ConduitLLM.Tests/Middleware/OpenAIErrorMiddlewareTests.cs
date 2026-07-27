using System.Text.Json;

using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Middleware;
using ConduitLLM.Core.Models;
using ConduitLLM.Configuration.Interfaces;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Middleware
{
    public class OpenAIErrorMiddlewareTests
    {
        private readonly Mock<RequestDelegate> _mockNext;
        private readonly Mock<ILogger<OpenAIErrorMiddleware>> _mockLogger;
        private readonly Mock<IWebHostEnvironment> _mockEnvironment;
        private readonly Mock<ISecurityEventLogger> _mockSecurityLogger;
        private readonly OpenAIErrorMiddleware _middleware;
        private readonly DefaultHttpContext _httpContext;

        public OpenAIErrorMiddlewareTests()
        {
            _mockNext = new Mock<RequestDelegate>();
            _mockLogger = new Mock<ILogger<OpenAIErrorMiddleware>>();
            _mockEnvironment = new Mock<IWebHostEnvironment>();
            _mockSecurityLogger = new Mock<ISecurityEventLogger>();

            _mockEnvironment.Setup(x => x.EnvironmentName).Returns(Environments.Production);

            _middleware = new OpenAIErrorMiddleware(
                _mockNext.Object,
                _mockLogger.Object,
                _mockEnvironment.Object,
                _mockSecurityLogger.Object);

            _httpContext = new DefaultHttpContext();
            _httpContext.Response.Body = new MemoryStream();
            _httpContext.TraceIdentifier = "test-trace-id";
        }

        [Fact]
        public async Task ModelNotFoundException_Returns404WithOpenAIFormat()
        {
            // Arrange
            var modelName = "gpt-5";
            _mockNext.Setup(x => x(It.IsAny<HttpContext>()))
                .ThrowsAsync(new ModelNotFoundException(modelName));

            // Act
            await _middleware.InvokeAsync(_httpContext);

            // Assert
            Assert.Equal(404, _httpContext.Response.StatusCode);
            Assert.Equal("application/json", _httpContext.Response.ContentType);

            var errorResponse = GetErrorResponse(_httpContext);

            Assert.NotNull(errorResponse);
            Assert.NotNull(errorResponse.Error);
            Assert.Contains(modelName, errorResponse.Error.Message);
            Assert.Equal("invalid_request_error", errorResponse.Error.Type);
            Assert.Equal("model_not_found", errorResponse.Error.Code);
            Assert.Equal("model", errorResponse.Error.Param);
        }

        [Fact]
        public async Task InvalidRequestException_Returns400WithOpenAIFormat()
        {
            // Arrange
            var exception = new InvalidRequestException("Invalid parameter", "invalid_param", "test_field");
            _mockNext.Setup(x => x(It.IsAny<HttpContext>()))
                .ThrowsAsync(exception);

            // Act
            await _middleware.InvokeAsync(_httpContext);

            // Assert
            Assert.Equal(400, _httpContext.Response.StatusCode);

            var errorResponse = GetErrorResponse(_httpContext);

            Assert.NotNull(errorResponse);
            Assert.Equal("Invalid parameter", errorResponse.Error.Message);
            Assert.Equal("invalid_request_error", errorResponse.Error.Type);
            Assert.Equal("invalid_param", errorResponse.Error.Code);
            Assert.Equal("test_field", errorResponse.Error.Param);
        }

        [Fact]
        public async Task AuthorizationException_Returns403WithOpenAIFormat()
        {
            // Arrange
            var exception = new AuthorizationException("Access denied");
            _mockNext.Setup(x => x(It.IsAny<HttpContext>()))
                .ThrowsAsync(exception);

            // Act
            await _middleware.InvokeAsync(_httpContext);

            // Assert
            Assert.Equal(403, _httpContext.Response.StatusCode);

            var errorResponse = GetErrorResponse(_httpContext);

            Assert.NotNull(errorResponse);
            Assert.Equal("Access denied", errorResponse.Error.Message);
            Assert.Equal("invalid_request_error", errorResponse.Error.Type);
            Assert.Equal("forbidden", errorResponse.Error.Code);
        }

        [Fact]
        public async Task RequestTimeoutException_Returns408WithOpenAIFormat()
        {
            // Arrange
            var exception = new RequestTimeoutException("Request timed out");
            _mockNext.Setup(x => x(It.IsAny<HttpContext>()))
                .ThrowsAsync(exception);

            // Act
            await _middleware.InvokeAsync(_httpContext);

            // Assert
            Assert.Equal(408, _httpContext.Response.StatusCode);

            var errorResponse = GetErrorResponse(_httpContext);

            Assert.NotNull(errorResponse);
            Assert.Equal("Request timed out", errorResponse.Error.Message);
            Assert.Equal("timeout_error", errorResponse.Error.Type);
            Assert.Equal("request_timeout", errorResponse.Error.Code);
        }

        [Fact]
        public async Task RateLimitException_Returns429WithOpenAIFormat()
        {
            // Arrange
            var exception = new RateLimitExceededException("Rate limit exceeded", 60);
            _mockNext.Setup(x => x(It.IsAny<HttpContext>()))
                .ThrowsAsync(exception);

            // Act
            await _middleware.InvokeAsync(_httpContext);

            // Assert
            Assert.Equal(429, _httpContext.Response.StatusCode);
            Assert.Equal("60", _httpContext.Response.Headers["Retry-After"]);

            var errorResponse = GetErrorResponse(_httpContext);

            Assert.NotNull(errorResponse);
            Assert.Equal("Rate limit exceeded", errorResponse.Error.Message);
            Assert.Equal("rate_limit_error", errorResponse.Error.Type);
            Assert.Equal("rate_limit_exceeded", errorResponse.Error.Code);
        }

        [Fact]
        public async Task ServiceUnavailableException_Returns503WithOpenAIFormat()
        {
            // Arrange
            var exception = new ServiceUnavailableException("Service unavailable", "TestService");
            _mockNext.Setup(x => x(It.IsAny<HttpContext>()))
                .ThrowsAsync(exception);

            // Act
            await _middleware.InvokeAsync(_httpContext);

            // Assert
            Assert.Equal(503, _httpContext.Response.StatusCode);

            var errorResponse = GetErrorResponse(_httpContext);

            Assert.NotNull(errorResponse);
            Assert.Equal("Service unavailable", errorResponse.Error.Message);
            Assert.Equal("service_unavailable", errorResponse.Error.Type);
            Assert.Equal("service_unavailable", errorResponse.Error.Code);
        }

        [Fact]
        public async Task RedisCircuitBreakerException_Returns503WithRetryAfter()
        {
            var exception = new RedisCircuitBreakerOpenException(
                CircuitState.Open,
                TimeSpan.FromMilliseconds(4200));
            _mockNext.Setup(x => x(It.IsAny<HttpContext>()))
                .ThrowsAsync(exception);

            await _middleware.InvokeAsync(_httpContext);

            Assert.Equal(StatusCodes.Status503ServiceUnavailable, _httpContext.Response.StatusCode);
            Assert.Equal("5", _httpContext.Response.Headers["Retry-After"]);
            var errorResponse = GetErrorResponse(_httpContext);
            Assert.Equal("service_unavailable", errorResponse.Error.Type);
            Assert.Equal("redis_circuit_open", errorResponse.Error.Code);
        }

        [Fact]
        public async Task LLMCommunicationException_WithStatusCode_ReturnsProviderStatus()
        {
            // Arrange
            var exception = new LLMCommunicationException("Provider error",
                System.Net.HttpStatusCode.BadGateway, "Bad gateway");
            _mockNext.Setup(x => x(It.IsAny<HttpContext>()))
                .ThrowsAsync(exception);

            // Act
            await _middleware.InvokeAsync(_httpContext);

            // Assert
            Assert.Equal(502, _httpContext.Response.StatusCode);

            var errorResponse = GetErrorResponse(_httpContext);

            Assert.NotNull(errorResponse);
            Assert.Equal("Provider error", errorResponse.Error.Message);
            Assert.Equal("server_error", errorResponse.Error.Type);
            Assert.Equal("provider_bad_gateway", errorResponse.Error.Code);
        }

        [Fact]
        public async Task LLMCommunicationException_WithoutStatusCode_Returns502()
        {
            // Arrange
            var exception = new LLMCommunicationException("Unknown provider error");
            _mockNext.Setup(x => x(It.IsAny<HttpContext>()))
                .ThrowsAsync(exception);

            // Act
            await _middleware.InvokeAsync(_httpContext);

            // Assert: an unattributed provider failure is an upstream fault (502), not a Conduit 500.
            Assert.Equal(502, _httpContext.Response.StatusCode);

            var errorResponse = GetErrorResponse(_httpContext);

            Assert.NotNull(errorResponse);
            Assert.Equal("Unknown provider error", errorResponse.Error.Message);
            Assert.Equal("server_error", errorResponse.Error.Type);
            Assert.Equal("provider_communication_error", errorResponse.Error.Code);
        }

        [Fact]
        public async Task UnhandledException_Returns500WithGenericMessage()
        {
            // Arrange
            _mockNext.Setup(x => x(It.IsAny<HttpContext>()))
                .ThrowsAsync(new Exception("Internal error details"));

            // Act
            await _middleware.InvokeAsync(_httpContext);

            // Assert
            Assert.Equal(500, _httpContext.Response.StatusCode);

            var errorResponse = GetErrorResponse(_httpContext);

            Assert.NotNull(errorResponse);
            // In production, should not expose internal details
            Assert.Equal("An unexpected error occurred", errorResponse.Error.Message);
            Assert.Equal("server_error", errorResponse.Error.Type);
            Assert.Equal("internal_error", errorResponse.Error.Code);
        }

        [Fact]
        public async Task UnhandledException_InDevelopment_ShowsDetails()
        {
            // Arrange
            _mockEnvironment.Setup(x => x.EnvironmentName).Returns(Environments.Development);
            _mockNext.Setup(x => x(It.IsAny<HttpContext>()))
                .ThrowsAsync(new Exception("Detailed error message"));

            // Act
            await _middleware.InvokeAsync(_httpContext);

            // Assert
            Assert.Equal(500, _httpContext.Response.StatusCode);

            var errorResponse = GetErrorResponse(_httpContext);

            Assert.NotNull(errorResponse);
            // In development, should show actual error message
            Assert.Equal("Detailed error message", errorResponse.Error.Message);
        }

        [Fact]
        public async Task ArgumentNullException_Returns400WithSafeMessage()
        {
            // Arrange
            _mockNext.Setup(x => x(It.IsAny<HttpContext>()))
                .ThrowsAsync(new ArgumentNullException("apiKey"));

            // Act
            await _middleware.InvokeAsync(_httpContext);

            // Assert
            Assert.Equal(400, _httpContext.Response.StatusCode);

            var errorResponse = GetErrorResponse(_httpContext);

            Assert.NotNull(errorResponse);
            Assert.Equal("Required parameter is missing", errorResponse.Error.Message);
            Assert.Equal("invalid_request_error", errorResponse.Error.Type);
            Assert.Equal("missing_parameter", errorResponse.Error.Code);
            Assert.Equal("apiKey", errorResponse.Error.Param);
        }

        [Fact]
        public async Task ArgumentNullException_InDevelopment_ShowsDetails()
        {
            // Arrange
            _mockEnvironment.Setup(x => x.EnvironmentName).Returns(Environments.Development);
            _mockNext.Setup(x => x(It.IsAny<HttpContext>()))
                .ThrowsAsync(new ArgumentNullException("apiKey"));

            // Act
            await _middleware.InvokeAsync(_httpContext);

            // Assert
            Assert.Equal(400, _httpContext.Response.StatusCode);

            var errorResponse = GetErrorResponse(_httpContext);

            Assert.NotNull(errorResponse);
            // In development, should show actual exception message
            Assert.Contains("apiKey", errorResponse.Error.Message);
        }

        [Fact]
        public async Task XRequestIdHeader_IsSet()
        {
            // Arrange
            _mockNext.Setup(x => x(It.IsAny<HttpContext>()))
                .ThrowsAsync(new Exception("test"));

            // Act
            await _middleware.InvokeAsync(_httpContext);

            // Assert
            Assert.Equal("test-trace-id", _httpContext.Response.Headers["X-Request-Id"]);
        }

        private OpenAIErrorMiddleware CreateMiddlewareWithCustomerMode(
            ConduitLLM.Core.Configuration.CustomerErrorMode mode)
            => new(
                _mockNext.Object,
                _mockLogger.Object,
                _mockEnvironment.Object,
                _mockSecurityLogger.Object,
                new ConduitLLM.Core.Services.ProviderErrorTranslator(
                    new ConduitLLM.Core.Configuration.CustomerErrorOptions { Mode = mode }));

        [Fact]
        public async Task ProviderError_ExternalMode_ReturnsGenericMessageWithoutMetadata()
        {
            // Arrange
            var middleware = CreateMiddlewareWithCustomerMode(
                ConduitLLM.Core.Configuration.CustomerErrorMode.External);
            var exception = new LLMCommunicationException(
                "API returned an error: 429 - secret quota text",
                System.Net.HttpStatusCode.TooManyRequests, "secret quota text")
            { ProviderName = "openai-prod" };
            _mockNext.Setup(x => x(It.IsAny<HttpContext>())).ThrowsAsync(exception);

            // Act
            await middleware.InvokeAsync(_httpContext);

            // Assert — status/code unchanged from legacy mapping; message sanitized
            Assert.Equal(429, _httpContext.Response.StatusCode);
            var errorResponse = GetErrorResponse(_httpContext);
            Assert.Equal("rate_limit_exceeded", errorResponse.Error.Code);
            Assert.DoesNotContain("secret quota text", errorResponse.Error.Message);
            Assert.DoesNotContain("openai-prod", errorResponse.Error.Message);
            Assert.Null(errorResponse.Error.Metadata);
        }

        [Fact]
        public async Task ProviderError_InternalMode_ReturnsDetailInMetadata()
        {
            // Arrange
            var middleware = CreateMiddlewareWithCustomerMode(
                ConduitLLM.Core.Configuration.CustomerErrorMode.Internal);
            var exception = new LLMCommunicationException(
                "API returned an error", System.Net.HttpStatusCode.TooManyRequests, "Rate limit reached for gpt-4o")
            { ProviderName = "openai-prod" };
            _mockNext.Setup(x => x(It.IsAny<HttpContext>())).ThrowsAsync(exception);

            // Act
            await middleware.InvokeAsync(_httpContext);

            // Assert
            Assert.Equal(429, _httpContext.Response.StatusCode);
            var errorResponse = GetErrorResponse(_httpContext);
            Assert.Contains("openai-prod", errorResponse.Error.Message);
            Assert.NotNull(errorResponse.Error.Metadata);
            var providerError = errorResponse.Error.Metadata!.Value.GetProperty("provider_error");
            Assert.Equal("openai-prod", providerError.GetProperty("provider").GetString());
            Assert.Equal("rate_limit_exceeded", providerError.GetProperty("error_type").GetString());
            Assert.Equal(429, providerError.GetProperty("upstream_status").GetInt32());
            Assert.Equal("Rate limit reached for gpt-4o", providerError.GetProperty("raw_message").GetString());
        }

        [Fact]
        public async Task ProviderError_ExternalMode_AuthFailureStays502WithGenericMessage()
        {
            // Arrange — the #1191 status masking must survive translation
            var middleware = CreateMiddlewareWithCustomerMode(
                ConduitLLM.Core.Configuration.CustomerErrorMode.External);
            var exception = new LLMCommunicationException(
                "invalid api key sk-abc", System.Net.HttpStatusCode.Unauthorized, "invalid api key sk-abc");
            _mockNext.Setup(x => x(It.IsAny<HttpContext>())).ThrowsAsync(exception);

            // Act
            await middleware.InvokeAsync(_httpContext);

            // Assert
            Assert.Equal(502, _httpContext.Response.StatusCode);
            var errorResponse = GetErrorResponse(_httpContext);
            Assert.Equal("provider_authentication_error", errorResponse.Error.Code);
            Assert.DoesNotContain("sk-abc", errorResponse.Error.Message);
        }

        [Fact]
        public async Task ProviderError_ExternalMode_WrappedStatuslessException_IsStillSanitized()
        {
            // Arrange — a status-less LLMCommunicationException must not leak its raw
            // message just because no status-bearing instance exists in the chain
            var middleware = CreateMiddlewareWithCustomerMode(
                ConduitLLM.Core.Configuration.CustomerErrorMode.External);
            var exception = new LLMCommunicationException("raw provider text with secrets");
            _mockNext.Setup(x => x(It.IsAny<HttpContext>())).ThrowsAsync(exception);

            // Act
            await middleware.InvokeAsync(_httpContext);

            // Assert
            Assert.Equal(502, _httpContext.Response.StatusCode);
            var errorResponse = GetErrorResponse(_httpContext);
            Assert.Equal("provider_communication_error", errorResponse.Error.Code);
            Assert.DoesNotContain("raw provider text", errorResponse.Error.Message);
        }

        [Fact]
        public async Task NonProviderException_WithTranslator_UsesLegacyMapping()
        {
            // Arrange — the translator only intercepts provider communication errors
            var middleware = CreateMiddlewareWithCustomerMode(
                ConduitLLM.Core.Configuration.CustomerErrorMode.External);
            _mockNext.Setup(x => x(It.IsAny<HttpContext>()))
                .ThrowsAsync(new ModelNotFoundException("gpt-5"));

            // Act
            await middleware.InvokeAsync(_httpContext);

            // Assert
            Assert.Equal(404, _httpContext.Response.StatusCode);
            var errorResponse = GetErrorResponse(_httpContext);
            Assert.Equal("model_not_found", errorResponse.Error.Code);
        }

        private static OpenAIErrorResponse GetErrorResponse(HttpContext context)
        {
            context.Response.Body.Position = 0;
            using var reader = new StreamReader(context.Response.Body);
            var body = reader.ReadToEnd();
            return JsonSerializer.Deserialize<OpenAIErrorResponse>(body)!;
        }
    }
}
