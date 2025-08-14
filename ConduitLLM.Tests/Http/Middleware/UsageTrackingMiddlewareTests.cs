using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ConduitLLM.Http.Middleware;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Configuration.Services;
using ConduitLLM.Configuration.DTOs;
using IVirtualKeyService = ConduitLLM.Core.Interfaces.IVirtualKeyService;
using ConduitLLM.Configuration.Interfaces;

namespace ConduitLLM.Tests.Http.Middleware
{
    public class UsageTrackingMiddlewareTests
    {
        private readonly Mock<ICostCalculationService> _mockCostService;
        private readonly Mock<IBatchSpendUpdateService> _mockBatchSpendService;
        private readonly Mock<IRequestLogService> _mockRequestLogService;
        private readonly Mock<IVirtualKeyService> _mockVirtualKeyService;
        private readonly Mock<ILogger<UsageTrackingMiddleware>> _mockLogger;
        private readonly UsageTrackingMiddleware _middleware;
        private RequestDelegate _next;

        public UsageTrackingMiddlewareTests()
        {
            _mockCostService = new Mock<ICostCalculationService>();
            _mockBatchSpendService = new Mock<IBatchSpendUpdateService>();
            _mockRequestLogService = new Mock<IRequestLogService>();
            _mockVirtualKeyService = new Mock<IVirtualKeyService>();
            _mockLogger = new Mock<ILogger<UsageTrackingMiddleware>>();
            
            // Default _next delegate - will be replaced by SetupMockResponse
            _next = (HttpContext ctx) => Task.CompletedTask;
            _middleware = new UsageTrackingMiddleware(_next, _mockLogger.Object);
        }

        [Fact]
        public async Task OpenAI_ChatCompletion_Response_Tracks_Usage()
        {
            // Arrange
            var context = CreateHttpContext("/v1/chat/completions");
            var virtualKeyId = 123;
            var virtualKey = "test-key-123";
            
            context.Items["VirtualKeyId"] = virtualKeyId;
            context.Items["VirtualKey"] = virtualKey;
            context.Items["ProviderType"] = "OpenAI";

            var openAiResponse = new
            {
                id = "chatcmpl-123",
                @object = "chat.completion",
                created = 1677652288,
                model = "gpt-4",
                choices = new[]
                {
                    new
                    {
                        index = 0,
                        message = new { role = "assistant", content = "Hello! How can I help you?" },
                        finish_reason = "stop"
                    }
                },
                usage = new
                {
                    prompt_tokens = 9,
                    completion_tokens = 12,
                    total_tokens = 21
                }
            };

            SetupMockResponse(context, openAiResponse);
            
            _mockCostService.Setup(x => x.CalculateCostAsync("gpt-4", It.IsAny<Usage>(), default))
                .ReturnsAsync(0.001m);
            
            _mockBatchSpendService.SetupGet(x => x.IsHealthy).Returns(true);

            // Create a new middleware instance that uses our updated _next delegate
            var middleware = new UsageTrackingMiddleware(_next, _mockLogger.Object);

            // Act
            await middleware.InvokeAsync(context, _mockCostService.Object, _mockBatchSpendService.Object, 
                _mockRequestLogService.Object, _mockVirtualKeyService.Object);

            // Assert
            _mockCostService.Verify(x => x.CalculateCostAsync("gpt-4", 
                It.Is<Usage>(u => u.PromptTokens == 9 && u.CompletionTokens == 12), 
                default), Times.Once);
            
            _mockBatchSpendService.Verify(x => x.QueueSpendUpdate(virtualKeyId, 0.001m), Times.Once);
            
            _mockRequestLogService.Verify(x => x.LogRequestAsync(It.Is<LogRequestDto>(dto =>
                dto.VirtualKeyId == virtualKeyId &&
                dto.ModelName == "gpt-4" &&
                dto.InputTokens == 9 &&
                dto.OutputTokens == 12 &&
                dto.Cost == 0.001m &&
                dto.RequestType == "chat"
            )), Times.Once);
        }

