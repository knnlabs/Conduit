# Admin API TypeScript Setup

This guide covers TypeScript setup, authentication, and type definitions for the Conduit Admin API.

## Overview

The Conduit Admin API provides programmatic access to all administrative functionality. This document covers the foundational setup needed for TypeScript development.

## Related Documentation

- [TypeScript Virtual Keys Guide](./typescript-virtual-keys.md) - Virtual key management examples
- [TypeScript Provider Management](./typescript-providers.md) - Provider configuration examples  
- [TypeScript Analytics Guide](./typescript-analytics.md) - Request logs and cost analytics
- [TypeScript Advanced Patterns](./typescript-advanced.md) - Error handling and production patterns

## Authentication

All Admin API requests require authentication using the master key. The API supports two header formats:

```typescript
const MASTER_KEY = 'your_master_key_here';
const ADMIN_API_URL = 'http://localhost:5002'; // Default Admin API URL

// Primary authentication header (recommended)
const headers = {
    'X-API-Key': MASTER_KEY,
    'Content-Type': 'application/json'
};

// Alternative authentication header (backward compatibility)
const alternativeHeaders = {
    'Authorization': `Bearer ${MASTER_KEY}`,
    'Content-Type': 'application/json'
};
```

## TypeScript Types

Comprehensive TypeScript interfaces for all Admin API operations:

