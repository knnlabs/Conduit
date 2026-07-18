# Batch Operation Framework

## Overview

The Batch Operation Framework provides a unified, consistent approach to implementing batch operations in Conduit. It eliminates code duplication and provides built-in support for:

- **Idempotency tracking** - Prevents duplicate processing
- **Retry logic** - Automatic retries with exponential backoff
- **Error handling** - Standardized error handling and logging
- **Progress tracking** - Real-time progress updates via SignalR
- **Cancellation support** - Graceful cancellation of operations
- **Metrics collection** - Comprehensive performance metrics

## Architecture

### Core Components

```
BatchOperationBase<TItem>
├── Abstract Methods (implemented by derived classes):
│   ├── GetOperationType() - Operation type identifier
│   ├── ValidateBatchAsync() - Batch-level validation
│   ├── ValidateItemAsync() - Item-level validation
│   ├── ProcessItemAsync() - Core business logic
│   └── GetItemIdentifier() - Item identifier for logging
│
├── Virtual Methods (can be overridden):
│   ├── ConfigureBatchOptions() - Parallelism, checkpointing
│   ├── IsRetryableException() - Retry logic customization
│   └── GetIdempotencyTtl() - Cache duration
│
└── Built-in Features:
    ├── Idempotency checking (via IBatchOperationIdempotencyService)
    ├── Retry with exponential backoff
    ├── Standard error handling
    ├── Progress reporting (via IBatchOperationService)
    └── Metrics collection
```

### Idempotency Service

`IBatchOperationIdempotencyService` provides Redis-based idempotency tracking:

- **Token Generation**: SHA256-based deterministic hashing
- **Storage**: Redis with configurable TTL (default 24 hours)
- **Deduplication**: Automatic duplicate detection
- **Result Caching**: Returns cached results for duplicate requests

## Usage

### Creating a New Batch Operation

```csharp
public class MyBatchOperation : BatchOperationBase<MyItem>
{
    private readonly IMyService _myService;

    public MyBatchOperation(
        ILogger<MyBatchOperation> logger,
        IBatchOperationService batchOperationService,
        IMyService myService,
        IBatchOperationIdempotencyService? idempotencyService = null)
        : base(logger, batchOperationService, idempotencyService)
    {
        _myService = myService;
    }

    // Execute the batch operation
    public async Task<BatchOperationResult> ExecuteAsync(
        List<MyItem> items,
        int virtualKeyId,
        string? idempotencyToken = null,
        CancellationToken cancellationToken = default)
    {
        return await base.ExecuteAsync(items, virtualKeyId, idempotencyToken, cancellationToken);
    }

    // Required implementations
    protected override string GetOperationType() => "my_operation";

    protected override Task ValidateBatchAsync(List<MyItem> items, CancellationToken ct)
    {
        if (items == null || items.Count == 0)
        {
            throw new ArgumentException("Items cannot be empty");
        }
        return Task.CompletedTask;
    }

    protected override Task ValidateItemAsync(MyItem item, CancellationToken ct)
    {
        if (item.Id <= 0)
        {
            throw new InvalidOperationException($"Invalid ID: {item.Id}");
        }
        return Task.CompletedTask;
    }

    protected override async Task<BatchItemResult> ProcessItemAsync(MyItem item, CancellationToken ct)
    {
        await _myService.ProcessAsync(item);

        return new BatchItemResult
        {
            Success = true,
            ItemIdentifier = $"Item-{item.Id}",
            Data = new { Processed = true }
        };
    }

    protected override string GetItemIdentifier(MyItem item) => $"Item-{item.Id}";

    // Optional: Customize retry behavior
    protected override RetryOptions RetryOptions => new()
    {
        MaxRetries = 5,
        InitialDelay = TimeSpan.FromSeconds(2),
        MaxDelay = TimeSpan.FromSeconds(60),
        BackoffMultiplier = 2.0
    };

    // Optional: Customize batch options
    protected override BatchOperationOptions ConfigureBatchOptions(int virtualKeyId)
    {
        return new BatchOperationOptions
        {
            VirtualKeyId = virtualKeyId,
            MaxDegreeOfParallelism = 20,
            ContinueOnError = true,
            EnableCheckpointing = true,
            CheckpointInterval = 100
        };
    }

    // Optional: Customize retryable exceptions
    protected override bool IsRetryableException(Exception exception)
    {
        return exception is TimeoutException
            || exception is MyTransientException
            || base.IsRetryableException(exception);
    }
}
```

### Service Registration

```csharp
// In Program.CoreServices.cs
builder.Services.AddScoped<MyBatchOperation>();
```

### Using the Batch Operation

```csharp
public class MyController : ControllerBase
{
    private readonly MyBatchOperation _batchOperation;

    public MyController(MyBatchOperation batchOperation)
    {
        _batchOperation = batchOperation;
    }

    [HttpPost("batch")]
    public async Task<IActionResult> ExecuteBatch(
        [FromBody] List<MyItem> items,
        [FromHeader(Name = "X-Idempotency-Token")] string? idempotencyToken = null)
    {
        var virtualKeyId = GetVirtualKeyId();

        var result = await _batchOperation.ExecuteAsync(
            items,
            virtualKeyId,
            idempotencyToken,
            HttpContext.RequestAborted);

        return Ok(result);
    }
}
```

