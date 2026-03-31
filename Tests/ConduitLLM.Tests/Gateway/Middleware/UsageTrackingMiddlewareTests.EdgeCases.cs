using ConduitLLM.Core.Models;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Gateway.Middleware;
using ConduitLLM.Tests.Http.Middleware.Builders;
using ConduitLLM.Tests.Http.Middleware.Assertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Http.Middleware
{
    /// <summary>
    /// Tests for UsageTrackingMiddleware edge cases including streaming,
    /// fallback behavior, billing policy, and error handling.
    /// </summary>
    public partial class UsageTrackingMiddlewareTests
    {
        [Fact]
        public async Task Streaming_Response_Uses_StreamingUsage_From_Context()
        {
            // Arrange
            var streamingUsage = new Usage
            {
                PromptTokens = 50,
                CompletionTokens = 150,
                TotalTokens = 200
            };

            var context = new HttpContextBuilder()
                .ForChatCompletions()
                .WithVirtualKey(654)
                .AsOpenAI()
                .AsStreaming(streamingUsage, "gpt-4")
                .Build();

            Fixture.SetupCostForModel("gpt-4", 0.006m);

            // Act
            await Invoker
                .AsStreamingResponse()
                .InvokeAsync(context);

            // Assert
            UsageTrackingAssertions.VerifyCostCalculated(Fixture.CostService, "gpt-4", 50, 150);
            UsageTrackingAssertions.VerifySpendQueued(Fixture.BatchSpendService, 654, 0.006m);
        }

        [Fact]
        public async Task BatchSpendService_Unhealthy_Falls_Back_To_Direct_Update()
        {
            // Arrange
            var context = new HttpContextBuilder()
                .ForChatCompletions()
                .WithVirtualKey(987)
                .Build();

            Fixture.SetupCostForModel("gpt-3.5-turbo", 0.0001m);
            Fixture.SetupBatchSpendServiceHealth(false); // Batch service is unhealthy

            // Act
            await Invoker
                .WithResponse(ResponseBuilders.OpenAI()
                    .WithModel("gpt-3.5-turbo")
                    .WithUsage(10, 20)
                    .Build())
                .InvokeAsync(context);

            // Assert - Should use direct update instead of batch
            Fixture.BatchSpendService.Verify(
                x => x.QueueSpendUpdateAsync(It.IsAny<int>(), It.IsAny<decimal>()),
                Times.Never);
            UsageTrackingAssertions.VerifyDirectSpendUpdate(
                Fixture.VirtualKeyService,
                987,
                0.0001m);
        }

        [Fact]
        public async Task Zero_Cost_Does_Not_Update_Spend()
        {
            // Arrange
            var context = new HttpContextBuilder()
                .ForChatCompletions()
                .WithVirtualKey(111)
                .Build();

            Fixture.SetupCostForModel("test-free-model", 0m);

            // Act
            await Invoker
                .WithResponse(ResponseBuilders.OpenAI()
                    .WithModel("test-free-model")
                    .WithUsage(10, 20)
                    .Build())
                .InvokeAsync(context);

            // Assert - No spend update for zero cost
            UsageTrackingAssertions.VerifyNoSpendUpdate(
                Fixture.BatchSpendService,
                Fixture.VirtualKeyService);
        }

        [Fact]
        public async Task Non_API_Request_Skips_Tracking()
        {
            // Arrange
            var context = new HttpContextBuilder()
                .ForNonApiPath()
                .Build();

            // Act
            await Invoker
                .AsPassthrough()
                .InvokeAsync(context);

            // Assert
            UsageTrackingAssertions.VerifyNoCostCalculation(Fixture.CostService);
            Fixture.BatchSpendService.Verify(
                x => x.QueueSpendUpdateAsync(It.IsAny<int>(), It.IsAny<decimal>()),
                Times.Never);
        }

        [Fact]
        public async Task Error_Response_Skips_Tracking()
        {
            // Arrange
            var context = new HttpContextBuilder()
                .ForChatCompletions()
                .WithVirtualKey(123)
                .AsError(400)
                .Build();

            // Act
            await Invoker
                .AsPassthrough()
                .InvokeAsync(context);

            // Assert - No cost calculation or spend update should occur
            UsageTrackingAssertions.VerifyNoCostCalculation(Fixture.CostService);
            Fixture.BatchSpendService.Verify(
                x => x.QueueSpendUpdateAsync(It.IsAny<int>(), It.IsAny<decimal>()),
                Times.Never);

            // Assert - Debug log should indicate billing was skipped due to error response
            UsageTrackingAssertions.VerifyDebugLog(
                Fixture.Logger,
                "Billing Policy: Skipping billing for error response");
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
            var context = new HttpContextBuilder()
                .ForChatCompletions()
                .WithVirtualKey(123)
                .AsError(statusCode)
                .Build();

            // Act
            await Invoker
                .AsPassthrough()
                .InvokeAsync(context);

            // Assert - No billing should occur for any error status
            UsageTrackingAssertions.VerifyNoCostCalculation(Fixture.CostService);
            Fixture.BatchSpendService.Verify(
                x => x.QueueSpendUpdateAsync(It.IsAny<int>(), It.IsAny<decimal>()),
                Times.Never);

            // Assert - Appropriate debug logging
            UsageTrackingAssertions.VerifyDebugLog(
                Fixture.Logger,
                "Billing Policy: Skipping billing for error response");
        }

        [Fact]
        public async Task Missing_VirtualKey_Skips_Tracking()
        {
            // Arrange - Don't add VirtualKeyId to context
            var context = new HttpContextBuilder()
                .ForChatCompletions()
                // Note: NOT calling WithVirtualKey()
                .Build();

            // Act
            await Invoker
                .AsPassthrough()
                .InvokeAsync(context);

            // Assert
            UsageTrackingAssertions.VerifyNoCostCalculation(Fixture.CostService);

            // Assert - Debug log should indicate no virtual key
            UsageTrackingAssertions.VerifyDebugLog(
                Fixture.Logger,
                "Billing Policy: Skipping billing - no virtual key found");
        }

        [Fact]
        public async Task Response_Time_Tracking()
        {
            // Arrange
            var startTime = DateTime.UtcNow.AddMilliseconds(-250); // 250ms ago

            var context = new HttpContextBuilder()
                .ForChatCompletions()
                .WithVirtualKey(555)
                .WithRequestStartTime(startTime)
                .Build();

            Fixture.SetupCostForModel("gpt-4", 0.001m);

            // Act
            await Invoker
                .WithResponse(ResponseBuilders.OpenAI()
                    .WithModel("gpt-4")
                    .WithUsage(10, 20)
                    .Build())
                .InvokeAsync(context);

            // Assert - Response time should be captured
            UsageTrackingAssertions.VerifyRequestLogged(Fixture.RequestLogService, dto =>
            {
                Assert.True(dto.ResponseTimeMs >= 250, $"Expected ResponseTimeMs >= 250, got {dto.ResponseTimeMs}");
                Assert.True(dto.ResponseTimeMs <= 1000, $"Expected ResponseTimeMs <= 1000, got {dto.ResponseTimeMs}");
            });
        }
    }
}
