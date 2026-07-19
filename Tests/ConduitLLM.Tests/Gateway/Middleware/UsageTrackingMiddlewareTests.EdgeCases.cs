using ConduitLLM.Core.Models;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Gateway.Constants;
using ConduitLLM.Gateway.Middleware;
using ConduitLLM.Tests.Http.Middleware.Builders;
using ConduitLLM.Tests.Http.Middleware.Assertions;
using Microsoft.AspNetCore.Http;
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
        public async Task NonStreaming_Response_Without_Model_Emits_RevenueLoss_Audit()
        {
            var context = new HttpContextBuilder().ForChatCompletions().WithVirtualKey(651).Build();

            await Invoker.WithResponse(new
                {
                    usage = new { prompt_tokens = 10, completion_tokens = 2 }
                })
                .InvokeAsync(context);

            var audit = Assert.Single(Fixture.CapturedBillingEvents);
            Assert.Equal(BillingAuditEventType.MissingUsageData, audit.EventType);
            Assert.Equal("Response did not contain a model", audit.FailureReason);
            UsageTrackingAssertions.VerifyNoCostCalculation(Fixture.CostService);
        }

        [Fact]
        public async Task NonStreaming_Response_With_Unparseable_Usage_Emits_RevenueLoss_Audit()
        {
            var context = new HttpContextBuilder().ForChatCompletions().WithVirtualKey(652).Build();

            await Invoker.WithResponse(new { model = "gpt-test", usage = new { } }).InvokeAsync(context);

            var audit = Assert.Single(Fixture.CapturedBillingEvents);
            Assert.Equal(BillingAuditEventType.MissingUsageData, audit.EventType);
            Assert.Equal("gpt-test", audit.Model);
            Assert.Equal("Response usage could not be extracted", audit.FailureReason);
            UsageTrackingAssertions.VerifyNoCostCalculation(Fixture.CostService);
        }

        [Fact]
        public async Task NonStreaming_Response_With_TotalTokensOnly_Is_Billed_As_Estimated_Input()
        {
            var context = new HttpContextBuilder().ForChatCompletions().WithVirtualKey(653).Build();
            Fixture.SetupCostForModel("gpt-test", 0.004m);

            await Invoker.WithResponse(new
                {
                    model = "gpt-test",
                    usage = new { total_tokens = 80 }
                })
                .InvokeAsync(context);

            UsageTrackingAssertions.VerifyCostCalculated(Fixture.CostService, "gpt-test", 80, 0);
            UsageTrackingAssertions.VerifySpendQueued(Fixture.BatchSpendService, 653, 0.004m);
            Assert.Contains(Fixture.CapturedBillingEvents, e =>
                e.EventType == BillingAuditEventType.UsageEstimated && e.IsEstimated);
        }

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
        public async Task Streaming_Response_Without_Usage_Bills_Known_Function_Cost()
        {
            var context = new HttpContextBuilder()
                .ForChatCompletions()
                .WithVirtualKey(656)
                .AsOpenAI()
                .AsStreaming()
                .WithItem(HttpContextKeys.ChatFunctionCost, 0.05m)
                .Build();

            await Invoker
                .AsStreamingResponse()
                .InvokeAsync(context);

            UsageTrackingAssertions.VerifySpendQueued(Fixture.BatchSpendService, 656, 0.05m);
            UsageTrackingAssertions.VerifyNoCostCalculation(Fixture.CostService);
            Assert.Contains(Fixture.CapturedBillingEvents,
                e => e.EventType == BillingAuditEventType.StreamingUsageMissing);
        }

        [Fact]
        public async Task Streaming_Response_Is_Billed_Before_Copy_To_Disconnected_Client()
        {
            var streamingUsage = new Usage
            {
                PromptTokens = 50,
                CompletionTokens = 150,
                TotalTokens = 200
            };

            var context = new HttpContextBuilder()
                .ForChatCompletions()
                .WithVirtualKey(655)
                .AsOpenAI()
                .AsStreaming(streamingUsage, "gpt-4")
                .Build();
            context.Response.Body = new ThrowingWriteStream();

            Fixture.SetupCostForModel("gpt-4", 0.006m);

            await Assert.ThrowsAsync<IOException>(() => Invoker
                .WithNextDelegate(async ctx =>
                {
                    ctx.Response.ContentType = "text/event-stream";
                    await ctx.Response.Body.WriteAsync("data: partial\n\n"u8.ToArray());
                })
                .InvokeAsync(context));

            UsageTrackingAssertions.VerifySpendQueued(Fixture.BatchSpendService, 655, 0.006m);
        }

        private sealed class ThrowingWriteStream : Stream
        {
            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => throw new NotSupportedException();
            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) =>
                throw new IOException("Client disconnected");

            public override ValueTask WriteAsync(
                ReadOnlyMemory<byte> buffer,
                CancellationToken cancellationToken = default) =>
                ValueTask.FromException(new IOException("Client disconnected"));

            public override Task WriteAsync(
                byte[] buffer,
                int offset,
                int count,
                CancellationToken cancellationToken) =>
                Task.FromException(new IOException("Client disconnected"));
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
        public async Task SpendPersistenceFailure_DoesNotLogSuccessfulBilling()
        {
            // Arrange
            var context = new HttpContextBuilder()
                .ForChatCompletions()
                .WithVirtualKey(988)
                .Build();

            Fixture.SetupCostForModel("gpt-3.5-turbo", 0.0001m);
            Fixture.BatchSpendService
                .Setup(x => x.QueueSpendUpdateAsync(988, 0.0001m))
                .ThrowsAsync(new InvalidOperationException("Redis unavailable"));
            Fixture.VirtualKeyService
                .Setup(x => x.UpdateSpendAsync(988, 0.0001m))
                .ReturnsAsync(false);

            // Act
            await Invoker
                .WithResponse(ResponseBuilders.OpenAI()
                    .WithModel("gpt-3.5-turbo")
                    .WithUsage(10, 20)
                    .Build())
                .InvokeAsync(context);

            // Assert
            Assert.DoesNotContain(Fixture.CapturedBillingEvents, e =>
                e.EventType == BillingAuditEventType.UsageTracked ||
                e.EventType == BillingAuditEventType.ToolUsageTracked);
            Assert.Contains(Fixture.CapturedBillingEvents,
                e => e.EventType == BillingAuditEventType.UnexpectedError);
            Fixture.BatchSpendService.Verify(
                x => x.QueueFallbackUpdate(988, 0.0001m),
                Times.Once);
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
