# ConduitLLM Admin API

The ConduitLLM Admin API provides a dedicated administrative interface for managing ConduitLLM configurations and resources. It centralizes all admin functionality in a single API surface, improving separation of concerns and maintainability.

## Current Status ✅

The Admin API is production-ready with all major architectural issues resolved:

1. ✅ **Standardized DTOs**: All 136+ DTOs properly centralized in ConduitLLM.Configuration.DTOs
2. ✅ **Clean Architecture**: Proper separation of concerns with no circular dependencies
3. ✅ **Complete Services**: All core services fully implemented:
   - AdminVirtualKeyService
   - AdminIpFilterService  
   - AdminCostDashboardService
   - AdminLogService
   - AdminModelCostService
   - AdminSystemInfoService
   - Plus additional specialized services

### ✅ Resolved Dependency Issues

Previous dependency challenges have been successfully resolved:

1. ✅ **No Circular Dependencies**: Clean dependency graph with proper project separation
2. ✅ **Eliminated Duplicate DTOs**: All DTOs centralized with domain-specific organization
3. ✅ **Proper Extension Methods**: All shared functionality properly abstracted

**Current Architecture**: The Admin project now maintains clean dependencies on Configuration and Core projects only, with no WebUI dependencies required.

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