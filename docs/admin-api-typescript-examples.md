# Conduit Admin API - TypeScript Examples

This comprehensive guide demonstrates how to programmatically access all Conduit Admin API endpoints using TypeScript.

## Table of Contents
- [Authentication](#authentication)
- [TypeScript Types](#typescript-types)
- [Admin API Client Class](#admin-api-client-class)
- [Virtual Keys](#virtual-keys)
- [Provider Management](#provider-management)
- [Model Mappings](#model-mappings)
- [IP Filtering](#ip-filtering)
- [Cost Management](#cost-management)
- [Request Logs & Analytics](#request-logs--analytics)
- [Audio Configuration](#audio-configuration)
- [Router Configuration](#router-configuration)
- [System Management](#system-management)
- [Error Handling](#error-handling)
- [Advanced Patterns](#advanced-patterns)

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

## Admin API Client Class

Here's a comprehensive TypeScript client for all Admin API endpoints:

```typescript
class ConduitAdminApiClient {
    private baseUrl: string;
    private headers: Record<string, string>;
    private retryConfig = {
        maxRetries: 3,
        retryDelay: 1000,
        backoffMultiplier: 2
    };

    constructor(baseUrl: string, masterKey: string, useXApiKey: boolean = true) {
        this.baseUrl = baseUrl.replace(/\/$/, ''); // Remove trailing slash
        this.headers = useXApiKey ? {
            'X-API-Key': masterKey,
            'Content-Type': 'application/json'
        } : {
            'Authorization': `Bearer ${masterKey}`,
            'Content-Type': 'application/json'
        };
    }

    private async request<T>(
        endpoint: string,
        options: RequestInit = {},
        retryCount: number = 0
    ): Promise<T> {
        const url = `${this.baseUrl}${endpoint}`;
        
        try {
            const response = await fetch(url, {
                ...options,
                headers: {
                    ...this.headers,
                    ...(options.headers || {})
                }
            });

            if (!response.ok) {
                const errorText = await response.text();
                const error = new Error(`API Error ${response.status}: ${errorText}`);
                (error as any).status = response.status;
                
                // Retry on 5xx errors or rate limiting
                if ((response.status >= 500 || response.status === 429) && retryCount < this.retryConfig.maxRetries) {
                    const delay = this.retryConfig.retryDelay * Math.pow(this.retryConfig.backoffMultiplier, retryCount);
                    await new Promise(resolve => setTimeout(resolve, delay));
                    return this.request<T>(endpoint, options, retryCount + 1);
                }
                
                throw error;
            }

            // Handle no-content responses
            if (response.status === 204) {
                return null as unknown as T;
            }

            return await response.json();
        } catch (error) {
            // Retry on network errors
            if (retryCount < this.retryConfig.maxRetries && error instanceof TypeError) {
                const delay = this.retryConfig.retryDelay * Math.pow(this.retryConfig.backoffMultiplier, retryCount);
                await new Promise(resolve => setTimeout(resolve, delay));
                return this.request<T>(endpoint, options, retryCount + 1);
            }
            throw error;
        }
    }

    // Virtual Key Operations
    async getAllVirtualKeys(): Promise<VirtualKeyDto[]> {
        return this.request<VirtualKeyDto[]>('/api/virtualkeys');
    }

    async getVirtualKeyById(id: number): Promise<VirtualKeyDto> {
        return this.request<VirtualKeyDto>(`/api/virtualkeys/${id}`);
    }

    async createVirtualKey(request: CreateVirtualKeyRequest): Promise<CreateVirtualKeyResponse> {
        return this.request<CreateVirtualKeyResponse>('/api/virtualkeys', {
            method: 'POST',
            body: JSON.stringify(request)
        });
    }

    async updateVirtualKey(id: number, request: UpdateVirtualKeyRequest): Promise<void> {
        return this.request<void>(`/api/virtualkeys/${id}`, {
            method: 'PUT',
            body: JSON.stringify(request)
        });
    }

    async deleteVirtualKey(id: number): Promise<void> {
        return this.request<void>(`/api/virtualkeys/${id}`, {
            method: 'DELETE'
        });
    }

    async resetVirtualKeySpend(id: number): Promise<void> {
        return this.request<void>(`/api/virtualkeys/${id}/reset-spend`, {
            method: 'POST'
        });
    }

    async validateVirtualKey(key: string, requestedModel?: string): Promise<VirtualKeyValidationResult> {
        const params = new URLSearchParams();
        if (requestedModel) params.append('requestedModel', requestedModel);
        
        return this.request<VirtualKeyValidationResult>(
            `/api/virtualkeys/validate?${params.toString()}`,
            {
                method: 'POST',
                body: JSON.stringify({ key })
            }
        );
    }

    async getVirtualKeyUsageStatistics(virtualKeyId?: number): Promise<any[]> {
        const params = virtualKeyId ? `?virtualKeyId=${virtualKeyId}` : '';
        return this.request<any[]>(`/api/costs/virtualkeys${params}`);
    }

    // Provider Management
    async getAllProviderCredentials(): Promise<ProviderCredentialDto[]> {
        return this.request<ProviderCredentialDto[]>('/api/providercredentials');
    }

    async getProviderCredentialById(id: number): Promise<ProviderCredentialDto> {
        return this.request<ProviderCredentialDto>(`/api/providercredentials/${id}`);
    }

    async getProviderCredentialByName(providerName: string): Promise<ProviderCredentialDto> {
        return this.request<ProviderCredentialDto>(`/api/providercredentials/name/${encodeURIComponent(providerName)}`);
    }

    async createProviderCredential(credential: CreateProviderCredentialDto): Promise<ProviderCredentialDto> {
        return this.request<ProviderCredentialDto>('/api/providercredentials', {
            method: 'POST',
            body: JSON.stringify(credential)
        });
    }

    async updateProviderCredential(id: number, credential: UpdateProviderCredentialDto): Promise<ProviderCredentialDto> {
        return this.request<ProviderCredentialDto>(`/api/providercredentials/${id}`, {
            method: 'PUT',
            body: JSON.stringify(credential)
        });
    }

    async deleteProviderCredential(id: number): Promise<void> {
        return this.request<void>(`/api/providercredentials/${id}`, {
            method: 'DELETE'
        });
    }

    async testProviderConnection(providerName: string): Promise<ProviderConnectionTestResultDto> {
        const provider = await this.getProviderCredentialByName(providerName);
        return this.request<ProviderConnectionTestResultDto>(`/api/providercredentials/test/${provider.id}`, {
            method: 'POST'
        });
    }

    // Provider Health
    async getAllProviderHealthConfigurations(): Promise<ProviderHealthConfigurationDto[]> {
        return this.request<ProviderHealthConfigurationDto[]>('/api/providerhealth/configurations');
    }

    async getProviderHealthConfiguration(providerName: string): Promise<ProviderHealthConfigurationDto> {
        return this.request<ProviderHealthConfigurationDto>(`/api/providerhealth/configurations/${encodeURIComponent(providerName)}`);
    }

    async createProviderHealthConfiguration(config: Omit<ProviderHealthConfigurationDto, 'lastCheckTime' | 'isHealthy'>): Promise<ProviderHealthConfigurationDto> {
        return this.request<ProviderHealthConfigurationDto>('/api/providerhealth/configurations', {
            method: 'POST',
            body: JSON.stringify(config)
        });
    }

    async updateProviderHealthConfiguration(providerName: string, config: Partial<ProviderHealthConfigurationDto>): Promise<ProviderHealthConfigurationDto> {
        return this.request<ProviderHealthConfigurationDto>(`/api/providerhealth/configurations/${encodeURIComponent(providerName)}`, {
            method: 'PUT',
            body: JSON.stringify(config)
        });
    }

    async deleteProviderHealthConfiguration(providerName: string): Promise<void> {
        return this.request<void>(`/api/providerhealth/configurations/${encodeURIComponent(providerName)}`, {
            method: 'DELETE'
        });
    }

    async getProviderHealthRecords(providerName?: string): Promise<ProviderHealthRecordDto[]> {
        const params = providerName ? `?providerName=${encodeURIComponent(providerName)}` : '';
        return this.request<ProviderHealthRecordDto[]>(`/api/providerhealth/records${params}`);
    }

    async getProviderHealthSummary(): Promise<any[]> {
        return this.request<any[]>('/api/providerhealth/summary');
    }

    // Model Provider Mappings
    async getAllModelProviderMappings(): Promise<ModelProviderMappingDto[]> {
        return this.request<ModelProviderMappingDto[]>('/api/modelprovidermapping');
    }

    async getModelProviderMappingById(id: number): Promise<ModelProviderMappingDto> {
        return this.request<ModelProviderMappingDto>(`/api/modelprovidermapping/${id}`);
    }

    async getModelProviderMappingByAlias(modelAlias: string): Promise<ModelProviderMappingDto> {
        return this.request<ModelProviderMappingDto>(`/api/modelprovidermapping/by-model/${encodeURIComponent(modelAlias)}`);
    }

    async createModelProviderMapping(mapping: Omit<ModelProviderMappingDto, 'id' | 'createdAt' | 'updatedAt'>): Promise<void> {
        return this.request<void>('/api/modelprovidermapping', {
            method: 'POST',
            body: JSON.stringify(mapping)
        });
    }

    async updateModelProviderMapping(id: number, mapping: Partial<ModelProviderMappingDto>): Promise<void> {
        return this.request<void>(`/api/modelprovidermapping/${id}`, {
            method: 'PUT',
            body: JSON.stringify(mapping)
        });
    }

    async deleteModelProviderMapping(id: number): Promise<void> {
        return this.request<void>(`/api/modelprovidermapping/${id}`, {
            method: 'DELETE'
        });
    }

    // IP Filter Management
    async getAllIpFilters(): Promise<IpFilterDto[]> {
        return this.request<IpFilterDto[]>('/api/ipfilter');
    }

    async getEnabledIpFilters(): Promise<IpFilterDto[]> {
        return this.request<IpFilterDto[]>('/api/ipfilter/enabled');
    }

    async getIpFilterSettings(): Promise<IpFilterSettingsDto> {
        return this.request<IpFilterSettingsDto>('/api/ipfilter/settings');
    }

    async updateIpFilterSettings(settings: IpFilterSettingsDto): Promise<void> {
        return this.request<void>('/api/ipfilter/settings', {
            method: 'PUT',
            body: JSON.stringify(settings)
        });
    }

    async getIpFilterById(id: number): Promise<IpFilterDto> {
        return this.request<IpFilterDto>(`/api/ipfilter/${id}`);
    }

    async createIpFilter(filter: Omit<IpFilterDto, 'id' | 'createdAt' | 'updatedAt'>): Promise<IpFilterDto> {
        return this.request<IpFilterDto>('/api/ipfilter', {
            method: 'POST',
            body: JSON.stringify(filter)
        });
    }

    async updateIpFilter(id: number, filter: Partial<IpFilterDto>): Promise<IpFilterDto> {
        return this.request<IpFilterDto>(`/api/ipfilter/${id}`, {
            method: 'PUT',
            body: JSON.stringify(filter)
        });
    }

    async deleteIpFilter(id: number): Promise<void> {
        return this.request<void>(`/api/ipfilter/${id}`, {
            method: 'DELETE'
        });
    }

    async checkIpAddress(ipAddress: string): Promise<IpCheckResult> {
        return this.request<IpCheckResult>(`/api/ipfilter/check?ip=${encodeURIComponent(ipAddress)}`);
    }

    // Model Cost Management
    async getAllModelCosts(): Promise<ModelCostDto[]> {
        return this.request<ModelCostDto[]>('/api/modelcosts');
    }

    async getModelCostById(id: number): Promise<ModelCostDto> {
        return this.request<ModelCostDto>(`/api/modelcosts/${id}`);
    }

    async getModelCostByPattern(pattern: string): Promise<ModelCostDto> {
        return this.request<ModelCostDto>(`/api/modelcosts/pattern/${encodeURIComponent(pattern)}`);
    }

    async createModelCost(cost: Omit<ModelCostDto, 'id'>): Promise<ModelCostDto> {
        return this.request<ModelCostDto>('/api/modelcosts', {
            method: 'POST',
            body: JSON.stringify(cost)
        });
    }

    async updateModelCost(id: number, cost: Partial<ModelCostDto>): Promise<ModelCostDto> {
        return this.request<ModelCostDto>(`/api/modelcosts/${id}`, {
            method: 'PUT',
            body: JSON.stringify(cost)
        });
    }

    async deleteModelCost(id: number): Promise<void> {
        return this.request<void>(`/api/modelcosts/${id}`, {
            method: 'DELETE'
        });
    }

    async calculateCost(modelId: string, inputTokens: number, outputTokens: number): Promise<number> {
        const params = new URLSearchParams({
            modelId,
            inputTokens: inputTokens.toString(),
            outputTokens: outputTokens.toString()
        });
        return this.request<number>(`/api/modelcosts/calculate?${params.toString()}`);
    }

    // Global Settings
    async getAllGlobalSettings(): Promise<GlobalSettingDto[]> {
        return this.request<GlobalSettingDto[]>('/api/globalsettings');
    }

    async getGlobalSettingByKey(key: string): Promise<GlobalSettingDto> {
        return this.request<GlobalSettingDto>(`/api/globalsettings/by-key/${encodeURIComponent(key)}`);
    }

    async upsertGlobalSetting(setting: Omit<GlobalSettingDto, 'id' | 'createdAt' | 'updatedAt'>): Promise<GlobalSettingDto> {
        return this.request<GlobalSettingDto>('/api/globalsettings/by-key', {
            method: 'PUT',
            body: JSON.stringify(setting)
        });
    }

    async deleteGlobalSetting(key: string): Promise<void> {
        return this.request<void>(`/api/globalsettings/by-key/${encodeURIComponent(key)}`, {
            method: 'DELETE'
        });
    }

    // Request Logs and Analytics
    async getRequestLogs(params: {
        page?: number;
        pageSize?: number;
        virtualKeyId?: number;
        modelId?: string;
        startDate?: Date;
        endDate?: Date;
    } = {}): Promise<PagedResult<RequestLogDto>> {
        const queryParams = new URLSearchParams();
        if (params.page) queryParams.append('page', params.page.toString());
        if (params.pageSize) queryParams.append('pageSize', params.pageSize.toString());
        if (params.virtualKeyId) queryParams.append('virtualKeyId', params.virtualKeyId.toString());
        if (params.modelId) queryParams.append('modelId', params.modelId);
        if (params.startDate) queryParams.append('startDate', params.startDate.toISOString());
        if (params.endDate) queryParams.append('endDate', params.endDate.toISOString());
        
        return this.request<PagedResult<RequestLogDto>>(`/api/logs?${queryParams.toString()}`);
    }

    async createRequestLog(log: Omit<RequestLogDto, 'id'>): Promise<RequestLogDto> {
        return this.request<RequestLogDto>('/api/logs', {
            method: 'POST',
            body: JSON.stringify(log)
        });
    }

    async getDailyUsageStats(startDate: Date, endDate: Date, virtualKeyId?: number): Promise<DailyUsageStatsDto[]> {
        const params = new URLSearchParams({
            startDate: startDate.toISOString(),
            endDate: endDate.toISOString()
        });
        if (virtualKeyId) params.append('virtualKeyId', virtualKeyId.toString());
        
        return this.request<DailyUsageStatsDto[]>(`/api/logs/daily-stats?${params.toString()}`);
    }

    async getDistinctModels(): Promise<string[]> {
        return this.request<string[]>('/api/logs/models');
    }

    async getLogsSummary(days: number = 7, virtualKeyId?: number): Promise<LogsSummaryDto> {
        const params = new URLSearchParams({ days: days.toString() });
        if (virtualKeyId) params.append('virtualKeyId', virtualKeyId.toString());
        
        return this.request<LogsSummaryDto>(`/api/logs/summary?${params.toString()}`);
    }

    // Cost Dashboard
    async getCostDashboard(params: {
        startDate?: Date;
        endDate?: Date;
        virtualKeyId?: number;
        modelName?: string;
    } = {}): Promise<CostDashboardDto> {
        const queryParams = new URLSearchParams();
        if (params.startDate) queryParams.append('startDate', params.startDate.toISOString());
        if (params.endDate) queryParams.append('endDate', params.endDate.toISOString());
        if (params.virtualKeyId) queryParams.append('virtualKeyId', params.virtualKeyId.toString());
        if (params.modelName) queryParams.append('modelName', params.modelName);
        
        return this.request<CostDashboardDto>(`/api/dashboard/costs?${queryParams.toString()}`);
    }

    async getDetailedCostData(params: {
        startDate?: Date;
        endDate?: Date;
        virtualKeyId?: number;
        modelName?: string;
    } = {}): Promise<Array<{ name: string; cost: number; percentage: number }>> {
        const queryParams = new URLSearchParams();
        if (params.startDate) queryParams.append('startDate', params.startDate.toISOString());
        if (params.endDate) queryParams.append('endDate', params.endDate.toISOString());
        if (params.virtualKeyId) queryParams.append('virtualKeyId', params.virtualKeyId.toString());
        if (params.modelName) queryParams.append('modelName', params.modelName);
        
        return this.request<Array<{ name: string; cost: number; percentage: number }>>(
            `/api/dashboard/costs/detailed?${queryParams.toString()}`
        );
    }

    // Router Configuration
    async getRouterConfig(): Promise<RouterConfig> {
        return this.request<RouterConfig>('/api/router/config');
    }

    async updateRouterConfig(config: RouterConfig): Promise<void> {
        return this.request<void>('/api/router/config', {
            method: 'PUT',
            body: JSON.stringify(config)
        });
    }

    async getAllModelDeployments(): Promise<ModelDeployment[]> {
        return this.request<ModelDeployment[]>('/api/router/deployments');
    }

    async getModelDeployment(modelName: string): Promise<ModelDeployment> {
        return this.request<ModelDeployment>(`/api/router/deployments/${encodeURIComponent(modelName)}`);
    }

    async saveModelDeployment(deployment: ModelDeployment): Promise<void> {
        return this.request<void>('/api/router/deployments', {
            method: 'POST',
            body: JSON.stringify(deployment)
        });
    }

    async deleteModelDeployment(modelName: string): Promise<void> {
        return this.request<void>(`/api/router/deployments/${encodeURIComponent(modelName)}`, {
            method: 'DELETE'
        });
    }

    async getAllFallbackConfigurations(): Promise<FallbackConfiguration[]> {
        return this.request<FallbackConfiguration[]>('/api/router/fallbacks');
    }

    async setFallbackConfiguration(config: FallbackConfiguration): Promise<void> {
        return this.request<void>('/api/router/fallbacks', {
            method: 'POST',
            body: JSON.stringify(config)
        });
    }

    async removeFallbackConfiguration(modelName: string): Promise<void> {
        return this.request<void>(`/api/router/fallbacks/${encodeURIComponent(modelName)}`, {
            method: 'DELETE'
        });
    }

    // Audio Configuration
    async getAllAudioProviders(): Promise<AudioProviderConfigDto[]> {
        return this.request<AudioProviderConfigDto[]>('/api/admin/audio/providers');
    }

    async getAudioProvider(id: number): Promise<AudioProviderConfigDto> {
        return this.request<AudioProviderConfigDto>(`/api/admin/audio/providers/${id}`);
    }

    async getAudioProvidersByName(providerName: string): Promise<AudioProviderConfigDto[]> {
        return this.request<AudioProviderConfigDto[]>(`/api/admin/audio/providers/by-name/${encodeURIComponent(providerName)}`);
    }

    async getEnabledAudioProviders(operationType: string): Promise<AudioProviderConfigDto[]> {
        return this.request<AudioProviderConfigDto[]>(`/api/admin/audio/providers/enabled/${encodeURIComponent(operationType)}`);
    }

    async createAudioProvider(provider: Omit<AudioProviderConfigDto, 'id' | 'createdAt' | 'updatedAt'>): Promise<AudioProviderConfigDto> {
        return this.request<AudioProviderConfigDto>('/api/admin/audio/providers', {
            method: 'POST',
            body: JSON.stringify(provider)
        });
    }

    async updateAudioProvider(id: number, provider: Partial<AudioProviderConfigDto>): Promise<AudioProviderConfigDto> {
        return this.request<AudioProviderConfigDto>(`/api/admin/audio/providers/${id}`, {
            method: 'PUT',
            body: JSON.stringify(provider)
        });
    }

    async deleteAudioProvider(id: number): Promise<void> {
        return this.request<void>(`/api/admin/audio/providers/${id}`, {
            method: 'DELETE'
        });
    }

    async testAudioProvider(id: number, operationType: string = 'transcription'): Promise<any> {
        return this.request<any>(`/api/admin/audio/providers/${id}/test?operationType=${operationType}`, {
            method: 'POST'
        });
    }

    // Audio Costs
    async getAllAudioCosts(): Promise<AudioCostDto[]> {
        return this.request<AudioCostDto[]>('/api/admin/audio/costs');
    }

    async getAudioCost(id: number): Promise<AudioCostDto> {
        return this.request<AudioCostDto>(`/api/admin/audio/costs/${id}`);
    }

    async getAudioCostsByProvider(provider: string): Promise<AudioCostDto[]> {
        return this.request<AudioCostDto[]>(`/api/admin/audio/costs/by-provider/${encodeURIComponent(provider)}`);
    }

    async getCurrentAudioCost(provider: string, operationType: string, model?: string): Promise<AudioCostDto> {
        const params = new URLSearchParams({ provider, operationType });
        if (model) params.append('model', model);
        
        return this.request<AudioCostDto>(`/api/admin/audio/costs/current?${params.toString()}`);
    }

    async createAudioCost(cost: Omit<AudioCostDto, 'id'>): Promise<AudioCostDto> {
        return this.request<AudioCostDto>('/api/admin/audio/costs', {
            method: 'POST',
            body: JSON.stringify(cost)
        });
    }

    async updateAudioCost(id: number, cost: Partial<AudioCostDto>): Promise<AudioCostDto> {
        return this.request<AudioCostDto>(`/api/admin/audio/costs/${id}`, {
            method: 'PUT',
            body: JSON.stringify(cost)
        });
    }

    async deleteAudioCost(id: number): Promise<void> {
        return this.request<void>(`/api/admin/audio/costs/${id}`, {
            method: 'DELETE'
        });
    }

    // Audio Usage
    async getAudioUsageLogs(params: {
        pageNumber?: number;
        pageSize?: number;
        virtualKey?: string;
        provider?: string;
        operationType?: string;
        startDate?: Date;
        endDate?: Date;
    } = {}): Promise<PagedResult<AudioUsageDto>> {
        const queryParams = new URLSearchParams();
        if (params.pageNumber) queryParams.append('pageNumber', params.pageNumber.toString());
        if (params.pageSize) queryParams.append('pageSize', params.pageSize.toString());
        if (params.virtualKey) queryParams.append('virtualKey', params.virtualKey);
        if (params.provider) queryParams.append('provider', params.provider);
        if (params.operationType) queryParams.append('operationType', params.operationType);
        if (params.startDate) queryParams.append('startDate', params.startDate.toISOString());
        if (params.endDate) queryParams.append('endDate', params.endDate.toISOString());
        
        return this.request<PagedResult<AudioUsageDto>>(`/api/admin/audio/usage?${queryParams.toString()}`);
    }

    async getAudioUsageSummary(startDate: Date, endDate: Date, virtualKey?: string, provider?: string): Promise<any> {
        const params = new URLSearchParams({
            startDate: startDate.toISOString(),
            endDate: endDate.toISOString()
        });
        if (virtualKey) params.append('virtualKey', virtualKey);
        if (provider) params.append('provider', provider);
        
        return this.request<any>(`/api/admin/audio/usage/summary?${params.toString()}`);
    }

    // Realtime Sessions
    async getRealtimeSessionMetrics(): Promise<any> {
        return this.request<any>('/api/admin/audio/realtime/metrics');
    }

    async getActiveRealtimeSessions(): Promise<RealtimeSessionDto[]> {
        return this.request<RealtimeSessionDto[]>('/api/admin/audio/realtime/sessions/active');
    }

    async getRealtimeSessionDetails(sessionId: string): Promise<RealtimeSessionDto> {
        return this.request<RealtimeSessionDto>(`/api/admin/audio/realtime/sessions/${encodeURIComponent(sessionId)}`);
    }

    async terminateRealtimeSession(sessionId: string): Promise<void> {
        return this.request<void>(`/api/admin/audio/realtime/sessions/${encodeURIComponent(sessionId)}/terminate`, {
            method: 'POST'
        });
    }

    // System Management
    async createDatabaseBackup(): Promise<void> {
        return this.request<void>('/api/database/backup', {
            method: 'POST'
        });
    }

    async getDatabaseBackupDownloadUrl(): Promise<string> {
        const response = await this.request<string>('/api/database/backup/download');
        return response;
    }

    async getSystemInfo(): Promise<SystemInfoDto> {
        return this.request<SystemInfoDto>('/api/systeminfo/info');
    }

    async checkProviderStatus(providerName: string): Promise<any> {
        return this.request<any>(`/api/providers/status/${encodeURIComponent(providerName)}`);
    }

    async checkAllProvidersStatus(): Promise<Record<string, any>> {
        return this.request<Record<string, any>>('/api/providers/status');
    }

    // Utility methods
    async searchVirtualKeys(searchTerm: string): Promise<VirtualKeyDto[]> {
        const allKeys = await this.getAllVirtualKeys();
        const lowerSearchTerm = searchTerm.toLowerCase();
        
        return allKeys.filter(key => 
            key.keyName.toLowerCase().includes(lowerSearchTerm) ||
            (key.metadata && key.metadata.toLowerCase().includes(lowerSearchTerm))
        );
    }

    async exportCostData(format: 'csv' | 'json', params: {
        startDate?: Date;
        endDate?: Date;
        virtualKeyId?: number;
        modelName?: string;
    } = {}): Promise<Blob> {
        const queryParams = new URLSearchParams({ format });
        if (params.startDate) queryParams.append('startDate', params.startDate.toISOString());
        if (params.endDate) queryParams.append('endDate', params.endDate.toISOString());
        if (params.virtualKeyId) queryParams.append('virtualKeyId', params.virtualKeyId.toString());
        if (params.modelName) queryParams.append('modelName', params.modelName);
        
        const response = await fetch(`${this.baseUrl}/api/dashboard/costs/export?${queryParams.toString()}`, {
            headers: this.headers
        });
        
        if (!response.ok) {
            throw new Error(`Export failed: ${response.status}`);
        }
        
        return response.blob();
    }
}
```

## Virtual Keys

### Initialize the Client

```typescript
const adminClient = new ConduitAdminApiClient(
    'http://localhost:5002',
    'your_master_key_here',
    true // Use X-API-Key header (recommended)
);
```

### Enumerate All Virtual Keys

```typescript
async function listAllVirtualKeys() {
    try {
        const virtualKeys = await adminClient.getAllVirtualKeys();
        
        console.log(`Found ${virtualKeys.length} virtual keys:`);
        virtualKeys.forEach(key => {
            console.log(`- ${key.keyName} (ID: ${key.id})`);
            console.log(`  Budget: $${key.currentSpend}/$${key.maxBudget}`);
            console.log(`  Status: ${key.isEnabled ? 'Active' : 'Inactive'}`);
            console.log(`  Expires: ${key.expiresAt || 'Never'}`);
            console.log('');
        });
    } catch (error) {
        console.error('Error listing virtual keys:', error);
    }
}

// Usage
await listAllVirtualKeys();
```

### Search Virtual Keys by Name or Comments

```typescript
async function searchKeys(searchTerm: string) {
    try {
        const matchingKeys = await adminClient.searchVirtualKeys(searchTerm);
        
        console.log(`Found ${matchingKeys.length} keys matching "${searchTerm}":`);
        matchingKeys.forEach(key => {
            console.log(`- ${key.keyName}`);
            if (key.metadata) {
                console.log(`  Metadata: ${key.metadata}`);
            }
        });
    } catch (error) {
        console.error('Error searching virtual keys:', error);
    }
}

// Usage examples
await searchKeys('development');
await searchKeys('Project: Research');
await searchKeys('test');
```

### Delete a Virtual Key

```typescript
async function deleteVirtualKey(keyId: number) {
    try {
        // First, get the key details for confirmation
        const keyDetails = await adminClient.getVirtualKeyById(keyId);
        console.log(`Deleting virtual key: ${keyDetails.keyName}`);
        
        // Delete the key
        await adminClient.deleteVirtualKey(keyId);
        console.log('Virtual key deleted successfully');
    } catch (error) {
        console.error('Error deleting virtual key:', error);
    }
}

// Usage
await deleteVirtualKey(123);
```

### Create Virtual Key with Budget and Expiration

```typescript
async function createVirtualKeyWithBudget() {
    try {
        const newKeyRequest: CreateVirtualKeyRequest = {
            keyName: 'Development API Key',
            allowedModels: 'gpt-4*,claude-*',
            maxBudget: 100.00,
            budgetDuration: 'Monthly',
            expiresAt: new Date(Date.now() + 90 * 24 * 60 * 60 * 1000).toISOString(), // 90 days
            metadata: 'Project: Development Environment',
            rateLimitRpm: 60,
            rateLimitRpd: 1000
        };

        const response = await adminClient.createVirtualKey(newKeyRequest);
        
        console.log('Virtual key created successfully!');
        console.log(`Key: ${response.virtualKey}`);
        console.log(`Name: ${response.keyInfo.keyName}`);
        console.log(`Budget: $${response.keyInfo.maxBudget}`);
        console.log(`Expires: ${response.keyInfo.expiresAt}`);
        
        return response;
    } catch (error) {
        console.error('Error creating virtual key:', error);
    }
}

// Usage
const newKey = await createVirtualKeyWithBudget();
```

### Create Virtual Key with Model Permissions

```typescript
async function createKeyWithModelPermissions() {
    try {
        const keyRequest: CreateVirtualKeyRequest = {
            keyName: 'GPT-4 Only Key',
            allowedModels: 'gpt-4*,gpt-4-turbo*', // Only GPT-4 models
            maxBudget: 50.00,
            budgetDuration: 'Weekly',
            metadata: 'Restricted to GPT-4 models only',
            rateLimitRpm: 30 // Lower rate limit
        };

        const response = await adminClient.createVirtualKey(keyRequest);
        
        console.log('Model-restricted key created:');
        console.log(`Key: ${response.virtualKey}`);
        console.log(`Allowed Models: ${response.keyInfo.allowedModels}`);
        console.log(`Rate Limit: ${response.keyInfo.rateLimitRpm} requests/minute`);
        
        return response;
    } catch (error) {
        console.error('Error creating model-restricted key:', error);
    }
}

// Usage
await createKeyWithModelPermissions();
```

### Validate Virtual Key

```typescript
async function validateKey(key: string, model?: string) {
    try {
        const validation = await adminClient.validateVirtualKey(key, model);
        
        if (validation.isValid) {
            console.log('Key is valid!');
            console.log(`Key Name: ${validation.keyName}`);
            console.log(`Allowed Models: ${validation.allowedModels?.join(', ')}`);
            console.log(`Budget: $${validation.currentSpend}/$${validation.maxBudget}`);
        } else {
            console.log('Key is invalid:', validation.reason);
        }
    } catch (error) {
        console.error('Error validating key:', error);
    }
}

// Usage
await validateKey('vk_abcd1234...');
await validateKey('vk_abcd1234...', 'gpt-4');
```

## Provider Management

### List All Providers

```typescript
async function listProviders() {
    try {
        const providers = await adminClient.getAllProviderCredentials();
        
        console.log('Configured Providers:');
        providers.forEach(provider => {
            console.log(`- ${provider.providerName}`);
            console.log(`  Status: ${provider.isEnabled ? 'Enabled' : 'Disabled'}`);
            console.log(`  Endpoint: ${provider.apiEndpoint || 'Default'}`);
            console.log(`  Created: ${new Date(provider.createdAt).toLocaleDateString()}`);
        });
    } catch (error) {
        console.error('Error listing providers:', error);
    }
}
```

### Add New Provider

```typescript
async function addProvider() {
    try {
        const newProvider: CreateProviderCredentialDto = {
            providerName: 'openai',
            apiKey: 'sk-...',
            organizationId: 'org-...',
            isEnabled: true,
            additionalConfig: JSON.stringify({
                maxRetries: 3,
                timeout: 30000
            })
        };
        
        const created = await adminClient.createProviderCredential(newProvider);
        console.log('Provider added:', created.providerName);
        
        // Test the connection
        const testResult = await adminClient.testProviderConnection(created.providerName);
        console.log('Connection test:', testResult.success ? 'Success' : 'Failed');
        if (testResult.modelsAvailable) {
            console.log('Available models:', testResult.modelsAvailable.join(', '));
        }
    } catch (error) {
        console.error('Error adding provider:', error);
    }
}
```

### Configure Provider Health Monitoring

```typescript
async function setupHealthMonitoring(providerName: string) {
    try {
        const healthConfig: Omit<ProviderHealthConfigurationDto, 'lastCheckTime' | 'isHealthy'> = {
            providerName,
            isEnabled: true,
            checkIntervalSeconds: 300, // 5 minutes
            timeoutSeconds: 30,
            unhealthyThreshold: 3,
            healthyThreshold: 2,
            testModel: 'gpt-3.5-turbo'
        };
        
        const created = await adminClient.createProviderHealthConfiguration(healthConfig);
        console.log('Health monitoring configured for:', created.providerName);
        
        // Get recent health records
        const records = await adminClient.getProviderHealthRecords(providerName);
        console.log(`Recent health checks: ${records.length}`);
        
        const recentRecord = records[0];
        if (recentRecord) {
            console.log(`Last check: ${recentRecord.checkTime}`);
            console.log(`Status: ${recentRecord.isHealthy ? 'Healthy' : 'Unhealthy'}`);
            console.log(`Response time: ${recentRecord.responseTimeMs}ms`);
        }
    } catch (error) {
        console.error('Error setting up health monitoring:', error);
    }
}
```

## Model Mappings

### Configure Model Routing

```typescript
async function setupModelMapping() {
    try {
        // Create a mapping for GPT-4 to use OpenAI
        const mapping: Omit<ModelProviderMappingDto, 'id' | 'createdAt' | 'updatedAt'> = {
            modelId: 'gpt-4',
            providerId: '1', // Provider credential ID
            providerModelId: 'gpt-4',
            isEnabled: true,
            priority: 100
        };
        
        await adminClient.createModelProviderMapping(mapping);
        console.log('Model mapping created');
        
        // List all mappings
        const allMappings = await adminClient.getAllModelProviderMappings();
        console.log(`Total mappings: ${allMappings.length}`);
        
        allMappings.forEach(m => {
            console.log(`${m.modelId} -> ${m.providerModelId} (Provider: ${m.providerId})`);
        });
    } catch (error) {
        console.error('Error setting up model mapping:', error);
    }
}
```

### Find Provider for Model

```typescript
async function findProviderForModel(modelAlias: string) {
    try {
        const mapping = await adminClient.getModelProviderMappingByAlias(modelAlias);
        console.log(`Model ${modelAlias} is served by:`);
        console.log(`- Provider ID: ${mapping.providerId}`);
        console.log(`- Provider Model: ${mapping.providerModelId}`);
        console.log(`- Priority: ${mapping.priority}`);
        console.log(`- Status: ${mapping.isEnabled ? 'Active' : 'Inactive'}`);
    } catch (error) {
        console.error(`No mapping found for model ${modelAlias}`);
    }
}
```

## IP Filtering

### Configure IP Access Control

```typescript
async function setupIpFiltering() {
    try {
        // Enable IP filtering with settings
        const settings: IpFilterSettingsDto = {
            isEnabled: true,
            defaultAllow: false, // Deny by default
            bypassForAdminUi: true,
            excludedEndpoints: ['/api/v1/health', '/api/v1/status'],
            filterMode: 'restrictive',
            whitelistFilters: [],
            blacklistFilters: []
        };
        
        await adminClient.updateIpFilterSettings(settings);
        console.log('IP filtering enabled');
        
        // Add allowed IP ranges
        const officeNetwork: Omit<IpFilterDto, 'id' | 'createdAt' | 'updatedAt'> = {
            name: 'Office Network',
            cidrRange: '192.168.1.0/24',
            filterType: 'Allow',
            isEnabled: true,
            description: 'Main office network'
        };
        
        const vpn: Omit<IpFilterDto, 'id' | 'createdAt' | 'updatedAt'> = {
            name: 'Corporate VPN',
            cidrRange: '10.0.0.0/16',
            filterType: 'Allow',
            isEnabled: true,
            description: 'VPN access for remote workers'
        };
        
        await adminClient.createIpFilter(officeNetwork);
        await adminClient.createIpFilter(vpn);
        
        console.log('IP filters created');
    } catch (error) {
        console.error('Error setting up IP filtering:', error);
    }
}
```

### Check IP Access

```typescript
async function checkIpAccess(ipAddress: string) {
    try {
        const result = await adminClient.checkIpAddress(ipAddress);
        
        console.log(`IP ${ipAddress}: ${result.isAllowed ? 'ALLOWED' : 'DENIED'}`);
        if (result.reason) {
            console.log(`Reason: ${result.reason}`);
        }
        if (result.matchedFilter) {
            console.log(`Matched filter: ${result.matchedFilter}`);
        }
    } catch (error) {
        console.error('Error checking IP:', error);
    }
}

// Usage
await checkIpAccess('192.168.1.100');
await checkIpAccess('203.0.113.45');
```

## Cost Management

### Configure Model Costs

```typescript
async function setupModelCosts() {
    try {
        // Add cost configuration for GPT-4
        const gpt4Cost: Omit<ModelCostDto, 'id'> = {
            modelIdPattern: 'gpt-4*',
            inputCostPerMillion: 30.00,
            outputCostPerMillion: 60.00,
            isActive: true,
            priority: 100,
            effectiveFrom: new Date().toISOString(),
            providerName: 'openai',
            notes: 'GPT-4 standard pricing'
        };
        
        const created = await adminClient.createModelCost(gpt4Cost);
        console.log('Cost configuration created:', created.modelIdPattern);
        
        // Calculate cost for a request
        const cost = await adminClient.calculateCost('gpt-4', 1000, 500);
        console.log(`Cost for 1000 input + 500 output tokens: $${cost.toFixed(4)}`);
    } catch (error) {
        console.error('Error setting up model costs:', error);
    }
}
```

### Generate Cost Report

```typescript
async function generateCostReport(days: number = 30) {
    try {
        const endDate = new Date();
        const startDate = new Date();
        startDate.setDate(startDate.getDate() - days);
        
        const dashboard = await adminClient.getCostDashboard({
            startDate,
            endDate
        });
        
        console.log('=== Cost Report ===');
        console.log(`Period: ${startDate.toLocaleDateString()} - ${endDate.toLocaleDateString()}`);
        console.log(`Total Cost: $${dashboard.totalCost.toFixed(2)}`);
        console.log(`Total Requests: ${dashboard.totalRequests.toLocaleString()}`);
        console.log(`Total Tokens: ${(dashboard.totalInputTokens + dashboard.totalOutputTokens).toLocaleString()}`);
        console.log(`Cost Trend: ${dashboard.costTrend}`);
        
        console.log('\nTop Models by Cost:');
        dashboard.costsByModel
            .sort((a, b) => b.cost - a.cost)
            .slice(0, 5)
            .forEach(item => {
                console.log(`- ${item.model}: $${item.cost.toFixed(2)}`);
            });
        
        console.log('\nTop Virtual Keys by Cost:');
        dashboard.costsByVirtualKey
            .sort((a, b) => b.cost - a.cost)
            .slice(0, 5)
            .forEach(item => {
                console.log(`- ${item.keyName}: $${item.cost.toFixed(2)}`);
            });
    } catch (error) {
        console.error('Error generating cost report:', error);
    }
}
```

### Export Cost Data

```typescript
async function exportCostData() {
    try {
        const endDate = new Date();
        const startDate = new Date();
        startDate.setMonth(startDate.getMonth() - 1);
        
        // Export as CSV
        const csvBlob = await adminClient.exportCostData('csv', {
            startDate,
            endDate
        });
        
        // Save to file (Node.js)
        const fs = require('fs');
        const buffer = await csvBlob.arrayBuffer();
        fs.writeFileSync('cost_report.csv', Buffer.from(buffer));
        
        console.log('Cost data exported to cost_report.csv');
    } catch (error) {
        console.error('Error exporting cost data:', error);
    }
}
```

## Request Logs & Analytics

### Query Request Logs

```typescript
async function queryLogs() {
    try {
        const logs = await adminClient.getRequestLogs({
            page: 1,
            pageSize: 50,
            startDate: new Date(Date.now() - 24 * 60 * 60 * 1000), // Last 24 hours
            modelId: 'gpt-4'
        });
        
        console.log(`Found ${logs.totalCount} requests`);
        console.log(`Showing page ${logs.pageNumber} of ${logs.totalPages}`);
        
        logs.items.forEach(log => {
            console.log(`\n[${log.requestTimestamp}]`);
            console.log(`Model: ${log.modelId}`);
            console.log(`Tokens: ${log.inputTokens} in, ${log.outputTokens} out`);
            console.log(`Cost: $${log.cost.toFixed(4)}`);
            console.log(`Duration: ${log.durationMs}ms`);
            console.log(`Status: ${log.statusCode}`);
            if (log.errorMessage) {
                console.log(`Error: ${log.errorMessage}`);
            }
        });
    } catch (error) {
        console.error('Error querying logs:', error);
    }
}
```

### Generate Usage Analytics

```typescript
async function generateUsageAnalytics() {
    try {
        const summary = await adminClient.getLogsSummary(7); // Last 7 days
        
        console.log('=== 7-Day Usage Summary ===');
        console.log(`Total Requests: ${summary.totalRequests.toLocaleString()}`);
        console.log(`Total Cost: $${summary.totalCost.toFixed(2)}`);
        console.log(`Success Rate: ${(summary.successRate * 100).toFixed(1)}%`);
        console.log(`Avg Response Time: ${summary.averageResponseTime.toFixed(0)}ms`);
        
        console.log('\nTop Models:');
        summary.topModels.forEach(model => {
            console.log(`- ${model.model}: ${model.count.toLocaleString()} requests`);
        });
        
        // Get daily stats
        const endDate = new Date();
        const startDate = new Date();
        startDate.setDate(startDate.getDate() - 7);
        
        const dailyStats = await adminClient.getDailyUsageStats(startDate, endDate);
        
        console.log('\nDaily Usage:');
        dailyStats.forEach(day => {
            console.log(`${day.date}: ${day.requestCount} requests, $${day.cost.toFixed(2)}`);
        });
    } catch (error) {
        console.error('Error generating analytics:', error);
    }
}
```

## Audio Configuration

### Setup Audio Providers

```typescript
async function setupAudioProviders() {
    try {
        // Configure OpenAI for transcription
        const openaiAudio: Omit<AudioProviderConfigDto, 'id' | 'createdAt' | 'updatedAt'> = {
            providerName: 'openai',
            apiKey: 'sk-...',
            isEnabled: true,
            supportedOperations: ['transcription', 'tts'],
            priority: 100,
            additionalConfig: {
                defaultModel: 'whisper-1',
                defaultVoice: 'alloy'
            }
        };
        
        const created = await adminClient.createAudioProvider(openaiAudio);
        console.log('Audio provider configured:', created.providerName);
        
        // Test the provider
        const testResult = await adminClient.testAudioProvider(created.id, 'transcription');
        console.log('Provider test:', testResult);
        
        // Configure costs
        const whisperCost: Omit<AudioCostDto, 'id'> = {
            provider: 'openai',
            operationType: 'transcription',
            model: 'whisper-1',
            costPerMinute: 0.006,
            effectiveFrom: new Date().toISOString(),
            isActive: true
        };
        
        await adminClient.createAudioCost(whisperCost);
        console.log('Audio costs configured');
    } catch (error) {
        console.error('Error setting up audio providers:', error);
    }
}
```

### Monitor Audio Usage

```typescript
async function monitorAudioUsage() {
    try {
        // Get usage logs
        const usage = await adminClient.getAudioUsageLogs({
            pageNumber: 1,
            pageSize: 20,
            operationType: 'transcription'
        });
        
        console.log('Recent Audio Usage:');
        usage.items.forEach(item => {
            console.log(`\n[${item.timestamp}]`);
            console.log(`Provider: ${item.provider}`);
            console.log(`Operation: ${item.operationType}`);
            console.log(`Duration: ${item.durationSeconds}s`);
            console.log(`Cost: $${item.cost.toFixed(4)}`);
        });
        
        // Get usage summary
        const endDate = new Date();
        const startDate = new Date();
        startDate.setDate(startDate.getDate() - 30);
        
        const summary = await adminClient.getAudioUsageSummary(startDate, endDate);
        console.log('\n30-Day Audio Summary:', summary);
    } catch (error) {
        console.error('Error monitoring audio usage:', error);
    }
}
```

### Manage Realtime Sessions

```typescript
async function manageRealtimeSessions() {
    try {
        // Get active sessions
        const activeSessions = await adminClient.getActiveRealtimeSessions();
        
        console.log(`Active realtime sessions: ${activeSessions.length}`);
        
        activeSessions.forEach(session => {
            const duration = (Date.now() - new Date(session.startTime).getTime()) / 1000;
            console.log(`\nSession: ${session.sessionId}`);
            console.log(`Provider: ${session.provider}`);
            console.log(`Duration: ${Math.floor(duration)}s`);
            console.log(`Current cost: $${session.totalCost.toFixed(2)}`);
        });
        
        // Get session metrics
        const metrics = await adminClient.getRealtimeSessionMetrics();
        console.log('\nRealtime Metrics:', metrics);
        
        // Terminate a session if needed
        if (activeSessions.length > 0 && activeSessions[0].totalCost > 100) {
            await adminClient.terminateRealtimeSession(activeSessions[0].sessionId);
            console.log('Terminated expensive session');
        }
    } catch (error) {
        console.error('Error managing realtime sessions:', error);
    }
}
```

## Router Configuration

### Configure Intelligent Routing

```typescript
async function configureRouter() {
    try {
        // Enable router with circuit breaker
        const routerConfig: RouterConfig = {
            fallbacksEnabled: true,
            retryEnabled: true,
            maxRetries: 3,
            retryDelayMs: 1000,
            circuitBreakerEnabled: true,
            circuitBreakerThreshold: 5,
            circuitBreakerResetTimeMs: 30000
        };
        
        await adminClient.updateRouterConfig(routerConfig);
        console.log('Router configuration updated');
        
        // Configure model deployment
        const deployment: ModelDeployment = {
            modelName: 'gpt-4',
            providers: ['openai-primary', 'openai-secondary', 'azure-openai'],
            loadBalancingStrategy: 'LeastLatency',
            isActive: true
        };
        
        await adminClient.saveModelDeployment(deployment);
        console.log('Model deployment configured');
        
        // Set up fallbacks
        const fallback: FallbackConfiguration = {
            primaryModelDeploymentId: 'gpt-4',
            fallbackModelDeploymentIds: ['gpt-3.5-turbo', 'claude-2']
        };
        
        await adminClient.setFallbackConfiguration(fallback);
        console.log('Fallback configuration set');
    } catch (error) {
        console.error('Error configuring router:', error);
    }
}
```

### Monitor Router Performance

```typescript
async function monitorRouterPerformance() {
    try {
        // Get all deployments
        const deployments = await adminClient.getAllModelDeployments();
        
        console.log('Active Model Deployments:');
        deployments.forEach(deployment => {
            console.log(`\n${deployment.modelName}:`);
            console.log(`- Providers: ${deployment.providers.join(', ')}`);
            console.log(`- Strategy: ${deployment.loadBalancingStrategy}`);
            console.log(`- Status: ${deployment.isActive ? 'Active' : 'Inactive'}`);
        });
        
        // Get fallback configurations
        const fallbacks = await adminClient.getAllFallbackConfigurations();
        
        console.log('\nFallback Chains:');
        fallbacks.forEach(config => {
            console.log(`${config.primaryModelDeploymentId} -> ${config.fallbackModelDeploymentIds.join(' -> ')}`);
        });
    } catch (error) {
        console.error('Error monitoring router:', error);
    }
}
```

## System Management

### System Health Check

```typescript
async function performHealthCheck() {
    try {
        // Check system info
        const systemInfo = await adminClient.getSystemInfo();
        
        console.log('=== System Information ===');
        console.log(`Version: ${systemInfo.version}`);
        console.log(`Environment: ${systemInfo.environment}`);
        console.log(`Uptime: ${systemInfo.uptime}`);
        console.log(`Memory: ${systemInfo.memoryUsage.used}MB / ${systemInfo.memoryUsage.total}MB`);
        console.log(`Disk: ${systemInfo.diskUsage.used}GB / ${systemInfo.diskUsage.total}GB`);
        
        // Check all providers
        const providerStatus = await adminClient.checkAllProvidersStatus();
        
        console.log('\n=== Provider Status ===');
        Object.entries(providerStatus).forEach(([provider, status]) => {
            console.log(`${provider}: ${status}`);
        });
        
        // Check global settings
        const settings = await adminClient.getAllGlobalSettings();
        console.log(`\nGlobal Settings: ${settings.length} configured`);
    } catch (error) {
        console.error('Error performing health check:', error);
    }
}
```

### Backup Management

```typescript
async function manageBackups() {
    try {
        // Create a backup
        console.log('Creating database backup...');
        await adminClient.createDatabaseBackup();
        
        // Get download URL
        const downloadUrl = await adminClient.getDatabaseBackupDownloadUrl();
        console.log('Backup created successfully');
        console.log(`Download URL: ${downloadUrl}`);
        
        // Download the backup (Node.js)
        const https = require('https');
        const fs = require('fs');
        
        const file = fs.createWriteStream('conduit_backup.db');
        https.get(downloadUrl, (response) => {
            response.pipe(file);
            file.on('finish', () => {
                file.close();
                console.log('Backup downloaded to conduit_backup.db');
            });
        });
    } catch (error) {
        console.error('Error managing backups:', error);
    }
}
```

### Configure Global Settings

```typescript
async function configureGlobalSettings() {
    try {
        // Set maintenance mode
        await adminClient.upsertGlobalSetting({
            key: 'MaintenanceMode',
            value: 'false',
            description: 'Enable/disable maintenance mode'
        });
        
        // Set rate limiting
        await adminClient.upsertGlobalSetting({
            key: 'GlobalRateLimit',
            value: '1000',
            description: 'Global rate limit per minute'
        });
        
        // Set default timeout
        await adminClient.upsertGlobalSetting({
            key: 'DefaultTimeout',
            value: '30000',
            description: 'Default request timeout in milliseconds'
        });
        
        console.log('Global settings configured');
        
        // Read a setting
        const maintenanceMode = await adminClient.getGlobalSettingByKey('MaintenanceMode');
        console.log(`Maintenance mode: ${maintenanceMode.value}`);
    } catch (error) {
        console.error('Error configuring global settings:', error);
    }
}
```

## Advanced Patterns

### Batch Operations

```typescript
async function batchCreateKeys(keyConfigs: CreateVirtualKeyRequest[]) {
    const results = await Promise.allSettled(
        keyConfigs.map(config => adminClient.createVirtualKey(config))
    );

    results.forEach((result, index) => {
        if (result.status === 'fulfilled') {
            console.log(`✓ Created key: ${result.value.keyInfo.keyName}`);
        } else {
            console.error(`✗ Failed to create key ${index + 1}:`, result.reason);
        }
    });
}

// Usage
const keyConfigs: CreateVirtualKeyRequest[] = [
    { keyName: 'Dev Team Key', maxBudget: 200 },
    { keyName: 'QA Team Key', maxBudget: 100 },
    { keyName: 'Production Key', maxBudget: 1000 }
];

await batchCreateKeys(keyConfigs);
```

### Monitor Key Usage

```typescript
async function monitorKeyUsage() {
    try {
        const keys = await adminClient.getAllVirtualKeys();
        
        console.log('Virtual Key Usage Report:');
        console.log('========================');
        
        keys.forEach(key => {
            const usagePercent = key.maxBudget ? (key.currentSpend / key.maxBudget * 100) : 0;
            const status = usagePercent > 90 ? '🔴 CRITICAL' : 
                          usagePercent > 75 ? '🟡 WARNING' : '🟢 OK';
            
            console.log(`${status} ${key.keyName}`);
            console.log(`  Spend: $${key.currentSpend.toFixed(2)}/$${key.maxBudget?.toFixed(2) || 'Unlimited'} (${usagePercent.toFixed(1)}%)`);
            console.log(`  Status: ${key.isEnabled ? 'Active' : 'Inactive'}`);
            console.log('');
        });
    } catch (error) {
        console.error('Error monitoring key usage:', error);
    }
}

// Usage
await monitorKeyUsage();
```

### Event Streaming with Server-Sent Events

```typescript
class ConduitSSEClient {
    private eventSource: EventSource | null = null;
    private reconnectInterval: number = 5000;
    private maxReconnectAttempts: number = 10;
    private reconnectAttempts: number = 0;

    constructor(
        private url: string,
        private masterKey: string
    ) {}

    connect() {
        const headers = new Headers();
        headers.append('X-API-Key', this.masterKey);
        
        this.eventSource = new EventSource(this.url, {
            headers: headers as any // TypeScript workaround
        });

        this.eventSource.onopen = () => {
            console.log('SSE connection established');
            this.reconnectAttempts = 0;
        };

        this.eventSource.onmessage = (event) => {
            const data = JSON.parse(event.data);
            this.handleMessage(data);
        };

        this.eventSource.onerror = (error) => {
            console.error('SSE error:', error);
            this.reconnect();
        };

        // Custom event handlers
        this.eventSource.addEventListener('cost-update', (event: any) => {
            const costData = JSON.parse(event.data);
            console.log('Cost update:', costData);
        });

        this.eventSource.addEventListener('health-status', (event: any) => {
            const healthData = JSON.parse(event.data);
            console.log('Health status:', healthData);
        });
    }

    private handleMessage(data: any) {
        console.log('Received message:', data);
    }

    private reconnect() {
        if (this.reconnectAttempts >= this.maxReconnectAttempts) {
            console.error('Max reconnection attempts reached');
            return;
        }

        this.reconnectAttempts++;
        console.log(`Reconnecting... (attempt ${this.reconnectAttempts})`);

        setTimeout(() => {
            this.disconnect();
            this.connect();
        }, this.reconnectInterval);
    }

    disconnect() {
        if (this.eventSource) {
            this.eventSource.close();
            this.eventSource = null;
        }
    }
}

// Usage
const sseClient = new ConduitSSEClient(
    'http://localhost:5002/api/events',
    'your_master_key'
);
sseClient.connect();
```

### Circuit Breaker Pattern

```typescript
class CircuitBreaker {
    private failures: number = 0;
    private lastFailureTime: number = 0;
    private state: 'CLOSED' | 'OPEN' | 'HALF_OPEN' = 'CLOSED';
    
    constructor(
        private threshold: number = 5,
        private timeout: number = 60000, // 1 minute
        private resetTimeout: number = 30000 // 30 seconds
    ) {}

    async execute<T>(operation: () => Promise<T>): Promise<T> {
        if (this.state === 'OPEN') {
            const timeSinceLastFailure = Date.now() - this.lastFailureTime;
            if (timeSinceLastFailure > this.timeout) {
                this.state = 'HALF_OPEN';
            } else {
                throw new Error('Circuit breaker is OPEN');
            }
        }

        try {
            const result = await operation();
            this.onSuccess();
            return result;
        } catch (error) {
            this.onFailure();
            throw error;
        }
    }

    private onSuccess() {
        this.failures = 0;
        this.state = 'CLOSED';
    }

    private onFailure() {
        this.failures++;
        this.lastFailureTime = Date.now();
        
        if (this.failures >= this.threshold) {
            this.state = 'OPEN';
            console.log('Circuit breaker opened due to failures');
        }
    }

    getState(): string {
        return this.state;
    }
}

// Usage with Admin API Client
class ResilientAdminApiClient extends ConduitAdminApiClient {
    private circuitBreaker = new CircuitBreaker();

    async getAllVirtualKeys(): Promise<VirtualKeyDto[]> {
        return this.circuitBreaker.execute(() => 
            super.getAllVirtualKeys()
        );
    }
}
```

### Rate Limiting

```typescript
class RateLimiter {
    private requests: number[] = [];
    
    constructor(
        private maxRequests: number,
        private windowMs: number
    ) {}

    async acquire(): Promise<void> {
        const now = Date.now();
        
        // Remove old requests outside the window
        this.requests = this.requests.filter(time => 
            now - time < this.windowMs
        );

        if (this.requests.length >= this.maxRequests) {
            const oldestRequest = this.requests[0];
            const waitTime = this.windowMs - (now - oldestRequest);
            
            console.log(`Rate limit reached. Waiting ${waitTime}ms`);
            await new Promise(resolve => setTimeout(resolve, waitTime));
            
            return this.acquire(); // Retry
        }

        this.requests.push(now);
    }
}

// Usage
const rateLimiter = new RateLimiter(10, 60000); // 10 requests per minute

async function rateLimitedOperation() {
    await rateLimiter.acquire();
    // Perform API operation
    return adminClient.getAllVirtualKeys();
}
```

### Caching Layer

```typescript
class CachedAdminApiClient extends ConduitAdminApiClient {
    private cache = new Map<string, { data: any; timestamp: number }>();
    private defaultTTL = 5 * 60 * 1000; // 5 minutes

    private getCacheKey(method: string, ...args: any[]): string {
        return `${method}:${JSON.stringify(args)}`;
    }

    private getFromCache<T>(key: string): T | null {
        const cached = this.cache.get(key);
        if (!cached) return null;

        if (Date.now() - cached.timestamp > this.defaultTTL) {
            this.cache.delete(key);
            return null;
        }

        return cached.data as T;
    }

    private setCache(key: string, data: any): void {
        this.cache.set(key, {
            data,
            timestamp: Date.now()
        });
    }

    async getAllVirtualKeys(): Promise<VirtualKeyDto[]> {
        const cacheKey = this.getCacheKey('getAllVirtualKeys');
        const cached = this.getFromCache<VirtualKeyDto[]>(cacheKey);
        
        if (cached) {
            console.log('Returning cached virtual keys');
            return cached;
        }

        const result = await super.getAllVirtualKeys();
        this.setCache(cacheKey, result);
        return result;
    }

    invalidateCache(pattern?: string): void {
        if (!pattern) {
            this.cache.clear();
            return;
        }

        for (const key of this.cache.keys()) {
            if (key.includes(pattern)) {
                this.cache.delete(key);
            }
        }
    }
}
```

### Monitoring and Metrics

```typescript
class MetricsCollector {
    private metrics: Map<string, any[]> = new Map();

    recordLatency(operation: string, duration: number): void {
        if (!this.metrics.has(operation)) {
            this.metrics.set(operation, []);
        }
        
        this.metrics.get(operation)!.push({
            timestamp: Date.now(),
            duration
        });
    }

    recordError(operation: string, error: Error): void {
        const key = `${operation}_errors`;
        if (!this.metrics.has(key)) {
            this.metrics.set(key, []);
        }
        
        this.metrics.get(key)!.push({
            timestamp: Date.now(),
            error: error.message
        });
    }

    getStats(operation: string): any {
        const latencies = this.metrics.get(operation) || [];
        const errors = this.metrics.get(`${operation}_errors`) || [];
        
        if (latencies.length === 0) {
            return { operation, noData: true };
        }

        const durations = latencies.map(m => m.duration);
        const avg = durations.reduce((a, b) => a + b, 0) / durations.length;
        const max = Math.max(...durations);
        const min = Math.min(...durations);

        return {
            operation,
            count: latencies.length,
            errors: errors.length,
            latency: {
                avg: avg.toFixed(2),
                min,
                max
            }
        };
    }
}

// Instrumented client
class InstrumentedAdminApiClient extends ConduitAdminApiClient {
    private metrics = new MetricsCollector();

    private async instrumentedCall<T>(
        operation: string,
        call: () => Promise<T>
    ): Promise<T> {
        const start = Date.now();
        
        try {
            const result = await call();
            this.metrics.recordLatency(operation, Date.now() - start);
            return result;
        } catch (error) {
            this.metrics.recordError(operation, error as Error);
            throw error;
        }
    }

    async getAllVirtualKeys(): Promise<VirtualKeyDto[]> {
        return this.instrumentedCall(
            'getAllVirtualKeys',
            () => super.getAllVirtualKeys()
        );
    }

    getMetrics(): any {
        return {
            getAllVirtualKeys: this.metrics.getStats('getAllVirtualKeys'),
            // Add more operations as needed
        };
    }
}
```

## Error Handling

### Comprehensive Error Handling

```typescript
class ApiError extends Error {
    constructor(
        message: string,
        public status?: number,
        public code?: string,
        public details?: any
    ) {
        super(message);
        this.name = 'ApiError';
    }
}

class ErrorHandler {
    static async handle<T>(
        operation: () => Promise<T>,
        context: string
    ): Promise<T> {
        try {
            return await operation();
        } catch (error) {
            if (error instanceof ApiError) {
                return this.handleApiError(error, context);
            }
            
            if (error instanceof TypeError && error.message.includes('fetch')) {
                throw new ApiError(
                    'Network error - unable to reach the API',
                    0,
                    'NETWORK_ERROR'
                );
            }
            
            throw new ApiError(
                `Unexpected error in ${context}`,
                undefined,
                'UNKNOWN_ERROR',
                error
            );
        }
    }

    private static handleApiError(error: ApiError, context: string): never {
        switch (error.status) {
            case 400:
                console.error(`Bad Request in ${context}:`, error.details);
                throw new ApiError(
                    'Invalid request parameters',
                    400,
                    'BAD_REQUEST',
                    error.details
                );
                
            case 401:
                console.error(`Authentication failed in ${context}`);
                throw new ApiError(
                    'Invalid or missing authentication credentials',
                    401,
                    'UNAUTHORIZED'
                );
                
            case 403:
                console.error(`Access denied in ${context}`);
                throw new ApiError(
                    'Insufficient permissions for this operation',
                    403,
                    'FORBIDDEN'
                );
                
            case 404:
                console.error(`Resource not found in ${context}`);
                throw new ApiError(
                    'The requested resource does not exist',
                    404,
                    'NOT_FOUND'
                );
                
            case 409:
                console.error(`Conflict in ${context}`);
                throw new ApiError(
                    'Resource conflict - the operation cannot be completed',
                    409,
                    'CONFLICT'
                );
                
            case 429:
                console.error(`Rate limit exceeded in ${context}`);
                throw new ApiError(
                    'Too many requests - please slow down',
                    429,
                    'RATE_LIMITED'
                );
                
            case 500:
            case 502:
            case 503:
            case 504:
                console.error(`Server error in ${context}`);
                throw new ApiError(
                    'Server error - please try again later',
                    error.status,
                    'SERVER_ERROR'
                );
                
            default:
                throw error;
        }
    }
}

// Usage
async function createVirtualKeyWithErrorHandling() {
    try {
        const result = await ErrorHandler.handle(
            () => adminClient.createVirtualKey({
                keyName: 'New API Key',
                maxBudget: 100
            }),
            'createVirtualKey'
        );
        
        console.log('Key created successfully:', result.virtualKey);
        return result;
    } catch (error) {
        if (error instanceof ApiError) {
            // Handle specific error codes
            switch (error.code) {
                case 'UNAUTHORIZED':
                    // Redirect to login or refresh token
                    console.log('Please check your master key');
                    break;
                    
                case 'RATE_LIMITED':
                    // Implement retry logic
                    console.log('Waiting before retry...');
                    break;
                    
                case 'BAD_REQUEST':
                    // Show validation errors
                    console.log('Validation failed:', error.details);
                    break;
                    
                default:
                    console.error('Operation failed:', error.message);
            }
        }
        throw error;
    }
}
```

### Retry with Exponential Backoff

```typescript
class RetryHandler {
    static async withRetry<T>(
        operation: () => Promise<T>,
        options: {
            maxRetries?: number;
            initialDelay?: number;
            maxDelay?: number;
            backoffFactor?: number;
            retryableStatuses?: number[];
        } = {}
    ): Promise<T> {
        const {
            maxRetries = 3,
            initialDelay = 1000,
            maxDelay = 30000,
            backoffFactor = 2,
            retryableStatuses = [429, 500, 502, 503, 504]
        } = options;

        let lastError: Error | null = null;
        
        for (let attempt = 0; attempt <= maxRetries; attempt++) {
            try {
                return await operation();
            } catch (error) {
                lastError = error as Error;
                
                // Check if error is retryable
                const isRetryable = error instanceof ApiError && 
                    error.status && 
                    retryableStatuses.includes(error.status);
                
                if (!isRetryable || attempt === maxRetries) {
                    throw error;
                }
                
                // Calculate delay with exponential backoff
                const delay = Math.min(
                    initialDelay * Math.pow(backoffFactor, attempt),
                    maxDelay
                );
                
                console.log(`Retry attempt ${attempt + 1}/${maxRetries} after ${delay}ms`);
                await new Promise(resolve => setTimeout(resolve, delay));
            }
        }
        
        throw lastError || new Error('Retry failed');
    }
}

// Usage
const keys = await RetryHandler.withRetry(
    () => adminClient.getAllVirtualKeys(),
    {
        maxRetries: 5,
        initialDelay: 500,
        retryableStatuses: [429, 500, 502, 503]
    }
);
```

## Node.js Setup

For Node.js environments, ensure you have the required dependencies:

```bash
npm install node-fetch @types/node-fetch
```

```typescript
// For Node.js < 18, you may need to polyfill fetch
import fetch from 'node-fetch';
globalThis.fetch = fetch as any;
```

## Environment Configuration

Use environment variables for configuration:

```typescript
const config = {
    adminApiUrl: process.env.CONDUIT_ADMIN_API_URL || 'http://localhost:5002',
    masterKey: process.env.CONDUIT_MASTER_KEY || '',
};

const adminClient = new ConduitAdminApiClient(config.adminApiUrl, config.masterKey);
```

## Production Considerations

### Security Best Practices

1. **Key Management**
   - Store master keys in secure environment variables or secret management systems
   - Never expose master keys in client-side code or version control
   - Rotate keys regularly and implement key versioning
   - Use separate keys for different environments (dev, staging, prod)

2. **Network Security**
   - Always use HTTPS in production
   - Implement certificate pinning for additional security
   - Use VPN or private networks when possible
   - Implement IP whitelisting for API access

3. **Authentication & Authorization**
   - Implement proper authentication flows
   - Use short-lived tokens where possible
   - Implement role-based access control (RBAC)
   - Audit all administrative actions

### Performance Optimization

1. **Connection Pooling**
   ```typescript
   // Use a shared HTTP agent for connection pooling
   import { Agent } from 'https';
   
   const httpsAgent = new Agent({
       keepAlive: true,
       maxSockets: 10,
       maxFreeSockets: 5,
       timeout: 60000,
       freeSocketTimeout: 30000
   });
   ```

2. **Request Batching**
   ```typescript
   class BatchProcessor {
       private queue: Array<{ operation: () => Promise<any>, resolve: Function, reject: Function }> = [];
       private processing = false;
       
       async add<T>(operation: () => Promise<T>): Promise<T> {
           return new Promise((resolve, reject) => {
               this.queue.push({ operation, resolve, reject });
               this.process();
           });
       }
       
       private async process() {
           if (this.processing || this.queue.length === 0) return;
           
           this.processing = true;
           const batch = this.queue.splice(0, 10); // Process 10 at a time
           
           await Promise.all(
               batch.map(async ({ operation, resolve, reject }) => {
                   try {
                       const result = await operation();
                       resolve(result);
                   } catch (error) {
                       reject(error);
                   }
               })
           );
           
           this.processing = false;
           if (this.queue.length > 0) {
               setTimeout(() => this.process(), 100);
           }
       }
   }
   ```

3. **Response Compression**
   ```typescript
   // Enable gzip compression for responses
   const headers = {
       'X-API-Key': masterKey,
       'Content-Type': 'application/json',
       'Accept-Encoding': 'gzip, deflate'
   };
   ```

### Monitoring & Observability

1. **Structured Logging**
   ```typescript
   import winston from 'winston';
   
   const logger = winston.createLogger({
       level: 'info',
       format: winston.format.json(),
       defaultMeta: { service: 'conduit-admin-client' },
       transports: [
           new winston.transports.File({ filename: 'error.log', level: 'error' }),
           new winston.transports.File({ filename: 'combined.log' })
       ]
   });
   ```

2. **Metrics Collection**
   ```typescript
   import { StatsD } from 'node-statsd';
   
   const metrics = new StatsD({
       host: 'localhost',
       port: 8125,
       prefix: 'conduit.admin.'
   });
   
   // Track API call metrics
   async function trackApiCall<T>(
       operation: string,
       call: () => Promise<T>
   ): Promise<T> {
       const start = Date.now();
       
       try {
           const result = await call();
           metrics.timing(`${operation}.duration`, Date.now() - start);
           metrics.increment(`${operation}.success`);
           return result;
       } catch (error) {
           metrics.timing(`${operation}.duration`, Date.now() - start);
           metrics.increment(`${operation}.error`);
           throw error;
       }
   }
   ```

3. **Health Checks**
   ```typescript
   class HealthChecker {
       async checkHealth(): Promise<{
           status: 'healthy' | 'degraded' | 'unhealthy';
           checks: Record<string, boolean>;
           timestamp: string;
       }> {
           const checks: Record<string, boolean> = {};
           
           // Check API connectivity
           try {
               await adminClient.getAllGlobalSettings();
               checks.apiConnectivity = true;
           } catch {
               checks.apiConnectivity = false;
           }
           
           // Check authentication
           try {
               await adminClient.getAllVirtualKeys();
               checks.authentication = true;
           } catch {
               checks.authentication = false;
           }
           
           // Determine overall status
           const failedChecks = Object.values(checks).filter(v => !v).length;
           const status = failedChecks === 0 ? 'healthy' : 
                          failedChecks === 1 ? 'degraded' : 'unhealthy';
           
           return {
               status,
               checks,
               timestamp: new Date().toISOString()
           };
       }
   }
   ```

### Error Recovery Strategies

1. **Graceful Degradation**
   ```typescript
   class GracefulClient {
       private cache = new Map<string, any>();
       private circuitBreaker = new CircuitBreaker();
       
       async getVirtualKeys(): Promise<VirtualKeyDto[]> {
           try {
               const keys = await this.circuitBreaker.execute(() => 
                   adminClient.getAllVirtualKeys()
               );
               this.cache.set('virtualKeys', keys);
               return keys;
           } catch (error) {
               // Return cached data if available
               const cached = this.cache.get('virtualKeys');
               if (cached) {
                   console.warn('Returning cached data due to error:', error);
                   return cached;
               }
               throw error;
           }
       }
   }
   ```

2. **Automatic Failover**
   ```typescript
   class MultiRegionClient {
       private clients: ConduitAdminApiClient[];
       private currentIndex = 0;
       
       constructor(endpoints: string[], masterKey: string) {
           this.clients = endpoints.map(endpoint => 
               new ConduitAdminApiClient(endpoint, masterKey)
           );
       }
       
       async execute<T>(operation: (client: ConduitAdminApiClient) => Promise<T>): Promise<T> {
           const startIndex = this.currentIndex;
           
           do {
               try {
                   return await operation(this.clients[this.currentIndex]);
               } catch (error) {
                   console.error(`Failed on endpoint ${this.currentIndex}:`, error);
                   this.currentIndex = (this.currentIndex + 1) % this.clients.length;
               }
           } while (this.currentIndex !== startIndex);
           
           throw new Error('All endpoints failed');
       }
   }
   ```

### Testing Strategies

1. **Unit Testing with Mocks**
   ```typescript
   import { jest } from '@jest/globals';
   
   describe('VirtualKeyService', () => {
       let mockClient: jest.Mocked<ConduitAdminApiClient>;
       
       beforeEach(() => {
           mockClient = {
               getAllVirtualKeys: jest.fn(),
               createVirtualKey: jest.fn(),
               // ... other methods
           } as any;
       });
       
       test('should create virtual key successfully', async () => {
           const mockResponse: CreateVirtualKeyResponse = {
               virtualKey: 'vk_test123',
               keyInfo: {
                   id: 1,
                   keyName: 'Test Key',
                   // ... other properties
               } as VirtualKeyDto
           };
           
           mockClient.createVirtualKey.mockResolvedValue(mockResponse);
           
           const result = await mockClient.createVirtualKey({
               keyName: 'Test Key'
           });
           
           expect(result.virtualKey).toBe('vk_test123');
           expect(mockClient.createVirtualKey).toHaveBeenCalledWith({
               keyName: 'Test Key'
           });
       });
   });
   ```

2. **Integration Testing**
   ```typescript
   describe('Admin API Integration', () => {
       let client: ConduitAdminApiClient;
       
       beforeAll(() => {
           client = new ConduitAdminApiClient(
               process.env.TEST_API_URL!,
               process.env.TEST_MASTER_KEY!
           );
       });
       
       test('full virtual key lifecycle', async () => {
           // Create
           const created = await client.createVirtualKey({
               keyName: `test_${Date.now()}`,
               maxBudget: 10
           });
           expect(created.virtualKey).toBeTruthy();
           
           // Read
           const retrieved = await client.getVirtualKeyById(created.keyInfo.id);
           expect(retrieved.keyName).toBe(created.keyInfo.keyName);
           
           // Update
           await client.updateVirtualKey(created.keyInfo.id, {
               maxBudget: 20
           });
           
           // Verify update
           const updated = await client.getVirtualKeyById(created.keyInfo.id);
           expect(updated.maxBudget).toBe(20);
           
           // Delete
           await client.deleteVirtualKey(created.keyInfo.id);
       });
   });
   ```

### Deployment Patterns

1. **SDK Package Structure**
   ```json
   {
     "name": "@your-org/conduit-admin-sdk",
     "version": "1.0.0",
     "main": "dist/index.js",
     "types": "dist/index.d.ts",
     "exports": {
       ".": {
         "import": "./dist/index.js",
         "require": "./dist/index.cjs",
         "types": "./dist/index.d.ts"
       }
     },
     "scripts": {
       "build": "tsup src/index.ts --format cjs,esm --dts",
       "test": "jest",
       "lint": "eslint src --ext .ts"
     }
   }
   ```

2. **Docker Integration**
   ```dockerfile
   FROM node:18-alpine
   
   WORKDIR /app
   
   # Copy package files
   COPY package*.json ./
   RUN npm ci --only=production
   
   # Copy application
   COPY dist ./dist
   
   # Set environment variables
   ENV CONDUIT_ADMIN_API_URL=http://conduit-api:5002
   
   # Run the application
   CMD ["node", "dist/index.js"]
   ```

### Compliance & Auditing

1. **Audit Trail Implementation**
   ```typescript
   class AuditedAdminClient extends ConduitAdminApiClient {
       private auditLog: Array<{
           timestamp: Date;
           operation: string;
           user: string;
           params: any;
           result: 'success' | 'failure';
           error?: string;
       }> = [];
       
       private async auditOperation<T>(
           operation: string,
           params: any,
           call: () => Promise<T>
       ): Promise<T> {
           const timestamp = new Date();
           const user = process.env.AUDIT_USER || 'system';
           
           try {
               const result = await call();
               this.auditLog.push({
                   timestamp,
                   operation,
                   user,
                   params,
                   result: 'success'
               });
               return result;
           } catch (error) {
               this.auditLog.push({
                   timestamp,
                   operation,
                   user,
                   params,
                   result: 'failure',
                   error: String(error)
               });
               throw error;
           }
       }
       
       async createVirtualKey(request: CreateVirtualKeyRequest): Promise<CreateVirtualKeyResponse> {
           return this.auditOperation(
               'createVirtualKey',
               request,
               () => super.createVirtualKey(request)
           );
       }
       
       getAuditLog() {
           return [...this.auditLog];
       }
   }
   ```

2. **Data Privacy Compliance**
   ```typescript
   class PrivacyCompliantClient extends ConduitAdminApiClient {
       // Redact sensitive information
       private redactSensitiveData(data: any): any {
           if (typeof data !== 'object' || data === null) return data;
           
           const redacted = { ...data };
           
           // Redact API keys
           if ('apiKey' in redacted) {
               redacted.apiKey = redacted.apiKey ? '***REDACTED***' : null;
           }
           
           // Redact virtual keys
           if ('virtualKey' in redacted) {
               redacted.virtualKey = redacted.virtualKey.substring(0, 8) + '...';
           }
           
           return redacted;
       }
       
       async getAllVirtualKeys(): Promise<VirtualKeyDto[]> {
           const keys = await super.getAllVirtualKeys();
           return keys.map(key => this.redactSensitiveData(key));
       }
   }
   ```

## Conclusion

This comprehensive TypeScript SDK for the Conduit Admin API provides:

- **Complete API Coverage**: All endpoints are implemented with proper typing
- **Enterprise Features**: Resilience, monitoring, and security built-in
- **Production Ready**: Error handling, retry logic, and circuit breakers
- **Developer Friendly**: Clear examples and comprehensive documentation
- **Extensible Design**: Easy to add custom functionality and middleware

For the latest updates and contributions, visit the [Conduit GitHub repository](https://github.com/your-org/conduit).

### Quick Start Checklist

- [ ] Install dependencies: `npm install node-fetch @types/node-fetch`
- [ ] Set environment variables: `CONDUIT_ADMIN_API_URL` and `CONDUIT_MASTER_KEY`
- [ ] Initialize the client with proper error handling
- [ ] Implement rate limiting for production use
- [ ] Set up monitoring and logging
- [ ] Test all critical operations
- [ ] Document your usage patterns

Happy coding with Conduit! 🚀