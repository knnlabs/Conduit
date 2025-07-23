# Admin API Integration

This document describes the integration between the ConduitLLM.Admin API and the ConduitLLM.WebUI project.

## Overview

The ConduitLLM.WebUI project now communicates with the ConduitLLM.Admin API through a well-defined HTTP client interface. This approach offers several benefits:

1. **Decoupled Architecture**: WebUI and Admin projects are fully decoupled, eliminating circular dependencies
2. **Scalability**: Services can be deployed separately in distributed environments
3. **Clear API Contracts**: API contracts are explicitly defined through interfaces and DTOs
4. **Simplified Testing**: Mock implementations of the API client can be used for testing

## Implementation Structure

### ConduitLLM.WebUI Components

#### Interfaces

- `IAdminApiClient`: Comprehensive interface for all Admin API operations, organized by feature areas (virtual keys, logs, settings, etc.)

#### API Client

- `AdminApiClient`: HTTP client implementation of `IAdminApiClient`, handling JSON serialization, error handling, and HTTP communication.

#### Service Adapters

These adapters implement the existing WebUI service interfaces but delegate to the Admin API:

- `VirtualKeyServiceAdapter`: Implements `IVirtualKeyService` using Admin API
- `GlobalSettingServiceAdapter`: Implements `IGlobalSettingService` using Admin API
- `RequestLogServiceAdapter`: Implements `IRequestLogService` using Admin API
- Additional adapters for all service interfaces

#### Configuration

- `AdminApiOptions`: Configuration options for the Admin API client
- `AdminClientExtensions`: Extension methods for registering Admin API services

### How It Works

1. **Service Registration**: WebUI registers the Admin API client and service adapters in Program.cs
2. **Configuration**: Uses environment variables (CONDUIT_ADMIN_API_URL, CONDUIT_API_TO_API_BACKEND_AUTH_KEY, CONDUIT_USE_ADMIN_API) for flexible configuration
3. **Adapter Pattern**: The adapter pattern allows WebUI to choose between direct database access or API access based on configuration
4. **Error Handling**: Comprehensive error handling with logging ensures resilient behavior

## Feature-Based Organization

The API client interface is organized by feature areas:

- **Virtual Keys**: Management of API keys and usage tracking
- **Global Settings**: Application-wide configuration settings
- **Provider Health**: LLM provider health monitoring and configuration
- **Model Costs**: Cost tracking and billing information
- **Provider Credentials**: Secure management of API provider credentials
- **IP Filters**: Network access control configuration
- **Logs**: Request logs and usage statistics

## Configuration Options

### Environment Variables

- `CONDUIT_ADMIN_API_URL`: Base URL for the Admin API (default: http://localhost:5000)
- `CONDUIT_API_TO_API_BACKEND_AUTH_KEY`: Authentication key for secure API access
- `CONDUIT_USE_ADMIN_API`: Flag to toggle between API access and direct DB access (true/false)

### appsettings.json Example

```json
{
  "AdminApi": {
    "BaseUrl": "http://admin-api:8080",
    "TimeoutSeconds": 30,
    "UseAdminApi": true
  }
}
```

## Deployment Considerations

### Local Development

For local development, both services can run on different ports on the same machine:
- WebUI: http://localhost:5001
- Admin API: http://localhost:5000

### Container Deployment

For container environments, services can be deployed separately:
- Set `CONDUIT_ADMIN_API_URL=http://admin-api:8080` in WebUI container
- Ensure networking allows communication between containers

### Security

- Master key authentication is used to secure API endpoints
- All API requests include the master key in the X-Master-Key header
- HTTPS should be used in production environments

## Future Enhancements

- Implement token-based authentication with expiration
- Add pagination support for large data sets
- Implement API versioning
- Add Swagger/OpenAPI documentation
- Consider implementing GraphQL for efficient data fetching