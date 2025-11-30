using ConduitLLM.Core.Models;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Gateway.Middleware;
using ConduitLLM.Gateway.Constants;
using Moq;

namespace ConduitLLM.Tests.Http.Middleware
{
    public partial class UsageTrackingMiddlewareTests
    {
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
                _mockRequestLogService.Object, _mockVirtualKeyService.Object, _mockBillingAuditService.Object, _mockToolCostService.Object);

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
        public async Task ImageGeneration_Response_Without_Usage_Tracks_From_HttpContext()
        {
            // Arrange - This tests the real OpenAI response format which doesn't include usage data
            var context = CreateHttpContext("/v1/images/generations");
            var virtualKeyId = 456;
            var virtualKey = "test-key-456";

            context.Items["VirtualKeyId"] = virtualKeyId;
            context.Items["VirtualKey"] = virtualKey;
            context.Items["ProviderType"] = "OpenAI";

            // Set image request details (as the controller would)
            context.Items[HttpContextKeys.ImageRequestModel] = "dall-e-3";
            context.Items[HttpContextKeys.ImageRequestQuality] = "hd";
            context.Items[HttpContextKeys.ImageRequestSize] = "1024x1024";
            context.Items[HttpContextKeys.ImageRequestN] = 2;

            // Real OpenAI image response format - NO usage property
            var imageResponse = new
            {
                created = 1677652288,
                data = new[]
                {
                    new
                    {
                        url = "https://example.com/image1.png",
                        revised_prompt = "A futuristic city with flying cars"
                    },
                    new
                    {
                        url = "https://example.com/image2.png",
                        revised_prompt = "A futuristic city with flying cars variant 2"
                    }
                }
            };

            SetupMockResponse(context, imageResponse);

            _mockCostService.Setup(x => x.CalculateCostAsync("dall-e-3", It.IsAny<Usage>(), default))
                .ReturnsAsync(0.08m);

            _mockBatchSpendService.SetupGet(x => x.IsHealthy).Returns(true);

            var middleware = new UsageTrackingMiddleware(_next, _mockLogger.Object);

            // Act
            await middleware.InvokeAsync(context, _mockCostService.Object, _mockBatchSpendService.Object,
                _mockRequestLogService.Object, _mockVirtualKeyService.Object, _mockBillingAuditService.Object, _mockToolCostService.Object);

            // Assert - Verify usage was constructed from HttpContext.Items and data array
            _mockCostService.Verify(x => x.CalculateCostAsync("dall-e-3",
                It.Is<Usage>(u =>
                    u.ImageCount == 2 &&
                    u.ImageQuality == "hd" &&
                    u.ImageResolution == "1024x1024"),
                default), Times.Once);

            _mockBatchSpendService.Verify(x => x.QueueSpendUpdate(virtualKeyId, 0.08m), Times.Once);

            _mockRequestLogService.Verify(x => x.LogRequestAsync(It.Is<LogRequestDto>(dto =>
                dto.RequestType == "image" &&
                dto.ModelName == "dall-e-3" &&
                dto.VirtualKeyId == virtualKeyId
            )), Times.Once);
        }

        [Fact]
        public async Task ImageGeneration_Response_Falls_Back_To_Response_Model()
        {
            // Arrange - When HttpContext.Items doesn't have the model, fall back to response
            var context = CreateHttpContext("/v1/images/generations");
            var virtualKeyId = 789;
            var virtualKey = "test-key-789";

            context.Items["VirtualKeyId"] = virtualKeyId;
            context.Items["VirtualKey"] = virtualKey;
            context.Items["ProviderType"] = "OpenAI";
            // Note: NOT setting HttpContextKeys.ImageRequestModel

            // Response includes model (some providers might include it)
            var imageResponse = new
            {
                created = 1677652288,
                model = "dall-e-2",
                data = new[]
                {
                    new
                    {
                        url = "https://example.com/image1.png"
                    }
                }
            };

            SetupMockResponse(context, imageResponse);

            _mockCostService.Setup(x => x.CalculateCostAsync("dall-e-2", It.IsAny<Usage>(), default))
                .ReturnsAsync(0.02m);

            _mockBatchSpendService.SetupGet(x => x.IsHealthy).Returns(true);

            var middleware = new UsageTrackingMiddleware(_next, _mockLogger.Object);

            // Act
            await middleware.InvokeAsync(context, _mockCostService.Object, _mockBatchSpendService.Object,
                _mockRequestLogService.Object, _mockVirtualKeyService.Object, _mockBillingAuditService.Object, _mockToolCostService.Object);

            // Assert - Model should come from response
            _mockCostService.Verify(x => x.CalculateCostAsync("dall-e-2",
                It.Is<Usage>(u => u.ImageCount == 1),
                default), Times.Once);

            _mockBatchSpendService.Verify(x => x.QueueSpendUpdate(virtualKeyId, 0.02m), Times.Once);

            _mockRequestLogService.Verify(x => x.LogRequestAsync(It.Is<LogRequestDto>(dto =>
                dto.ModelName == "dall-e-2"
            )), Times.Once);
        }
    }
}