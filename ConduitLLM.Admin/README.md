# ConduitLLM Admin API

The ConduitLLM Admin API provides a dedicated administrative interface for managing ConduitLLM configurations and resources. It centralizes all admin functionality in a single API surface, improving separation of concerns and maintainability.

## Current Status ⚠️

The Admin API is currently in development. Major progress has been made:

1. ✅ Created proper DTO classes in ConduitLLM.Configuration
2. ✅ Implemented controllers with appropriate authorization
3. ✅ Implemented several core services:
   - AdminVirtualKeyService
   - AdminIpFilterService
   - AdminCostDashboardService (partial)
   - AdminLogService (partial)

### Dependency Issues

There are currently unresolved dependency issues between this project and ConduitLLM.WebUI. See [DEPENDENCY-ISSUES.md](../README-DEPENDENCY-ISSUES.md) for a detailed analysis.

Key challenges:

1. **Circular Dependencies**: The Admin project depends on WebUI's services, while WebUI depends on the Admin API.
2. **Duplicate DTOs**: Many DTOs are duplicated between WebUI and Configuration, causing ambiguous reference errors.
3. **Missing Extension Methods**: Many methods used in Admin services are defined as extension methods in WebUI.

**Current Workaround**: For development purposes, this project maintains a reference to ConduitLLM.WebUI until the dependency issues are fully resolved.

### Path to Resolution

To fully resolve the dependency issues:

1. Move all shared DTOs to ConduitLLM.Configuration
2. Create extension method libraries in ConduitLLM.Configuration or ConduitLLM.Core
3. Implement Admin services without depending on WebUI code
4. Update WebUI to use the Admin API client

## Features

- **Virtual Keys Management**: Create, read, update, and delete virtual API keys
- **Model Provider Mapping**: Configure model-to-provider mappings
- **Router Configuration**: Manage routing rules, model deployments, and fallback configurations
- **IP Filtering**: Control access with IP whitelist/blacklist rules
- **Logs Management**: Query request logs and usage data
- **Cost Dashboard**: Track spending and view cost analytics
- **System Information**: Monitor system health and configuration

## Getting Started

### Prerequisites

- .NET 9.0 SDK or later
- Access to a ConduitLLM database instance

### Configuration

The Admin API uses the following configuration settings:

```json
{
  "AdminApi": {
    "MasterKey": "your-master-key-here",
    "AllowedOrigins": [ "http://localhost:5000", "https://localhost:5001" ]
  },
  "ConnectionStrings": {
    "ConfigurationDb": "..."
  }
}
```

### Running the Admin API

To run the Admin API:

```bash
dotnet run --project ConduitLLM.Admin
```

By default, the API will be available at `https://localhost:7000`.

## API Authentication

The Admin API uses API key authentication. Include your master key in the `X-API-Key` header for all requests:

```
X-API-Key: your-master-key-here
```

## API Endpoints

### Virtual Keys

- `GET /api/virtualkeys` - List all virtual keys
- `GET /api/virtualkeys/{id}` - Get a specific virtual key
- `POST /api/virtualkeys` - Create a new virtual key
- `PUT /api/virtualkeys/{id}` - Update a virtual key
- `DELETE /api/virtualkeys/{id}` - Delete a virtual key
- `POST /api/virtualkeys/{id}/reset-spend` - Reset spend for a virtual key

### IP Filters

- `GET /api/ipfilter` - List all IP filters
- `GET /api/ipfilter/enabled` - List enabled IP filters
- `GET /api/ipfilter/{id}` - Get a specific IP filter
- `POST /api/ipfilter` - Create a new IP filter
- `PUT /api/ipfilter` - Update an IP filter
- `DELETE /api/ipfilter/{id}` - Delete an IP filter
- `GET /api/ipfilter/settings` - Get IP filter settings
- `PUT /api/ipfilter/settings` - Update IP filter settings

### Cost Dashboard

- `GET /api/costs/summary` - Get cost summary
- `GET /api/costs/trends` - Get cost trends
- `GET /api/costs/models` - Get costs by model
- `GET /api/costs/virtualkeys` - Get costs by virtual key

### Logs

- `GET /api/logs` - Get paginated logs
- `GET /api/logs/{id}` - Get a specific log
- `GET /api/logs/summary` - Get logs summary

### System Info

- `GET /api/systeminfo` - Get system information

## Development

### Project Structure

- **Controllers/**: API endpoints
- **Services/**: Business logic
- **Interfaces/**: Service contracts
- **Security/**: Authentication and authorization
- **Middleware/**: Request processing
- **Extensions/**: Service configuration

### Building

```bash
dotnet build ConduitLLM.Admin
```

### Testing

```bash
dotnet test ConduitLLM.Admin.Tests
```

## Integration with WebUI

The WebUI project can be configured to use the Admin API for administrative functions instead of direct database access. This improves separation of concerns and maintainability.

## API Documentation

API documentation is available via Swagger at `/swagger` when running in development mode. The API also provides comprehensive XML documentation for use with tools like Swagger.