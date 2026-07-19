using System.Text.Json;

using ConduitLLM.Admin.Middleware;
using ConduitLLM.Core.Exceptions;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Admin.Middleware
{
    [Trait("Category", "Unit")]
    [Trait("Component", "Admin")]
    public class AdminExceptionMiddlewareTests
    {
        private readonly Mock<RequestDelegate> _mockNext;
        private readonly Mock<ILogger<AdminExceptionMiddleware>> _mockLogger;
        private readonly Mock<IWebHostEnvironment> _mockEnvironment;
        private readonly AdminExceptionMiddleware _middleware;
        private readonly DefaultHttpContext _httpContext;

        public AdminExceptionMiddlewareTests()
        {
            _mockNext = new Mock<RequestDelegate>();
            _mockLogger = new Mock<ILogger<AdminExceptionMiddleware>>();
            _mockEnvironment = new Mock<IWebHostEnvironment>();

            _mockEnvironment.Setup(x => x.EnvironmentName).Returns(Environments.Production);

            _middleware = new AdminExceptionMiddleware(
                _mockNext.Object,
                _mockLogger.Object,
                _mockEnvironment.Object);

            _httpContext = new DefaultHttpContext();
            _httpContext.Response.Body = new MemoryStream();
            _httpContext.TraceIdentifier = "test-trace-id";
        }

        [Fact]
        public async Task ModelNotFoundException_Returns404()
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

            var (error, code) = GetErrorResponse(_httpContext);

            Assert.Contains(modelName, error);
            Assert.Equal("model_not_found", code);
        }

        [Fact]
        public async Task InvalidRequestException_Returns400()
        {
            // Arrange
            var exception = new InvalidRequestException("Invalid parameter", "invalid_param", "test_field");
            _mockNext.Setup(x => x(It.IsAny<HttpContext>()))
                .ThrowsAsync(exception);

            // Act
            await _middleware.InvokeAsync(_httpContext);

            // Assert
            Assert.Equal(400, _httpContext.Response.StatusCode);

            var (error, code) = GetErrorResponse(_httpContext);

            Assert.Equal("Invalid parameter", error);
            Assert.Equal("invalid_param", code);
        }

        [Fact]
        public async Task AuthorizationException_Returns403()
        {
            // Arrange
            _mockNext.Setup(x => x(It.IsAny<HttpContext>()))
                .ThrowsAsync(new AuthorizationException("Access denied"));

            // Act
            await _middleware.InvokeAsync(_httpContext);

            // Assert
            Assert.Equal(403, _httpContext.Response.StatusCode);

            var (error, code) = GetErrorResponse(_httpContext);

            Assert.Equal("Access denied", error);
            Assert.Equal("forbidden", code);
        }

        [Fact]
        public async Task RequestTimeoutException_Returns408()
        {
            // Arrange
            _mockNext.Setup(x => x(It.IsAny<HttpContext>()))
                .ThrowsAsync(new RequestTimeoutException("Request timed out"));

            // Act
            await _middleware.InvokeAsync(_httpContext);

            // Assert
            Assert.Equal(408, _httpContext.Response.StatusCode);

            var (error, code) = GetErrorResponse(_httpContext);

            Assert.Equal("Request timed out", error);
            Assert.Equal("request_timeout", code);
        }

        [Fact]
        public async Task RateLimitException_Returns429_WithRetryAfterHeader()
        {
            // Arrange
            _mockNext.Setup(x => x(It.IsAny<HttpContext>()))
                .ThrowsAsync(new RateLimitExceededException("Rate limit exceeded", 60));

            // Act
            await _middleware.InvokeAsync(_httpContext);

            // Assert
            Assert.Equal(429, _httpContext.Response.StatusCode);
            Assert.Equal("60", _httpContext.Response.Headers["Retry-After"]);

            var (error, code) = GetErrorResponse(_httpContext);

            Assert.Equal("Rate limit exceeded", error);
            Assert.Equal("rate_limit_exceeded", code);
        }

        [Fact]
        public async Task ServiceUnavailableException_Returns503()
        {
            // Arrange
            _mockNext.Setup(x => x(It.IsAny<HttpContext>()))
                .ThrowsAsync(new ServiceUnavailableException("Service unavailable", "TestService"));

            // Act
            await _middleware.InvokeAsync(_httpContext);

            // Assert
            Assert.Equal(503, _httpContext.Response.StatusCode);

            var (error, code) = GetErrorResponse(_httpContext);

            Assert.Equal("Service unavailable", error);
            Assert.Equal("service_unavailable", code);
        }

        [Fact]
        public async Task ArgumentNullException_Returns400()
        {
            // Arrange
            _mockNext.Setup(x => x(It.IsAny<HttpContext>()))
                .ThrowsAsync(new ArgumentNullException("param"));

            // Act
            await _middleware.InvokeAsync(_httpContext);

            // Assert
            Assert.Equal(400, _httpContext.Response.StatusCode);

            var (error, code) = GetErrorResponse(_httpContext);

            Assert.Equal("Required parameter is missing", error);
            Assert.Equal("missing_parameter", code);
        }

        [Fact]
        public async Task KeyNotFoundException_Returns404()
        {
            // Arrange
            _mockNext.Setup(x => x(It.IsAny<HttpContext>()))
                .ThrowsAsync(new KeyNotFoundException("Resource not found"));

            // Act
            await _middleware.InvokeAsync(_httpContext);

            // Assert
            Assert.Equal(404, _httpContext.Response.StatusCode);

            var (error, code) = GetErrorResponse(_httpContext);

            Assert.Equal("The requested resource was not found", error);
            Assert.Equal("not_found", code);
        }

        [Fact]
        public async Task UnhandledException_Returns500_GenericMessage()
        {
            // Arrange
            _mockNext.Setup(x => x(It.IsAny<HttpContext>()))
                .ThrowsAsync(new Exception("secret details"));

            // Act
            await _middleware.InvokeAsync(_httpContext);

            // Assert
            Assert.Equal(500, _httpContext.Response.StatusCode);

            var (error, code) = GetErrorResponse(_httpContext);

            // In production, should not expose internal details
            Assert.Equal("An unexpected error occurred", error);
            Assert.Equal("internal_error", code);
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

            var (error, _) = GetErrorResponse(_httpContext);

            // In development, should show actual error message
            Assert.Equal("Detailed error message", error);
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

        [Fact]
        public async Task ResponseAlreadyStarted_DoesNotThrow()
        {
            // Arrange
            _mockNext.Setup(x => x(It.IsAny<HttpContext>()))
                .ThrowsAsync(new Exception("test"));

            // Use a mock response that reports HasStarted = true
            var mockResponse = new Mock<HttpResponse>();
            mockResponse.Setup(r => r.HasStarted).Returns(true);
            var mockContext = new Mock<HttpContext>();
            mockContext.Setup(c => c.Response).Returns(mockResponse.Object);
            mockContext.Setup(c => c.TraceIdentifier).Returns("test-trace-id");
            mockContext.Setup(c => c.Request.Method).Returns("GET");
            mockContext.Setup(c => c.Request.Path).Returns(new PathString("/test"));

            // Act & Assert - should not throw
            await _middleware.InvokeAsync(mockContext.Object);
        }

        [Fact]
        public async Task NoException_PassesThrough()
        {
            // Arrange
            var nextCalled = false;
            _mockNext.Setup(x => x(It.IsAny<HttpContext>()))
                .Callback<HttpContext>(_ => nextCalled = true)
                .Returns(Task.CompletedTask);

            // Act
            await _middleware.InvokeAsync(_httpContext);

            // Assert
            Assert.True(nextCalled);
            Assert.Equal(200, _httpContext.Response.StatusCode);
        }

        [Fact]
        public async Task ContentType_IsJson()
        {
            // Arrange
            _mockNext.Setup(x => x(It.IsAny<HttpContext>()))
                .ThrowsAsync(new Exception("test"));

            // Act
            await _middleware.InvokeAsync(_httpContext);

            // Assert
            Assert.Equal("application/json", _httpContext.Response.ContentType);
        }

        [Fact]
        public async Task LogsError_WithExceptionAndRequestDetails()
        {
            // Arrange
            var exception = new InvalidOperationException("something broke");
            _mockNext.Setup(x => x(It.IsAny<HttpContext>()))
                .ThrowsAsync(exception);

            _httpContext.Request.Method = "POST";
            _httpContext.Request.Path = "/api/providers";

            // Act
            await _middleware.InvokeAsync(_httpContext);

            // Assert — LogError was called with the exception
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("test-trace-id")),
                    exception,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task ResponseAlreadyStarted_LogsWarning()
        {
            // Arrange
            _mockNext.Setup(x => x(It.IsAny<HttpContext>()))
                .ThrowsAsync(new Exception("test"));

            var mockResponse = new Mock<HttpResponse>();
            mockResponse.Setup(r => r.HasStarted).Returns(true);
            var mockContext = new Mock<HttpContext>();
            mockContext.Setup(c => c.Response).Returns(mockResponse.Object);
            mockContext.Setup(c => c.TraceIdentifier).Returns("started-trace-id");
            mockContext.Setup(c => c.Request.Method).Returns("GET");
            mockContext.Setup(c => c.Request.Path).Returns(new PathString("/test"));

            // Act
            await _middleware.InvokeAsync(mockContext.Object);

            // Assert — LogWarning was called about response already started
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("started-trace-id")),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        /// <summary>
        /// Deserializes the response body and extracts the error message and code.
        /// ErrorResponseDto.error is object type, which deserializes as JsonElement.
        /// </summary>
        private static (string error, string? code) GetErrorResponse(HttpContext context)
        {
            context.Response.Body.Position = 0;
            using var reader = new StreamReader(context.Response.Body);
            var body = reader.ReadToEnd();

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            var error = root.GetProperty("error").GetString() ?? string.Empty;
            var code = root.TryGetProperty("code", out var codeElement)
                ? codeElement.GetString()
                : null;

            return (error, code);
        }
    }
}
