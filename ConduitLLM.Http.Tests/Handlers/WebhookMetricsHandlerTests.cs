using System.Net;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;
using ConduitLLM.Http.Handlers;

namespace ConduitLLM.Http.Tests.Handlers
{
    public class WebhookMetricsHandlerTests
    {
        private readonly Mock<ILogger<WebhookMetricsHandler>> _loggerMock;
        private readonly WebhookMetricsHandler _handler;
        private readonly Mock<HttpMessageHandler> _innerHandlerMock;

        public WebhookMetricsHandlerTests()
        {
            _loggerMock = new Mock<ILogger<WebhookMetricsHandler>>();
            _handler = new WebhookMetricsHandler(_loggerMock.Object);
            _innerHandlerMock = new Mock<HttpMessageHandler>();
            _handler.InnerHandler = _innerHandlerMock.Object;
        }

        [Fact]
        public async Task SendAsync_SuccessfulRequest_RecordsMetrics()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Post, "https://example.com/webhook");
            var response = new HttpResponseMessage(HttpStatusCode.OK);

            _innerHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            // Act
            var result = await _handler.SendAsync(request, CancellationToken.None);

            // Assert
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            _loggerMock.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
        }

        [Fact]
        public async Task SendAsync_FailedRequest_LogsWarning()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Post, "https://example.com/webhook");
            var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);

            _innerHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            // Act
            var result = await _handler.SendAsync(request, CancellationToken.None);

            // Assert
            Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
            _loggerMock.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("example.com")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task SendAsync_Timeout_RecordsTimeoutMetrics()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Post, "https://example.com/webhook");

            _innerHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new TaskCanceledException());

            // Act & Assert
            await Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await _handler.SendAsync(request, CancellationToken.None));

            _loggerMock.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("timed out")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task SendAsync_SlowRequest_LogsWarning()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Post, "https://example.com/webhook");
            var response = new HttpResponseMessage(HttpStatusCode.OK);

            _innerHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() =>
                {
                    // Simulate slow request
                    Thread.Sleep(5100);
                    return response;
                });

            // Act
            var result = await _handler.SendAsync(request, CancellationToken.None);

            // Assert
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            _loggerMock.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Slow webhook request")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task SendAsync_HttpRequestException_RecordsErrorMetrics()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Post, "https://example.com/webhook");

            _innerHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Connection failed"));

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(async () =>
                await _handler.SendAsync(request, CancellationToken.None));

            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("HTTP error")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }
}