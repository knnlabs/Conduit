# ConduitLLM.Admin Project Plan

## Overview

This document outlines the plan for creating a dedicated Admin API for ConduitLLM. Currently, administrative functions are handled directly through the ConduitLLM.WebUI project, with controllers making direct database modifications through EF Core. Creating a dedicated Admin API will improve separation of concerns, centralize admin functionality, and provide a more maintainable architecture.

## Project Structure

### 1. ConduitLLM.Admin Project

```
ConduitLLM.Admin/
├── Controllers/
│   ├── AuthController.cs
│   ├── VirtualKeysController.cs
│   ├── ModelProviderMappingController.cs
│   ├── RouterController.cs
│   ├── LogsController.cs
│   ├── IpFilterController.cs
│   ├── CostDashboardController.cs
│   ├── SystemInfoController.cs
│   └── DatabaseBackupController.cs
├── DTOs/
│   ├── Auth/
│   ├── VirtualKey/
│   ├── ModelProviderMapping/
│   ├── Router/
│   ├── Logs/
│   ├── IpFilter/
│   ├── CostDashboard/
│   └── DatabaseBackup/
├── Services/
│   ├── Auth/
│   │   └── AdminAuthService.cs
│   ├── VirtualKeys/
│   │   └── AdminVirtualKeyService.cs
│   ├── ModelProviderMapping/
│   │   └── AdminModelProviderMappingService.cs
│   ├── Router/
│   │   └── AdminRouterService.cs
│   ├── Logs/
│   │   └── AdminLogService.cs
│   ├── IpFilter/
│   │   └── AdminIpFilterService.cs
│   ├── CostDashboard/
│   │   └── AdminCostDashboardService.cs
│   └── DatabaseBackup/
│       └── AdminDatabaseBackupService.cs
├── Interfaces/
│   ├── IAdminAuthService.cs
│   ├── IAdminVirtualKeyService.cs
│   ├── IAdminModelProviderMappingService.cs
│   ├── IAdminRouterService.cs
│   ├── IAdminLogService.cs
│   ├── IAdminIpFilterService.cs
│   ├── IAdminCostDashboardService.cs
│   └── IAdminDatabaseBackupService.cs
├── Security/
│   ├── MasterKeyAuthorizationHandler.cs
│   └── MasterKeyRequirement.cs
├── Middleware/
│   ├── AdminAuthenticationMiddleware.cs
│   └── AdminRequestTrackingMiddleware.cs
├── Extensions/
│   ├── ServiceCollectionExtensions.cs
│   └── WebApplicationExtensions.cs
├── Program.cs
└── ConduitLLM.Admin.csproj
```

## Core Components

### Controllers

The controllers will handle API requests and direct them to the appropriate service. Each controller will be focused on a specific domain area:

1. **AuthController**: Authentication for admin operations
2. **VirtualKeysController**: Management of virtual API keys
3. **ModelProviderMappingController**: Configuration of model-to-provider mappings
4. **RouterController**: Configuration of routing rules and model deployments
5. **LogsController**: Access to request logs and usage data
6. **IpFilterController**: Management of IP filtering rules
7. **CostDashboardController**: Cost tracking and reporting
8. **SystemInfoController**: System metrics and status information
9. **DatabaseBackupController**: Database backup and restore operations

### Services

Each domain area will have a dedicated service that implements the business logic and interacts with the repositories. The services will:

1. Be interface-based for testability
2. Handle validation logic
3. Coordinate operations across multiple repositories when needed
4. Abstract database access from controllers

### Security

The Admin API will use the same master key authentication mechanism as the current WebUI, but with dedicated middleware for the API context. This ensures that only authorized administrators can access the API.

### Database Access

The Admin API will:

1. Use the repository pattern to access data
2. Reuse the same repositories used by the Core and WebUI projects
3. Not implement any direct EF Core access to maintain separation of concerns

## Integration with Existing Projects

### Shared Components

1. **DTOs**: Will leverage existing DTOs from ConduitLLM.Configuration where possible
2. **Repositories**: Will reuse repositories from ConduitLLM.Configuration
3. **Authentication**: Will reuse the MasterKeyAuthorizationHandler from ConduitLLM.WebUI

### Changes to Existing Projects

1. **ConduitLLM.WebUI**:
   - Update to consume the Admin API instead of directly accessing repositories
   - Create a client service to communicate with the Admin API
   - Remove duplicate business logic that should be centralized in the Admin API

2. **ConduitLLM.Core**:
   - No significant changes required, as it should already be using repositories

## Test Strategy

1. **Unit Tests**: Create unit tests for all services
2. **Integration Tests**: Create integration tests for the API endpoints
3. **End-to-End Tests**: Test the WebUI using the Admin API

## Deployment Strategy

The Admin API will be deployed as a separate service, with these options:

1. **Standalone Mode**: Run as an independent service
2. **Integrated Mode**: Run alongside the WebUI and HTTP API in a single deployment

## Next Steps

1. Create the ConduitLLM.Admin project and basic structure
2. Define interfaces for all services
3. Implement each service, starting with the most critical (VirtualKeys, ModelProviderMapping)
4. Add controllers that expose the service functionality
5. Update the WebUI to use the Admin API
6. Add comprehensive tests
7. Update documentation