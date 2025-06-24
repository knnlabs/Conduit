using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace ConduitLLM.Tests.Services
{
    public class WebhookNotificationServiceTests
    {
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly Mock<ILogger<WebhookNotificationService>> _mockLogger;
        private readonly HttpClient _httpClient;
        private readonly WebhookNotificationService _service;

        public WebhookNotificationServiceTests()
        {
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            _mockLogger = new Mock<ILogger<WebhookNotificationService>>();
            _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
            _service = new WebhookNotificationService(_httpClient, _mockLogger.Object);
        }

        [Fact]
        public async Task SendTaskCompletionWebhookAsync_Success_ReturnsTrue()
        {
            // Arrange
            var webhookUrl = "https://example.com/webhook";
            var payload = new VideoCompletionWebhookPayload
            {
                TaskId = "test-task-123",
                Status = "completed",
                VideoUrl = "https://cdn.example.com/video.mp4",
                Model = "minimax-video",
                Prompt = "A cat playing piano"
            };

            var expectedResponse = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("Success")
            };

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    Moq.Protected.ItExpr.IsAny<HttpRequestMessage>(),
                    Moq.Protected.ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _service.SendTaskCompletionWebhookAsync(webhookUrl, payload);

            // Assert
            Assert.True(result);
            _mockHttpMessageHandler.Protected().Verify(
                "SendAsync",
                Times.Once(),
                Moq.Protected.ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.ToString() == webhookUrl),
                Moq.Protected.ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task SendTaskCompletionWebhookAsync_WithHeaders_IncludesHeaders()
        {
            // Arrange
            var webhookUrl = "https://example.com/webhook";
            var headers = new Dictionary<string, string>
            {
                { "Authorization", "Bearer secret-token" },
                { "X-Custom-Header", "custom-value" }
            };
            var payload = new VideoCompletionWebhookPayload
            {
                TaskId = "test-task-123",
                Status = "completed"
            };

            HttpRequestMessage? capturedRequest = null;
            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    Moq.Protected.ItExpr.IsAny<HttpRequestMessage>(),
                    Moq.Protected.ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
                .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

            // Act
            await _service.SendTaskCompletionWebhookAsync(webhookUrl, payload, headers);

            // Assert
            Assert.NotNull(capturedRequest);
            Assert.Contains("Bearer secret-token", capturedRequest.Headers.GetValues("Authorization"));
            Assert.Contains("custom-value", capturedRequest.Headers.GetValues("X-Custom-Header"));
        }

        [Fact]
        public async Task SendTaskCompletionWebhookAsync_HttpError_ReturnsFalse()
        {
            // Arrange
            var webhookUrl = "https://example.com/webhook";
            var payload = new VideoCompletionWebhookPayload
            {
                TaskId = "test-task-123",
                Status = "failed",
                Error = "Generation failed"
            };

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    Moq.Protected.ItExpr.IsAny<HttpRequestMessage>(),
                    Moq.Protected.ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.InternalServerError,
                    ReasonPhrase = "Internal Server Error"
                });

            // Act
            var result = await _service.SendTaskCompletionWebhookAsync(webhookUrl, payload);

            // Assert
            Assert.False(result);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to send completion webhook")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task SendTaskCompletionWebhookAsync_Timeout_ReturnsFalse()
        {
            // Arrange
            var webhookUrl = "https://example.com/webhook";
            var payload = new VideoCompletionWebhookPayload
            {
                TaskId = "test-task-123",
                Status = "completed"
            };

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    Moq.Protected.ItExpr.IsAny<HttpRequestMessage>(),
                    Moq.Protected.ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new TaskCanceledException());

            // Act
            var result = await _service.SendTaskCompletionWebhookAsync(webhookUrl, payload);

            // Assert
            Assert.False(result);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("timed out")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task SendTaskProgressWebhookAsync_Success_ReturnsTrue()
        {
            // Arrange
            var webhookUrl = "https://example.com/webhook";
            var payload = new VideoProgressWebhookPayload
            {
                TaskId = "test-task-123",
                Status = "processing",
                ProgressPercentage = 50,
                Message = "Rendering video content"
            };

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    Moq.Protected.ItExpr.IsAny<HttpRequestMessage>(),
                    Moq.Protected.ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

            // Act
            var result = await _service.SendTaskProgressWebhookAsync(webhookUrl, payload);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task SendWebhookAsync_VerifiesPayloadSerialization()
        {
            // Arrange
            var webhookUrl = "https://example.com/webhook";
            var payload = new VideoCompletionWebhookPayload
            {
                TaskId = "test-task-123",
                Status = "completed",
                VideoUrl = "https://cdn.example.com/video.mp4",
                GenerationDurationSeconds = 45.5,
                Model = "minimax-video",
                Prompt = "A beautiful sunset"
            };

            string? capturedContent = null;
            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    Moq.Protected.ItExpr.IsAny<HttpRequestMessage>(),
                    Moq.Protected.ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>(async (req, _) =>
                {
                    capturedContent = await req.Content!.ReadAsStringAsync();
                })
                .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

            // Act
            await _service.SendTaskCompletionWebhookAsync(webhookUrl, payload);

            // Assert
            Assert.NotNull(capturedContent);
            var deserialized = JsonSerializer.Deserialize<Dictionary<string, object>>(capturedContent);
            Assert.NotNull(deserialized);
            Assert.Equal("test-task-123", deserialized["task_id"].ToString());
            Assert.Equal("completed", deserialized["status"].ToString());
            Assert.Equal("https://cdn.example.com/video.mp4", deserialized["video_url"].ToString());
            Assert.Equal("video_generation_completed", deserialized["webhook_type"].ToString());
        }

        [Fact]
        public async Task SendWebhookAsync_NetworkError_ReturnsFalse()
        {
            // Arrange
            var webhookUrl = "https://example.com/webhook";
            var payload = new VideoCompletionWebhookPayload
            {
                TaskId = "test-task-123",
                Status = "completed"
            };

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    Moq.Protected.ItExpr.IsAny<HttpRequestMessage>(),
                    Moq.Protected.ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Network error"));

            // Act
            var result = await _service.SendTaskCompletionWebhookAsync(webhookUrl, payload);

            // Assert
            Assert.False(result);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error sending completion webhook")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }
}