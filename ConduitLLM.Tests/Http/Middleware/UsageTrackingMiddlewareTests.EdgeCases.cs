using ConduitLLM.Core.Models;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Http.Middleware;
using Microsoft.Extensions.Logging;
using Moq;

namespace ConduitLLM.Tests.Http.Middleware
{
    public partial class UsageTrackingMiddlewareTests
    {
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
                _mockRequestLogService.Object, _mockVirtualKeyService.Object, _mockBillingAuditService.Object);

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
                _mockRequestLogService.Object, _mockVirtualKeyService.Object, _mockBillingAuditService.Object);

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
                _mockRequestLogService.Object, _mockVirtualKeyService.Object, _mockBillingAuditService.Object);

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
                _mockRequestLogService.Object, _mockVirtualKeyService.Object, _mockBillingAuditService.Object);

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
                _mockRequestLogService.Object, _mockVirtualKeyService.Object, _mockBillingAuditService.Object);

            // Assert - No cost calculation or spend update should occur
            _mockCostService.Verify(x => x.CalculateCostAsync(It.IsAny<string>(), It.IsAny<Usage>(), default), Times.Never);
            _mockBatchSpendService.Verify(x => x.QueueSpendUpdate(It.IsAny<int>(), It.IsAny<decimal>()), Times.Never);
            
            // Assert - Debug log should indicate billing was skipped due to error response
            _mockLogger.Verify(
                x => x.Log(
                    Microsoft.Extensions.Logging.LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Billing Policy: Skipping billing for error response") && 
                                                  v.ToString().Contains("Status=400") &&
                                                  v.ToString().Contains("Reason=ErrorResponse_NoChargePolicy")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Theory]
        [InlineData(400)] // Bad Request
        [InlineData(401)] // Unauthorized
        [InlineData(404)] // Not Found
        [InlineData(429)] // Rate Limited
        [InlineData(500)] // Internal Server Error
        [InlineData(503)] // Service Unavailable
        public async Task Billing_Policy_Skips_All_Error_Status_Codes(int statusCode)
        {
            // Arrange
            var context = CreateHttpContext("/v1/chat/completions", statusCode);
            context.Items["VirtualKeyId"] = 123;
            context.Items["VirtualKey"] = "test-key";

            // Act
            await _middleware.InvokeAsync(context, _mockCostService.Object, _mockBatchSpendService.Object, 
                _mockRequestLogService.Object, _mockVirtualKeyService.Object, _mockBillingAuditService.Object);

            // Assert - No billing should occur for any error status
            _mockCostService.Verify(x => x.CalculateCostAsync(It.IsAny<string>(), It.IsAny<Usage>(), default), Times.Never);
            _mockBatchSpendService.Verify(x => x.QueueSpendUpdate(It.IsAny<int>(), It.IsAny<decimal>()), Times.Never);
            
            // Assert - Appropriate debug logging
            _mockLogger.Verify(
                x => x.Log(
                    Microsoft.Extensions.Logging.LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Billing Policy: Skipping billing for error response") && 
                                                  v.ToString().Contains($"Status={statusCode}") &&
                                                  v.ToString().Contains("Reason=ErrorResponse_NoChargePolicy")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Missing_VirtualKey_Skips_Tracking()
        {
            // Arrange
            var context = CreateHttpContext("/v1/chat/completions");
            // Don't add VirtualKeyId to context

            // Act
            await _middleware.InvokeAsync(context, _mockCostService.Object, _mockBatchSpendService.Object, 
                _mockRequestLogService.Object, _mockVirtualKeyService.Object, _mockBillingAuditService.Object);

            // Assert
            _mockCostService.Verify(x => x.CalculateCostAsync(It.IsAny<string>(), It.IsAny<Usage>(), default), Times.Never);
            
            // Assert - Debug log should indicate no virtual key
            _mockLogger.Verify(
                x => x.Log(
                    Microsoft.Extensions.Logging.LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Billing Policy: Skipping billing - no virtual key found") && 
                                                  v.ToString().Contains("Reason=NoVirtualKey")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
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
                _mockRequestLogService.Object, _mockVirtualKeyService.Object, _mockBillingAuditService.Object);

            // Assert
            _mockRequestLogService.Verify(x => x.LogRequestAsync(It.Is<LogRequestDto>(dto =>
                dto.ResponseTimeMs >= 250 && dto.ResponseTimeMs <= 500 // Allow some tolerance
            )), Times.Once);
        }
    }
}