        [Fact]
        public async Task Anthropic_ChatCompletion_Response_Tracks_Usage()
        {
            // Arrange
            var context = CreateHttpContext("/v1/chat/completions");
            var virtualKeyId = 456;
            var virtualKey = "test-key-456";
            
            context.Items["VirtualKeyId"] = virtualKeyId;
            context.Items["VirtualKey"] = virtualKey;
            context.Items["ProviderType"] = "Anthropic";

            var anthropicResponse = new
            {
                id = "msg_01XhEY9K2nPNTxWZj5vZ2VBm",
                type = "message",
                role = "assistant",
                model = "claude-3-5-sonnet-20241022",
                content = new[]
                {
                    new { type = "text", text = "Hello! I'm Claude, an AI assistant created by Anthropic." }
                },
                stop_reason = "end_turn",
                stop_sequence = (string?)null,
                usage = new
                {
                    input_tokens = 2095,
                    output_tokens = 503,
                    cache_creation_input_tokens = 0,
                    cache_read_input_tokens = 0
                }
            };

            SetupMockResponse(context, anthropicResponse);
            
            _mockCostService.Setup(x => x.CalculateCostAsync("claude-3-5-sonnet-20241022", It.IsAny<Usage>(), default))
                .ReturnsAsync(0.015m);
            
            _mockBatchSpendService.SetupGet(x => x.IsHealthy).Returns(true);

            // Create a new middleware instance that uses our updated _next delegate
            var middleware = new UsageTrackingMiddleware(_next, _mockLogger.Object);

            // Act
            await middleware.InvokeAsync(context, _mockCostService.Object, _mockBatchSpendService.Object, 
                _mockRequestLogService.Object, _mockVirtualKeyService.Object);

            // Assert
            _mockCostService.Verify(x => x.CalculateCostAsync("claude-3-5-sonnet-20241022", 
                It.Is<Usage>(u => 
                    u.PromptTokens == 2095 && 
                    u.CompletionTokens == 503 &&
                    u.CachedWriteTokens == 0 &&
                    u.CachedInputTokens == 0), 
                default), Times.Once);
            
            _mockBatchSpendService.Verify(x => x.QueueSpendUpdate(virtualKeyId, 0.015m), Times.Once);
        }

        [Fact]
        public async Task Anthropic_WithCachedTokens_Tracks_Usage()
        {
            // Arrange
            var context = CreateHttpContext("/v1/chat/completions");
            var virtualKeyId = 789;
            var virtualKey = "test-key-789";
            
            context.Items["VirtualKeyId"] = virtualKeyId;
            context.Items["VirtualKey"] = virtualKey;
            context.Items["ProviderType"] = "Anthropic";

            var anthropicResponse = new
            {
                id = "msg_01XhEY9K2nPNTxWZj5vZ2VBm",
                type = "message",
                role = "assistant",
                model = "claude-3-5-sonnet-20241022",
                content = new[]
                {
                    new { type = "text", text = "Based on our previous discussion..." }
                },
                stop_reason = "end_turn",
                usage = new
                {
                    input_tokens = 500,
                    output_tokens = 100,
                    cache_creation_input_tokens = 1500,
                    cache_read_input_tokens = 2000
                }
            };

            SetupMockResponse(context, anthropicResponse);
            
            _mockCostService.Setup(x => x.CalculateCostAsync("claude-3-5-sonnet-20241022", It.IsAny<Usage>(), default))
                .ReturnsAsync(0.008m);
            
            _mockBatchSpendService.SetupGet(x => x.IsHealthy).Returns(true);

            // Create a new middleware instance that uses our updated _next delegate
            var middleware = new UsageTrackingMiddleware(_next, _mockLogger.Object);

            // Act
            await middleware.InvokeAsync(context, _mockCostService.Object, _mockBatchSpendService.Object, 
                _mockRequestLogService.Object, _mockVirtualKeyService.Object);

            // Assert
            _mockCostService.Verify(x => x.CalculateCostAsync("claude-3-5-sonnet-20241022", 
                It.Is<Usage>(u => 
                    u.PromptTokens == 500 && 
                    u.CompletionTokens == 100 &&
                    u.CachedWriteTokens == 1500 &&
                    u.CachedInputTokens == 2000), 
                default), Times.Once);
        }

        [Fact]
        public async Task OpenAI_ImageGeneration_Response_Tracks_Usage()
        {
            // Arrange
            var context = CreateHttpContext("/v1/images/generations");
            var virtualKeyId = 321;
            var virtualKey = "test-key-321";
            
            context.Items["VirtualKeyId"] = virtualKeyId;
            context.Items["VirtualKey"] = virtualKey;
            context.Items["ProviderType"] = "OpenAI";

            var imageResponse = new
            {
                created = 1677652288,
                model = "dall-e-3",
                data = new[]
                {
                    new
                    {
                        url = "https://example.com/image1.png",
                        revised_prompt = "A futuristic city with flying cars"
                    }
                },
                usage = new
                {
                    images = 1
                }
            };

            SetupMockResponse(context, imageResponse);
            
            _mockCostService.Setup(x => x.CalculateCostAsync("dall-e-3", It.IsAny<Usage>(), default))
                .ReturnsAsync(0.04m);
            
            _mockBatchSpendService.SetupGet(x => x.IsHealthy).Returns(true);

            // Create a new middleware instance that uses our updated _next delegate
            var middleware = new UsageTrackingMiddleware(_next, _mockLogger.Object);

            // Act
            await middleware.InvokeAsync(context, _mockCostService.Object, _mockBatchSpendService.Object, 
                _mockRequestLogService.Object, _mockVirtualKeyService.Object);

            // Assert
            _mockCostService.Verify(x => x.CalculateCostAsync("dall-e-3", 
                It.Is<Usage>(u => u.ImageCount == 1), 
                default), Times.Once);
            
            _mockBatchSpendService.Verify(x => x.QueueSpendUpdate(virtualKeyId, 0.04m), Times.Once);
            
            _mockRequestLogService.Verify(x => x.LogRequestAsync(It.Is<LogRequestDto>(dto =>
                dto.RequestType == "image"
            )), Times.Once);
        }

