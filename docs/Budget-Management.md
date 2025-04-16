# Budget Management

## Overview

ConduitLLM provides comprehensive budget management capabilities to help control costs, track usage, and ensure predictable spending across LLM services. The budget management system is tightly integrated with the virtual key framework, allowing granular control over spending at the key level.

## Key Components

### Budget-Related Entities

#### VirtualKey Budget Properties

```csharp
public class VirtualKey
{
    // Other properties omitted for brevity
    
    // Budget limit for this key
    public decimal? Budget { get; set; }
    
    // Current amount spent
    public decimal Spent { get; set; }
    
    // Period for budget reset (daily/monthly)
    public BudgetPeriod? BudgetPeriod { get; set; }
    
    // When the budget was last reset
    public DateTime? LastBudgetReset { get; set; }
}
```

#### BudgetPeriod Enum

```csharp
public enum BudgetPeriod
{
    Daily,
    Monthly
}
```

#### RequestLog Cost Tracking

```csharp
public class RequestLog
{
    // Other properties omitted for brevity
    
    // Cost of this specific request
    public decimal Cost { get; set; }
    
    // Token counts for cost calculation
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
}
```

### Budget Management Services

#### VirtualKeyService

Handles core budget functionality:

- Setting budget limits
- Tracking spending
- Resetting budgets based on configured periods

#### VirtualKeyMaintenanceService

Manages automated budget operations:

- Scheduled budget resets
- Budget threshold notifications
- Usage reporting

#### RequestLogService

Provides analytics for budget monitoring:

- Usage trends analysis
- Cost projection
- Budget utilization tracking

## Budget Features

### Budget Setting

Each virtual key can have an optional budget limit:

- **No Budget**: Keys can operate without spending limits
- **Fixed Budget**: Set a maximum spend amount
- **Budget Period**: Define daily or monthly reset cycles

### Spend Tracking

Accurate tracking of costs per request:

- **Token Counting**: Record exact token usage
- **Cost Calculation**: Apply provider-specific pricing models
- **Cumulative Tracking**: Maintain running totals on keys

### Budget Enforcement

Proactive limits on spending:

- **Request Validation**: Check budget before processing
- **Request Rejection**: Block requests that would exceed budget
- **Grace Margin**: Optional buffer beyond strict limit

### Automatic Budget Reset

Periodic budget refreshing:

- **Daily Reset**: Budget resets every 24 hours
- **Monthly Reset**: Budget resets on the 1st of each month
- **Reset Tracking**: Last reset time recorded for validation

### Notifications

Alerts for budget-related events:

- **Threshold Warnings**: Notify when approaching limits (50%, 80%, 90%)
- **Limit Reached**: Alert when budget is fully consumed
- **Reset Notification**: Confirm when budget has been reset

## Budget Visualization

The WebUI provides comprehensive budget visualization:

### VirtualKeys Dashboard

- **Budget Utilization Charts**: Visual representation of spent vs. remaining budget
- **Historical Trends**: Graph of spending over time
- **Projection Analytics**: Estimated time until budget depletion

### Usage Reports

- **Cost Breakdown**: Spending by model and provider
- **Request Volume**: Number of requests per time period
- **Average Cost**: Mean cost per request

## Implementation Examples

### Setting a Budget

```csharp
// Creating a key with a budget
var newKey = await _virtualKeyService.CreateVirtualKeyAsync(new CreateVirtualKeyDto
{
    Name = "Marketing API",
    Budget = 100.0m,                   // $100 budget
    BudgetPeriod = BudgetPeriod.Monthly, // Reset monthly
    IsActive = true
});

// Updating a key's budget
await _virtualKeyService.UpdateVirtualKeyAsync(keyId, new UpdateVirtualKeyDto
{
    Budget = 150.0m,                   // Increase to $150
    BudgetPeriod = BudgetPeriod.Monthly  // Keep monthly reset
});
```

### Tracking Spend

```csharp
// Record spending from a request
await _virtualKeyService.UpdateSpendAsync(
    keyId,    // Virtual key ID 
    0.25m     // Cost of the request
);

// Manually resetting spend
await _virtualKeyService.ResetSpendAsync(keyId);
```

### Budget Validation

```csharp
// Check if a request can proceed
var validationResult = await _virtualKeyService.ValidateVirtualKeyAsync(keyValue);

if (validationResult.IsValid)
{
    // Key is valid and within budget
    // Proceed with request
}
else if (validationResult.ErrorCode == "BudgetExceeded")
{
    // Budget limit reached
    // Handle gracefully
}
```

## Budget Reset Logic

