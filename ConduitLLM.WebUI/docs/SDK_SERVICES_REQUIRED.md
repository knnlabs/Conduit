# SDK Services Required for WebUI

This document outlines all the services and methods that need to be added to the Conduit Admin SDK (`@knn_labs/conduit-admin-client`) to fully support the WebUI functionality.

**Generated**: January 11, 2025

## Current SDK Status

The SDK currently only exports two services:
- ✅ `virtualKeys` - Virtual key management
- ✅ `dashboard` - Dashboard metrics

## Required Services

### 1. Providers Service (`adminClient.providers`)

**Priority**: 🔴 Critical - Core functionality

```typescript
interface ProvidersService {
  // CRUD operations
  list(): Promise<ProviderDto[]>;
  getById(id: number): Promise<ProviderDto>;
  create(data: CreateProviderDto): Promise<ProviderDto>;
  update(id: number, data: UpdateProviderDto): Promise<ProviderDto>;
  deleteById(id: number): Promise<void>;
  
  // Testing
  testConnectionById(id: number): Promise<TestConnectionResult>;
  testConfig(config: ProviderConfig): Promise<TestConnectionResult>;
  
  // Health
  getHealthStatus(params?: HealthStatusParams): Promise<ProviderHealthStatus>;
  exportHealthData(params: ExportParams): Promise<ExportResult>;
}
```

**Used in API routes**:
- `/api/providers/*` - All provider management endpoints
- `/api/provider-health/*` - Provider health monitoring
- `/api/health/providers/*` - Health check endpoints

### 2. Provider Models Service (`adminClient.providerModels`)

**Priority**: 🔴 Critical - Model selection depends on this

```typescript
interface ProviderModelsService {
  getProviderModels(providerName: string): Promise<ModelDto[]>;
  refreshProviderModels(providerName: string): Promise<ModelDto[]>;
  getModelCapabilities(model: string): Promise<ModelCapabilities>;
}
```

**Used in API routes**:
- `/api/providers/[id]/models` - List models for a provider

### 3. Model Mappings Service (`adminClient.modelMappings`)

**Priority**: 🔴 Critical - Core routing functionality

```typescript
interface ModelMappingsService {
  // CRUD operations
  list(): Promise<ModelMappingDto[]>;
  getById(id: number): Promise<ModelMappingDto>;
  create(data: CreateModelMappingDto): Promise<ModelMappingDto>;
  update(id: number, data: UpdateModelMappingDto): Promise<ModelMappingDto>;
  deleteById(id: number): Promise<void>;
  
  // Discovery and testing
  discoverModels(): Promise<DiscoveredModel[]>;
  testCapability(id: number, capability: string, params: any): Promise<TestResult>;
}
```

**Used in API routes**:
- `/api/model-mappings/*` - All model mapping endpoints

### 4. System Service (`adminClient.system`)

**Priority**: 🔴 Critical - Authentication and system info

```typescript
interface SystemService {
  // System information
  getSystemInfo(): Promise<SystemInfoDto>;
  getHealth(): Promise<SystemHealthDto>;
  
  // WebUI specific
  getWebUIVirtualKey(): Promise<string>;
  
  // Performance
  getPerformanceMetrics(params?: MetricsParams): Promise<PerformanceMetrics>;
  exportPerformanceData(params: ExportParams): Promise<ExportResult>;
}
```

**Used in API routes**:
- `/api/auth/login` - Get WebUI virtual key for authentication
- `/api/auth/validate` - Validate sessions
- `/api/admin/system/*` - System information endpoints
- `/api/settings/system-info` - System configuration

### 5. Settings Service (`adminClient.settings`)

**Priority**: 🟡 Important - Configuration management

```typescript
interface SettingsService {
  getGlobalSettings(): Promise<SettingsDto>;
  getGlobalSetting(key: string): Promise<SettingDto>;
  updateGlobalSetting(key: string, data: UpdateSettingDto): Promise<void>;
  batchUpdateSettings(settings: SettingUpdate[]): Promise<void>;
}
```

**Used in API routes**:
- `/api/settings/*` - All settings management endpoints

### 6. Analytics Service (`adminClient.analytics`)

**Priority**: 🟡 Important - Usage tracking and insights

```typescript
interface AnalyticsService {
  // Request logs
  getRequestLogs(params?: RequestLogParams): Promise<RequestLog[]>;
  exportRequestLogs(params: ExportParams): Promise<ExportResult>;
  
  // Usage analytics
  getUsageAnalytics(params?: UsageParams): Promise<UsageAnalytics>;
  exportUsageAnalytics(params: ExportParams): Promise<ExportResult>;
  
  // Virtual key analytics
  getVirtualKeyAnalytics(params?: VirtualKeyParams): Promise<VirtualKeyAnalytics>;
  exportVirtualKeyAnalytics(params: ExportParams): Promise<ExportResult>;
}
```

