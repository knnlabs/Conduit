using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Http;
using ConduitLLM.Core.Services;

using Microsoft.Extensions.Logging;

using Moq;
using Moq.Protected;

using Xunit;

using ItExpr = Moq.Protected.ItExpr;

namespace ConduitLLM.Tests.Core.Http
{
    public class CorrelationPropagationHandlerTests
    {
        private readonly Mock<ICorrelationContextService> _mockCorrelationService;
        private readonly Mock<ILogger<CorrelationPropagationHandler>> _mockLogger;
        private readonly Mock<HttpMessageHandler> _mockInnerHandler;
        private readonly CorrelationPropagationHandler _handler;
        private readonly HttpClient _httpClient;

        public CorrelationPropagationHandlerTests()
        {
            _mockCorrelationService = new Mock<ICorrelationContextService>();
            _mockLogger = new Mock<ILogger<CorrelationPropagationHandler>>();
            _mockInnerHandler = new Mock<HttpMessageHandler>();
            
            _handler = new CorrelationPropagationHandler(_mockCorrelationService.Object, _mockLogger.Object)
            {
                InnerHandler = _mockInnerHandler.Object
            };
            
            _httpClient = new HttpClient(_handler);
        }

        [Fact]
        public async Task SendAsync_AddsCorrelationHeaders()
        {
            // Arrange
            var correlationId = "test-correlation-123";
            var traceId = "trace-123";
            var propagationHeaders = new Dictionary<string, string>
            {
                ["X-Correlation-ID"] = correlationId,
                ["X-Request-ID"] = correlationId,
                ["traceparent"] = $"00-{traceId}-span123-01"
            };
            
            _mockCorrelationService.Setup(x => x.GetPropagationHeaders())
                .Returns(propagationHeaders);
            _mockCorrelationService.Setup(x => x.CorrelationId)
                .Returns(correlationId);

            HttpRequestMessage? capturedRequest = null;
            _mockInnerHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

            // Act
            var response = await _httpClient.GetAsync("https://api.example.com/test");

            // Assert
            Assert.NotNull(capturedRequest);
            Assert.True(capturedRequest!.Headers.Contains("X-Correlation-ID"));
            Assert.True(capturedRequest.Headers.Contains("X-Request-ID"));
            Assert.True(capturedRequest.Headers.Contains("traceparent"));
            Assert.Equal(correlationId, capturedRequest.Headers.GetValues("X-Correlation-ID").First());
        }

        [Fact]
        public async Task SendAsync_DoesNotOverwriteExistingHeaders()
        {
            // Arrange
            var existingCorrelationId = "existing-correlation";
            var newCorrelationId = "new-correlation";
            var propagationHeaders = new Dictionary<string, string>
            {
                ["X-Correlation-ID"] = newCorrelationId
            };
            
            _mockCorrelationService.Setup(x => x.GetPropagationHeaders())
                .Returns(propagationHeaders);
            _mockCorrelationService.Setup(x => x.CorrelationId)
                .Returns(newCorrelationId);

            HttpRequestMessage? capturedRequest = null;
            _mockInnerHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/test");
            request.Headers.Add("X-Correlation-ID", existingCorrelationId);

            // Act
            var response = await _httpClient.SendAsync(request);

            // Assert
            Assert.NotNull(capturedRequest);
            Assert.Equal(existingCorrelationId, capturedRequest!.Headers.GetValues("X-Correlation-ID").First());
        }

        [Fact]
        public async Task SendAsync_HandlesEmptyPropagationHeaders()
        {
            // Arrange
            _mockCorrelationService.Setup(x => x.GetPropagationHeaders())
                .Returns(new Dictionary<string, string>());
            _mockCorrelationService.Setup(x => x.CorrelationId)
                .Returns((string?)null);

            _mockInnerHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

            // Act
            var response = await _httpClient.GetAsync("https://api.example.com/test");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task SendAsync_LogsResponseCorrelationId()
        {
            // Arrange
            var requestCorrelationId = "request-correlation";
            var responseCorrelationId = "response-correlation";
            var propagationHeaders = new Dictionary<string, string>
            {
                ["X-Correlation-ID"] = requestCorrelationId
            };
            
            _mockCorrelationService.Setup(x => x.GetPropagationHeaders())
                .Returns(propagationHeaders);
            _mockCorrelationService.Setup(x => x.CorrelationId)
                .Returns(requestCorrelationId);

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK);
            responseMessage.Headers.Add("X-Correlation-ID", responseCorrelationId);

            _mockInnerHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            // Act
            var response = await _httpClient.GetAsync("https://api.example.com/test");

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(responseCorrelationId)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task SendAsync_HandlesException_LogsError()
        {
            // Arrange
            var correlationId = "error-correlation";
            var propagationHeaders = new Dictionary<string, string>
            {
                ["X-Correlation-ID"] = correlationId
            };
            
            _mockCorrelationService.Setup(x => x.GetPropagationHeaders())
                .Returns(propagationHeaders);
            _mockCorrelationService.Setup(x => x.CorrelationId)
                .Returns(correlationId);

            var expectedException = new HttpRequestException("Network error");
            _mockInnerHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(expectedException);

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(() => 
                _httpClient.GetAsync("https://api.example.com/test"));

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(correlationId)),
                    expectedException,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task SendAsync_PropagatesMultipleContextHeaders()
        {
            // Arrange
            var propagationHeaders = new Dictionary<string, string>
            {
                ["X-Correlation-ID"] = "correlation-123",
                ["X-Request-ID"] = "request-123",
                ["traceparent"] = "00-trace123-span456-01",
                ["tracestate"] = "vendor1=value1,vendor2=value2",
                ["X-Context-user-id"] = "user789",
                ["X-Context-tenant-id"] = "tenant456"
            };
            
            _mockCorrelationService.Setup(x => x.GetPropagationHeaders())
                .Returns(propagationHeaders);

            HttpRequestMessage? capturedRequest = null;
            _mockInnerHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

            // Act
            var response = await _httpClient.GetAsync("https://api.example.com/test");

            // Assert
            Assert.NotNull(capturedRequest);
            foreach (var header in propagationHeaders)
            {
                Assert.True(capturedRequest!.Headers.Contains(header.Key));
                Assert.Equal(header.Value, capturedRequest.Headers.GetValues(header.Key).First());
            }
        }
    }
}