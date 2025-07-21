# Virtual Key Management

## Overview

ConduitLLM's Virtual Key Management system provides a secure and flexible way to control access to LLM services, track usage, manage budgets, and enforce usage limits. Virtual keys act as proxies for the actual provider API keys, allowing granular control and monitoring without exposing sensitive provider credentials.

## Architecture

Virtual key management follows ConduitLLM's microservices architecture with these components:

1. **Admin API**: Manages virtual keys and provides endpoints for the WebUI
2. **LLM API**: Validates virtual keys and tracks usage for API requests
3. **WebUI**: Provides a user interface for managing virtual keys

### Core Entities

#### VirtualKey

The primary entity containing all information about a virtual key:

```csharp
public class VirtualKey
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Key { get; set; }
    public DateTime Created { get; set; }
    public DateTime? Expiration { get; set; }
    public decimal? Budget { get; set; }
    public decimal Spent { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastUsed { get; set; }
    public BudgetPeriod? BudgetPeriod { get; set; }
    public DateTime? LastBudgetReset { get; set; }
}
```

#### RequestLog

Tracks detailed information about requests made with a virtual key:

```csharp
public class RequestLog
{
    public string Id { get; set; }
    public string VirtualKeyId { get; set; }
    public DateTime Timestamp { get; set; }
    public string Model { get; set; }
    public string Provider { get; set; }
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public decimal Cost { get; set; }
    public bool Success { get; set; }
    public string ErrorMessage { get; set; }
}
```

### Service Architecture

#### Admin API Services

The Admin API provides these virtual key services:

**AdminVirtualKeyService**
- **CRUD Operations**: Create, read, update, and delete virtual keys
- **Spend Management**: Track and update usage amounts
- **Budget Reset**: Manually reset spending

**VirtualKeyMaintenanceService**
- **Budget Reset**: Reset spending on appropriate intervals
- **Expiration Handling**: Disable keys that have expired
- **Notification Generation**: Create alerts for budget limits and expirations

#### LLM API Services

The LLM API handles virtual key validation and request tracking:

**ApiVirtualKeyService**
- **Key Validation**: Verify the provided key is valid, active, and within budget
- **Spend Updating**: Calculate and record costs

#### Middleware

**LlmRequestTrackingMiddleware**
- **Request Interception**: Process incoming API requests
- **Key Extraction**: Extract virtual keys from request headers
- **Key Validation**: Verify the provided key is valid
- **Request Tracking**: Log details about the request
- **Limit Enforcement**: Block requests that exceed budgets

#### WebUI Components

The WebUI provides adapter services that communicate with the Admin API:

**VirtualKeyServiceAdapter**
- Implements `IVirtualKeyService` interface
- Translates WebUI operations to Admin API requests
- Handles HTTP communication with the Admin API
- Provides backward compatibility for existing UI components

## Virtual Key Features

### Authentication and Authorization

Virtual keys provide a secure authentication mechanism:

- **API Key Format**: Virtual keys follow the format `vk-xxxxxxxxxxxxxxxxxxxx`
- **Header-Based Authentication**: Keys are provided in the `X-API-Key` header
- **Validation Process**: Keys are validated for existence, expiration, and budget

### Budget Management

Control spending with customizable budgets:

- **Budget Limits**: Set maximum spending for each key
- **Budget Periods**: Configure daily or monthly reset periods
- **Spend Tracking**: Automatically track costs based on token usage
- **Budget Reset**: Automatically reset spending at configured intervals

### Usage Tracking

Detailed monitoring of API usage:

- **Request Logging**: Record every API call made with a key
- **Token Counting**: Track prompt, completion, and total tokens
- **Cost Calculation**: Calculate costs based on provider pricing
- **Usage Analytics**: Generate statistics on usage patterns

### Expiration Control

Limit the validity period of keys:

- **Expiration Dates**: Set when keys should become invalid
- **Automatic Disabling**: Expired keys are automatically deactivated
- **Expiration Notifications**: Alerts when keys are approaching expiration

### Notification System

Real-time alerts for key events:

- **Budget Alerts**: Notifications when approaching budget limits
- **Expiration Alerts**: Warnings before key expiration
- **Error Notifications**: Alerts for repeated validation failures
- **WebUI Integration**: In-app notification display

## API Endpoints

Virtual keys are managed through the Admin API. 

### Admin API Endpoints

#### List All Virtual Keys

```
GET /api/virtualkeys
```

#### Create Virtual Key

```
POST /api/virtualkeys
```

#### Get Virtual Key

```
GET /api/virtualkeys/{id}
```

#### Update Virtual Key

```
PUT /api/virtualkeys/{id}
```

#### Delete Virtual Key

```
DELETE /api/virtualkeys/{id}
```

#### Reset Spend

```
POST /api/virtualkeys/{id}/reset-spend
```

See the [API Reference](API-Reference.md) for detailed endpoint documentation.

### LLM API Usage

Virtual keys are used for authentication with the LLM API:

```
POST /v1/chat/completions
Authorization: Bearer condt_yourvirtualkey
```

## Security

### Master Key Protection

Sensitive virtual key operations are protected by master key authentication:

- **Master Key Requirement**: Required for create, update, delete, and reset operations
- **Header-Based Authentication**: Master key provided in the `X-Master-Key` header
- **Authorization Policy**: Implemented through `MasterKeyRequirement`, `MasterKeyAuthorizationHandler`

### Secure Storage

All virtual key data is securely stored:

