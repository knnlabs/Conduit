using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services.BatchOperations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;
using CoreVirtualKeyService = ConduitLLM.Core.Interfaces.IVirtualKeyService;

namespace ConduitLLM.Tests.Integration
{
    [Trait("Category", "Integration")]
    [Trait("Component", "Core")]
    [Trait("Phase", "2")]
    public class BatchSpendUpdateOperationV2IntegrationTests : TestBase
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Mock<CoreVirtualKeyService> _mockVirtualKeyService;
        private readonly Mock<ISpendNotificationService> _mockSpendNotificationService;
        private readonly Mock<IBatchOperationIdempotencyService> _mockIdempotencyService;

        public BatchSpendUpdateOperationV2IntegrationTests(ITestOutputHelper output) : base(output)
        {
            _mockVirtualKeyService = new Mock<IVirtualKeyService>();
            _mockSpendNotificationService = new Mock<ISpendNotificationService>();
            _mockIdempotencyService = new Mock<IBatchOperationIdempotencyService>();

            var services = new ServiceCollection();

            // Add logging
            services.AddLogging(builder => builder.AddXUnit(output));

            // Add required services
            services.AddScoped<IBatchOperationService, ConduitLLM.Core.Services.BatchOperationService>();
            services.AddSingleton<ITaskHub>(_ => new Mock<ITaskHub>().Object);
            services.AddSingleton<CoreVirtualKeyService>(_ => _mockVirtualKeyService.Object);
            services.AddSingleton<ISpendNotificationService>(_ => _mockSpendNotificationService.Object);
            services.AddSingleton<IBatchOperationIdempotencyService>(_ => _mockIdempotencyService.Object);
            services.AddScoped<BatchSpendUpdateOperationV2>();

            _serviceProvider = services.BuildServiceProvider();
        }

