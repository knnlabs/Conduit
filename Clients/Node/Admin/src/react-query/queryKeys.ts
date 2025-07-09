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
  analytics: () => [...adminQueryKeys.all, 'analytics'] as const,
  analyticsUsage: () => [...adminQueryKeys.analytics(), 'usage'] as const,
  analyticsExports: () => [...adminQueryKeys.analytics(), 'exports'] as const,
  
  // System
  system: () => [...adminQueryKeys.all, 'system'] as const,
  systemInfo: () => [...adminQueryKeys.system(), 'info'] as const,
  systemHealth: () => [...adminQueryKeys.system(), 'health'] as const,
  systemSettings: () => [...adminQueryKeys.system(), 'settings'] as const,
  
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