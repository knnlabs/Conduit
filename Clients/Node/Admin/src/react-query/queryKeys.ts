export const adminQueryKeys = {
  all: ['conduit-admin'] as const,
  
  // Providers
  providers: () => [...adminQueryKeys.all, 'providers'] as const,
  provider: (id: string) => [...adminQueryKeys.providers(), id] as const,
  providerModels: (id: string) => [...adminQueryKeys.provider(id), 'models'] as const,
  
  // Virtual Keys
  virtualKeys: () => [...adminQueryKeys.all, 'virtualKeys'] as const,
  virtualKey: (id: string) => [...adminQueryKeys.virtualKeys(), id] as const,
  
  // Model Mappings
  modelMappings: () => [...adminQueryKeys.all, 'modelMappings'] as const,
  modelMapping: (id: string) => [...adminQueryKeys.modelMappings(), id] as const,
  
  // Analytics
  analytics: {
    all: () => [...adminQueryKeys.all, 'analytics'] as const,
    costSummary: (dateRange: any) => 
      [...adminQueryKeys.analytics.all(), 'cost-summary', dateRange] as const,
    costByPeriod: (params: any) => 
      [...adminQueryKeys.analytics.all(), 'cost-by-period', params] as const,
    costByModel: (dateRange: any) => 
      [...adminQueryKeys.analytics.all(), 'cost-by-model', dateRange] as const,
    costByKey: (dateRange: any) => 
      [...adminQueryKeys.analytics.all(), 'cost-by-key', dateRange] as const,
    requestLogs: (filters?: any) => 
      [...adminQueryKeys.analytics.all(), 'request-logs', filters] as const,
    requestLog: (id: string) => 
      [...adminQueryKeys.analytics.all(), 'request-log', id] as const,
    searchLogs: (query: string, filters?: any) => 
      [...adminQueryKeys.analytics.all(), 'search-logs', query, filters] as const,
    usageMetrics: (dateRange: any) => 
      [...adminQueryKeys.analytics.all(), 'usage-metrics', dateRange] as const,
    modelUsage: (modelId: string, dateRange: any) => 
      [...adminQueryKeys.analytics.all(), 'model-usage', modelId, dateRange] as const,
    keyUsage: (keyId: number, dateRange: any) => 
      [...adminQueryKeys.analytics.all(), 'key-usage', keyId, dateRange] as const,
    exports: () => [...adminQueryKeys.analytics.all(), 'exports'] as const,
  },
  
  // System
  system: {
    all: () => [...adminQueryKeys.all, 'system'] as const,
    info: () => [...adminQueryKeys.system.all(), 'info'] as const,
    health: () => [...adminQueryKeys.system.all(), 'health'] as const,
    settings: () => [...adminQueryKeys.system.all(), 'settings'] as const,
    featureAvailability: () => [...adminQueryKeys.system.all(), 'features'] as const,
    backups: {
      all: () => [...adminQueryKeys.system.all(), 'backups'] as const,
      list: () => [...adminQueryKeys.system.backups.all(), 'list'] as const,
      detail: (id: string) => [...adminQueryKeys.system.backups.all(), id] as const,
    },
    notifications: {
      all: () => [...adminQueryKeys.system.all(), 'notifications'] as const,
      list: (filters?: any) => [...adminQueryKeys.system.notifications.all(), 'list', filters] as const,
      summary: () => [...adminQueryKeys.system.notifications.all(), 'summary'] as const,
    },
  },
  
  // Configuration
  configuration: () => [...adminQueryKeys.all, 'configuration'] as const,
  configurationRouting: () => [...adminQueryKeys.configuration(), 'routing'] as const,
  configurationCaching: () => [...adminQueryKeys.configuration(), 'caching'] as const,
  
  // Security
  security: () => [...adminQueryKeys.all, 'security'] as const,
  securityEvents: () => [...adminQueryKeys.security(), 'events'] as const,
  securityIpRules: () => [...adminQueryKeys.security(), 'ipRules'] as const,
  
  // Request Logs
  requestLogs: () => [...adminQueryKeys.all, 'requestLogs'] as const,
  
  // Media
  media: () => [...adminQueryKeys.all, 'media'] as const,
  mediaStorage: () => [...adminQueryKeys.media(), 'storage'] as const,
  
  // Metrics
  metrics: () => [...adminQueryKeys.all, 'metrics'] as const,
  
  // Notifications
  notifications: () => [...adminQueryKeys.all, 'notifications'] as const,
  
  // Database Backups
  databaseBackups: () => [...adminQueryKeys.all, 'databaseBackups'] as const,
  
  // Audio Configuration
  audioConfiguration: () => [...adminQueryKeys.all, 'audioConfiguration'] as const,
} as const;