        [Fact]
        public async Task Streaming_Response_Uses_StreamingUsage_From_Context()
        {
            // Arrange
            var context = CreateHttpContext("/v1/chat/completions");
            var virtualKeyId = 654;
            var virtualKey = "test-key-654";
            
            context.Items["VirtualKeyId"] = virtualKeyId;
            context.Items["VirtualKey"] = virtualKey;
            context.Items["IsStreamingRequest"] = true;
            context.Items["ProviderType"] = "OpenAI";
            context.Response.ContentType = "text/event-stream";

            // Simulate SSE writer storing usage data
            var streamingUsage = new Usage
            {
                PromptTokens = 50,
                CompletionTokens = 150,
                TotalTokens = 200
            };
            context.Items["StreamingUsage"] = streamingUsage;
            context.Items["StreamingModel"] = "gpt-4";
            
            _mockCostService.Setup(x => x.CalculateCostAsync("gpt-4", It.IsAny<Usage>(), default))
                .ReturnsAsync(0.006m);
            
            _mockBatchSpendService.SetupGet(x => x.IsHealthy).Returns(true);

            // Act
            await _middleware.InvokeAsync(context, _mockCostService.Object, _mockBatchSpendService.Object, 
                _mockRequestLogService.Object, _mockVirtualKeyService.Object);

            // Assert
            _mockCostService.Verify(x => x.CalculateCostAsync("gpt-4", 
                It.Is<Usage>(u => u.PromptTokens == 50 && u.CompletionTokens == 150), 
                default), Times.Once);
            
            _mockBatchSpendService.Verify(x => x.QueueSpendUpdate(virtualKeyId, 0.006m), Times.Once);
        }

        [Fact]
        public async Task BatchSpendService_Unhealthy_Falls_Back_To_Direct_Update()
        {
            // Arrange
            var context = CreateHttpContext("/v1/chat/completions");
            var virtualKeyId = 987;
            var virtualKey = "test-key-987";
            
            context.Items["VirtualKeyId"] = virtualKeyId;
            context.Items["VirtualKey"] = virtualKey;

            var response = new
            {
                model = "gpt-3.5-turbo",
                usage = new { prompt_tokens = 10, completion_tokens = 20, total_tokens = 30 }
            };

            SetupMockResponse(context, response);
            
            _mockCostService.Setup(x => x.CalculateCostAsync("gpt-3.5-turbo", It.IsAny<Usage>(), default))
                .ReturnsAsync(0.0001m);
            
            // Batch service is unhealthy
            _mockBatchSpendService.SetupGet(x => x.IsHealthy).Returns(false);

            // Create a new middleware instance that uses our updated _next delegate
            var middleware = new UsageTrackingMiddleware(_next, _mockLogger.Object);

            // Act
            await middleware.InvokeAsync(context, _mockCostService.Object, _mockBatchSpendService.Object, 
                _mockRequestLogService.Object, _mockVirtualKeyService.Object);

            // Assert
            _mockBatchSpendService.Verify(x => x.QueueSpendUpdate(It.IsAny<int>(), It.IsAny<decimal>()), Times.Never);
            _mockVirtualKeyService.Verify(x => x.UpdateSpendAsync(virtualKeyId, 0.0001m), Times.Once);
        }

        [Fact]
        public async Task Zero_Cost_Does_Not_Update_Spend()
        {
            // Arrange
            var context = CreateHttpContext("/v1/chat/completions");
            var virtualKeyId = 111;
            var virtualKey = "test-key-111";
            
            context.Items["VirtualKeyId"] = virtualKeyId;
            context.Items["VirtualKey"] = virtualKey;

            var response = new
            {
                model = "test-free-model",
                usage = new { prompt_tokens = 10, completion_tokens = 20, total_tokens = 30 }
            };

            SetupMockResponse(context, response);
            
            _mockCostService.Setup(x => x.CalculateCostAsync("test-free-model", It.IsAny<Usage>(), default))
                .ReturnsAsync(0m);

            // Act
            await _middleware.InvokeAsync(context, _mockCostService.Object, _mockBatchSpendService.Object, 
                _mockRequestLogService.Object, _mockVirtualKeyService.Object);

            // Assert
            _mockBatchSpendService.Verify(x => x.QueueSpendUpdate(It.IsAny<int>(), It.IsAny<decimal>()), Times.Never);
            _mockVirtualKeyService.Verify(x => x.UpdateSpendAsync(It.IsAny<int>(), It.IsAny<decimal>()), Times.Never);
        }

