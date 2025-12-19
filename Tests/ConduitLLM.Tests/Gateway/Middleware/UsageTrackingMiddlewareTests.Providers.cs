using ConduitLLM.Core.Models;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Gateway.Middleware;
using ConduitLLM.Tests.Http.Middleware.Builders;
using ConduitLLM.Tests.Http.Middleware.Assertions;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Http.Middleware
{
    /// <summary>
    /// Tests for UsageTrackingMiddleware provider-specific behavior.
    /// </summary>
    public partial class UsageTrackingMiddlewareTests
    {
        [Fact]
        public async Task OpenAI_ChatCompletion_Response_Tracks_Usage()
        {
            // Arrange
            var context = new HttpContextBuilder()
                .ForChatCompletions()
                .WithVirtualKey(123)
                .AsOpenAI()
                .Build();

            Fixture.SetupCostForModel("gpt-4", 0.001m);

            // Act
            await Invoker
                .WithResponse(ResponseBuilders.OpenAI()
                    .WithModel("gpt-4")
                    .WithUsage(9, 12)
                    .Build())
                .InvokeAsync(context);

            // Assert
            UsageTrackingAssertions.VerifyCostCalculated(Fixture.CostService, "gpt-4", 9, 12);
            UsageTrackingAssertions.VerifySpendQueued(Fixture.BatchSpendService, 123, 0.001m);
            UsageTrackingAssertions.VerifyRequestLogged(Fixture.RequestLogService, dto =>
            {
                Assert.Equal(123, dto.VirtualKeyId);
                Assert.Equal("gpt-4", dto.ModelName);
                Assert.Equal(9, dto.InputTokens);
                Assert.Equal(12, dto.OutputTokens);
                Assert.Equal(0.001m, dto.Cost);
                Assert.Equal("chat", dto.RequestType);
            });
        }

        [Fact]
        public async Task Anthropic_ChatCompletion_Response_Tracks_Usage()
        {
            // Arrange
            var context = new HttpContextBuilder()
                .ForChatCompletions()
                .WithVirtualKey(456)
                .AsAnthropic()
                .Build();

            Fixture.SetupCostForModel("claude-3-5-sonnet-20241022", 0.015m);

            // Act
            await Invoker
                .WithResponse(ResponseBuilders.Anthropic()
                    .WithModel("claude-3-5-sonnet-20241022")
                    .WithUsage(2095, 503)
                    .Build())
                .InvokeAsync(context);

            // Assert
            UsageTrackingAssertions.VerifyCostCalculated(
                Fixture.CostService,
                "claude-3-5-sonnet-20241022",
                2095,
                503);
            UsageTrackingAssertions.VerifySpendQueued(Fixture.BatchSpendService, 456, 0.015m);
        }

        [Fact]
        public async Task Anthropic_WithCachedTokens_Tracks_Usage()
        {
            // Arrange
            var context = new HttpContextBuilder()
                .ForChatCompletions()
                .WithVirtualKey(789)
                .AsAnthropic()
                .Build();

            Fixture.SetupCostForModel("claude-3-5-sonnet-20241022", 0.008m);

            // Act
            await Invoker
                .WithResponse(ResponseBuilders.Anthropic()
                    .WithModel("claude-3-5-sonnet-20241022")
                    .WithUsage(500, 100)
                    .WithCachedTokens(1500, 2000)
                    .Build())
                .InvokeAsync(context);

            // Assert
            UsageTrackingAssertions.VerifyAnthropicCaching(
                Fixture.CostService,
                "claude-3-5-sonnet-20241022",
                1500,
                2000);
        }
    }
}
