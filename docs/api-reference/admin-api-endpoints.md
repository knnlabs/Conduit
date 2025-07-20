# ConduitLLM Admin API Endpoints

This document describes the proposed endpoints for the ConduitLLM Admin API. The Admin API provides a centralized interface for administrative operations, replacing direct database access from the WebUI.

## Authentication

All Admin API endpoints require authentication using the master key. The authentication scheme uses the `X-API-Key` header.

**Example:**
```
X-API-Key: your-master-key
```

## Virtual Keys Management

### Get All Virtual Keys
**GET** `/api/virtualkeys`

**Response:** Array of VirtualKeyDto objects
```json
[
  {
    "id": 1,
    "keyName": "Test Key",
    "allowedModels": "gpt-4*,claude-*",
    "maxBudget": 100.00,
    "currentSpend": 25.50,
    "budgetDuration": "Monthly",
    "budgetStartDate": "2024-05-01T00:00:00Z",
    "isEnabled": true,
    "expiresAt": "2025-05-01T00:00:00Z",
    "createdAt": "2024-04-15T14:30:00Z",
    "updatedAt": "2024-05-08T09:15:00Z",
    "metadata": "Project: Research",
    "rateLimitRpm": 60,
    "rateLimitRpd": 1000
  }
]
```

### Get Virtual Key by ID
**GET** `/api/virtualkeys/{id}`

**Response:** VirtualKeyDto object
```json
{
  "id": 1,
  "keyName": "Test Key",
  "allowedModels": "gpt-4*,claude-*",
  "maxBudget": 100.00,
  "currentSpend": 25.50,
  "budgetDuration": "Monthly",
  "budgetStartDate": "2024-05-01T00:00:00Z",
  "isEnabled": true,
  "expiresAt": "2025-05-01T00:00:00Z",
  "createdAt": "2024-04-15T14:30:00Z",
  "updatedAt": "2024-05-08T09:15:00Z",
  "metadata": "Project: Research",
  "rateLimitRpm": 60,
  "rateLimitRpd": 1000
}
```

### Create Virtual Key
**POST** `/api/virtualkeys`

**Request Body:** CreateVirtualKeyRequestDto
```json
{
  "keyName": "New Test Key",
  "allowedModels": "gpt-4*,claude-*",
  "maxBudget": 50.00,
  "budgetDuration": "Monthly",
  "expiresAt": "2025-05-01T00:00:00Z",
  "metadata": "Project: Development",
  "rateLimitRpm": 30,
  "rateLimitRpd": 500
}
```

**Response:** CreateVirtualKeyResponseDto
```json
{
  "virtualKey": "vk-abc123def456...",
  "keyInfo": {
    "id": 2,
    "keyName": "New Test Key",
    "allowedModels": "gpt-4*,claude-*",
    "maxBudget": 50.00,
    "currentSpend": 0.00,
    "budgetDuration": "Monthly",
    "budgetStartDate": "2024-05-01T00:00:00Z",
    "isEnabled": true,
    "expiresAt": "2025-05-01T00:00:00Z",
    "createdAt": "2024-05-10T10:30:00Z",
    "updatedAt": "2024-05-10T10:30:00Z",
    "metadata": "Project: Development",
    "rateLimitRpm": 30,
    "rateLimitRpd": 500
  }
}
```

### Update Virtual Key
**PUT** `/api/virtualkeys/{id}`

**Request Body:** UpdateVirtualKeyRequestDto
```json
{
  "keyName": "Updated Test Key",
  "allowedModels": "gpt-4*,claude-*,llama-*",
  "maxBudget": 75.00,
  "isEnabled": true,
  "metadata": "Project: Updated Project",
  "rateLimitRpm": 45
}
```

**Response:** 204 No Content

### Delete Virtual Key
**DELETE** `/api/virtualkeys/{id}`

**Response:** 204 No Content

### Reset Virtual Key Spend
**POST** `/api/virtualkeys/{id}/reset-spend`

**Response:** 204 No Content

## Model Provider Mappings

### Get All Mappings
**GET** `/api/modelprovider`

**Response:** Array of ModelProviderMapping objects
```json
[
  {
    "id": 1,
    "modelAlias": "gpt-4",
    "providerName": "OpenAI",
    "providerModel": "gpt-4-turbo-preview",
    "priority": 1,
    "isEnabled": true
  }
]
```

### Get Mapping by ID
**GET** `/api/modelprovider/{id}`

**Response:** ModelProviderMapping object

### Get Mapping by Alias
**GET** `/api/modelprovider/by-alias/{modelAlias}`

**Response:** ModelProviderMapping object

### Create Mapping
**POST** `/api/modelprovider`

**Request Body:** ModelProviderMapping object

**Response:** 201 Created with the created mapping

### Update Mapping
**PUT** `/api/modelprovider/{id}`

**Request Body:** ModelProviderMapping object

**Response:** 204 No Content

### Delete Mapping
**DELETE** `/api/modelprovider/{id}`

