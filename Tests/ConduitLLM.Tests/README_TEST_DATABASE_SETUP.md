# Test Database Setup

## Overview

Integration tests that use `WebApplicationFactory` need special configuration to avoid database initialization issues during test runs.

## Solution

We've implemented a custom `TestWebApplicationFactory` that:

1. Sets `CONDUIT_SKIP_DATABASE_INIT=true` environment variable before the application starts
2. Configures default in-memory SQLite connection strings for tests
3. Sets the environment to "Test"

## Usage

### For Admin API Tests

Use the `AdminApiTestFactory` which includes the necessary configuration:

```csharp
public class MyAdminTests : IClassFixture<AdminApiTestFactory>
{
    private readonly AdminApiTestFactory _factory;
    
    public MyAdminTests(AdminApiTestFactory factory)
    {
        _factory = factory;
    }
}
```

### For Http API Tests

Use the `TestWebApplicationFactory<Program>`:

```csharp
public class MyHttpTests : IClassFixture<TestWebApplicationFactory<Program>>
{
    private readonly TestWebApplicationFactory<Program> _factory;
    
    public MyHttpTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }
}
```

## Known Issues

### Health Check Tests

Some health check tests may fail because:
- The database tables don't exist (we skip initialization)
- Health checks try to connect to real services

For tests that specifically test health endpoints, you may need to:
1. Mock the health checks in your test setup
2. Accept that health endpoints return "Unhealthy" in test scenarios
3. Test only the health check response format rather than the actual health status

## Environment Variables

The following environment variables are set automatically by `TestWebApplicationFactory`:
- `CONDUIT_SKIP_DATABASE_INIT=true` - Skips database migration/initialization
- `ASPNETCORE_ENVIRONMENT=Test` - Sets the environment to Test

## Connection Strings

Default test connection strings:
- `ConnectionStrings:DefaultConnection` = `Data Source=:memory:`
- `ConnectionStrings:ConfigurationDb` = `Data Source=:memory:`