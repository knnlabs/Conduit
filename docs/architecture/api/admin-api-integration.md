# Admin API Integration

This document describes the integration between the ConduitLLM.Admin API and the WebAdmin project, including the Admin API client implementation.

## Table of Contents

1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Configuration](#configuration)
4. [Implementation](#implementation)
5. [Usage Patterns](#usage-patterns)
6. [Deployment](#deployment)
7. [Security](#security)

---

## Overview

The WebAdmin project communicates with the ConduitLLM.Admin API through a well-defined HTTP client interface. This approach provides several benefits:

1. **Decoupled Architecture**: WebAdmin and Admin projects are fully decoupled, eliminating circular dependencies
2. **Scalability**: Services can be deployed separately in distributed environments
3. **Clear API Contracts**: API contracts are explicitly defined through interfaces and DTOs
4. **Simplified Testing**: Mock implementations of the API client can be used for testing
5. **Flexible Deployment**: Choose between direct database access (development) or API access (production)

---

## Architecture

### Implementation Structure

#### WebAdmin Components

**Interfaces:**
- `IAdminApiClient`: Comprehensive interface for all Admin API operations, organized by feature areas

**API Client:**
- `AdminApiClient`: HTTP client implementation handling JSON serialization, error handling, and HTTP communication

**Service Adapters:**
These adapters implement existing WebAdmin service interfaces but delegate to the Admin API:
- `VirtualKeyServiceAdapter`: Implements `IVirtualKeyService` using Admin API
- `GlobalSettingServiceAdapter`: Implements `IGlobalSettingService` using Admin API
- `RequestLogServiceAdapter`: Implements `IRequestLogService` using Admin API
- `ProviderHealthServiceAdapter`: Implements `IProviderHealthService` using Admin API
- Additional adapters for all service interfaces

**Configuration:**
- `AdminApiOptions`: Configuration options for the Admin API client
- `AdminClientExtensions`: Extension methods for registering Admin API services

### Feature Organization

The API client interface is organized by feature areas:

- **Virtual Keys**: Management of API keys and usage tracking
- **Global Settings**: Application-wide configuration settings
- **Provider Health**: LLM provider health monitoring and configuration
- **Model Costs**: Cost tracking and billing information
- **Provider Credentials**: Secure management of API provider credentials
- **IP Filters**: Network access control configuration
- **Logs**: Request logs and usage statistics

### How It Works

1. **Service Registration**: WebAdmin registers the Admin API client and service adapters in Program.cs
2. **Configuration**: Uses environment variables for flexible configuration
3. **Adapter Pattern**: Choose between direct database access or API access based on configuration
4. **Error Handling**: Comprehensive error handling with logging ensures resilient behavior

**Request Flow:**

```
WebAdmin Component → Service Interface → Adapter/Implementation → Admin API Client → HTTP → Admin API → Repository → Database
```

---

## Configuration

### Environment Variables

The Admin API client can be configured using environment variables:

**Required:**
- `CONDUIT_ADMIN_API_URL` - The base URL of the Admin API. Default: "http://localhost:5000"
- `CONDUIT_API_TO_API_BACKEND_AUTH_KEY` - The master key for authenticating with the Admin API

**Optional:**
- `CONDUIT_USE_ADMIN_API` - Whether to use the Admin API client (true) or direct repository access (false). Default: true

### appsettings.json

You can also configure the Admin API client in appsettings.json:

```json
{
  "AdminApi": {
    "BaseUrl": "http://localhost:5000",
    "TimeoutSeconds": 30,
    "UseAdminApi": true
  }
}
```

---

## Implementation

### Service Registration

The Admin API client is registered in Program.cs using extension methods:

```csharp
// Register the Admin API client
builder.Services.AddAdminApiClient(builder.Configuration);

// Register Admin API service adapters
builder.Services.AddAdminApiAdapters(builder.Configuration);
```

### Adapter Pattern

The WebAdmin project uses the adapter pattern to maintain compatibility with existing service interfaces:

```csharp
// Depending on CONDUIT_USE_ADMIN_API setting, either use:
// 1. Direct repository access (default repositories)
// 2. API access (adapters that use the Admin API client)
if (useAdminApi)
{
    // Register adapters that use the Admin API client
    services.AddScoped<IGlobalSettingService, GlobalSettingServiceAdapter>();
    services.AddScoped<IProviderHealthService, ProviderHealthServiceAdapter>();
    services.AddScoped<IVirtualKeyService, VirtualKeyServiceAdapter>();
    // ...
}
else
{
    // Register services that use direct repository access
    services.AddScoped<IGlobalSettingService, GlobalSettingService>();
    services.AddScoped<IProviderHealthService, ProviderHealthService>();
    // ...
}
```

This allows you to switch between direct repository access and API access without changing your component code.

---

## Usage Patterns

### Direct Usage

You can inject `IAdminApiClient` into your components and services:

```csharp
public class MyService
{
    private readonly IAdminApiClient _adminApiClient;

    public MyService(IAdminApiClient adminApiClient)
    {
        _adminApiClient = adminApiClient;
    }

    public async Task<IEnumerable<VirtualKeyDto>> GetVirtualKeysAsync()
    {
        return await _adminApiClient.GetAllVirtualKeysAsync();
    }
}
```

### Service Interface Usage (Recommended)

Use existing service interfaces for better testability and flexibility:

```csharp
public class MyComponent
{
    private readonly IVirtualKeyService _virtualKeyService;

    public MyComponent(IVirtualKeyService virtualKeyService)
    {
        _virtualKeyService = virtualKeyService;
    }

    public async Task<List<VirtualKeyDto>> GetKeysAsync()
    {
        // Implementation automatically uses adapter or direct access
        // based on CONDUIT_USE_ADMIN_API configuration
        return await _virtualKeyService.GetAllAsync();
    }
}
```

### Available Features

The Admin API client provides access to the following features:

- **Virtual Keys** - Management of API keys
- **Global Settings** - Application configuration
- **Provider Health** - LLM provider health monitoring
- **Model Costs** - Cost tracking
- **Provider Credentials** - API provider credentials
- **IP Filters** - Network access control
- **Logs** - Request logs and usage statistics

### Error Handling

The Admin API client includes built-in error handling:

- Errors are logged with contextual information
- Failed requests return graceful fallbacks (empty collections, nulls, etc.)
- HTTP status codes are handled appropriately (e.g., 404 returns null)
- Automatic retry logic for transient failures (configurable)

**Example:**

```csharp
try
{
    var virtualKeys = await _adminApiClient.GetAllVirtualKeysAsync();
    return virtualKeys;
}
catch (HttpRequestException ex)
{
    _logger.LogError(ex, "Failed to retrieve virtual keys from Admin API");
    // Fallback behavior or rethrow
    return new List<VirtualKeyDto>();
}
```

---

## Deployment

### Local Development

For local development, you can run both services on the same machine:

```bash
# Option 1: Run Admin API and use direct repository access in WebAdmin
dotnet run --project ConduitLLM.Admin  # Runs on http://localhost:5000
CONDUIT_USE_ADMIN_API=false dotnet run --project WebAdmin

# Option 2: Run both services and use API access
dotnet run --project ConduitLLM.Admin  # Runs on http://localhost:5000
CONDUIT_USE_ADMIN_API=true CONDUIT_ADMIN_API_URL=http://localhost:5000 dotnet run --project WebAdmin
```

**Typical local setup:**
- Admin API: http://localhost:5000
- WebAdmin: http://localhost:5001

### Docker Environment

In a Docker environment, services are typically deployed in separate containers:

```yaml
services:
  webadmin:
    image: conduit-webadmin:latest
    environment:
      CONDUIT_ADMIN_API_URL: http://admin:8080
      CONDUIT_API_TO_API_BACKEND_AUTH_KEY: ${MASTER_KEY}
      CONDUIT_USE_ADMIN_API: "true"
    depends_on:
      - admin
    ports:
      - "5001:8080"

  admin:
    image: conduit-admin:latest
    environment:
      DATABASE_URL: postgresql://user:password@postgres:5432/conduitdb
      AdminApi__MasterKey: ${MASTER_KEY}
      AdminApi__AllowedOrigins__0: http://webadmin:8080
    depends_on:
      - postgres
    ports:
      - "5000:8080"

  postgres:
    image: postgres:16
    environment:
      POSTGRES_USER: user
      POSTGRES_PASSWORD: password
      POSTGRES_DB: conduitdb
    volumes:
      - pgdata:/var/lib/postgresql/data

volumes:
  pgdata:
```

### Container Deployment Considerations

For container environments:
- Set `CONDUIT_ADMIN_API_URL=http://admin-api:8080` in WebAdmin container
- Ensure networking allows communication between containers
- Use Docker Compose networks or Kubernetes service discovery
- Configure health checks for each service

---

## Security

### Authentication

- **Master Key Authentication**: All Admin API requests require the master key
- **Header-Based**: The master key is passed in the `X-API-Key` header (or `X-Master-Key` for backward compatibility)
- **Environment-Based**: Master keys should be stored in environment variables or secrets management systems

### Best Practices

1. **Use Strong Master Keys**: Generate cryptographically random keys (minimum 32 characters)
   ```bash
   openssl rand -base64 32
   ```

2. **Secure Key Storage**:
   - Never commit keys to source control
   - Use environment variables in development
   - Use secrets management (AWS Secrets Manager, Azure Key Vault) in production

3. **HTTPS Only**: Always use HTTPS in production environments

4. **Network Isolation**: Consider network-level restrictions (firewall rules, VPNs) for additional security

5. **Audit Logging**: Monitor all administrative operations

### Security Headers

The Admin API client automatically includes:
- `X-API-Key`: Master key for authentication
- `Content-Type: application/json`
- Custom headers as configured

---

## Future Enhancements

- [ ] Implement token-based authentication with expiration
- [ ] Add pagination support for large data sets
- [ ] Implement API versioning
- [ ] Add Swagger/OpenAPI documentation generation
- [ ] Consider GraphQL for efficient data fetching
- [ ] Implement request batching for efficiency
- [ ] Add circuit breaker pattern for fault tolerance
- [ ] Implement caching layer for frequently accessed data

---

## Related Documentation

- [Repository Pattern](../patterns/repository-and-data-access.md) - Data access patterns
- [DTO Guidelines](../data-transfer/dto-guidelines.md) - Data transfer object patterns
- [Provider Architecture](../provider-system/provider-architecture.md) - Provider system design
