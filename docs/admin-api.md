# Admin API

The ConduitLLM Admin API provides administrative endpoints for managing and configuring the Conduit platform. It is separate from the main LLM API and is used by the WebUI project and other admin tools.

## Architecture

The Admin API is designed to be a separate service from the main LLM API, with these key components:

- **ConduitLLM.Admin**: The Admin API project containing controllers and services
- **ConduitLLM.Configuration**: Shared DTOs and entities used by both Admin API and WebUI
- **WebUI ↔ Admin Integration**: WebUI uses the Admin API client to communicate with the Admin API

This architecture provides several benefits:

1. **Decoupled Components**: Services can be developed, deployed, and scaled independently
2. **Clean API Contracts**: Clearly defined interfaces between components
3. **Enhanced Security**: Administrative functions are isolated from user-facing LLM API
4. **Flexible Deployment**: Different deployment scenarios for different environments

## API Endpoints

The Admin API provides endpoints for managing:

- **Virtual Keys**: Create, read, update, delete, and monitor usage
- **Global Settings**: Manage application-wide settings
- **Provider Health**: Monitor and configure health checks for LLM providers
- **Model Costs**: Track and manage costs for different models
- **Provider Credentials**: Securely manage API keys and other credentials
- **IP Filters**: Control network access to the platform
- **Request Logs**: Access and analyze usage logs

## Admin API Client

The Admin API client is a component in the WebUI project that provides a type-safe way to communicate with the Admin API. It includes:

1. **IAdminApiClient Interface**: Defines all available Admin API operations
2. **AdminApiClient Implementation**: HTTP client implementation of the interface
3. **Service Adapters**: Adapters that implement WebUI service interfaces but delegate to the Admin API

### Configuration

The Admin API client can be configured using environment variables:

- `CONDUIT_ADMIN_API_URL`: Base URL for the Admin API (default: `http://localhost:5000`)
- `CONDUIT_MASTER_KEY`: Master key for authenticating with the Admin API
- `CONDUIT_USE_ADMIN_API`: Toggle between API access (`true`) and direct DB access (`false`)

Or using appsettings.json:

```json
{
  "AdminApi": {
    "BaseUrl": "http://localhost:5000",
    "TimeoutSeconds": 30,
    "UseAdminApi": true
  }
}
```

### Registration in WebUI

The Admin API client and adapters are registered in the WebUI's Program.cs:

```csharp
// Register the Admin API client
builder.Services.AddAdminApiClient(builder.Configuration);

// Register Admin API service adapters
builder.Services.AddAdminApiAdapters(builder.Configuration);
```

### Adapter Pattern

The WebUI project uses the adapter pattern to maintain compatibility with existing service interfaces:

```csharp
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

## Docker Compose Setup

Here's an example Docker Compose configuration for running the Admin API alongside WebUI and main API:

```yaml
services:
  webui:
    image: ghcr.io/knnlabs/conduit-webui:latest
    environment:
      CONDUIT_ADMIN_API_URL: http://admin:8080
      CONDUIT_MASTER_KEY: your_master_key
      CONDUIT_USE_ADMIN_API: "true"
    ports:
      - "5001:8080"
    depends_on:
      - admin
      - postgres

  admin:
    image: ghcr.io/knnlabs/conduit-admin:latest
    environment:
      DATABASE_URL: postgresql://user:password@postgres:5432/conduitdb
      AdminApi__MasterKey: your_master_key
    ports:
      - "5002:8080"
    depends_on:
      - postgres

  http:
    image: ghcr.io/knnlabs/conduit-http:latest
    environment:
      DATABASE_URL: postgresql://user:password@postgres:5432/conduitdb
    ports:
      - "5000:8080"
    depends_on:
      - postgres

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

## Security Considerations

- **Master Key Authentication**: The Admin API uses the same master key as the WebUI for authentication
- **Network Isolation**: In production, consider using network isolation to restrict access to Admin API
- **API Key Management**: The Admin API handles sensitive provider credentials; ensure proper security measures

## Future Enhancements

- **Token-based Authentication**: Replace master key with token-based authentication
- **API Versioning**: Implement API versioning for backward compatibility
- **Request Batching**: Support batching multiple operations for efficiency
- **Caching Layer**: Add caching for frequently accessed data