# Admin API TypeScript Client

A comprehensive TypeScript client for all Conduit Admin API endpoints with built-in retry logic, error handling, and type safety.

## Overview

This client provides a complete abstraction over the Conduit Admin API, handling authentication, retries, and type-safe method calls for all available endpoints.

## Related Documentation

- [TypeScript Setup Guide](./typescript-setup.md) - Authentication and type definitions
- [Virtual Keys Examples](./typescript-virtual-keys.md) - Virtual key management examples
- [Provider Management](./typescript-providers.md) - Provider configuration examples  
- [Analytics Guide](./typescript-analytics.md) - Request logs and cost analytics

## Complete Client Implementation

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

## Usage Example

```typescript
// Initialize the client
const adminClient = new ConduitAdminApiClient(
    'http://localhost:5002',
    'your_master_key_here',
    true // Use X-API-Key header (recommended)
);

// Example usage
async function example() {
    try {
        // Get all virtual keys
        const keys = await adminClient.getAllVirtualKeys();
        console.log(`Found ${keys.length} virtual keys`);

        // Get provider health summary
        const healthSummary = await adminClient.getProviderHealthSummary();
        console.log('Provider health:', healthSummary);

        // Get cost dashboard
        const costData = await adminClient.getCostDashboard({
            startDate: new Date(Date.now() - 7 * 24 * 60 * 60 * 1000), // 7 days ago
            endDate: new Date()
        });
        console.log('Weekly costs:', costData);

    } catch (error) {
        console.error('API Error:', error);
    }
}
```

## Error Handling

The client includes built-in retry logic and comprehensive error handling:

- **Automatic retries** for 5xx errors and rate limiting (429)
- **Exponential backoff** with configurable delay and multiplier
- **Network error handling** with retry for connection failures
- **Type-safe error responses** with HTTP status codes
- **Proper handling** of no-content (204) responses

## Client Features

- **Type Safety**: Full TypeScript type definitions for all methods and responses
- **Retry Logic**: Automatic retry with exponential backoff for failed requests
- **Authentication**: Support for both X-API-Key and Authorization headers
- **Error Handling**: Comprehensive error handling with detailed error messages
- **URL Safety**: Proper URL encoding for all parameters
- **Response Parsing**: Automatic JSON parsing with proper error handling
- **Method Coverage**: Complete coverage of all Admin API endpoints

## Next Steps

Continue with specific functionality examples:

- [Virtual Keys Management](./typescript-virtual-keys.md)
- [Provider Configuration](./typescript-providers.md)
- [Analytics and Logging](./typescript-analytics.md)
- [Advanced Error Handling](./typescript-advanced.md)