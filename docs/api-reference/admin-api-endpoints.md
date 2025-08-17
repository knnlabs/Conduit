# ConduitLLM Admin API Endpoints

This document describes the current endpoints for the ConduitLLM Admin API. The Admin API provides a centralized interface for administrative operations, replacing direct database access from the WebUI.

## Authentication

All Admin API endpoints require authentication using the master key. The authentication scheme uses the `X-API-Key` header with the `MasterKeyPolicy` authorization policy.

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
    "virtualKeyGroupId": 1,
    "isEnabled": true,
    "expiresAt": "2025-05-01T00:00:00Z",
    "createdAt": "2024-04-15T14:30:00Z",
    "updatedAt": "2024-05-08T09:15:00Z",
    "metadata": "Project: Research",
    "rateLimitRpm": 60,
    "rateLimitRpd": 1000,
    "description": "Key for research project"
  }
]
```

### Get Virtual Key by ID
**GET** `/api/virtualkeys/{id}`

**Response:** VirtualKeyDto object

### Create Virtual Key
**POST** `/api/virtualkeys`

**Request Body:** CreateVirtualKeyRequestDto
**Response:** CreateVirtualKeyResponseDto (201 Created)

### Update Virtual Key
**PUT** `/api/virtualkeys/{id}`

**Request Body:** UpdateVirtualKeyRequestDto
**Response:** 204 No Content

### Delete Virtual Key
**DELETE** `/api/virtualkeys/{id}`

**Response:** 204 No Content

### Validate Virtual Key
**POST** `/api/virtualkeys/validate`

**Request Body:** VirtualKeyValidationRequest
**Response:** VirtualKeyValidationResult

### Get Validation Info
**GET** `/api/virtualkeys/{id}/validation-info`

**Response:** VirtualKeyValidationInfo

### Perform Maintenance
**POST** `/api/virtualkeys/maintenance`

**Response:** 204 No Content

Performs maintenance tasks including disabling expired keys.

### Preview Discovery
**GET** `/api/virtualkeys/{id}/discovery-preview`

**Query Parameters:**
- `capability`: Optional capability filter (e.g., "chat", "vision", "audio_transcription")

**Response:** VirtualKeyDiscoveryPreviewDto

### Get Key Group
**GET** `/api/virtualkeys/{id}/group`

**Response:** VirtualKeyGroupDto

## Virtual Key Groups Management

### Get All Groups
**GET** `/api/virtualkeygroups`

**Response:** Array of VirtualKeyGroupDto objects
```json
[
  {
    "id": 1,
    "externalGroupId": "ext-group-123",
    "groupName": "Research Team",
    "balance": 500.00,
    "lifetimeCreditsAdded": 1000.00,
    "lifetimeSpent": 500.00,
    "createdAt": "2024-01-01T00:00:00Z",
    "updatedAt": "2024-05-01T00:00:00Z",
    "virtualKeyCount": 5
  }
]
```

### Get Group by ID
**GET** `/api/virtualkeygroups/{id}`

**Response:** VirtualKeyGroupDto object

### Create Group
**POST** `/api/virtualkeygroups`

**Request Body:** CreateVirtualKeyGroupDto
**Response:** VirtualKeyGroupDto (201 Created)

### Update Group
**PUT** `/api/virtualkeygroups/{id}`

**Request Body:** UpdateVirtualKeyGroupDto
**Response:** 204 No Content

### Adjust Balance
**POST** `/api/virtualkeygroups/{id}/adjust-balance`

**Request Body:** AdjustBalanceDto
**Response:** 204 No Content

### Delete Group
**DELETE** `/api/virtualkeygroups/{id}`

**Response:** 204 No Content

### Get Transaction History
**GET** `/api/virtualkeygroups/{id}/transactions`

**Query Parameters:**
- `page`: Page number (default: 1)
- `pageSize`: Page size (default: 50, max: 100)

**Response:** PagedResult<VirtualKeyGroupTransactionDto>

### Get Keys in Group
**GET** `/api/virtualkeygroups/{id}/keys`

**Response:** Array of VirtualKeyDto objects

## Model Provider Mappings

### Get All Mappings
**GET** `/api/modelprovider`

**Response:** Array of ModelProviderMappingDto objects
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

**Response:** ModelProviderMappingDto object

### Create Mapping
**POST** `/api/modelprovider`

**Request Body:** ModelProviderMappingDto object
**Response:** 201 Created with the created mapping

### Update Mapping
**PUT** `/api/modelprovider/{id}`

**Request Body:** ModelProviderMappingDto object
**Response:** 204 No Content

### Delete Mapping
**DELETE** `/api/modelprovider/{id}`

**Response:** 204 No Content

### Get Providers
**GET** `/api/modelprovider/providers`

**Response:** List of provider names and IDs

### Create Bulk Mappings
**POST** `/api/modelprovider/bulk`

**Request Body:** Array of ModelProviderMappingDto objects
**Response:** BulkMappingResult object
```json
{
  "created": [...],
  "errors": [...],
  "totalProcessed": 10,
  "successCount": 8,
  "failureCount": 2
}
```

### Discover Models
**GET** `/api/modelprovider/discover/{providerId}`

**Response:** Array of DiscoveredModel objects

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

**Response:** Array of ModelDeployment objects

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

**Response:** Dictionary<string, List<string>> mapping primary models to their fallback models

### Set Fallback Configuration
**POST** `/api/router/fallbacks/{primaryModel}`

**Request Body:** Array of fallback model strings
**Response:** 200 OK

### Remove Fallback Configuration
**DELETE** `/api/router/fallbacks/{primaryModel}`

**Response:** 200 OK

## Global Settings Management

### Get All Settings
**GET** `/api/globalsettings`

**Response:** Array of GlobalSettingDto objects

### Get Setting by ID
**GET** `/api/globalsettings/{id}`

**Response:** GlobalSettingDto object

### Get Setting by Key
**GET** `/api/globalsettings/by-key/{key}`

**Response:** GlobalSettingDto object

### Create Setting
**POST** `/api/globalsettings`

**Request Body:** CreateGlobalSettingDto object
**Response:** GlobalSettingDto (201 Created)

### Update Setting
**PUT** `/api/globalsettings/{id}`

**Request Body:** UpdateGlobalSettingDto object
**Response:** 204 No Content

### Update Setting by Key
**PUT** `/api/globalsettings/by-key`

**Request Body:** UpdateGlobalSettingByKeyDto object
**Response:** 204 No Content

### Delete Setting
**DELETE** `/api/globalsettings/{id}`

**Response:** 204 No Content

### Delete Setting by Key
**DELETE** `/api/globalsettings/by-key/{key}`

**Response:** 204 No Content

## Audio Configuration Management

### Provider Configuration

#### Get All Audio Providers
**GET** `/api/admin/audio/providers`

**Response:** Array of AudioProviderConfigDto objects

#### Get Audio Provider by ID
**GET** `/api/admin/audio/providers/{id}`

**Response:** AudioProviderConfigDto object

#### Get Providers by Provider ID
**GET** `/api/admin/audio/providers/by-id/{providerId}`

**Response:** Array of AudioProviderConfigDto objects

#### Get Enabled Providers
**GET** `/api/admin/audio/providers/enabled/{operationType}`

**Parameters:**
- `operationType`: The operation type (transcription, tts, realtime)

**Response:** Array of AudioProviderConfigDto objects

#### Create Audio Provider
**POST** `/api/admin/audio/providers`

**Request Body:** AudioProviderConfigDto object
**Response:** AudioProviderConfigDto (201 Created)

#### Update Audio Provider
**PUT** `/api/admin/audio/providers/{id}`

**Request Body:** AudioProviderConfigDto object
**Response:** AudioProviderConfigDto (200 OK)

#### Delete Audio Provider
**DELETE** `/api/admin/audio/providers/{id}`

**Response:** 204 No Content

#### Test Audio Provider
**POST** `/api/admin/audio/providers/{id}/test`

**Query Parameters:**
- `operationType`: The operation type to test

**Response:** Test results object

### Audio Cost Configuration

#### Get All Audio Costs
**GET** `/api/admin/audio/costs`

**Response:** Array of AudioCostConfigDto objects

#### Get Audio Cost by ID
**GET** `/api/admin/audio/costs/{id}`

**Response:** AudioCostConfigDto object

#### Get Costs by Provider
**GET** `/api/admin/audio/costs/by-provider/{providerId}`

**Response:** Array of AudioCostConfigDto objects

#### Create Audio Cost
**POST** `/api/admin/audio/costs`

**Request Body:** AudioCostConfigDto object
**Response:** AudioCostConfigDto (201 Created)

#### Update Audio Cost
**PUT** `/api/admin/audio/costs/{id}`

**Request Body:** AudioCostConfigDto object
**Response:** AudioCostConfigDto (200 OK)

#### Delete Audio Cost
**DELETE** `/api/admin/audio/costs/{id}`

**Response:** 204 No Content

### Audio Usage Analytics

#### Get Usage Summary
**GET** `/api/admin/audio/usage/summary`

**Query Parameters:**
- `startDate`: Start date (optional)
- `endDate`: End date (optional)

**Response:** AudioUsageSummaryDto object

#### Get Usage by Key
**GET** `/api/admin/audio/usage/by-key/{virtualKey}`

**Query Parameters:**
- `startDate`: Start date (optional)
- `endDate`: End date (optional)

**Response:** AudioKeyUsageDto object

#### Get Usage by Provider
**GET** `/api/admin/audio/usage/by-provider/{providerId}`

**Query Parameters:**
- `startDate`: Start date (optional)
- `endDate`: End date (optional)

**Response:** AudioProviderUsageDto object

### Real-time Session Management

#### Get Session Metrics
**GET** `/api/admin/audio/sessions/metrics`

**Response:** RealtimeSessionMetricsDto object

#### Get Active Sessions
**GET** `/api/admin/audio/sessions`

**Response:** Array of RealtimeSessionDto objects

#### Get Session Details
**GET** `/api/admin/audio/sessions/{sessionId}`

**Response:** RealtimeSessionDto object

#### Terminate Session
**DELETE** `/api/admin/audio/sessions/{sessionId}`

**Response:** 204 No Content

## Media Management

### Storage Statistics

#### Get Overall Stats
**GET** `/api/admin/media/stats`

**Response:** Overall storage statistics object

#### Get Stats by Virtual Key
**GET** `/api/admin/media/stats/virtual-key/{virtualKeyId}`

**Response:** Storage statistics for virtual key

#### Get Stats by Provider
**GET** `/api/admin/media/stats/by-provider`

**Response:** Dictionary of provider names to storage size

#### Get Stats by Media Type
**GET** `/api/admin/media/stats/by-type`

**Response:** Dictionary of media types to storage size

### Media Operations

#### Get Media by Virtual Key
**GET** `/api/admin/media/virtual-key/{virtualKeyId}`

**Response:** Array of media records

#### Search Media
**GET** `/api/admin/media/search`

**Query Parameters:**
- `pattern`: Pattern to search for in storage keys

**Response:** Array of matching media records

#### Delete Media
**DELETE** `/api/admin/media/{mediaId}`

**Response:** Success status object

### Media Cleanup

#### Cleanup Expired Media
**POST** `/api/admin/media/cleanup/expired`

**Response:** Cleanup result with count

#### Cleanup Orphaned Media
**POST** `/api/admin/media/cleanup/orphaned`

**Response:** Cleanup result with count

#### Prune Old Media
**POST** `/api/admin/media/cleanup/prune`

**Request Body:** PruneMediaRequest object
```json
{
  "daysToKeep": 30
}
```

**Response:** Prune result with count

## Health Monitoring

### Get Service Health
**GET** `/api/health/services`

**Response:** Array of service health objects
```json
[
  {
    "id": "core-api",
    "name": "Core API",
    "status": "healthy",
    "uptime": "P1DT2H30M",
    "lastCheck": "2024-05-08T10:30:00Z",
    "responseTime": 15,
    "details": {
      "version": "1.0.0",
      "environment": "Production",
      "requestsHandled": 1250
    }
  }
]
```

### Get Incidents
**GET** `/api/health/incidents`

**Query Parameters:**
- `days`: Number of days to look back (default: 7)

**Response:** Incident history data

### Get Health History
**GET** `/api/health/history`

**Query Parameters:**
- `hours`: Number of hours to look back (default: 24)

**Response:** Health history time series data

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
**POST** `/api/databasebackup`

**Response:** Backup file information object

### Get Backups
**GET** `/api/databasebackup`

**Response:** Array of available backup objects

### Restore Backup
**POST** `/api/databasebackup/restore`

**Request Body:** Backup identifier object
**Response:** 200 OK

## System Information

### Get System Info
**GET** `/api/systeminfo`

**Response:** System information object with environment, database, and runtime details

## Analytics

### Get Request Logs
**GET** `/api/analytics/logs`

**Query Parameters:**
- `page` (int, default: 1): Page number (1-based)
- `pageSize` (int, default: 50, max: 100): Number of items per page
- `startDate` (DateTime, optional): Filter by start date
- `endDate` (DateTime, optional): Filter by end date
- `model` (string, optional): Filter by model name
- `virtualKeyId` (int, optional): Filter by virtual key ID
- `status` (int, optional): Filter by HTTP status code

**Response:** PagedResult<LogRequestDto>
```json
{
  "page": 1,
  "pageSize": 50,
  "totalItems": 150,
  "totalPages": 3,
  "items": [
    {
      "id": 1234,
      "virtualKeyId": 5,
      "modelName": "gpt-4",
      "requestType": "chat",
      "inputTokens": 500,
      "outputTokens": 250,
      "cost": 0.0125,
      "responseTimeMs": 1250.5,
      "statusCode": 200,
      "timestamp": "2024-05-15T14:30:00Z"
    }
  ]
}
```

### Get Log by ID
**GET** `/api/analytics/logs/{id}`

**Response:** LogRequestDto object

### Get Distinct Models
**GET** `/api/analytics/models`

**Response:** Array of model names
```json
["gpt-4", "gpt-3.5-turbo", "claude-3-opus", "llama-3"]
```

### Get Cost Summary
**GET** `/api/analytics/costs/summary`

**Query Parameters:**
- `timeframe` (string, default: "daily"): One of "daily", "weekly", "monthly"
- `startDate` (DateTime, optional): Start date for analysis
- `endDate` (DateTime, optional): End date for analysis

**Response:** CostDashboardDto
```json
{
  "timeFrame": "daily",
  "startDate": "2024-05-01T00:00:00Z",
  "endDate": "2024-05-15T23:59:59Z",
  "totalCost": 125.50,
  "last24HoursCost": 8.75,
  "last7DaysCost": 45.20,
  "last30DaysCost": 125.50,
  "topModelsBySpend": [
    {
      "name": "gpt-4",
      "cost": 75.25,
      "percentage": 60,
      "requestCount": 500
    }
  ],
  "topProvidersBySpend": [...],
  "topVirtualKeysBySpend": [...]
}
```

### Get Cost Trends
**GET** `/api/analytics/costs/trends`

**Query Parameters:**
- `period` (string, default: "daily"): One of "daily", "weekly", "monthly"
- `startDate` (DateTime, optional): Start date
- `endDate` (DateTime, optional): End date

**Response:** CostTrendDto
```json
{
  "period": "daily",
  "startDate": "2024-05-01T00:00:00Z",
  "endDate": "2024-05-15T23:59:59Z",
  "data": [
    {
      "date": "2024-05-01T00:00:00Z",
      "cost": 8.50,
      "requestCount": 125
    }
  ]
}
```

### Get Model Costs
**GET** `/api/analytics/costs/models`

**Query Parameters:**
- `startDate` (DateTime, optional): Start date
- `endDate` (DateTime, optional): End date
- `topN` (int, default: 10): Number of top models to return

**Response:** ModelCostBreakdownDto

### Get Virtual Key Costs
**GET** `/api/analytics/costs/virtualkeys`

**Query Parameters:**
- `startDate` (DateTime, optional): Start date
- `endDate` (DateTime, optional): End date
- `topN` (int, default: 10): Number of top virtual keys to return

**Response:** VirtualKeyCostBreakdownDto

### Get Analytics Summary
**GET** `/api/analytics/summary`

**Query Parameters:**
- `timeframe` (string, default: "daily"): One of "daily", "weekly", "monthly"
- `startDate` (DateTime, optional): Start date
- `endDate` (DateTime, optional): End date

**Response:** AnalyticsSummaryDto with comprehensive analytics data including:
- Total requests, cost, tokens
- Success rate and response times
- Top models and virtual keys
- Daily statistics
- Period-over-period comparison

### Get Virtual Key Usage
**GET** `/api/analytics/virtualkeys/{virtualKeyId}/usage`

**Query Parameters:**
- `startDate` (DateTime, optional): Start date
- `endDate` (DateTime, optional): End date

**Response:** UsageStatisticsDto

### Export Analytics Data
**GET** `/api/analytics/export`

**Query Parameters:**
- `format` (string, default: "csv"): Export format ("csv" or "json")
- `startDate` (DateTime, optional): Start date
- `endDate` (DateTime, optional): End date
- `model` (string, optional): Filter by model
- `virtualKeyId` (int, optional): Filter by virtual key

**Response:** File download (CSV or JSON)

### Get Cache Metrics
**GET** `/api/analytics/metrics/cache`

**Response:** Cache performance metrics
```json
{
  "TotalHits": 5000,
  "TotalMisses": 1200,
  "HitRate": 80.65,
  "CacheMemoryMB": 45.2,
  "TotalInvalidations": 15,
  "UptimeMinutes": 1440,
  "TopHitKeys": [...],
  "TopMissKeys": [...]
}
```

### Get Operation Metrics
**GET** `/api/analytics/metrics/operations`

**Response:** Operation performance metrics
```json
{
  "GetLogsAsync_avg_ms": 125.5,
  "GetLogsAsync_p95_ms": 250.0,
  "GetLogsAsync_max_ms": 500.0,
  "fetch_RequestLogRepository.GetAllAsync_avg_ms": 75.2,
  "fetch_RequestLogRepository.GetAllAsync_p95_ms": 150.0
}
```

### Invalidate Analytics Cache
**POST** `/api/analytics/cache/invalidate`

**Query Parameters:**
- `reason` (string, optional): Reason for invalidation

**Response:** Success message
```json
{
  "message": "Cache invalidation initiated",
  "reason": "Manual invalidation"
}
```

## Additional Controllers

The following controllers provide additional administrative functionality:

- **CacheMonitoringController** (`/api/cachemonitoring`): Cache performance monitoring
- **ConfigurationController** (`/api/configuration`): System configuration management  
- **ErrorQueueController** (`/api/errorqueue`): Error queue management
- **MetricsController** (`/api/metrics`): System metrics and analytics
- **NotificationsController** (`/api/notifications`): Admin notification management
- **ProviderCredentialsController** (`/api/providercredentials`): Provider credential management
- **SecurityMonitoringController** (`/api/securitymonitoring`): Security event monitoring
- **TasksController** (`/api/tasks`): Background task management

## Notes

- All endpoints require the `MasterKeyPolicy` authorization policy unless otherwise specified
- Response formats use standard HTTP status codes (200, 201, 204, 400, 404, 500)
- Date/time values are in ISO 8601 format (UTC)
- Pagination is supported on list endpoints where applicable
- The API uses standard REST conventions for CRUD operations