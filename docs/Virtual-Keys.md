# Virtual Key Management

## Overview

ConduitLLM's Virtual Key Management system provides a secure and flexible way to control access to LLM services, track usage, manage budgets, and enforce usage limits. Virtual keys act as proxies for the actual provider API keys, allowing granular control and monitoring without exposing sensitive provider credentials.

## Key Components

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

### Services

#### VirtualKeyService

Manages all aspects of virtual keys:

- **CRUD Operations**: Create, read, update, and delete virtual keys
- **Validation**: Ensure keys are valid, active, and within budget
- **Spend Management**: Track and update usage amounts
- **Budget Reset**: Automatically reset spending based on configured periods

#### RequestLogService

Handles request logging and analysis:

- **Log Requests**: Record detailed information about each API call
- **Usage Analysis**: Generate statistics and reports on usage
- **Cost Tracking**: Calculate and attribute costs to virtual keys

#### VirtualKeyMaintenanceService

Handles automated maintenance tasks:

- **Budget Reset**: Reset spending on appropriate intervals
- **Expiration Handling**: Disable keys that have expired
- **Notification Generation**: Create alerts for budget limits and expirations

### Middleware

#### LlmRequestTrackingMiddleware

Intercepts and processes API requests:

- **Key Validation**: Verify the provided key is valid
- **Request Tracking**: Log details about the request
- **Spend Updating**: Calculate and record costs
- **Limit Enforcement**: Block requests that exceed budgets

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

### List All Virtual Keys

```
GET /api/virtual-keys
```

### Create Virtual Key

```
POST /api/virtual-keys
```

### Get Virtual Key

```
GET /api/virtual-keys/{id}
```

### Update Virtual Key

```
PUT /api/virtual-keys/{id}
```

### Delete Virtual Key

```
DELETE /api/virtual-keys/{id}
```

### Reset Spend

```
POST /api/virtual-keys/{id}/reset-spend
```

See the [API Reference](API-Reference.md) for detailed endpoint documentation.

## Security

### Master Key Protection

Sensitive virtual key operations are protected by master key authentication:

- **Master Key Requirement**: Required for create, update, delete, and reset operations
- **Header-Based Authentication**: Master key provided in the `X-Master-Key` header
- **Authorization Policy**: Implemented through `MasterKeyRequirement`, `MasterKeyAuthorizationHandler`

### Secure Storage

All virtual key data is securely stored:

- **Database Encryption**: Sensitive fields are encrypted at rest
- **API Key Security**: Provider API keys are never exposed via virtual keys
- **Audit Logging**: Changes to virtual keys are logged for security review

## WebUI Management

The WebUI provides a comprehensive interface for managing virtual keys:

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

## Implementation Example

### Creating a Virtual Key

```csharp
// Using the VirtualKeyService
var newKey = await _virtualKeyService.CreateVirtualKeyAsync(new CreateVirtualKeyDto
{
    Name = "Test API Access",
    Budget = 100.0m,
    BudgetPeriod = BudgetPeriod.Monthly,
    Expiration = DateTime.UtcNow.AddMonths(3),
    IsActive = true
});

// The returned key object includes the generated key value
string keyValue = newKey.Key; // Format: vk-xxxxxxxxxxxxxxxxxxxx
```

### Validating a Virtual Key

```csharp
// Using the VirtualKeyService
var validationResult = await _virtualKeyService.ValidateVirtualKeyAsync("vk-xxxxxxxxxxxxxxxxxxxx");

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

### Updating Spend

```csharp
// Using the VirtualKeyService
await _virtualKeyService.UpdateSpendAsync("vk-xxxxxxxxxxxxxxxxxxxx", 0.25m);
```

### Resetting Spend

```csharp
// Using the VirtualKeyService
await _virtualKeyService.ResetSpendAsync("vk-xxxxxxxxxxxxxxxxxxxx");
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
