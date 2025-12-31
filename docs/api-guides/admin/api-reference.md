# Admin API Reference

Complete endpoint documentation for the Conduit Admin API.

## Table of Contents

1. [Virtual Keys Management](#virtual-keys-management)
2. [Provider Configuration](#provider-configuration)
3. [Model Mappings](#model-mappings)
4. [Usage and Monitoring](#usage-and-monitoring)
5. [System Configuration](#system-configuration)
6. [IP Filtering](#ip-filtering)

---

## Virtual Keys Management

### List All Virtual Keys

```
GET /api/virtualkeys
```

Returns a list of all virtual keys.

**Response:**
```json
[
  {
    "id": 1,
    "keyName": "Production Key",
    "virtualKey": "condt_sk_abc123...",
    "allowedModels": "gpt-4*,claude-*",
    "maxBudget": 1000.00,
    "currentSpend": 245.67,
    "budgetDuration": "Monthly",
    "isEnabled": true,
    "createdAt": "2025-01-01T00:00:00Z",
    "rateLimitRpm": 60,
    "rateLimitRpd": 10000
  }
]
```

### Get Virtual Key by ID

```
GET /api/virtualkeys/{id}
```

Returns details for a specific virtual key.

### Create Virtual Key

```
POST /api/virtualkeys
```

Creates a new virtual key.

**Request Body:**
```json
{
  "keyName": "New Application Key",
  "allowedModels": "gpt-4*,claude-*",
  "maxBudget": 500.00,
  "budgetDuration": "Monthly",
  "rateLimitRpm": 60,
  "rateLimitRpd": 10000,
  "expiresAt": "2025-12-31T23:59:59Z",
  "metadata": "{\"team\":\"engineering\",\"env\":\"production\"}"
}
```

**Response:**
```json
{
  "virtualKey": "condt_sk_xyz789...",
  "keyInfo": {
    "id": 2,
    "keyName": "New Application Key",
    "allowedModels": "gpt-4*,claude-*",
    "maxBudget": 500.00,
    "currentSpend": 0.00,
    "budgetDuration": "Monthly",
    "isEnabled": true,
    "createdAt": "2025-01-07T10:30:00Z"
  }
}
```

### Update Virtual Key

```
PUT /api/virtualkeys/{id}
```

Updates an existing virtual key.

**Request Body:**
```json
{
  "keyName": "Updated Key Name",
  "maxBudget": 1500.00,
  "allowedModels": "gpt-4*,claude-*,gemini-*",
  "isEnabled": true,
  "rateLimitRpm": 120
}
```

### Delete Virtual Key

```
DELETE /api/virtualkeys/{id}
```

Deletes a virtual key.

**Response:** 204 No Content

### Validate Virtual Key

```
POST /api/virtualkeys/validate
```

Validates a virtual key and returns its configuration.

**Request Body:**
```json
{
  "virtualKey": "condt_sk_abc123..."
}
```

**Response:**
```json
{
  "isValid": true,
  "keyName": "Production Key",
  "allowedModels": ["gpt-4", "gpt-4-turbo", "claude-3-opus"],
  "maxBudget": 1000.00,
  "currentSpend": 245.67,
  "isEnabled": true,
  "rateLimitRpm": 60,
  "rateLimitRpd": 10000
}
```

---

## Provider Configuration

### List All Providers

```
GET /api/providers
```

Returns a list of all configured providers.

**Response:**
```json
[
  {
    "id": 1,
    "name": "Production OpenAI",
    "providerType": "OpenAI",
    "isEnabled": true,
    "priority": 100,
    "createdAt": "2025-01-01T00:00:00Z"
  }
]
```

### Get Provider by ID

```
GET /api/providers/{id}
```

Returns details for a specific provider.

### Create Provider

```
POST /api/providers
```

Creates a new provider configuration.

**Request Body:**
```json
{
  "name": "Production OpenAI",
  "providerType": "OpenAI",
  "isEnabled": true,
  "priority": 100
}
```

### Update Provider

```
PUT /api/providers/{id}
```

Updates an existing provider.

**Request Body:**
```json
{
  "name": "Updated Provider Name",
  "isEnabled": true,
  "priority": 90
}
```

### Delete Provider

```
DELETE /api/providers/{id}
```

Deletes a provider configuration.

**Response:** 204 No Content

### Add Provider Credential

```
POST /api/providers/{id}/credentials
```

Adds API credentials to a provider.

**Request Body:**
```json
{
  "apiKey": "sk-...",
  "apiEndpoint": "https://api.openai.com/v1",
  "organizationId": "org-...",
  "isEnabled": true
}
```

### Test Provider Connection

```
POST /api/providers/{id}/test
```

Tests connectivity to a provider.

**Response:**
```json
{
  "success": true,
  "providerName": "Production OpenAI",
  "message": "Connection successful",
  "modelsAvailable": ["gpt-4", "gpt-4-turbo", "gpt-3.5-turbo"],
  "responseTimeMs": 234
}
```

### Get Provider Health Status

```
GET /api/providerhealth/{id}
```

Returns health status for a provider.

**Response:**
```json
{
  "providerId": 1,
  "providerName": "Production OpenAI",
  "isHealthy": true,
  "lastCheckAt": "2025-01-07T10:35:00Z",
  "successRate": 99.8,
  "averageResponseTimeMs": 450,
  "errorRate": 0.2
}
```

### Get Provider Health Summary

```
GET /api/providerhealth/summary
```

Returns health summary for all providers.

---

## Model Mappings

### List All Model Mappings

```
GET /api/mappings
```

Returns all model-to-provider mappings.

**Response:**
```json
[
  {
    "id": 1,
    "modelAlias": "gpt-4",
    "providerId": 1,
    "providerModelId": "gpt-4-0613",
    "isEnabled": true,
    "priority": 100
  }
]
```

### Create Model Mapping

```
POST /api/mappings
```

Creates a new model mapping.

**Request Body:**
```json
{
  "modelAlias": "gpt-4",
  "providerId": 1,
  "providerModelId": "gpt-4-0613",
  "isEnabled": true,
  "priority": 100
}
```

### Update Model Mapping

```
PUT /api/mappings/{id}
```

Updates an existing model mapping.

**Request Body:**
```json
{
  "isEnabled": true,
  "priority": 90
}
```

### Delete Model Mapping

```
DELETE /api/mappings/{id}
```

Deletes a model mapping.

**Response:** 204 No Content

---

## Usage and Monitoring

### Get Request Logs

```
GET /api/logs
```

Returns request logs with optional filtering.

**Query Parameters:**
- `startDate` - Filter by start date (ISO 8601)
- `endDate` - Filter by end date (ISO 8601)
- `virtualKeyId` - Filter by virtual key ID
- `modelId` - Filter by model name
- `pageSize` - Number of results per page (default: 100)
- `page` - Page number (default: 1)

**Response:**
```json
{
  "items": [
    {
      "id": 12345,
      "virtualKeyId": 1,
      "modelId": "gpt-4",
      "providerId": 1,
      "promptTokens": 150,
      "completionTokens": 75,
      "totalTokens": 225,
      "cost": 0.0135,
      "responseTimeMs": 1250,
      "statusCode": 200,
      "createdAt": "2025-01-07T10:30:00Z"
    }
  ],
  "totalCount": 5000,
  "page": 1,
  "pageSize": 100
}
```

### Get Cost Dashboard

```
GET /api/cost-dashboard
```

Returns cost analytics and usage statistics.

**Query Parameters:**
- `startDate` - Start date for analytics period
- `endDate` - End date for analytics period

**Response:**
```json
{
  "totalCost": 1234.56,
  "totalRequests": 50000,
  "averageCostPerRequest": 0.0247,
  "costsByModel": [
    {
      "model": "gpt-4",
      "cost": 890.12,
      "requests": 12000
    },
    {
      "model": "claude-3-opus",
      "cost": 344.44,
      "requests": 38000
    }
  ],
  "costsByKey": [
    {
      "keyName": "Production Key",
      "cost": 789.00,
      "requests": 25000
    }
  ],
  "costsByProvider": [
    {
      "providerName": "Production OpenAI",
      "cost": 1100.00,
      "requests": 40000
    }
  ]
}
```

---

## System Configuration

### Get Global Settings

```
GET /api/settings
```

Returns global system settings.

**Response:**
```json
{
  "systemName": "Conduit LLM",
  "defaultMaxBudget": 100.00,
  "defaultBudgetDuration": "Monthly",
  "enableUsageTracking": true,
  "enableCostTracking": true,
  "enableHealthMonitoring": true,
  "healthCheckIntervalSeconds": 300
}
```

### Update Global Settings

```
PUT /api/settings
```

Updates global system settings.

**Request Body:**
```json
{
  "defaultMaxBudget": 150.00,
  "defaultBudgetDuration": "Monthly",
  "healthCheckIntervalSeconds": 600
}
```

### Get System Information

```
GET /api/system-info
```

Returns system version and status information.

**Response:**
```json
{
  "version": "1.0.0",
  "environment": "production",
  "databaseStatus": "healthy",
  "uptime": "10d 5h 23m",
  "activeVirtualKeys": 125,
  "activeProviders": 5,
  "totalRequests24h": 150000
}
```

### Create Database Backup

```
POST /api/database/backup
```

Creates a database backup.

**Response:**
```json
{
  "backupId": "backup_20250107_103000",
  "fileName": "conduit_backup_20250107_103000.sql",
  "sizeBytes": 52428800,
  "createdAt": "2025-01-07T10:30:00Z"
}
```

---

## IP Filtering

### List IP Filters

```
GET /api/ipfilter
```

Returns all configured IP filters.

**Response:**
```json
[
  {
    "id": 1,
    "name": "Office Network",
    "cidrRange": "203.0.113.0/24",
    "filterType": "Allow",
    "isEnabled": true,
    "description": "Main office IP range",
    "createdAt": "2025-01-01T00:00:00Z"
  }
]
```

### Create IP Filter

```
POST /api/ipfilter
```

Creates a new IP filter rule.

**Request Body:**
```json
{
  "name": "Office Network",
  "cidrRange": "203.0.113.0/24",
  "filterType": "Allow",
  "isEnabled": true,
  "description": "Main office IP range"
}
```

### Update IP Filter

```
PUT /api/ipfilter/{id}
```

Updates an existing IP filter.

### Delete IP Filter

```
DELETE /api/ipfilter/{id}
```

Deletes an IP filter rule.

### Check IP Address

```
GET /api/ipfilter/check/{ipAddress}
```

Checks if an IP address is allowed (anonymous access for performance).

**Response:**
```json
{
  "ipAddress": "203.0.113.50",
  "isAllowed": true,
  "matchedRule": "Office Network"
}
```

---

## Error Responses

All endpoints may return the following error responses:

| HTTP Status | Error Type | Description |
|-------------|------------|-------------|
| 400 | Bad Request | Invalid request format or parameters |
| 401 | Unauthorized | Missing or invalid authentication |
| 403 | Forbidden | Insufficient permissions |
| 404 | Not Found | Resource not found |
| 409 | Conflict | Resource conflict (duplicate, etc.) |
| 429 | Too Many Requests | Rate limit exceeded |
| 500 | Internal Server Error | Server-side error |

**Error Response Format:**
```json
{
  "error": {
    "code": "error_code",
    "message": "Error message description",
    "details": {
      "field": "Additional error details"
    }
  }
}
```

---

## Related Documentation

- **[Getting Started](./getting-started.md)** - Authentication and setup
- **[TypeScript SDK](./typescript-sdk.md)** - Complete SDK guide with examples
- **[Gateway API](../core/)** - User-facing LLM API documentation
- **[Architecture Docs](../../architecture/)** - System design and patterns
