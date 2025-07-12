import type { 
  VirtualKeyDto, 
  ProviderCredentialDto,
  ModelProviderMappingDto,
  ProviderHealthStatusDto,
  BudgetDuration
} from '@/types/api-types';

// Types that aren't in api-types.ts yet
interface HealthStatusDto {
  isHealthy: boolean;
  version?: string;
  timestamp?: string;
  services?: Array<{ name: string; isHealthy: boolean; version?: string; message?: string }>;
}

interface RequestLogDto {
  id?: string;
  virtualKeyId?: number;
  model?: string;
  provider?: string;
  requestType?: string;
  type?: string;
  responseTime?: number;
  timestamp?: string;
  statusCode?: number;
  latencyMs?: number;
  latency?: number;
  totalCost?: number;
  cost?: number;
  errorMessage?: string;
  error?: string;
  ipAddress?: string;
  clientIp?: string;
  tokenUsage?: any;
}

interface SystemInfoDto {
  version?: string;
}

interface ProviderHealthSummaryDto {
  totalProviders?: number;
  total?: number;
  healthyCount?: number;
  healthy?: number;
  unhealthyCount?: number;
  unhealthy?: number;
  averageResponseTimeMs?: number;
  averageResponseTime?: number;
  providers?: ProviderHealthStatusDto[];
}

// UI-specific types that extend SDK types
export interface UIVirtualKey extends Omit<VirtualKeyDto, 'keyName' | 'budgetDuration' | 'apiKey' | 'maxBudget' | 'isEnabled' | 'createdAt' | 'updatedAt' | 'expiresAt' | 'lastUsedAt' | 'metadata'> {
  name: string;
  key: string;
  isActive: boolean;
  budget: number;
  budgetPeriod: 'daily' | 'monthly' | 'total';
  allowedProviders: string[] | null;
  expirationDate: string | null;
  createdDate: string;
  modifiedDate: string;
  lastUsedDate: string | null;
  metadata: Record<string, unknown> | null;
  // Note: allowedModels is inherited from VirtualKeyDto as a string
}

export interface UIProvider extends Omit<ProviderCredentialDto, 'id' | 'providerName' | 'apiEndpoint' | 'additionalConfig' | 'createdAt' | 'updatedAt'> {
  id: string;
  name: string;
  type: string;
  endpoint?: string;
  supportedModels: string[];
  configuration: Record<string, unknown>;
  createdDate: string;
  modifiedDate: string;
}

export interface UIModelMapping extends Omit<ModelProviderMappingDto, 'id' | 'modelId' | 'isEnabled' | 'createdAt' | 'updatedAt' | 'metadata' | 'providerId'> {
  id: string;
  sourceModel: string;
  targetProvider: string;
  targetModel: string;
  isActive: boolean;
  metadata?: Record<string, unknown>;
  createdDate: string;
  modifiedDate: string;
  // All capability fields are inherited from ModelProviderMappingDto
}

export interface UIProviderHealth extends Omit<ProviderHealthStatusDto, 'providerId' | 'lastCheckTime' | 'responseTimeMs' | 'errorMessage'> {
  providerId: string;
  lastChecked: string;
  responseTime: number;
  lastError?: string;
  uptime: number;
  errorRate: number;
  incidents: Array<{
    id: string;
    providerId: string;
    startTime: string;
    endTime?: string;
    severity: 'low' | 'medium' | 'high' | 'critical';
    description: string;
    affectedModels: string[];
    status: 'active' | 'resolved';
  }>;
}

export interface UISystemHealth {
  status: 'healthy' | 'degraded' | 'unhealthy';
  version: string;
  uptime: number;
  services: Array<{
    name: string;
    status: 'healthy' | 'degraded' | 'unhealthy';
    version?: string;
    uptime?: number;
    lastCheck: string;
    message?: string;
  }>;
  dependencies: Array<{
    name: string;
    type: 'database' | 'cache' | 'queue' | 'external';
    status: 'connected' | 'disconnected' | 'error';
    latency?: number;
    lastCheck: string;
    error?: string;
  }>;
  timestamp: string;
}

export interface UIRequestLog {
  id: string;
  virtualKeyId?: number;
  virtualKeyName?: string;
  model?: string;
  provider?: string;
  requestType?: string;
  responseTime?: number;
  timestamp: string;
  statusCode?: number;
  latency: number;
  cost?: number;
  error?: string;
  clientIp?: string;
  tokenUsage?: {
    promptTokens?: number;
    completionTokens?: number;
    totalTokens?: number;
  };
}

