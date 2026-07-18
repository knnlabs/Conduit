using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ConduitLLM.Configuration.Messaging.MassTransit;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using ConduitLLM.Gateway.Consumers;
using ConduitLLM.Gateway.Services;

namespace ConduitLLM.Tests.Integration
{
    /// <summary>
    /// Regression tests for issue #957: WebhookDeliveryConsumer's deferred retry uses
    /// context.ScheduleSend, which throws ("The payload was not found:
    /// MassTransit.MessageSchedulerContext") unless the bus configures a message scheduler
    /// via UseDelayedMessageScheduler(). These tests mirror the production bus configuration
    /// and verify the deferred retry ladder actually runs.
    /// </summary>
    [Trait("Category", "Integration")]
    public class WebhookDeliveryRetrySchedulingTests : IAsyncLifetime
    {
        private ServiceProvider _serviceProvider;
        private ITestHarness _harness;
        private Mock<IWebhookNotificationService> _webhookService;

        public async Task InitializeAsync()
        {
            _webhookService = new Mock<IWebhookNotificationService>();

            var deliveryTracker = new Mock<IWebhookDeliveryTracker>();
            deliveryTracker.Setup(x => x.IsDeliveredAsync(It.IsAny<string>())).ReturnsAsync(false);

            var circuitBreaker = new Mock<IWebhookCircuitBreaker>();
            circuitBreaker.Setup(x => x.IsOpen(It.IsAny<string>())).Returns(false);

            var services = new ServiceCollection();
            // WebhookDeliveryConsumer implements IEventHandler<T> (epic #909), so register
            // the generic bridge consumer on the bus and the handler + event bus in DI.
            services.AddMassTransitTestHarness(cfg =>
            {
                cfg.AddEventBridge<WebhookDeliveryRequested>();

                cfg.UsingInMemory((context, busCfg) =>
                {
                    // Mirrors Program.Messaging.cs — without this line ScheduleSend throws
                    // and the deferred retry never happens (issue #957)
                    busCfg.UseDelayedMessageScheduler();
                    busCfg.ConfigureEndpoints(context);
                });
            });

            services.AddMassTransitEventBus();
            services.AddEventHandler<WebhookDeliveryRequested, WebhookDeliveryConsumer>();

            services.AddSingleton(_webhookService.Object);
            services.AddSingleton(deliveryTracker.Object);
            services.AddSingleton(circuitBreaker.Object);
            services.AddSingleton(Mock.Of<IWebhookDeliveryNotificationService>());
            services.AddSingleton(Mock.Of<ILogger<WebhookDeliveryConsumer>>());

            _serviceProvider = services.BuildServiceProvider();
            _harness = _serviceProvider.GetRequiredService<ITestHarness>();
            _harness.TestTimeout = TimeSpan.FromSeconds(15);

            await _harness.Start();
        }

        public async Task DisposeAsync()
        {
            await _harness.Stop();
            if (_serviceProvider is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else
            {
                _serviceProvider?.Dispose();
            }
        }

        private static WebhookDeliveryRequested CreateRequest(int retryCount = 0) => new()
        {
            TaskId = "task-957",
            TaskType = "video",
            WebhookUrl = "https://example.com/webhook",
            EventType = WebhookEventType.TaskCompleted,
            PayloadJson = "{\"status\":\"completed\"}",
            RetryCount = retryCount
        };

        [Fact]
        public async Task FailedDelivery_SchedulesDeferredRetry_WithIncrementedRetryCount()
        {
            // Webhook endpoint always fails
            _webhookService
                .Setup(x => x.SendTaskCompletionWebhookAsync(
                    It.IsAny<string>(), It.IsAny<object>(),
                    It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            await _harness.Bus.Publish(CreateRequest(retryCount: 0));

            // First consume must complete WITHOUT faulting — before the fix, ScheduleSend
            // threw and faulted the consume, so the transport retried the same message
            // with RetryCount stuck at 0 instead of running the deferred ladder
            Assert.True(await _harness.Consumed.Any<WebhookDeliveryRequested>(
                x => x.Context.Message.RetryCount == 0));
            Assert.False(await _harness.Consumed.Any<WebhookDeliveryRequested>(
                x => x.Context.Message.RetryCount == 0 && x.Exception != null));

            // The scheduled retry (2^1 = 2s delay) must arrive with RetryCount incremented
            var retryConsumed = await WaitForConsumedAsync(
                m => m.RetryCount == 1, TimeSpan.FromSeconds(10));
            Assert.True(retryConsumed, "Deferred retry with RetryCount=1 was never consumed");
        }

        [Fact]
        public async Task FailedDelivery_AtMaxRetries_GivesUpWithoutScheduling()
        {
            _webhookService
                .Setup(x => x.SendTaskCompletionWebhookAsync(
                    It.IsAny<string>(), It.IsAny<object>(),
                    It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // RetryCount already at the max (3) — consumer should take the give-up path
            await _harness.Bus.Publish(CreateRequest(retryCount: 3));

            Assert.True(await _harness.Consumed.Any<WebhookDeliveryRequested>(
                x => x.Context.Message.RetryCount == 3));

            // Give-up path throws InvalidOperationException so MassTransit dead-letters it
            Assert.True(await _harness.Consumed.Any<WebhookDeliveryRequested>(
                x => x.Context.Message.RetryCount == 3
                     && x.Exception is InvalidOperationException));

            // No further retry message may be scheduled past the max
            Assert.DoesNotContain(
                _harness.Consumed.Select<WebhookDeliveryRequested>(),
                x => x.Context.Message.RetryCount > 3);
        }

        private async Task<bool> WaitForConsumedAsync(
            Func<WebhookDeliveryRequested, bool> predicate, TimeSpan timeout)
        {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < timeout)
            {
                if (_harness.Consumed
                    .Select<WebhookDeliveryRequested>()
                    .Any(x => predicate(x.Context.Message)))
                {
                    return true;
                }
                await Task.Delay(250);
            }
            return false;
        }
    }
}
