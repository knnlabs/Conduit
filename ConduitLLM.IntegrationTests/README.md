# ConduitLLM Integration Tests

This project contains comprehensive integration tests for the ConduitLLM system, testing end-to-end functionality across Core API, Admin API, and event-driven architecture.

## Test Categories

### 1. End-to-End Tests (`EndToEndTests.cs`)
- Full request flow from API key authentication to LLM response
- Streaming response handling
- Budget enforcement and rate limiting
- Model mapping priority resolution
- IP whitelisting/blacklisting

### 2. Admin API Tests (`AdminApiTests.cs`)
- Virtual key CRUD operations
- Provider credential management
- Model mapping configuration
- Analytics and request log filtering
- Health check endpoints

### 3. Event-Driven Tests (`EventDrivenTests.cs`)
- Virtual key creation/update cache invalidation
- Spend update consistency across concurrent requests
- Provider credential update and capability refresh
- Event ordering guarantees

## Infrastructure

### Test Base (`IntegrationTestBase.cs`)
- Containerized PostgreSQL, Redis, and RabbitMQ setup
- Database reset between tests using Respawn
- WebApplicationFactory configuration
- XUnit logging integration

### Mock LLM Server (`MockLLMServer.cs`)
- WireMock-based LLM API simulation
- Supports chat completions, streaming, embeddings, and image generation
- Request verification and history tracking
- Error simulation (rate limits, timeouts)

### Test Data Builder (`TestDataBuilder.cs`)
- Consistent test data generation
- API key creation and hashing
- Request log batch generation for analytics testing

## Running Tests

### Prerequisites
- Docker installed and running (optional - tests will skip if not available)
- .NET 9.0 SDK
- Sufficient resources for containers (PostgreSQL, Redis, RabbitMQ) if running with infrastructure

### Controlling Test Execution

Integration tests are **always skipped by default** to ensure `dotnet test` works without any infrastructure.

To **run integration tests**, you must explicitly enable them:
```bash
RUN_INTEGRATION_TESTS=true dotnet test
```

Without this environment variable, integration tests will be skipped with the message:
"Integration tests require infrastructure. Set RUN_INTEGRATION_TESTS=true to run."

### Run All Tests
```bash
cd ConduitLLM.IntegrationTests
dotnet test
```

### Run Specific Category
```bash
# End-to-end tests only
dotnet test --filter "FullyQualifiedName~EndToEndTests"

# Admin API tests only
dotnet test --filter "FullyQualifiedName~AdminApiTests"

# Event-driven tests only
dotnet test --filter "FullyQualifiedName~EventDrivenTests"
```

### Run with Logging
```bash
dotnet test --logger "console;verbosity=detailed"
```

## Test Configuration

### Environment Variables
Tests can be configured with environment variables:
- `USE_REAL_INFRASTRUCTURE`: Set to `false` to use in-memory alternatives
- `POSTGRES_CONNECTION`: Override default PostgreSQL connection
- `REDIS_CONNECTION`: Override default Redis connection
- `RABBITMQ_CONNECTION`: Override default RabbitMQ connection

### Timeouts
Default test timeout is 60 seconds. Long-running tests should use:
```csharp
[Fact(Timeout = 120000)] // 2 minutes
```

## Writing New Tests

### Basic Test Structure
```csharp
public class MyIntegrationTests : IntegrationTestBase
{
    public MyIntegrationTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public async Task MyTest()
    {
        // Arrange
        await ResetDatabaseAsync(); // Clean database
        
        // Setup test data
        var testData = _dataBuilder.CreateVirtualKey();
        
        // Act
        var response = await Client.GetAsync("/api/endpoint");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
```

### Using Mock LLM Server
```csharp
var mockServer = new MockLLMServer();
mockServer.SetupChatCompletion("Test response");

// Configure client to use mock
services.PostConfigure<ConduitOptions>(options =>
{
    options.Providers["openai"].BaseUrl = mockServer.BaseUrl;
});

// Make request and verify
var response = await client.PostAsJsonAsync("/v1/chat/completions", request);
mockServer.GetRequestCount("/v1/chat/completions").Should().Be(1);
```

## Troubleshooting

### Container Issues
- Ensure Docker is running
- Check port availability (5432, 6379, 5672)
- Verify sufficient disk space

### Test Failures
- Check test output for container startup logs
- Verify database migrations completed
- Look for port conflicts with local services

### Performance
- Run tests in parallel: `dotnet test --parallel`
- Use test collections for isolation
- Consider running heavy tests separately

## CI/CD Integration

### GitHub Actions Example
```yaml
- name: Run Integration Tests
  run: |
    cd ConduitLLM.IntegrationTests
    dotnet test --logger "trx;LogFileName=integration-tests.trx"
    
- name: Upload Test Results
  uses: actions/upload-artifact@v3
  if: always()
  with:
    name: integration-test-results
    path: '**/*.trx'
```

## Best Practices

1. **Always reset database** between tests using `ResetDatabaseAsync()`
2. **Use test data builder** for consistent test data
3. **Dispose resources** properly (mock servers, HTTP clients)
4. **Log important steps** using `Output.WriteLine()`
5. **Verify both success and failure** scenarios
6. **Test concurrent operations** for race conditions
7. **Mock external dependencies** to avoid flakiness