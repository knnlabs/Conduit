using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using MassTransit;
using MassTransit.Testing;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using ConduitLLM.Http.Consumers;

namespace ConduitLLM.Tests.Consumers
{
    /// <summary>
    /// Tests for WebhookDeliveryConsumer
    /// </summary>
    public class WebhookDeliveryConsumerTests
    {
        private readonly Mock<IWebhookNotificationService> _mockWebhookService;
        private readonly Mock<IWebhookDeliveryTracker> _mockDeliveryTracker;
        private readonly Mock<IWebhookCircuitBreaker> _mockCircuitBreaker;
        private readonly Mock<ILogger<WebhookDeliveryConsumer>> _mockLogger;
        private readonly WebhookDeliveryConsumer _consumer;
        
        public WebhookDeliveryConsumerTests()
        {
            _mockWebhookService = new Mock<IWebhookNotificationService>();
            _mockDeliveryTracker = new Mock<IWebhookDeliveryTracker>();
            _mockCircuitBreaker = new Mock<IWebhookCircuitBreaker>();
            _mockLogger = new Mock<ILogger<WebhookDeliveryConsumer>>();
            
            _consumer = new WebhookDeliveryConsumer(
                _mockWebhookService.Object,
                _mockDeliveryTracker.Object,
                _mockCircuitBreaker.Object,
                _mockLogger.Object);
        }
        
        [Fact]
        public async Task Consume_WhenWebhookAlreadyDelivered_ShouldSkipDelivery()
        {
            // Arrange
            var request = new WebhookDeliveryRequested
            {
                TaskId = "test-task-123",
                TaskType = "video",
                WebhookUrl = "https://example.com/webhook",
                EventType = WebhookEventType.TaskCompleted,
                PayloadJson = "{\"status\":\"completed\"}",
                Headers = new Dictionary<string, string>()
            };
            
            var harness = new InMemoryTestHarness();
            var consumerHarness = harness.Consumer(() => _consumer);
            
            _mockDeliveryTracker
                .Setup(x => x.IsDeliveredAsync(It.IsAny<string>()))
                .ReturnsAsync(true);
            
            _mockCircuitBreaker
                .Setup(x => x.IsOpen(It.IsAny<string>()))
                .Returns(false);
            
            await harness.Start();
            
            try
            {
                // Act
                await harness.InputQueueSendEndpoint.Send(request);
                
                // Assert
                Assert.True(await harness.Consumed.Any<WebhookDeliveryRequested>());
                Assert.True(await consumerHarness.Consumed.Any<WebhookDeliveryRequested>());
                
                // Verify webhook service was not called
                _mockWebhookService.Verify(
                    x => x.SendTaskCompletionWebhookAsync(
                        It.IsAny<string>(),
                        It.IsAny<object>(),
                        It.IsAny<Dictionary<string, string>>(),
                        It.IsAny<CancellationToken>()),
                    Times.Never);
                
                // Verify delivery was checked
                _mockDeliveryTracker.Verify(
                    x => x.IsDeliveredAsync(It.IsAny<string>()),
                    Times.Once);
            }
            finally
            {
                await harness.Stop();
            }
        }
        
