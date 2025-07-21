# ConduitLLM.AdminClient

Official .NET client library for the Conduit Admin API.

## Features

- Complete Admin API coverage with 10 major service areas
- Type-safe C# models with comprehensive validation
- Built-in caching and retry policies with TTL and invalidation
- Async/await throughout
- Comprehensive error handling with custom exception hierarchy
- Master key authentication
- Dependency injection support

## Services Included

- **VirtualKeyService** - Manage API keys, budget controls, and refunds
- **ProviderService** - Manage provider credentials and health monitoring
- **AnalyticsService** - Cost analysis, usage metrics, and reporting
- **DiscoveryService** - Model discovery and capability testing
- **ModelMappingService** - Manage model provider mappings and routing
- **SettingsService** - Global settings and configuration management
- **IpFilterService** - IP access control and filtering
- **ModelCostService** - Model pricing configurations and cost tracking
- **SystemService** - System information and health status monitoring
- **ProviderModelsService** - Provider model discovery and capability testing

## Installation

```bash
dotnet add package ConduitLLM.AdminClient
```

## Usage

```csharp
using ConduitLLM.AdminClient;

// Create client from environment variables
var client = ConduitAdminClient.FromEnvironment();

// Or create with explicit configuration
var client = ConduitAdminClient.Create("your-master-key", "https://admin-api-url");

// Use the services
var virtualKeys = await client.VirtualKeys.ListAsync();
var costSummary = await client.Analytics.GetCostSummaryAsync(DateTime.Now.AddDays(-7), DateTime.Now);

// Manage model costs
var modelCosts = await client.ModelCosts.ListAsync();

// Issue refunds
var refundRequest = new RefundSpendRequest
{
    Amount = 10.50m,
    Reason = "Service interruption",
    OriginalTransactionId = "txn_12345" // Optional
};
await client.VirtualKeys.RefundSpendAsync(virtualKeyId, refundRequest);
var costOverview = await client.ModelCosts.GetOverviewAsync(DateTime.Now.AddDays(-30), DateTime.Now);

// System monitoring
var systemInfo = await client.System.GetSystemInfoAsync();
var healthStatus = await client.System.GetHealthStatusAsync();

// Provider model discovery
var discoveredModels = await client.ProviderModels.GetDiscoveredModelsAsync();
var providerModels = await client.ProviderModels.GetProviderModelsAsync("openai");
```