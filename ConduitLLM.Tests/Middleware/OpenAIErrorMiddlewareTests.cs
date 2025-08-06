using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Middleware;
using ConduitLLM.Core.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

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
            
            var responseBody = GetResponseBody(_httpContext);
            var errorResponse = JsonSerializer.Deserialize<OpenAIErrorResponse>(responseBody);
            
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
            
            var responseBody = GetResponseBody(_httpContext);
            var errorResponse = JsonSerializer.Deserialize<OpenAIErrorResponse>(responseBody);
            
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
            
            var responseBody = GetResponseBody(_httpContext);
            var errorResponse = JsonSerializer.Deserialize<OpenAIErrorResponse>(responseBody);
            
            Assert.NotNull(errorResponse);
            Assert.Equal("Access denied", errorResponse.Error.Message);
            Assert.Equal("invalid_request_error", errorResponse.Error.Type);
            Assert.Equal("authorization_required", errorResponse.Error.Code);
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
            
            var responseBody = GetResponseBody(_httpContext);
            var errorResponse = JsonSerializer.Deserialize<OpenAIErrorResponse>(responseBody);
            
            Assert.NotNull(errorResponse);
            Assert.Equal("Request timed out", errorResponse.Error.Message);
            Assert.Equal("timeout_error", errorResponse.Error.Type);
            Assert.Equal("request_timeout", errorResponse.Error.Code);
        }

        [Fact]
        public async Task PayloadTooLargeException_Returns413WithOpenAIFormat()
        {
            // Arrange
            var exception = new PayloadTooLargeException("Payload too large", 10000, 5000);
            _mockNext.Setup(x => x(It.IsAny<HttpContext>()))
                .ThrowsAsync(exception);
            
            // Act
            await _middleware.InvokeAsync(_httpContext);
            
            // Assert
            Assert.Equal(413, _httpContext.Response.StatusCode);
            
            var responseBody = GetResponseBody(_httpContext);
            var errorResponse = JsonSerializer.Deserialize<OpenAIErrorResponse>(responseBody);
            
            Assert.NotNull(errorResponse);
            Assert.Equal("Payload too large", errorResponse.Error.Message);
            Assert.Equal("invalid_request_error", errorResponse.Error.Type);
            Assert.Equal("payload_too_large", errorResponse.Error.Code);
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
            
            var responseBody = GetResponseBody(_httpContext);
            var errorResponse = JsonSerializer.Deserialize<OpenAIErrorResponse>(responseBody);
            
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
            
            var responseBody = GetResponseBody(_httpContext);
            var errorResponse = JsonSerializer.Deserialize<OpenAIErrorResponse>(responseBody);
            
            Assert.NotNull(errorResponse);
            Assert.Equal("Service unavailable", errorResponse.Error.Message);
            Assert.Equal("service_unavailable", errorResponse.Error.Type);
            Assert.Equal("service_unavailable", errorResponse.Error.Code);
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
            
            var responseBody = GetResponseBody(_httpContext);
            var errorResponse = JsonSerializer.Deserialize<OpenAIErrorResponse>(responseBody);
            
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
            
            var responseBody = GetResponseBody(_httpContext);
            var errorResponse = JsonSerializer.Deserialize<OpenAIErrorResponse>(responseBody);
            
            Assert.NotNull(errorResponse);
            // In development, should show actual error message
            Assert.Equal("Detailed error message", errorResponse.Error.Message);
        }

        private static string GetResponseBody(HttpContext context)
        {
            context.Response.Body.Position = 0;
            using var reader = new StreamReader(context.Response.Body);
            return reader.ReadToEnd();
        }
    }
}