        [Fact]
        public async Task Non_API_Request_Skips_Tracking()
        {
            // Arrange
            var context = CreateHttpContext("/health");

            // Act
            await _middleware.InvokeAsync(context, _mockCostService.Object, _mockBatchSpendService.Object, 
                _mockRequestLogService.Object, _mockVirtualKeyService.Object);

            // Assert
            _mockCostService.Verify(x => x.CalculateCostAsync(It.IsAny<string>(), It.IsAny<Usage>(), default), Times.Never);
            _mockBatchSpendService.Verify(x => x.QueueSpendUpdate(It.IsAny<int>(), It.IsAny<decimal>()), Times.Never);
        }

        [Fact]
        public async Task Error_Response_Skips_Tracking()
        {
            // Arrange
            var context = CreateHttpContext("/v1/chat/completions", 400);
            context.Items["VirtualKeyId"] = 123;
            context.Items["VirtualKey"] = "test-key";

            // Act
            await _middleware.InvokeAsync(context, _mockCostService.Object, _mockBatchSpendService.Object, 
                _mockRequestLogService.Object, _mockVirtualKeyService.Object);

            // Assert
            _mockCostService.Verify(x => x.CalculateCostAsync(It.IsAny<string>(), It.IsAny<Usage>(), default), Times.Never);
        }

        [Fact]
        public async Task Missing_VirtualKey_Skips_Tracking()
        {
            // Arrange
            var context = CreateHttpContext("/v1/chat/completions");
            // Don't add VirtualKeyId to context

            // Act
            await _middleware.InvokeAsync(context, _mockCostService.Object, _mockBatchSpendService.Object, 
                _mockRequestLogService.Object, _mockVirtualKeyService.Object);

            // Assert
            _mockCostService.Verify(x => x.CalculateCostAsync(It.IsAny<string>(), It.IsAny<Usage>(), default), Times.Never);
        }

        [Fact]
        public async Task Response_Time_Tracking()
        {
            // Arrange
            var context = CreateHttpContext("/v1/chat/completions");
            var virtualKeyId = 555;
            var virtualKey = "test-key-555";
            var startTime = DateTime.UtcNow.AddMilliseconds(-250); // 250ms ago
            
            context.Items["VirtualKeyId"] = virtualKeyId;
            context.Items["VirtualKey"] = virtualKey;
            context.Items["RequestStartTime"] = startTime;

            var response = new
            {
                model = "gpt-4",
                usage = new { prompt_tokens = 10, completion_tokens = 20, total_tokens = 30 }
            };

            SetupMockResponse(context, response);
            
            _mockCostService.Setup(x => x.CalculateCostAsync("gpt-4", It.IsAny<Usage>(), default))
                .ReturnsAsync(0.001m);
            
            _mockBatchSpendService.SetupGet(x => x.IsHealthy).Returns(true);

            // Create a new middleware instance that uses our updated _next delegate
            var middleware = new UsageTrackingMiddleware(_next, _mockLogger.Object);

            // Act
            await middleware.InvokeAsync(context, _mockCostService.Object, _mockBatchSpendService.Object, 
                _mockRequestLogService.Object, _mockVirtualKeyService.Object);

            // Assert
            _mockRequestLogService.Verify(x => x.LogRequestAsync(It.Is<LogRequestDto>(dto =>
                dto.ResponseTimeMs >= 250 && dto.ResponseTimeMs <= 500 // Allow some tolerance
            )), Times.Once);
        }

        private HttpContext CreateHttpContext(string path, int statusCode = 200)
        {
            var context = new DefaultHttpContext();
            context.Request.Path = path;
            context.Response.StatusCode = statusCode;
            context.Response.Body = new MemoryStream();
            return context;
        }

        private void SetupMockResponse(HttpContext context, object responseData)
        {
            var json = JsonSerializer.Serialize(responseData);
            var bytes = Encoding.UTF8.GetBytes(json);
            
            // Replace the _next delegate to write the response
            _next = async (HttpContext ctx) => 
            {
                // Set response properties first
                ctx.Response.ContentType = "application/json";
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentLength = bytes.Length;
                
                // Write the response data to the response body
                await ctx.Response.Body.WriteAsync(bytes, 0, bytes.Length);
            };
        }
    }
}