## Migration Guide

### Migrating Existing Batch Operations

1. **Identify duplicate code patterns** in your existing batch operation
2. **Create a new class** inheriting from `BatchOperationBase<TItem>`
3. **Implement abstract methods** with your business logic
4. **Override virtual methods** to customize behavior
5. **Register the new service** in DI container
6. **Test thoroughly** before deprecating old implementation
7. **Update controllers** to use new implementation
8. **Remove old code** after migration complete

### Example: BatchSpendUpdateOperation Migration

**Before:**
```csharp
public class BatchSpendUpdateOperation : IBatchSpendUpdateOperation
{
    // 118 lines of code
    // Duplicate error handling
    // No idempotency
    // Manual progress tracking
}
```

**After:**
```csharp
public class BatchSpendUpdateOperationV2 : BatchOperationBase<SpendUpdateItem>
{
    // 85 lines of code (28% reduction)
    // Automatic error handling via base class
    // Built-in idempotency
    // Automatic progress tracking
}
```

**Benefits:**
- 33 lines of code eliminated
- Idempotency support added
- Retry logic with exponential backoff
- Consistent error handling

## Implementation Status

### Phase 1: Foundation ✅ COMPLETE

- [x] `IBatchOperationIdempotencyService` interface
- [x] `BatchOperationIdempotencyService` Redis implementation
- [x] `BatchOperationBase<T>` abstract class
- [x] Comprehensive unit tests
- [x] Service registration
- [x] Documentation

### Phase 2: First Migration (In Progress)

- [x] `BatchSpendUpdateOperationV2` created
- [ ] Integration tests
- [ ] Performance benchmarks
- [ ] Controller migration
- [ ] Deprecate legacy implementation

### Phase 3: Additional Migrations (Pending)

- [ ] Refactor `BatchVirtualKeyUpdateOperation`
- [ ] Refactor `BatchWebhookSendOperation`
- [ ] Integration tests
- [ ] API documentation

### Phase 4: Enhancement (Pending)

- [ ] Enhanced metrics dashboard
- [ ] Performance optimizations
- [ ] Advanced retry policies
- [ ] Circuit breaker support

### Phase 5: Cleanup (Pending)

- [ ] Remove legacy implementations
- [ ] Final documentation
- [ ] Knowledge transfer
- [ ] Production monitoring

## Testing

### Unit Testing

```csharp
[Fact]
public async Task ExecuteAsync_WithIdempotencyToken_ShouldCheckForDuplicates()
{
    // Arrange
    var items = new List<MyItem> { new() { Id = 1 } };
    var token = "test-token-123";

    _mockIdempotencyService
        .Setup(s => s.IsOperationProcessedAsync(token, It.IsAny<CancellationToken>()))
        .ReturnsAsync(false);

    // Act
    await _operation.ExecuteAsync(items, virtualKeyId: 1, idempotencyToken: token);

    // Assert
    _mockIdempotencyService.Verify(
        s => s.IsOperationProcessedAsync(token, It.IsAny<CancellationToken>()),
        Times.Once);
}
```

### Integration Testing

```csharp
[Fact]
public async Task ExecuteBatch_WithDuplicateRequest_ShouldReturnCachedResult()
{
    // First request
    var result1 = await _operation.ExecuteAsync(items, 1, "token-123");

    // Duplicate request
    var result2 = await _operation.ExecuteAsync(items, 1, "token-123");

    // Should return cached result
    Assert.Equal(result1.OperationId, result2.OperationId);
}
```

## Performance Considerations

### Idempotency Token Storage

- **Redis Storage**: O(1) lookup performance
- **TTL**: 24 hours (configurable)
- **Memory Impact**: ~1KB per token
- **Cleanup**: Automatic via TTL

### Retry Logic

- **Exponential Backoff**: 1s → 2s → 4s → 8s (default)
- **Max Retries**: 3 (configurable)
- **Max Delay**: 30s (configurable)
- **Circuit Breaker**: Consider adding for external services

### Parallelism

- **Default**: `Environment.ProcessorCount`
- **Database Operations**: Limit to 5-10
- **HTTP Requests**: Can use 20-50
- **Checkpointing**: Every 50-100 items

## Best Practices

1. **Always provide idempotency tokens** for billing-related operations
2. **Validate early** - fail fast in `ValidateBatchAsync`
3. **Keep ProcessItemAsync focused** - one responsibility per operation
4. **Use appropriate parallelism** - don't overwhelm databases
5. **Enable checkpointing** for long-running operations
6. **Log generously** - the framework handles structured logging
7. **Test idempotency** - verify duplicate requests work correctly
8. **Monitor metrics** - track success rates and performance

## Troubleshooting

### Idempotency Not Working

- Check Redis connection
- Verify token generation consistency
- Check TTL configuration
- Review logs for errors

### Retry Not Working

- Verify exception type is retryable
- Check `IsRetryableException()` implementation
- Review retry configuration
- Check cancellation token handling

### Poor Performance

- Review parallelism settings
- Check database connection pooling
- Review checkpoint interval
- Monitor Redis latency

## Related Documentation

- [MassTransit Events](../events/masstransit-event-inventory.md) - Event-driven architecture

## Contributors

- Implemented by: Claude Code Assistant
- Reviewed by: TBD
- Issue: #215