The budget reset system automatically manages spending periods:

### Daily Reset Process

```csharp
// Pseudocode for daily reset logic
if (key.BudgetPeriod == BudgetPeriod.Daily && 
    (key.LastBudgetReset == null || 
     DateTime.UtcNow.Subtract(key.LastBudgetReset.Value).TotalHours >= 24))
{
    key.Spent = 0;
    key.LastBudgetReset = DateTime.UtcNow;
    await _dbContext.SaveChangesAsync();
}
```

### Monthly Reset Process

```csharp
// Pseudocode for monthly reset logic
if (key.BudgetPeriod == BudgetPeriod.Monthly && 
    (key.LastBudgetReset == null || 
     key.LastBudgetReset.Value.Month != DateTime.UtcNow.Month ||
     key.LastBudgetReset.Value.Year != DateTime.UtcNow.Year))
{
    key.Spent = 0;
    key.LastBudgetReset = DateTime.UtcNow;
    await _dbContext.SaveChangesAsync();
}
```

## Cost Calculation

The system calculates costs based on provider-specific pricing models:

### Token-Based Pricing

Most providers charge based on token count:

```csharp
// Pseudocode for token-based cost calculation
decimal CalculateCost(string provider, string model, int promptTokens, int completionTokens)
{
    var pricing = _pricingService.GetPricing(provider, model);
    
    decimal promptCost = (promptTokens / 1000.0m) * pricing.PromptPricePerThousandTokens;
    decimal completionCost = (completionTokens / 1000.0m) * pricing.CompletionPricePerThousandTokens;
    
    return promptCost + completionCost;
}
```

### Request Tracking Middleware

The middleware automatically calculates and records costs:

```csharp
// Pseudocode for middleware cost tracking
public async Task InvokeAsync(HttpContext context)
{
    // Process the request
    await _next(context);
    
    // Extract token counts from the response
    var tokenCounts = ExtractTokenCounts(context.Response);
    
    // Calculate cost
    decimal cost = CalculateCost(
        provider: context.Items["Provider"] as string,
        model: context.Items["Model"] as string,
        promptTokens: tokenCounts.PromptTokens,
        completionTokens: tokenCounts.CompletionTokens
    );
    
    // Update key spend
    string keyValue = context.Request.Headers["X-API-Key"];
    await _virtualKeyService.UpdateSpendAsync(keyValue, cost);
    
    // Log the request
    await _requestLogService.LogRequestAsync(
        keyValue,
        context.Items["Provider"] as string,
        context.Items["Model"] as string,
        tokenCounts.PromptTokens,
        tokenCounts.CompletionTokens,
        cost,
        success: context.Response.StatusCode >= 200 && context.Response.StatusCode < 300
    );
}
```

## API Endpoints

### Get Virtual Key with Budget Information

```
GET /api/virtual-keys/{id}
```

Response includes budget details:

```json
{
  "id": "key-id",
  "name": "Marketing API",
  "key": "vk-xxxxxxxxxxxxxxxxxxxx",
  "budget": 100.0,
  "spent": 25.5,
  "budgetPeriod": "Monthly",
  "lastBudgetReset": "2023-04-01T00:00:00Z",
  "isActive": true
}
```

### Reset Virtual Key Spend

```
POST /api/virtual-keys/{id}/reset-spend
```

Manually resets the spent amount to zero.

### Get Usage Statistics

```
GET /api/usage-stats?virtualKeyId={id}&startDate={start}&endDate={end}
```

Returns detailed usage statistics for budget analysis.

## Best Practices

### Budget Setting

- **Start Conservative**: Begin with lower budgets and adjust as needed
- **Different Tiers**: Create different budget tiers for different use cases
- **Include Buffer**: Set budgets slightly higher than expected needs
- **Consider Peaks**: Account for usage spikes in budget planning

### Budget Monitoring

- **Regular Reviews**: Check usage patterns frequently
- **Alert Configuration**: Set up notifications before limits are reached
- **Dashboard Monitoring**: Use the WebUI to visualize trends
- **Usage Forecasting**: Project future spending based on current patterns

### Cost Optimization

- **Model Selection**: Use cheaper models for less critical tasks
- **Prompt Engineering**: Optimize prompts to reduce token usage
- **Caching**: Implement caching for common requests
- **Request Batching**: Combine multiple requests where possible

### Organizational Strategy

- **Department-Specific Keys**: Create keys with budgets aligned to department allocations
- **Project-Based Budgeting**: Assign budgets to specific projects
- **Budget Periods**: Align reset periods with accounting cycles
- **Testing vs. Production**: Use different budget strategies for different environments
