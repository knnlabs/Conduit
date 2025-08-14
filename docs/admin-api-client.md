# Admin API Client

This document explains how to use the Admin API client in the ConduitLLM.WebUI project.

## Overview

The Admin API client provides a way for the WebUI project to communicate with the Admin API without direct project references. This breaks the circular dependency between the projects and improves the architecture.

## Configuration

### Environment Variables

The Admin API client can be configured using environment variables:

- `CONDUIT_ADMIN_API_URL` - The base URL of the Admin API. Default is "http://localhost:5000".
- `CONDUIT_API_TO_API_BACKEND_AUTH_KEY` - The master key for authenticating with the Admin API.
- `CONDUIT_USE_ADMIN_API` - Whether to use the Admin API client (true) or direct repository access (false). Default is true.

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

## Service Registration

The Admin API client is registered in Program.cs using extension methods:

```csharp
// Register the Admin API client
builder.Services.AddAdminApiClient(builder.Configuration);

// Register Admin API service adapters
builder.Services.AddAdminApiAdapters(builder.Configuration);
```

## Using the Admin API Client

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

### Adapter Pattern

The WebUI project uses the adapter pattern to maintain compatibility with existing service interfaces:

```csharp
// Depending on CONDUIT_USE_ADMIN_API setting, either use:
// 1. Direct repository access (default repositories)
// 2. API access (adapters that use the Admin API client)
if (useAdminApi)
{
    // Register adapters that use the Admin API client
    services.AddScoped<IGlobalSettingService, GlobalSettingServiceAdapter>();
    services.AddScoped<IProviderHealthService, ProviderHealthServiceAdapter>();
    // ...
}
else
{
    // Register services that use direct repository access
    // ...
}
```

This allows you to switch between direct repository access and API access without changing your component code.

## Available Features

The Admin API client provides access to the following features:

- Virtual Keys - Management of API keys
- Global Settings - Application configuration
- Provider Health - LLM provider health monitoring
- Model Costs - Cost tracking
- Provider Credentials - API provider credentials
- IP Filters - Network access control
- Logs - Request logs and usage statistics

## Development vs. Production

### Local Development

For local development, you can run both services on the same machine:

```
# Run Admin API
dotnet run --project ConduitLLM.Admin

# Run WebUI with direct repository access
CONDUIT_USE_ADMIN_API=false dotnet run --project ConduitLLM.WebUI

# OR run WebUI with API access
CONDUIT_USE_ADMIN_API=true CONDUIT_ADMIN_API_URL=http://localhost:5000 dotnet run --project ConduitLLM.WebUI
```

### Docker Environment

In a Docker environment, services are typically deployed in separate containers:

```yaml
services:
  webui:
    environment:
      CONDUIT_ADMIN_API_URL: http://admin:8080
      CONDUIT_USE_ADMIN_API: "true"
    # ...

  admin:
    environment:
      AdminApi__MasterKey: alpha
      AdminApi__AllowedOrigins__0: http://webui:8080
      # ...
```

## Error Handling

The Admin API client includes built-in error handling:

- Errors are logged with contextual information
- Failed requests return graceful fallbacks (empty collections, nulls, etc.)
- HTTP status codes are handled appropriately (e.g., 404 returns null)

## Security

- Master key authentication is used to secure API endpoints
- HTTPS should be used in production environments
- The master key is passed in the X-Master-Key header