        [Fact]
        public async Task Consume_WhenWebhookDeliverySucceeds_ShouldMarkAsDelivered()
        {
            // Arrange
            var request = new WebhookDeliveryRequested
            {
                TaskId = "test-task-123",
                TaskType = "video",
                WebhookUrl = "https://example.com/webhook",
                EventType = WebhookEventType.TaskCompleted,
                PayloadJson = "{\"status\":\"completed\"}",
                Headers = new Dictionary<string, string> { { "X-Custom", "Header" } }
            };
            
            var harness = new InMemoryTestHarness();
            var consumerHarness = harness.Consumer(() => _consumer);
            
            _mockDeliveryTracker
                .Setup(x => x.IsDeliveredAsync(It.IsAny<string>()))
                .ReturnsAsync(false);
            
            _mockWebhookService
                .Setup(x => x.SendTaskCompletionWebhookAsync(
                    It.IsAny<string>(),
                    It.IsAny<object>(),
                    It.IsAny<Dictionary<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            
            await harness.Start();
            
            try
            {
                // Act
                await harness.InputQueueSendEndpoint.Send(request);
                
                // Assert
                Assert.True(await harness.Consumed.Any<WebhookDeliveryRequested>());
                Assert.True(await consumerHarness.Consumed.Any<WebhookDeliveryRequested>());
                
                // Verify webhook was sent
                _mockWebhookService.Verify(
                    x => x.SendTaskCompletionWebhookAsync(
                        request.WebhookUrl,
                        It.IsAny<object>(),
                        request.Headers,
                        It.IsAny<CancellationToken>()),
                    Times.Once);
                
                // Verify delivery was marked
                _mockDeliveryTracker.Verify(
                    x => x.MarkDeliveredAsync(It.IsAny<string>(), request.WebhookUrl),
                    Times.Once);
            }
            finally
            {
                await harness.Stop();
            }
        }
        
        [Fact]
        public async Task Consume_WhenWebhookDeliveryFails_ShouldScheduleRetry()
        {
            // Arrange
            var request = new WebhookDeliveryRequested
            {
                TaskId = "test-task-123",
                TaskType = "image",
                WebhookUrl = "https://example.com/webhook",
                EventType = WebhookEventType.TaskFailed,
                PayloadJson = "{\"status\":\"failed\",\"error\":\"Test error\"}",
                Headers = null,
                RetryCount = 0
            };
            
            var harness = new InMemoryTestHarness();
            var consumerHarness = harness.Consumer(() => _consumer);
            
            _mockDeliveryTracker
                .Setup(x => x.IsDeliveredAsync(It.IsAny<string>()))
                .ReturnsAsync(false);
            
            _mockWebhookService
                .Setup(x => x.SendTaskCompletionWebhookAsync(
                    It.IsAny<string>(),
                    It.IsAny<object>(),
                    It.IsAny<Dictionary<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            
            await harness.Start();
            
            try
            {
                // Act
                await harness.InputQueueSendEndpoint.Send(request);
                
                // Wait for message processing
                await Task.Delay(100);
                
                // Assert
                Assert.True(await harness.Consumed.Any<WebhookDeliveryRequested>());
                
                // Verify failure was recorded
                _mockDeliveryTracker.Verify(
                    x => x.RecordFailureAsync(
                        It.IsAny<string>(),
                        request.WebhookUrl,
                        It.IsAny<string>()),
                    Times.Once);
                
                // Note: MassTransit InMemoryTestHarness doesn't expose scheduled messages directly
                // The retry logic is handled internally by MassTransit's retry mechanism
            }
            finally
            {
                await harness.Stop();
            }
        }
        
        [Fact]
        public async Task Consume_WhenMaxRetriesExceeded_ShouldThrowException()
        {
            // Arrange
            var request = new WebhookDeliveryRequested
            {
                TaskId = "test-task-123",
                TaskType = "video",
                WebhookUrl = "https://example.com/webhook",
                EventType = WebhookEventType.TaskCompleted,
                PayloadJson = "{\"status\":\"completed\"}",
                Headers = null,
                RetryCount = 3 // Max retries already reached
            };
            
            var harness = new InMemoryTestHarness();
            var consumerHarness = harness.Consumer(() => _consumer);
            
            _mockDeliveryTracker
                .Setup(x => x.IsDeliveredAsync(It.IsAny<string>()))
                .ReturnsAsync(false);
            
            _mockWebhookService
                .Setup(x => x.SendTaskCompletionWebhookAsync(
                    It.IsAny<string>(),
                    It.IsAny<object>(),
                    It.IsAny<Dictionary<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            
            await harness.Start();
            
            try
            {
                // Act
                await harness.InputQueueSendEndpoint.Send(request);
                
                // Wait for message processing
                await Task.Delay(100);
                
                // Assert
                Assert.True(await harness.Consumed.Any<WebhookDeliveryRequested>());
                
                // Verify the consumer faulted (threw exception)
                Assert.True(await consumerHarness.Consumed.Any<WebhookDeliveryRequested>(
                    x => x.Exception != null));
                
                // Verify failure was recorded
                _mockDeliveryTracker.Verify(
                    x => x.RecordFailureAsync(
                        It.IsAny<string>(),
                        request.WebhookUrl,
                        It.Is<string>(msg => msg.Contains("Max retries"))),
                    Times.Once);
            }
            finally
            {
                await harness.Stop();
            }
        }
        
        [Fact]
        public async Task Consume_ProgressWebhook_ShouldCallProgressMethod()
        {
            // Arrange
            var request = new WebhookDeliveryRequested
            {
                TaskId = "test-task-123",
                TaskType = "video",
                WebhookUrl = "https://example.com/webhook",
                EventType = WebhookEventType.TaskProgress,
                PayloadJson = "{\"progress\":50}",
                Headers = null
            };
            
            var harness = new InMemoryTestHarness();
            var consumerHarness = harness.Consumer(() => _consumer);
            
            _mockDeliveryTracker
                .Setup(x => x.IsDeliveredAsync(It.IsAny<string>()))
                .ReturnsAsync(false);
            
            _mockWebhookService
                .Setup(x => x.SendTaskProgressWebhookAsync(
                    It.IsAny<string>(),
                    It.IsAny<object>(),
                    It.IsAny<Dictionary<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            
            await harness.Start();
            
            try
            {
                // Act
                await harness.InputQueueSendEndpoint.Send(request);
                
                // Assert
                Assert.True(await harness.Consumed.Any<WebhookDeliveryRequested>());
                
                // Verify progress webhook was called
                _mockWebhookService.Verify(
                    x => x.SendTaskProgressWebhookAsync(
                        request.WebhookUrl,
                        It.IsAny<object>(),
                        It.IsAny<Dictionary<string, string>>(),
                        It.IsAny<CancellationToken>()),
                    Times.Once);
                
                // Verify completion webhook was not called
                _mockWebhookService.Verify(
                    x => x.SendTaskCompletionWebhookAsync(
                        It.IsAny<string>(),
                        It.IsAny<object>(),
                        It.IsAny<Dictionary<string, string>>(),
                        It.IsAny<CancellationToken>()),
                    Times.Never);
            }
            finally
            {
                await harness.Stop();
            }
        }
    }
}