```typescript
// Virtual Key Types
interface VirtualKeyDto {
    id: number;
    keyName: string;
    allowedModels: string;
    maxBudget: number;
    currentSpend: number;
    budgetDuration: 'Daily' | 'Weekly' | 'Monthly';
    budgetStartDate: string;
    isEnabled: boolean;
    expiresAt?: string;
    createdAt: string;
    updatedAt: string;
    metadata?: string;
    rateLimitRpm?: number;
    rateLimitRpd?: number;
}

interface CreateVirtualKeyRequest {
    keyName: string;
    allowedModels?: string;
    maxBudget?: number;
    budgetDuration?: 'Daily' | 'Weekly' | 'Monthly';
    expiresAt?: string;
    metadata?: string;
    rateLimitRpm?: number;
    rateLimitRpd?: number;
}

interface CreateVirtualKeyResponse {
    virtualKey: string;
    keyInfo: VirtualKeyDto;
}

interface UpdateVirtualKeyRequest {
    keyName?: string;
    allowedModels?: string;
    maxBudget?: number;
    isEnabled?: boolean;
    metadata?: string;
    rateLimitRpm?: number;
    rateLimitRpd?: number;
}

interface VirtualKeyValidationResult {
    isValid: boolean;
    virtualKeyId?: number;
    keyName?: string;
    reason?: string;
    allowedModels?: string[];
    maxBudget?: number;
    currentSpend?: number;
}

// Provider Types
interface ProviderCredentialDto {
    id: number;
    providerName: string;
    apiKey?: string;
    apiEndpoint?: string;
    organizationId?: string;
    additionalConfig?: string;
    isEnabled: boolean;
    createdAt: string;
    updatedAt: string;
}

interface CreateProviderCredentialDto {
    providerName: string;
    apiKey?: string;
    apiEndpoint?: string;
    organizationId?: string;
    additionalConfig?: string;
    isEnabled?: boolean;
}

interface UpdateProviderCredentialDto {
    apiKey?: string;
    apiEndpoint?: string;
    organizationId?: string;
    additionalConfig?: string;
    isEnabled?: boolean;
}

interface ProviderConnectionTestResultDto {
    success: boolean;
    message: string;
    errorDetails?: string;
    providerName: string;
    modelsAvailable?: string[];
}

interface ProviderHealthConfigurationDto {
    providerName: string;
    isEnabled: boolean;
    checkIntervalSeconds: number;
    timeoutSeconds: number;
    unhealthyThreshold: number;
    healthyThreshold: number;
    testModel?: string;
    lastCheckTime?: string;
    isHealthy?: boolean;
}

interface ProviderHealthRecordDto {
    id: number;
    providerName: string;
    checkTime: string;
    isHealthy: boolean;
    responseTimeMs?: number;
    errorMessage?: string;
    statusCode?: number;
}

// Model Mapping Types
interface ModelProviderMappingDto {
    id: number;
    modelId: string;
    providerId: string;
    providerModelId: string;
    isEnabled: boolean;
    priority: number;
    createdAt: string;
    updatedAt: string;
}

// IP Filter Types
interface IpFilterDto {
    id: number;
    name: string;
    cidrRange: string;
    filterType: 'Allow' | 'Deny';
    isEnabled: boolean;
    description?: string;
    createdAt: string;
    updatedAt: string;
}

interface IpFilterSettingsDto {
    isEnabled: boolean;
    defaultAllow: boolean;
    bypassForAdminUi: boolean;
    excludedEndpoints: string[];
    filterMode: 'permissive' | 'restrictive';
    whitelistFilters: IpFilterDto[];
    blacklistFilters: IpFilterDto[];
}

interface IpCheckResult {
    isAllowed: boolean;
    reason?: string;
    matchedFilter?: string;
}

// Model Cost Types
interface ModelCostDto {
    id: number;
    modelIdPattern: string;
    inputCostPerMillion: number;
    outputCostPerMillion: number;
    isActive: boolean;
    priority: number;
    effectiveFrom: string;
    effectiveTo?: string;
    providerName?: string;
    notes?: string;
}

// Global Settings Types
interface GlobalSettingDto {
    id: number;
    key: string;
    value: string;
    description?: string;
    createdAt: string;
    updatedAt: string;
}

// Request Log Types
interface RequestLogDto {
    id: number;
    virtualKeyId?: number;
    modelId: string;
    providerId: string;
    requestTimestamp: string;
    responseTimestamp?: string;
    inputTokens: number;
    outputTokens: number;
    totalTokens: number;
    cost: number;
    statusCode: number;
    errorMessage?: string;
    durationMs: number;
}

interface PagedResult<T> {
    items: T[];
    totalCount: number;
    pageNumber: number;
    pageSize: number;
    totalPages: number;
}

interface DailyUsageStatsDto {
    date: string;
    requestCount: number;
    inputTokens: number;
    outputTokens: number;
    cost: number;
    modelName?: string;
}

interface LogsSummaryDto {
    totalRequests: number;
    totalCost: number;
    totalInputTokens: number;
    totalOutputTokens: number;
    averageResponseTime: number;
    successRate: number;
    topModels: Array<{ model: string; count: number }>;
}

// Cost Dashboard Types
interface CostDashboardDto {
    totalCost: number;
    totalRequests: number;
    totalInputTokens: number;
    totalOutputTokens: number;
    dailyCosts: Array<{ date: string; cost: number }>;
    costsByModel: Array<{ model: string; cost: number }>;
    costsByVirtualKey: Array<{ keyName: string; cost: number }>;
    costTrend: 'increasing' | 'decreasing' | 'stable';
}

// Router Configuration Types
interface RouterConfig {
    fallbacksEnabled: boolean;
    retryEnabled: boolean;
    maxRetries: number;
    retryDelayMs: number;
    circuitBreakerEnabled: boolean;
    circuitBreakerThreshold: number;
    circuitBreakerResetTimeMs: number;
}

interface ModelDeployment {
    modelName: string;
    providers: string[];
    loadBalancingStrategy: 'RoundRobin' | 'Random' | 'LeastLatency';
    isActive: boolean;
}

interface FallbackConfiguration {
    primaryModelDeploymentId: string;
    fallbackModelDeploymentIds: string[];
}

// Audio Configuration Types
interface AudioProviderConfigDto {
    id: number;
    providerName: string;
    apiKey?: string;
    apiEndpoint?: string;
    isEnabled: boolean;
    supportedOperations: string[];
    additionalConfig?: Record<string, any>;
    priority: number;
    createdAt: string;
    updatedAt: string;
}

interface AudioCostDto {
    id: number;
    provider: string;
    operationType: 'transcription' | 'tts' | 'realtime';
    model?: string;
    costPerMinute?: number;
    costPerCharacter?: number;
    costPerRequest?: number;
    effectiveFrom: string;
    effectiveTo?: string;
    isActive: boolean;
}

interface AudioUsageDto {
    id: number;
    virtualKeyId?: number;
    provider: string;
    operationType: string;
    model?: string;
    durationSeconds?: number;
    characterCount?: number;
    cost: number;
    timestamp: string;
}

interface RealtimeSessionDto {
    sessionId: string;
    virtualKeyId?: number;
    provider: string;
    startTime: string;
    endTime?: string;
    durationSeconds: number;
    totalCost: number;
    isActive: boolean;
}

// System Types
interface SystemInfoDto {
    version: string;
    environment: string;
    uptime: string;
    memoryUsage: {
        total: number;
        used: number;
        free: number;
    };
    diskUsage: {
        total: number;
        used: number;
        free: number;
    };
}
```

## Node.js Setup

Install required dependencies for TypeScript development:

```bash
npm install --save-dev typescript @types/node
npm install axios # For HTTP requests
```

## Environment Configuration

Create a `.env` file for local development:

```env
CONDUIT_ADMIN_API_URL=http://localhost:5002
CONDUIT_MASTER_KEY=your_master_key_here
NODE_ENV=development
```

## Production Considerations

### Security
- Never commit master keys to version control
- Use environment variables for all credentials
- Implement proper error handling for authentication failures
- Consider implementing rate limiting for your client applications

### Performance
- Reuse HTTP connections when possible
- Implement caching for frequently accessed data
- Use appropriate timeout values
- Consider implementing circuit breaker patterns for resilience

### Monitoring
- Log all API interactions for debugging
- Monitor response times and error rates
- Implement health checks for your integration
- Set up alerts for authentication failures

## Next Steps

Continue with specific functionality guides:

- [Virtual Keys Management](./typescript-virtual-keys.md)
- [Provider Configuration](./typescript-providers.md) 
- [Analytics and Logging](./typescript-analytics.md)
- [Advanced Error Handling](./typescript-advanced.md)