        [Fact]
        public async Task ExecuteAsync_WithValidUpdates_ShouldProcessSuccessfully()
        {
            // Arrange
            var operation = _serviceProvider.GetRequiredService<BatchSpendUpdateOperationV2>();
            var items = new List<SpendUpdateItem>
            {
                new() { VirtualKeyId = 1, Amount = 10.50m, Model = "gpt-4", Provider = "OpenAI" },
                new() { VirtualKeyId = 2, Amount = 5.25m, Model = "claude-3", Provider = "Anthropic" }
            };

            _mockVirtualKeyService.Setup(s => s.GetVirtualKeyInfoForValidationAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ConduitLLM.Configuration.Entities.VirtualKey { Id = 1, KeyName = "Test" });

            _mockVirtualKeyService.Setup(s => s.UpdateSpendAsync(It.IsAny<int>(), It.IsAny<decimal>()))
                .Returns(Task.CompletedTask);

            _mockSpendNotificationService.Setup(s => s.NotifySpendUpdatedAsync(
                    It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await operation.ExecuteAsync(items, virtualKeyId: 1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(BatchOperationStatusEnum.Completed, result.Status);
            Assert.Equal(2, result.TotalItems);
            Assert.Equal(2, result.SuccessCount);
            Assert.Equal(0, result.FailedCount);

            // Verify all updates were processed
            _mockVirtualKeyService.Verify(s => s.UpdateSpendAsync(1, 10.50m), Times.Once);
            _mockVirtualKeyService.Verify(s => s.UpdateSpendAsync(2, 5.25m), Times.Once);

            // Verify notifications were sent
            _mockSpendNotificationService.Verify(s => s.NotifySpendUpdatedAsync(
                1, 10.50m, "gpt-4", "OpenAI"), Times.Once);
            _mockSpendNotificationService.Verify(s => s.NotifySpendUpdatedAsync(
                2, 5.25m, "claude-3", "Anthropic"), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_WithIdempotencyToken_ShouldCheckForDuplicates()
        {
            // Arrange
            var operation = _serviceProvider.GetRequiredService<BatchSpendUpdateOperationV2>();
            var items = new List<SpendUpdateItem>
            {
                new() { VirtualKeyId = 1, Amount = 10.50m, Model = "gpt-4", Provider = "OpenAI" }
            };
            var token = "test-token-12345";

            _mockIdempotencyService.Setup(s => s.IsOperationProcessedAsync(token, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            _mockVirtualKeyService.Setup(s => s.GetVirtualKeyInfoForValidationAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ConduitLLM.Configuration.Entities.VirtualKey { Id = 1, KeyName = "Test" });

            _mockVirtualKeyService.Setup(s => s.UpdateSpendAsync(It.IsAny<int>(), It.IsAny<decimal>()))
                .Returns(Task.CompletedTask);

            _mockSpendNotificationService.Setup(s => s.NotifySpendUpdatedAsync(
                    It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await operation.ExecuteAsync(items, virtualKeyId: 1, idempotencyToken: token);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(BatchOperationStatusEnum.Completed, result.Status);

            // Verify idempotency was checked
            _mockIdempotencyService.Verify(
                s => s.IsOperationProcessedAsync(token, It.IsAny<CancellationToken>()),
                Times.Once);

            // Verify result was stored for future duplicate detection
            _mockIdempotencyService.Verify(
                s => s.StoreOperationResultAsync(token, It.IsAny<BatchOperationResult>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_WithDuplicateToken_ShouldReturnCachedResult()
        {
            // Arrange
            var operation = _serviceProvider.GetRequiredService<BatchSpendUpdateOperationV2>();
            var items = new List<SpendUpdateItem>
            {
                new() { VirtualKeyId = 1, Amount = 10.50m, Model = "gpt-4", Provider = "OpenAI" }
            };
            var token = "duplicate-token-12345";

            var cachedResult = new BatchOperationResult
            {
                OperationId = "cached-op-123",
                OperationType = "spend_update",
                Status = BatchOperationStatusEnum.Completed,
                TotalItems = 1,
                SuccessCount = 1,
                FailedCount = 0
            };

            _mockIdempotencyService.Setup(s => s.IsOperationProcessedAsync(token, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            _mockIdempotencyService.Setup(s => s.GetOperationResultAsync<BatchOperationResult>(token, It.IsAny<CancellationToken>()))
                .ReturnsAsync(cachedResult);

            // Act
            var result = await operation.ExecuteAsync(items, virtualKeyId: 1, idempotencyToken: token);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("cached-op-123", result.OperationId);
            Assert.Equal(BatchOperationStatusEnum.Completed, result.Status);

            // Verify the operation was NOT executed again
            _mockVirtualKeyService.Verify(s => s.UpdateSpendAsync(It.IsAny<int>(), It.IsAny<decimal>()), Times.Never);
            _mockSpendNotificationService.Verify(s => s.NotifySpendUpdatedAsync(
                It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_WithInvalidVirtualKey_ShouldFailGracefully()
        {
            // Arrange
            var operation = _serviceProvider.GetRequiredService<BatchSpendUpdateOperationV2>();
            var items = new List<SpendUpdateItem>
            {
                new() { VirtualKeyId = 999, Amount = 10.50m, Model = "gpt-4", Provider = "OpenAI" }
            };

            _mockVirtualKeyService.Setup(s => s.GetVirtualKeyInfoForValidationAsync(999, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ConduitLLM.Configuration.Entities.VirtualKey?)null);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await operation.ExecuteAsync(items, virtualKeyId: 1));
        }

        [Fact]
        public async Task ExecuteAsync_WithPartialFailures_ShouldContinueProcessing()
        {
            // Arrange
            var operation = _serviceProvider.GetRequiredService<BatchSpendUpdateOperationV2>();
            var items = new List<SpendUpdateItem>
            {
                new() { VirtualKeyId = 1, Amount = 10.50m, Model = "gpt-4", Provider = "OpenAI" },
                new() { VirtualKeyId = 999, Amount = 5.25m, Model = "claude-3", Provider = "Anthropic" }, // Invalid
                new() { VirtualKeyId = 2, Amount = 7.75m, Model = "gemini-pro", Provider = "Google" }
            };

            _mockVirtualKeyService.Setup(s => s.GetVirtualKeyInfoForValidationAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ConduitLLM.Configuration.Entities.VirtualKey { Id = 1, KeyName = "Test1" });

            _mockVirtualKeyService.Setup(s => s.GetVirtualKeyInfoForValidationAsync(2, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ConduitLLM.Configuration.Entities.VirtualKey { Id = 2, KeyName = "Test2" });

            _mockVirtualKeyService.Setup(s => s.GetVirtualKeyInfoForValidationAsync(999, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ConduitLLM.Configuration.Entities.VirtualKey?)null);

            _mockVirtualKeyService.Setup(s => s.UpdateSpendAsync(It.IsAny<int>(), It.IsAny<decimal>()))
                .Returns(Task.CompletedTask);

            _mockSpendNotificationService.Setup(s => s.NotifySpendUpdatedAsync(
                    It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await operation.ExecuteAsync(items, virtualKeyId: 1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.TotalItems);
            Assert.Equal(2, result.SuccessCount);
            Assert.Equal(1, result.FailedCount);
            Assert.Single(result.Errors);

            // Verify successful updates were processed
            _mockVirtualKeyService.Verify(s => s.UpdateSpendAsync(1, 10.50m), Times.Once);
            _mockVirtualKeyService.Verify(s => s.UpdateSpendAsync(2, 7.75m), Times.Once);

            // Verify invalid update was NOT processed
            _mockVirtualKeyService.Verify(s => s.UpdateSpendAsync(999, It.IsAny<decimal>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_WithRetryableError_ShouldRetryAutomatically()
        {
            // Arrange
            var operation = _serviceProvider.GetRequiredService<BatchSpendUpdateOperationV2>();
            var items = new List<SpendUpdateItem>
            {
                new() { VirtualKeyId = 1, Amount = 10.50m, Model = "gpt-4", Provider = "OpenAI" }
            };

            var attemptCount = 0;

            _mockVirtualKeyService.Setup(s => s.GetVirtualKeyInfoForValidationAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ConduitLLM.Configuration.Entities.VirtualKey { Id = 1, KeyName = "Test" });

            _mockVirtualKeyService.Setup(s => s.UpdateSpendAsync(1, 10.50m))
                .Returns(() =>
                {
                    attemptCount++;
                    if (attemptCount < 2)
                    {
                        throw new TimeoutException("Transient error");
                    }
                    return Task.CompletedTask;
                });

            _mockSpendNotificationService.Setup(s => s.NotifySpendUpdatedAsync(
                    It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await operation.ExecuteAsync(items, virtualKeyId: 1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(BatchOperationStatusEnum.Completed, result.Status);
            Assert.Equal(1, result.SuccessCount);
            Assert.Equal(0, result.FailedCount);

            // Verify retry happened
            Assert.Equal(2, attemptCount);
        }

        [Fact]
        public async Task ExecuteAsync_WithLargeBatch_ShouldProcessEfficiently()
        {
            // Arrange
            var operation = _serviceProvider.GetRequiredService<BatchSpendUpdateOperationV2>();
            var items = Enumerable.Range(1, 100).Select(i => new SpendUpdateItem
            {
                VirtualKeyId = i,
                Amount = i * 0.5m,
                Model = "gpt-4",
                Provider = "OpenAI"
            }).ToList();

            _mockVirtualKeyService.Setup(s => s.GetVirtualKeyInfoForValidationAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ConduitLLM.Configuration.Entities.VirtualKey { Id = 1, KeyName = "Test" });

            _mockVirtualKeyService.Setup(s => s.UpdateSpendAsync(It.IsAny<int>(), It.IsAny<decimal>()))
                .Returns(Task.CompletedTask);

            _mockSpendNotificationService.Setup(s => s.NotifySpendUpdatedAsync(
                    It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            var startTime = DateTime.UtcNow;
            var result = await operation.ExecuteAsync(items, virtualKeyId: 1);
            var duration = DateTime.UtcNow - startTime;

            // Assert
            Assert.NotNull(result);
            Assert.Equal(BatchOperationStatusEnum.Completed, result.Status);
            Assert.Equal(100, result.TotalItems);
            Assert.Equal(100, result.SuccessCount);
            Assert.Equal(0, result.FailedCount);

            // Verify performance (should process 100 items quickly with parallelism)
            Assert.True(duration.TotalSeconds < 10, $"Batch took {duration.TotalSeconds}s, expected < 10s");
            Assert.True(result.ItemsPerSecond > 5, $"Processing rate was {result.ItemsPerSecond} items/sec, expected > 5");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                (_serviceProvider as IDisposable)?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