**Response:** 204 No Content

### Get Providers
**GET** `/api/modelprovider/providers`

**Response:** List of provider names and IDs

## Router Configuration

### Get Router Config
**GET** `/api/router/config`

**Response:** RouterConfig object

### Update Router Config
**PUT** `/api/router/config`

**Request Body:** RouterConfig object

**Response:** 200 OK

### Get Model Deployments
**GET** `/api/router/deployments`

**Response:** List of ModelDeployment objects

### Get Model Deployment
**GET** `/api/router/deployments/{deploymentName}`

**Response:** ModelDeployment object

### Create or Update Model Deployment
**POST** `/api/router/deployments`

**Request Body:** ModelDeployment object

**Response:** 200 OK

### Delete Model Deployment
**DELETE** `/api/router/deployments/{deploymentName}`

**Response:** 200 OK

### Get Fallback Configurations
**GET** `/api/router/fallbacks`

**Response:** Dictionary mapping primary models to their fallback models

### Set Fallback Configuration
**POST** `/api/router/fallbacks/{primaryModel}`

**Request Body:** List of fallback model strings

**Response:** 200 OK

### Remove Fallback Configuration
**DELETE** `/api/router/fallbacks/{primaryModel}`

**Response:** 200 OK

## IP Filtering

### Get All Filters
**GET** `/api/ipfilter`

**Response:** List of IpFilterDto objects

### Get Enabled Filters
**GET** `/api/ipfilter/enabled`

**Response:** List of enabled IpFilterDto objects

### Get Filter by ID
**GET** `/api/ipfilter/{id}`

**Response:** IpFilterDto object

### Create Filter
**POST** `/api/ipfilter`

**Request Body:** CreateIpFilterDto object

**Response:** 201 Created with the created filter

### Update Filter
**PUT** `/api/ipfilter/{id}`

**Request Body:** UpdateIpFilterDto object

**Response:** 204 No Content

### Delete Filter
**DELETE** `/api/ipfilter/{id}`

**Response:** 204 No Content

### Get IP Filter Settings
**GET** `/api/ipfilter/settings`

**Response:** IpFilterSettings object

### Update IP Filter Settings
**PUT** `/api/ipfilter/settings`

**Request Body:** IpFilterSettings object

**Response:** 204 No Content

## Logs Management

### Get Logs
**GET** `/api/logs`

**Query Parameters:** 
- `page`: Page number (default: 1)
- `pageSize`: Page size (default: 50)
- `startDate`: Filter by start date
- `endDate`: Filter by end date
- `model`: Filter by model
- `virtualKeyId`: Filter by virtual key ID
- `status`: Filter by status code

**Response:** Paged result of RequestLogDto objects

### Get Log by ID
**GET** `/api/logs/{id}`

**Response:** Detailed RequestLogDto object

### Get Logs Summary
**GET** `/api/logs/summary`

**Query Parameters:** 
- `timeframe`: Summary timeframe (daily, weekly, monthly)
- `startDate`: Start date
- `endDate`: End date

**Response:** LogsSummaryDto object

## Cost Dashboard

### Get Cost Summary
**GET** `/api/costs/summary`

**Query Parameters:**
- `timeframe`: Summary timeframe (daily, weekly, monthly)
- `startDate`: Start date
- `endDate`: End date

**Response:** CostDashboardDto object

### Get Cost Trends
**GET** `/api/costs/trends`

**Query Parameters:**
- `period`: Trend period (daily, weekly, monthly)
- `startDate`: Start date
- `endDate`: End date

**Response:** CostTrendDto object

### Get Model Costs
**GET** `/api/costs/models`

**Query Parameters:**
- `startDate`: Start date
- `endDate`: End date

**Response:** List of ModelCostDataDto objects

### Get Virtual Key Costs
**GET** `/api/costs/virtualkeys`

**Query Parameters:**
- `startDate`: Start date
- `endDate`: End date

**Response:** List of VirtualKeyCostDataDto objects

## Database Backup

### Create Backup
**POST** `/api/database/backup`

**Response:** 200 OK with backup file information

### Get Backups
**GET** `/api/database/backups`

**Response:** List of available backups

### Restore Backup
**POST** `/api/database/restore`

**Request Body:** Backup identifier

**Response:** 200 OK

## System Information

### Get System Info
**GET** `/api/system/info`

**Response:** System information object with details about the environment, database, and runtime

### Get Health Status
**GET** `/api/health`

**Response:** Health status object with overall system health and component statuses

## Provider Health

### Get Provider Health Status
**GET** `/api/providerhealth`

**Response:** List of provider health records

### Get Provider Health Config
**GET** `/api/providerhealth/config`

**Response:** Provider health configuration settings

### Update Provider Health Config
**PUT** `/api/providerhealth/config`

**Request Body:** Updated provider health configuration settings

**Response:** 204 No Content

### Run Provider Health Check
**POST** `/api/providerhealth/check/{providerName}`

**Response:** 200 OK with health check results