// Mapping functions
export function mapVirtualKeyFromSDK(sdk: VirtualKeyDto): UIVirtualKey {
  return {
    ...sdk,
    name: sdk.keyName,
    // TODO: SDK should include keyHash or masked key for display purposes
    // Currently apiKey is only returned on creation, afterwards it's not available
    // Using keyPrefix as a workaround for display
    key: sdk.keyPrefix || sdk.apiKey || `key_${sdk.id}`, 
    isActive: sdk.isEnabled,
    budget: sdk.maxBudget || 0,
    budgetPeriod: (sdk.budgetDuration || 'Total').toLowerCase() as 'daily' | 'monthly' | 'total',
    // TODO: SDK should include allowedProviders field
    allowedProviders: null, // Not available in SDK
    expirationDate: sdk.expiresAt || null,
    createdDate: sdk.createdAt,
    modifiedDate: sdk.updatedAt,
    lastUsedDate: sdk.lastUsedAt || null,
    metadata: (() => {
      if (!sdk.metadata) return null;
      try {
        return JSON.parse(sdk.metadata);
      } catch (e) {
        console.warn('[mapVirtualKeyFromSDK] Failed to parse metadata:', sdk.metadata, e);
        // Return the raw string if parsing fails
        return { raw: sdk.metadata };
      }
    })(),
  };
}

export function mapVirtualKeyToSDK(ui: UIVirtualKey): Partial<VirtualKeyDto> {
  const { name, key, isActive, budget, budgetPeriod, expirationDate, createdDate, modifiedDate, lastUsedDate, metadata, allowedProviders, ...rest } = ui;
  return {
    ...rest,
    keyName: name,
    apiKey: key,
    isEnabled: isActive,
    maxBudget: budget,
    budgetDuration: (budgetPeriod.charAt(0).toUpperCase() + budgetPeriod.slice(1)) as BudgetDuration,
    expiresAt: expirationDate || undefined,
    createdAt: createdDate,
    updatedAt: modifiedDate,
    lastUsedAt: lastUsedDate || undefined,
    metadata: metadata ? JSON.stringify(metadata) : undefined,
  };
}

export function mapProviderFromSDK(sdk: ProviderCredentialDto): UIProvider {
  const additionalConfig = sdk.additionalConfig ? JSON.parse(sdk.additionalConfig) : {};
  return {
    ...sdk,
    id: sdk.id.toString(),
    name: sdk.providerName,
    type: sdk.providerName.toLowerCase(),
    endpoint: sdk.apiEndpoint,
    supportedModels: [], // Not available in SDK, would need to fetch separately
    configuration: {
      ...additionalConfig,
      ...(sdk.organizationId && { organizationId: sdk.organizationId }),
    },
    createdDate: sdk.createdAt,
    modifiedDate: sdk.updatedAt,
  };
}

export function mapProviderToSDK(ui: UIProvider): Partial<ProviderCredentialDto> {
  const { id, name, type, endpoint, supportedModels, configuration, createdDate, modifiedDate, ...rest } = ui;
  const { organizationId, ...additionalConfig } = configuration;
  
  return {
    ...rest,
    id: parseInt(id, 10),
    providerName: name,
    apiEndpoint: endpoint,
    organizationId: organizationId as string | undefined,
    additionalConfig: Object.keys(additionalConfig).length > 0 ? JSON.stringify(additionalConfig) : undefined,
    createdAt: createdDate,
    updatedAt: modifiedDate,
  };
}

export function mapModelMappingFromSDK(sdk: ModelProviderMappingDto): UIModelMapping {
  let parsedMetadata: Record<string, unknown> | undefined;
  if (sdk.metadata) {
    try {
      parsedMetadata = JSON.parse(sdk.metadata);
    } catch (e) {
      console.warn('[mapModelMappingFromSDK] Failed to parse metadata:', sdk.metadata, e);
      parsedMetadata = { raw: sdk.metadata };
    }
  }
  
  return {
    ...sdk,
    id: sdk.id.toString(),
    sourceModel: sdk.modelId,
    targetProvider: sdk.providerId,
    targetModel: sdk.providerModelId,
    isActive: sdk.isEnabled,
    metadata: parsedMetadata,
    createdDate: sdk.createdAt,
    modifiedDate: sdk.updatedAt,
  };
}