**Used in API routes**:
- `/api/request-logs/*` - Request logging endpoints
- `/api/usage-analytics/*` - Usage analytics endpoints
- `/api/virtual-keys-analytics/*` - Virtual key analytics

### 7. Provider Health Service (`adminClient.providerHealth`)

**Priority**: 🟡 Important - Monitoring

```typescript
interface ProviderHealthService {
  getHealthSummary(): Promise<HealthSummaryDto>;
  getProviderHealth(providerId: string): Promise<ProviderHealthDto>;
  getHealthHistory(providerId: string, params?: HistoryParams): Promise<HealthHistory>;
}
```

**Used in API routes**:
- `/api/health/providers/[id]` - Individual provider health

## Missing Services (Not Critical)

These services are referenced in the code but not actively used:

### 8. Security Service (`adminClient.security`)
- IP filtering
- Security events
- Threat detection

### 9. Configuration Service (`adminClient.configuration`)
- Routing configuration
- Caching settings
- Advanced configurations

### 10. Monitoring Service (`adminClient.monitoring`)
- Real-time monitoring
- Alerts and notifications
- Performance tracking

## Implementation Priority

### Phase 1: Critical Services (Required for basic functionality)
1. **Providers Service** - Core provider management
2. **Provider Models Service** - Model discovery
3. **Model Mappings Service** - Routing configuration
4. **System Service** - Authentication and health

### Phase 2: Important Services (Enhanced functionality)
5. **Settings Service** - Configuration management
6. **Analytics Service** - Usage tracking
7. **Provider Health Service** - Health monitoring

### Phase 3: Nice-to-Have Services (Future enhancements)
8. **Security Service** - Advanced security features
9. **Configuration Service** - Advanced configuration
10. **Monitoring Service** - Real-time monitoring

## Type Definitions Needed

The SDK also needs to export proper TypeScript types for all DTOs:

```typescript
// Provider types
export interface ProviderDto {
  id: number;
  providerName: string;
  displayName: string;
  isEnabled: boolean;
  apiKeyConfigured: boolean;
  baseUrl?: string;
  supportedModels: string[];
  createdAt: string;
  updatedAt: string;
}

export interface CreateProviderDto {
  providerName: string;
  displayName: string;
  apiKey: string;
  baseUrl?: string;
  isEnabled?: boolean;
}

export interface UpdateProviderDto {
  displayName?: string;
  apiKey?: string;
  baseUrl?: string;
  isEnabled?: boolean;
}

// Model Mapping types
export interface ModelMappingDto {
  id: number;
  modelMappingName: string;
  sourceModel: string;
  targetProvider: string;
  targetModel: string;
  isEnabled: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CreateModelMappingDto {
  modelMappingName: string;
  sourceModel: string;
  targetProvider: string;
  targetModel: string;
  isEnabled?: boolean;
}

// System types
export interface SystemInfoDto {
  version: string;
  environment: string;
  uptime: number;
  services: Record<string, ServiceStatus>;
}

export interface SystemHealthDto {
  status: 'healthy' | 'degraded' | 'unhealthy';
  services: Record<string, ServiceHealth>;
  timestamp: string;
}

// Analytics types
export interface RequestLog {
  id: string;
  timestamp: string;
  virtualKeyId: string;
  provider: string;
  model: string;
  statusCode: number;
  latency: number;
  tokenCount?: number;
  cost?: number;
}

export interface UsageAnalytics {
  totalRequests: number;
  totalTokens: number;
  totalCost: number;
  byProvider: Record<string, ProviderUsage>;
  byVirtualKey: Record<string, VirtualKeyUsage>;
  timeRange: TimeRange;
}
```

## Migration Path

1. **Immediate**: Use the type augmentation file (`sdk-augmentation.d.ts`) to make TypeScript happy
2. **Short-term**: Implement Phase 1 services in the SDK
3. **Medium-term**: Implement Phase 2 services
4. **Long-term**: Add Phase 3 services and remove type augmentation

## Notes

- The WebUI was built expecting a fully-featured SDK, but the SDK only provides basic functionality
- Many API routes are using mock data or hardcoded responses due to missing SDK methods
- The type augmentation file is a temporary workaround and should be removed once the SDK is complete
- Some routes have TODO comments indicating where SDK methods should be used
- The SDK should follow the same patterns as the existing `virtualKeys` service for consistency