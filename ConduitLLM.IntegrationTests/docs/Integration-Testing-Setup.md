# Integration Testing Setup for ConduitLLM

This document describes the integration testing infrastructure created for the ConduitLLM project.

## Overview

The integration test project provides a comprehensive framework for testing ConduitLLM's functionality end-to-end, including:

- Database integration with PostgreSQL
- Cache integration with Redis  
- Message queue integration with RabbitMQ (optional)
- HTTP API testing with authenticated requests
- Mock LLM server for simulating external API calls

## Project Structure

```
ConduitLLM.IntegrationTests/
├── Infrastructure/
│   ├── IntegrationTestBase.cs      # Base class for all integration tests
│   ├── MockLLMServer.cs           # WireMock-based LLM API simulator
│   └── SimpleTestDataBuilder.cs    # Test data generation utilities
├── Tests/
│   └── BasicIntegrationTests.cs    # Basic tests to verify infrastructure
├── docs/
│   └── Integration-Testing-Setup.md # This file
└── README.md                        # User guide for running tests
```

## Key Components

### IntegrationTestBase

The base class for all integration tests that provides:

- **Container Management**: Automatically starts PostgreSQL, Redis, and optionally RabbitMQ containers using Testcontainers
- **WebApplicationFactory**: Creates test instances of the HTTP and Admin APIs
- **Database Reset**: Uses Respawn to reset database state between tests
- **Logging**: Integrates xUnit output with ASP.NET Core logging

### MockLLMServer

A WireMock-based server that simulates LLM provider APIs:

- Supports OpenAI-compatible endpoints (chat, embeddings, images)
- Can simulate errors, rate limits, and timeouts
- Tracks request history for verification
- Configurable response delays

### SimpleTestDataBuilder

Utilities for creating test data:

- API key generation and hashing
- Test request object creation
- No direct entity dependencies (uses anonymous objects)

## Configuration

### Environment Setup

The test infrastructure automatically configures:

- `DATABASE_URL` for PostgreSQL connection
- `ConnectionStrings:DefaultConnection` for Entity Framework
- `ConnectionStrings:Redis` for caching
- RabbitMQ settings when `UseRabbitMq` is enabled

### Test Modes

Tests can run in different modes:

1. **In-Memory Mode** (`UseRealInfrastructure = false`):
   - Uses in-memory database
   - No containers required
   - Fastest execution

2. **Container Mode** (`UseRealInfrastructure = true`):
   - Starts real PostgreSQL, Redis containers
   - More realistic testing
   - Requires Docker

3. **With RabbitMQ** (`UseRabbitMq = true`):
   - Additionally starts RabbitMQ container
   - Tests event-driven features
   - Required for multi-instance scenarios

## Writing Tests

### Basic Test Example

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
        await ResetDatabaseAsync();
        
        // Act
        var response = await Client.GetAsync("/health");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
```

### Using Mock LLM Server

```csharp
[Fact]
public async Task TestWithMockLLM()
{
    using var mockServer = new MockLLMServer();
    mockServer.SetupChatCompletion("Test response");
    
    // Configure app to use mock server
    // Make requests
    // Verify calls
    
    mockServer.GetRequestCount("/v1/chat/completions").Should().Be(1);
}
```

## Current Limitations

1. **Entity Dependencies**: The test project currently uses simplified test data builders without direct entity references due to namespace complexities

2. **DTO Mapping**: Complex DTO tests were removed in favor of simpler integration tests focused on HTTP contracts

3. **Database Migrations**: Tests rely on the application's automatic migration feature

## Future Enhancements

1. **Entity Integration**: Add proper entity references once namespace issues are resolved

2. **DTO Testing**: Create separate test files for Admin API DTOs and Core API DTOs

3. **Performance Testing**: Add load testing scenarios using the infrastructure

4. **Event Testing**: Comprehensive tests for the event-driven architecture

5. **Security Testing**: Authentication and authorization edge cases

## Troubleshooting

### Container Issues
- Ensure Docker is running
- Check port availability (5432, 6379, 5672)
- Verify disk space for containers

### Test Failures
- Check DATABASE_URL is set correctly in logs
- Verify containers started successfully
- Look for port binding conflicts

### Performance
- Use in-memory mode for unit-test-like scenarios
- Run container tests only in CI/CD
- Use test collections to isolate heavy tests