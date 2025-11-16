using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services.BatchOperations;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Core.Services
{
    [Trait("Category", "Unit")]
    [Trait("Component", "Core")]
    [Trait("Phase", "2")]
    public class BatchOperationBaseTests : TestBase
    {
        private readonly Mock<ILogger<TestBatchOperation>> _mockLogger;
        private readonly Mock<IBatchOperationService> _mockBatchService;
        private readonly Mock<IBatchOperationIdempotencyService> _mockIdempotencyService;
        private readonly TestBatchOperation _operation;

        public BatchOperationBaseTests(ITestOutputHelper output) : base(output)
        {
            _mockLogger = new Mock<ILogger<TestBatchOperation>>();
            _mockBatchService = new Mock<IBatchOperationService>();
            _mockIdempotencyService = new Mock<IBatchOperationIdempotencyService>();

            _operation = new TestBatchOperation(
                _mockLogger.Object,
                _mockBatchService.Object,
                _mockIdempotencyService.Object);
        }

        [Fact]
        public async Task ExecuteAsync_WithValidItems_ShouldProcessSuccessfully()
        {
            // Arrange
            var items = new List<TestItem>
            {
                new() { Id = 1, Value = "Item1" },
                new() { Id = 2, Value = "Item2" }
            };

            var expectedResult = new BatchOperationResult
            {
                OperationId = "op-123",
                OperationType = "test_operation",
                Status = BatchOperationStatusEnum.Completed,
                TotalItems = 2,
                SuccessCount = 2,
                FailedCount = 0
            };

            _mockBatchService.Setup(s => s.StartBatchOperationAsync(
                    It.IsAny<string>(),
                    It.IsAny<IEnumerable<TestItem>>(),
                    It.IsAny<Func<TestItem, CancellationToken, Task<BatchItemResult>>>(),
                    It.IsAny<BatchOperationOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _operation.ExecuteAsync(items, virtualKeyId: 1);

            // Assert
            Assert.Equal(BatchOperationStatusEnum.Completed, result.Status);
            Assert.Equal(2, result.TotalItems);
            Assert.Equal(2, result.SuccessCount);
        }

        [Fact]
        public async Task ExecuteAsync_WithIdempotencyToken_ShouldCheckForDuplicates()
        {
            // Arrange
            var items = new List<TestItem> { new() { Id = 1, Value = "Item1" } };
            var token = "test-token-123";

            _mockIdempotencyService.Setup(s => s.IsOperationProcessedAsync(token, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var expectedResult = new BatchOperationResult
            {
                OperationId = "op-123",
                Status = BatchOperationStatusEnum.Completed,
                TotalItems = 1,
                SuccessCount = 1
            };

            _mockBatchService.Setup(s => s.StartBatchOperationAsync(
                    It.IsAny<string>(),
                    It.IsAny<IEnumerable<TestItem>>(),
                    It.IsAny<Func<TestItem, CancellationToken, Task<BatchItemResult>>>(),
                    It.IsAny<BatchOperationOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            await _operation.ExecuteAsync(items, virtualKeyId: 1, idempotencyToken: token);

            // Assert
            _mockIdempotencyService.Verify(
                s => s.IsOperationProcessedAsync(token, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_WhenDuplicateDetected_ShouldReturnCachedResult()
        {
            // Arrange
            var items = new List<TestItem> { new() { Id = 1, Value = "Item1" } };
            var token = "test-token-123";

            var cachedResult = new BatchOperationResult
            {
                OperationId = "op-cached",
                Status = BatchOperationStatusEnum.Completed,
                TotalItems = 1,
                SuccessCount = 1
            };

            _mockIdempotencyService.Setup(s => s.IsOperationProcessedAsync(token, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            _mockIdempotencyService.Setup(s => s.GetOperationResultAsync<BatchOperationResult>(
                    token,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(cachedResult);

            // Act
            var result = await _operation.ExecuteAsync(items, virtualKeyId: 1, idempotencyToken: token);

            // Assert
            Assert.Equal("op-cached", result.OperationId);
            _mockBatchService.Verify(
                s => s.StartBatchOperationAsync(
                    It.IsAny<string>(),
                    It.IsAny<IEnumerable<TestItem>>(),
                    It.IsAny<Func<TestItem, CancellationToken, Task<BatchItemResult>>>(),
                    It.IsAny<BatchOperationOptions>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_OnSuccess_ShouldStoreResultForIdempotency()
        {
            // Arrange
            var items = new List<TestItem> { new() { Id = 1, Value = "Item1" } };
            var token = "test-token-123";

            _mockIdempotencyService.Setup(s => s.IsOperationProcessedAsync(token, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var expectedResult = new BatchOperationResult
            {
                OperationId = "op-123",
                Status = BatchOperationStatusEnum.Completed,
                TotalItems = 1,
                SuccessCount = 1
            };

            _mockBatchService.Setup(s => s.StartBatchOperationAsync(
                    It.IsAny<string>(),
                    It.IsAny<IEnumerable<TestItem>>(),
                    It.IsAny<Func<TestItem, CancellationToken, Task<BatchItemResult>>>(),
                    It.IsAny<BatchOperationOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            await _operation.ExecuteAsync(items, virtualKeyId: 1, idempotencyToken: token);

            // Assert
            _mockIdempotencyService.Verify(
                s => s.StoreOperationResultAsync(
                    token,
                    It.IsAny<BatchOperationResult>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_WithRetryableError_ShouldRetry()
        {
            // Arrange
            var items = new List<TestItem> { new() { Id = 1, Value = "Item1" } };
            var attemptCount = 0;

            _operation.ProcessItemHandler = async (item, ct) =>
            {
                attemptCount++;
                if (attemptCount < 2)
                {
                    throw new TimeoutException("Retryable error");
                }

                return new BatchItemResult
                {
                    Success = true,
                    ItemIdentifier = $"Item-{item.Id}"
                };
            };

            var result = new BatchOperationResult
            {
                Status = BatchOperationStatusEnum.Completed,
                TotalItems = 1,
                SuccessCount = 1
            };

            _mockBatchService.Setup(s => s.StartBatchOperationAsync(
                    It.IsAny<string>(),
                    It.IsAny<IEnumerable<TestItem>>(),
                    It.IsAny<Func<TestItem, CancellationToken, Task<BatchItemResult>>>(),
                    It.IsAny<BatchOperationOptions>(),
                    It.IsAny<CancellationToken>()))
                .Returns<string, IEnumerable<TestItem>, Func<TestItem, CancellationToken, Task<BatchItemResult>>, BatchOperationOptions, CancellationToken>(
                    async (type, items, processFunc, opts, ct) =>
                    {
                        foreach (var item in items)
                        {
                            await processFunc(item, ct);
                        }
                        return result;
                    });

            // Act
            await _operation.ExecuteAsync(items, virtualKeyId: 1);

            // Assert
            Assert.Equal(2, attemptCount);
        }

        [Fact]
        public async Task ExecuteAsync_WithValidationFailure_ShouldThrow()
        {
            // Arrange
            var items = new List<TestItem> { new() { Id = 0, Value = "" } }; // Invalid
            _operation.ShouldFailValidation = true;

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _operation.ExecuteAsync(items, virtualKeyId: 1));
        }

        [Fact]
        public async Task ExecuteAsync_WithoutIdempotencyService_ShouldStillWork()
        {
            // Arrange
            var operationWithoutIdempotency = new TestBatchOperation(
                _mockLogger.Object,
                _mockBatchService.Object,
                idempotencyService: null);

            var items = new List<TestItem> { new() { Id = 1, Value = "Item1" } };

            var expectedResult = new BatchOperationResult
            {
                Status = BatchOperationStatusEnum.Completed,
                TotalItems = 1,
                SuccessCount = 1
            };

            _mockBatchService.Setup(s => s.StartBatchOperationAsync(
                    It.IsAny<string>(),
                    It.IsAny<IEnumerable<TestItem>>(),
                    It.IsAny<Func<TestItem, CancellationToken, Task<BatchItemResult>>>(),
                    It.IsAny<BatchOperationOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await operationWithoutIdempotency.ExecuteAsync(items, virtualKeyId: 1);

            // Assert
            Assert.Equal(BatchOperationStatusEnum.Completed, result.Status);
        }

        // Test implementation of BatchOperationBase
        private class TestBatchOperation : BatchOperationBase<TestItem>
        {
            public bool ShouldFailValidation { get; set; }
            public Func<TestItem, CancellationToken, Task<BatchItemResult>>? ProcessItemHandler { get; set; }

            public TestBatchOperation(
                ILogger<TestBatchOperation> logger,
                IBatchOperationService batchOperationService,
                IBatchOperationIdempotencyService? idempotencyService = null)
                : base(logger, batchOperationService, idempotencyService)
            {
            }

            protected override string GetOperationType() => "test_operation";

            protected override Task ValidateBatchAsync(List<TestItem> items, CancellationToken cancellationToken)
            {
                if (ShouldFailValidation)
                {
                    throw new InvalidOperationException("Batch validation failed");
                }
                return Task.CompletedTask;
            }

            protected override Task ValidateItemAsync(TestItem item, CancellationToken cancellationToken)
            {
                if (item.Id <= 0)
                {
                    throw new InvalidOperationException($"Invalid item ID: {item.Id}");
                }
                return Task.CompletedTask;
            }

            protected override async Task<BatchItemResult> ProcessItemAsync(TestItem item, CancellationToken cancellationToken)
            {
                if (ProcessItemHandler != null)
                {
                    return await ProcessItemHandler(item, cancellationToken);
                }

                return new BatchItemResult
                {
                    Success = true,
                    ItemIdentifier = $"Item-{item.Id}",
                    Data = new { Processed = true, Value = item.Value }
                };
            }

            protected override string GetItemIdentifier(TestItem item) => $"Item-{item.Id}";
        }

        private class TestItem
        {
            public int Id { get; set; }
            public string Value { get; set; } = string.Empty;
        }
    }
}