export function mapModelMappingToSDK(ui: UIModelMapping): Partial<ModelProviderMappingDto> {
  const { id, sourceModel, targetProvider, targetModel, isActive, metadata, createdDate, modifiedDate, ...rest } = ui;
  
  return {
    ...rest,
    id: parseInt(id, 10),
    modelId: sourceModel,
    providerId: targetProvider,
    providerModelId: targetModel,
    isEnabled: isActive,
    metadata: metadata ? JSON.stringify(metadata) : undefined,
    createdAt: createdDate,
    updatedAt: modifiedDate,
  };
}

// Convenience alias for mapModelMappingFromSDK
export const toUIModelMapping = mapModelMappingFromSDK;

export function mapProviderHealthFromSDK(sdk: ProviderHealthStatusDto): UIProviderHealth {
  const sdkAny = sdk as any;
  return {
    ...sdk,
    providerId: sdkAny.providerId?.toString() || sdkAny.id?.toString() || '', // Handle either field name
    lastChecked: sdkAny.lastCheckTime || sdkAny.lastChecked || new Date().toISOString(),
    responseTime: sdkAny.responseTimeMs || sdkAny.responseTime || 0,
    lastError: sdkAny.errorMessage || sdkAny.lastError,
    uptime: 99.9, // Not available in SDK
    errorRate: 0, // Not available in SDK
    incidents: [], // Not available in SDK
  };
}

export function mapSystemHealthFromSDK(sdk: HealthStatusDto, systemInfo?: SystemInfoDto): UISystemHealth {
  const sdkAny = sdk as any;
  return {
    status: sdkAny.isHealthy || sdkAny.status === 'healthy' ? 'healthy' : 'unhealthy',
    version: sdkAny.version || systemInfo?.version || 'unknown',
    uptime: 0, // Not available in SDK
    services: sdkAny.services?.map((service: any) => ({
      name: service.name,
      status: service.isHealthy || service.status === 'healthy' ? 'healthy' : 'unhealthy',
      version: service.version,
      uptime: undefined,
      lastCheck: new Date().toISOString(),
      message: service.message,
    })) || [],
    dependencies: [], // Not available in SDK
    timestamp: sdkAny.timestamp || new Date().toISOString(),
  };
}

export function mapRequestLogFromSDK(sdk: RequestLogDto): UIRequestLog {
  const sdkAny = sdk as any;
  return {
    id: sdkAny.id || '',
    virtualKeyId: sdkAny.virtualKeyId,
    virtualKeyName: undefined, // Would need to fetch separately
    model: sdkAny.model,
    provider: sdkAny.provider,
    requestType: sdkAny.requestType || sdkAny.type,
    responseTime: sdkAny.responseTime,
    timestamp: sdkAny.timestamp || new Date().toISOString(),
    statusCode: sdkAny.statusCode,
    latency: sdkAny.latencyMs || sdkAny.latency || 0,
    cost: sdkAny.totalCost || sdkAny.cost,
    error: sdkAny.errorMessage || sdkAny.error,
    clientIp: sdkAny.ipAddress || sdkAny.clientIp,
    tokenUsage: sdkAny.tokenUsage,
  };
}

export function mapProviderHealthSummaryFromSDK(sdk: ProviderHealthSummaryDto): {
  totalProviders: number;
  healthyProviders: number;
  degradedProviders: number;
  unhealthyProviders: number;
  averageResponseTime: number;
  averageUptime: number;
  providers: UIProviderHealth[];
} {
  const sdkAny = sdk as any;
  const healthyCount = sdkAny.healthyCount || sdkAny.healthy || 0;
  const unhealthyCount = sdkAny.unhealthyCount || sdkAny.unhealthy || 0;
  const totalCount = sdkAny.totalProviders || sdkAny.total || healthyCount + unhealthyCount;
  
  return {
    totalProviders: totalCount,
    healthyProviders: healthyCount,
    degradedProviders: 0, // Not available in SDK
    unhealthyProviders: unhealthyCount,
    averageResponseTime: sdkAny.averageResponseTimeMs || sdkAny.averageResponseTime || 0,
    averageUptime: 99.9, // Not available in SDK
    providers: sdkAny.providers?.map(mapProviderHealthFromSDK) || [],
  };
}