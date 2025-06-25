using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Providers;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace ConduitLLM.Tests
{
    public class MiniMaxClientTests
    {
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
        private readonly Mock<ILogger<MiniMaxClient>> _loggerMock;
        private readonly ProviderCredentials _credentials;
        private readonly MiniMaxClient _client;

        public MiniMaxClientTests()
        {
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
            _loggerMock = new Mock<ILogger<MiniMaxClient>>();
            _credentials = new ProviderCredentials
            {
                ApiKey = "test-api-key",
                ProviderName = "minimax",
                ApiBase = "https://api.minimax.chat"
            };

            var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
            {
                BaseAddress = new Uri("https://api.minimax.chat")
            };
            _httpClientFactoryMock.Setup(f => f.CreateClient("minimaxLLMClient")).Returns(httpClient);
            _httpClientFactoryMock.Setup(f => f.CreateClient("minimaxVideoClient")).Returns(httpClient);
            _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            _client = new MiniMaxClient(_credentials, "video-01", _loggerMock.Object, _httpClientFactoryMock.Object);
        }

        [Fact]
        public async Task CreateVideoAsync_Success_ReturnsVideoUrl()
        {
            // Arrange
            var request = new VideoGenerationRequest
            {
                Prompt = "A beautiful sunset over the ocean",
                Model = "video-01",
                Duration = 6,
                Size = "1280x720"
            };

            var initialResponse = new
            {
                task_id = "test-task-123",
                status = "processing",
                base_resp = new { status_code = 0 }
            };

            var statusResponse = new
            {
                status = "Finished",
                video = new
                {
                    url = "https://example.com/video.mp4",
                    duration = 6.0,
                    resolution = "1280x720"
                },
                base_resp = new { status_code = 0 }
            };

            var callCount = 0;
            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    Moq.Protected.ItExpr.IsAny<HttpRequestMessage>(),
                    Moq.Protected.ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() =>
                {
                    callCount++;
                    if (callCount == 1)
                    {
                        // Initial video generation request
                        return new HttpResponseMessage
                        {
                            StatusCode = HttpStatusCode.OK,
                            Content = new StringContent(JsonSerializer.Serialize(initialResponse), Encoding.UTF8, "application/json")
                        };
                    }
                    else
                    {
                        // Status check request
                        return new HttpResponseMessage
                        {
                            StatusCode = HttpStatusCode.OK,
                            Content = new StringContent(JsonSerializer.Serialize(statusResponse), Encoding.UTF8, "application/json")
                        };
                    }
                });

            // Act
            var response = await _client.CreateVideoAsync(request, _credentials.ApiKey);

            // Assert
            Assert.NotNull(response);
            Assert.NotNull(response.Data);
            Assert.Single(response.Data);
            Assert.Equal("https://example.com/video.mp4", response.Data[0].Url);
            Assert.Equal(6, response.Data[0].Metadata?.Duration);
            Assert.Equal("video-01", response.Model);
            Assert.Equal(1, response.Usage?.VideosGenerated);
            Assert.Equal(6, response.Usage?.TotalDurationSeconds);
        }

        [Fact]
        public async Task CreateVideoAsync_TaskFailed_ThrowsException()
        {
            // Arrange
            var request = new VideoGenerationRequest
            {
                Prompt = "Test video",
                Model = "video-01"
            };

            var initialResponse = new
            {
                task_id = "test-task-123",
                status = "processing",
                base_resp = new { status_code = 0 }
            };

            var failedStatusResponse = new
            {
                status = "Failed",
                base_resp = new
                {
                    status_code = 2001,
                    status_msg = "Video generation failed"
                }
            };

            var callCount = 0;
            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    Moq.Protected.ItExpr.IsAny<HttpRequestMessage>(),
                    Moq.Protected.ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() =>
                {
                    callCount++;
                    if (callCount == 1)
                    {
                        return new HttpResponseMessage
                        {
                            StatusCode = HttpStatusCode.OK,
                            Content = new StringContent(JsonSerializer.Serialize(initialResponse), Encoding.UTF8, "application/json")
                        };
                    }
                    else
                    {
                        return new HttpResponseMessage
                        {
                            StatusCode = HttpStatusCode.OK,
                            Content = new StringContent(JsonSerializer.Serialize(failedStatusResponse), Encoding.UTF8, "application/json")
                        };
                    }
                });

            // Act & Assert
            await Assert.ThrowsAsync<LLMCommunicationException>(
                () => _client.CreateVideoAsync(request, _credentials.ApiKey));
        }

        [Fact]
        public async Task CreateVideoAsync_RateLimited_RetriesWithBackoff()
        {
            // Arrange
            var request = new VideoGenerationRequest
            {
                Prompt = "Test video",
                Model = "video-01"
            };

            var initialResponse = new
            {
                task_id = "test-task-123",
                status = "processing",
                base_resp = new { status_code = 0 }
            };

            var rateLimitResponse = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.TooManyRequests,
                Content = new StringContent("Rate limited", Encoding.UTF8, "application/json")
            };

            var successResponse = new
            {
                status = "Finished",
                video = new
                {
                    url = "https://example.com/video.mp4",
                    duration = 6.0,
                    resolution = "1280x720"
                },
                base_resp = new { status_code = 0 }
            };

            var callCount = 0;
            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    Moq.Protected.ItExpr.IsAny<HttpRequestMessage>(),
                    Moq.Protected.ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() =>
                {
                    callCount++;
                    if (callCount == 1)
                    {
                        // Initial request
                        return new HttpResponseMessage
                        {
                            StatusCode = HttpStatusCode.OK,
                            Content = new StringContent(JsonSerializer.Serialize(initialResponse), Encoding.UTF8, "application/json")
                        };
                    }
                    else if (callCount == 2)
                    {
                        // First status check - rate limited
                        return rateLimitResponse;
                    }
                    else
                    {
                        // Second status check - success
                        return new HttpResponseMessage
                        {
                            StatusCode = HttpStatusCode.OK,
                            Content = new StringContent(JsonSerializer.Serialize(successResponse), Encoding.UTF8, "application/json")
                        };
                    }
                });

            // Act
            var response = await _client.CreateVideoAsync(request, _credentials.ApiKey);

            // Assert
            Assert.NotNull(response);
            Assert.NotNull(response.Data);
            Assert.Single(response.Data);
            Assert.Equal("https://example.com/video.mp4", response.Data[0].Url);
            Assert.Equal(3, callCount); // Initial + rate limited + success
        }

        [Fact]
        public async Task CreateVideoAsync_ContentPolicyViolation_ThrowsException()
        {
            // Arrange
            var request = new VideoGenerationRequest
            {
                Prompt = "Inappropriate content",
                Model = "video-01"
            };

            var errorResponse = new
            {
                base_resp = new
                {
                    status_code = 2013,
                    status_msg = "Content policy violation"
                }
            };

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    Moq.Protected.ItExpr.IsAny<HttpRequestMessage>(),
                    Moq.Protected.ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(errorResponse), Encoding.UTF8, "application/json")
                });

            // Act & Assert
            var ex = await Assert.ThrowsAsync<LLMCommunicationException>(
                () => _client.CreateVideoAsync(request, _credentials.ApiKey));
            Assert.Contains("Content policy violation", ex.Message);
        }

        [Theory]
        [InlineData("720x480", 0.8)]
        [InlineData("1280x720", 1.0)]
        [InlineData("1920x1080", 1.5)]
        [InlineData("720x1280", 1.0)]
        [InlineData("1080x1920", 1.5)]
        public async Task CreateVideoAsync_WithDifferentResolutions_MapsCorrectly(string resolution, double expectedMultiplier)
        {
            // Arrange
            var request = new VideoGenerationRequest
            {
                Prompt = "Test video",
                Model = "video-01",
                Duration = 6,
                Size = resolution
            };

            var initialResponse = new
            {
                task_id = $"test-task-{resolution}",
                status = "processing",
                base_resp = new { status_code = 0 }
            };

            var statusResponse = new
            {
                status = "Finished",
                video = new
                {
                    url = $"https://example.com/video-{resolution}.mp4",
                    duration = 6.0,
                    resolution = resolution
                },
                base_resp = new { status_code = 0 }
            };

            // Recreate the mock handler and client for each test to avoid disposal issues
            var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
            var httpClient = new HttpClient(httpMessageHandlerMock.Object)
            {
                BaseAddress = new Uri("https://api.minimax.chat")
            };
            var httpClientFactoryMock = new Mock<IHttpClientFactory>();
            httpClientFactoryMock.Setup(f => f.CreateClient("minimaxLLMClient")).Returns(httpClient);
            httpClientFactoryMock.Setup(f => f.CreateClient("minimaxVideoClient")).Returns(httpClient);
            httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);
            
            var client = new MiniMaxClient(_credentials, "video-01", _loggerMock.Object, httpClientFactoryMock.Object);

            var callCount = 0;
            httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    Moq.Protected.ItExpr.IsAny<HttpRequestMessage>(),
                    Moq.Protected.ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() =>
                {
                    callCount++;
                    if (callCount == 1)
                    {
                        return new HttpResponseMessage
                        {
                            StatusCode = HttpStatusCode.OK,
                            Content = new StringContent(JsonSerializer.Serialize(initialResponse), Encoding.UTF8, "application/json")
                        };
                    }
                    else
                    {
                        return new HttpResponseMessage
                        {
                            StatusCode = HttpStatusCode.OK,
                            Content = new StringContent(JsonSerializer.Serialize(statusResponse), Encoding.UTF8, "application/json")
                        };
                    }
                });

            // Act
            var response = await client.CreateVideoAsync(request, _credentials.ApiKey);

            // Assert
            Assert.NotNull(response);
            var expectedCost = 6 * 0.15m * (decimal)expectedMultiplier; // 6 seconds * base cost * multiplier
            Assert.Equal(expectedCost, response.Usage?.EstimatedCost);
        }

        [Fact]
        public void EstimateVideoGenerationCost_CalculatesCorrectly()
        {
            // Test various scenarios
            Assert.Equal(0.9m, MiniMaxClient.EstimateVideoGenerationCost(6, "1280x720")); // 6 * 0.15 * 1.0
            Assert.Equal(1.35m, MiniMaxClient.EstimateVideoGenerationCost(6, "1920x1080")); // 6 * 0.15 * 1.5
            Assert.Equal(0.72m, MiniMaxClient.EstimateVideoGenerationCost(6, "720x480")); // 6 * 0.15 * 0.8
            Assert.Equal(0.45m, MiniMaxClient.EstimateVideoGenerationCost(3, "1280x720")); // 3 * 0.15 * 1.0
            Assert.Equal(0.15m, MiniMaxClient.EstimateVideoGenerationCost(1, "unknown")); // 1 * 0.15 * 1.0 (default)
        }
    }
}