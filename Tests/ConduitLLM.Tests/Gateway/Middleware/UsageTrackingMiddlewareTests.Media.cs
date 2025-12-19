using ConduitLLM.Core.Models;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Gateway.Middleware;
using ConduitLLM.Gateway.Constants;
using ConduitLLM.Tests.Http.Middleware.Builders;
using ConduitLLM.Tests.Http.Middleware.Assertions;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Http.Middleware
{
    /// <summary>
    /// Tests for UsageTrackingMiddleware image and media generation handling.
    /// </summary>
    public partial class UsageTrackingMiddlewareTests
    {
        [Fact]
        public async Task OpenAI_ImageGeneration_Response_Tracks_Usage()
        {
            // Arrange
            var context = new HttpContextBuilder()
                .ForImageGenerations()
                .WithVirtualKey(321)
                .AsOpenAI()
                .Build();

            Fixture.SetupCostForModel("dall-e-3", 0.04m);

            // Act
            await Invoker
                .WithResponse(ResponseBuilders.Image()
                    .WithModel("dall-e-3")
                    .WithImages(1)
                    .WithUsage()
                    .Build())
                .InvokeAsync(context);

            // Assert
            UsageTrackingAssertions.VerifyImageUsage(Fixture.CostService, "dall-e-3", 1);
            UsageTrackingAssertions.VerifySpendQueued(Fixture.BatchSpendService, 321, 0.04m);
            UsageTrackingAssertions.VerifyRequestLogged(Fixture.RequestLogService, dto =>
            {
                Assert.Equal("image", dto.RequestType);
            });
        }

        [Fact]
        public async Task ImageGeneration_Response_Without_Usage_Tracks_From_HttpContext()
        {
            // Arrange - This tests the real OpenAI response format which doesn't include usage data
            var context = new HttpContextBuilder()
                .ForImageGenerations()
                .WithVirtualKey(456)
                .AsOpenAI()
                .WithImageRequest("dall-e-3", "hd", "1024x1024", 2)
                .Build();

            Fixture.SetupCostForModel("dall-e-3", 0.08m);

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

            // Act
            await Invoker
                .WithResponse(imageResponse)
                .InvokeAsync(context);

            // Assert - Verify usage was constructed from HttpContext.Items and data array
            UsageTrackingAssertions.VerifyImageUsage(
                Fixture.CostService,
                "dall-e-3",
                expectedImageCount: 2,
                expectedQuality: "hd",
                expectedSize: "1024x1024");
            UsageTrackingAssertions.VerifySpendQueued(Fixture.BatchSpendService, 456, 0.08m);
            UsageTrackingAssertions.VerifyRequestLogged(Fixture.RequestLogService, dto =>
            {
                Assert.Equal("image", dto.RequestType);
                Assert.Equal("dall-e-3", dto.ModelName);
                Assert.Equal(456, dto.VirtualKeyId);
            });
        }

        [Fact]
        public async Task ImageGeneration_Response_Falls_Back_To_Response_Model()
        {
            // Arrange - When HttpContext.Items doesn't have the model, fall back to response
            var context = new HttpContextBuilder()
                .ForImageGenerations()
                .WithVirtualKey(789)
                .AsOpenAI()
                // Note: NOT setting image request metadata
                .Build();

            Fixture.SetupCostForModel("dall-e-2", 0.02m);

            // Response includes model (some providers might include it)
            var imageResponse = new
            {
                created = 1677652288,
                model = "dall-e-2",
                data = new[]
                {
                    new { url = "https://example.com/image1.png" }
                }
            };

            // Act
            await Invoker
                .WithResponse(imageResponse)
                .InvokeAsync(context);

            // Assert - Model should come from response
            UsageTrackingAssertions.VerifyImageUsage(Fixture.CostService, "dall-e-2", 1);
            UsageTrackingAssertions.VerifySpendQueued(Fixture.BatchSpendService, 789, 0.02m);
            UsageTrackingAssertions.VerifyRequestLogged(Fixture.RequestLogService, dto =>
            {
                Assert.Equal("dall-e-2", dto.ModelName);
            });
        }
    }
}