- **Virtual Key Hashing**: Virtual key values are never stored in plaintext. Only a secure hash (e.g., SHA-256) of each key is persisted in the database for validation purposes.
- **Provider API Key Security**: Provider API keys are never exposed via virtual keys. Storage of provider API keys depends on your database and deployment environment.
- **Audit Logging**: Changes to virtual keys are logged for security review.

> **Note:** ConduitLLM does not perform field-level encryption or full database encryption by default. For additional protection, use full-disk or database encryption at the infrastructure level as appropriate for your environment.
> 
> Hashing is used for virtual key values to prevent recovery of the original key from the database. Encryption, if required, should be implemented at the storage or infrastructure layer.

## WebUI Management

The WebUI provides a comprehensive interface for managing virtual keys via the Admin API:

### VirtualKeys Management Page

A dedicated interface for key management:

- **Key Creation**: Wizard for creating new virtual keys
- **Key Listing**: View all virtual keys with filtering and sorting
- **Key Editing**: Update key properties including budgets and expiration
- **Key Deletion**: Securely delete unneeded keys

### VirtualKeys Dashboard

Visualization of key usage and status:

- **Usage Statistics**: Charts and graphs showing usage patterns
- **Budget Consumption**: Visual indicators of budget utilization
- **Active/Inactive Status**: Clear indicators of key status
- **Recent Activity**: Timeline of recent key usage

### NotificationDisplay

Real-time alerts related to virtual keys:

- **Budget Warnings**: Notifications when keys approach their budget
- **Expiration Alerts**: Warnings when keys are nearing expiration
- **Error Notifications**: Alerts for validation or usage issues

### Implementation Architecture

The WebUI interacts with virtual keys through a layered approach:

1. **UI Components**: User interface elements in the Blazor application
2. **Service Adapters**: Implement service interfaces and communicate with Admin API
3. **Admin API Client**: Makes HTTP requests to the Admin API endpoints
4. **Admin API**: Performs actual operations on the database

This architecture provides:
- **Clean separation** of UI and business logic
- **Improved security** by restricting direct database access
- **Scalability** through service distribution
- **Backward compatibility** with existing UI components

## Implementation Examples

### WebUI - Creating a Virtual Key

```csharp
// Using the VirtualKeyServiceAdapter in WebUI
// This internally calls the Admin API
var newKey = await _virtualKeyService.CreateVirtualKeyAsync(new CreateVirtualKeyRequestDto
{
    Name = "Test API Access",
    Budget = 100.0m,
    BudgetPeriod = BudgetPeriod.Monthly,
    Expiration = DateTime.UtcNow.AddMonths(3),
    IsActive = true
});

// The returned key object includes the generated key value
string keyValue = newKey.Key; // Format: condt_xxxxxxxxxxxxxxxxxxxx
```

### LLM API - Validating a Virtual Key

```csharp
// Using the ApiVirtualKeyService in LLM API
// This calls the Admin API to validate
var validationResult = await _apiVirtualKeyService.ValidateVirtualKeyAsync("condt_xxxxxxxxxxxxxxxxxxxx");

if (validationResult.IsValid)
{
    // Key is valid, proceed with request
}
else
{
    // Handle validation error
    string errorReason = validationResult.ErrorMessage;
}
```

### Admin API - Direct Implementation

```csharp
// Using the AdminVirtualKeyService in the Admin API
// This accesses the database directly
var result = await _adminVirtualKeyService.UpdateSpendAsync("condt_xxxxxxxxxxxxxxxxxxxx", 0.25m);

// Reset spend
await _adminVirtualKeyService.ResetSpendAsync("condt_xxxxxxxxxxxxxxxxxxxx");
```

### Calling the Admin API Directly

```csharp
// Example of calling the Admin API directly from any client
using var httpClient = new HttpClient();
httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {masterKey}");

// Create a virtual key
var createRequest = new CreateVirtualKeyRequestDto
{
    Name = "External API Access",
    Budget = 50.0m,
    BudgetPeriod = BudgetPeriod.Monthly,
    IsActive = true
};

var response = await httpClient.PostAsJsonAsync(
    "http://localhost:5001/api/virtualkeys", 
    createRequest);

var newKey = await response.Content.ReadFromJsonAsync<CreateVirtualKeyResponseDto>();
string keyValue = newKey.Key;
```

## Best Practices

### Key Management

- **Name Keys Meaningfully**: Use descriptive names that identify the purpose
- **Set Reasonable Budgets**: Configure budgets appropriate to expected usage
- **Use Expiration Dates**: Set expirations to enforce periodic review
- **Regular Auditing**: Periodically review and clean up unused keys

### Security

- **Secure the Master Key**: Treat the master key as a sensitive secret
- **Rotate Keys**: Periodically create new keys and retire old ones
- **Least Privilege**: Give keys only the budget they need
- **Monitor Usage**: Regularly review the usage patterns for anomalies

### Budget Configuration

- **Start Conservative**: Begin with lower budgets and adjust as needed
- **Match Budget Periods**: Align reset periods with billing cycles
- **Set Buffer Margins**: Configure budgets with room for unexpected usage
- **Monitor Closely**: Watch usage patterns to refine budget settings

### Error Handling

- **Client-Side Validation**: Include proper error handling when keys are rejected
- **Graceful Degradation**: Handle key validation failures appropriately
- **Retry With Backoff**: Implement retries for transient errors
- **Error Notification**: Configure alerts for repeated validation failures
