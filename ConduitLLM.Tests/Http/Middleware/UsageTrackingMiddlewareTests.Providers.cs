using System.Threading.Tasks;
using ConduitLLM.Core.Models;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Http.Middleware;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Http.Middleware
{
    public partial class UsageTrackingMiddlewareTests
    {
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
    }
}