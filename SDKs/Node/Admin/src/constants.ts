// Import shared HTTP constants from Common package
import { HTTP_HEADERS, CONTENT_TYPES, HTTP_STATUS as COMMON_HTTP_STATUS } from '@knn_labs/conduit-common';
import { ProviderType } from './models/providerType';

// Re-export for backward compatibility
export { HTTP_HEADERS, CONTENT_TYPES };

export const API_VERSION = 'v1';
export const API_PREFIX = '/api';

/**
 * HTTP method constants for type-safe method specification.
 * @deprecated Use HttpMethod enum from '@knn_labs/conduit-common' instead
 */
export const HTTP_METHODS = {
  GET: 'GET',
  POST: 'POST',
  PUT: 'PUT',
  DELETE: 'DELETE',
  PATCH: 'PATCH',
} as const;

/**
 * Client information constants.
 */
export const CLIENT_INFO = {
  NAME: '@conduit/admin',
  VERSION: '0.1.0',
  USER_AGENT: '@conduit/admin/0.1.0',
} as const;


/**
 * Date format constants.
 */
export const DATE_FORMATS = {
  API_DATETIME: 'YYYY-MM-DDTHH:mm:ss[Z]',
  API_DATE: 'YYYY-MM-DD',
  DISPLAY_DATETIME: 'MMM D, YYYY [at] h:mm A',
  DISPLAY_DATE: 'MMM D, YYYY',
} as const;

export const ENDPOINTS = {
  // Virtual Keys
  VIRTUAL_KEYS: {
    BASE: '/api/VirtualKeys',
    BY_ID: (id: number) => `/api/VirtualKeys/${id}`,
    RESET_SPEND: (id: number) => `/api/VirtualKeys/${id}/reset-spend`,
    VALIDATE: '/api/VirtualKeys/validate',
    SPEND: (id: number) => `/api/VirtualKeys/${id}/spend`,
    REFUND: (id: number) => `/api/VirtualKeys/${id}/refund`,
    CHECK_BUDGET: (id: number) => `/api/VirtualKeys/${id}/check-budget`,
    VALIDATION_INFO: (id: number) => `/api/VirtualKeys/${id}/validation-info`,
    MAINTENANCE: '/api/VirtualKeys/maintenance',
    DISCOVERY_PREVIEW: (id: number) => `/api/VirtualKeys/${id}/discovery-preview`,
  },

  // Provider Credentials
  PROVIDERS: {
    BASE: '/api/ProviderCredentials',
    BY_ID: (id: number) => `/api/ProviderCredentials/${id}`,
    BY_NAME: (name: string) => `/api/ProviderCredentials/name/${name}`,
    NAMES: '/api/ProviderCredentials/names',
    TEST_BY_ID: (id: number) => `/api/ProviderCredentials/test/${id}`,
    TEST: '/api/ProviderCredentials/test',
  },

  // Provider Key Credentials
  PROVIDER_KEYS: {
    BASE: (providerId: number) => `/api/ProviderCredentials/${providerId}/keys`,
    BY_ID: (providerId: number, keyId: number) => `/api/ProviderCredentials/${providerId}/keys/${keyId}`,
    SET_PRIMARY: (providerId: number, keyId: number) => `/api/ProviderCredentials/${providerId}/keys/${keyId}/set-primary`,
    ROTATE: (providerId: number, keyId: number) => `/api/ProviderCredentials/${providerId}/keys/${keyId}/rotate`,
    TEST: (providerId: number, keyId: number) => `/api/ProviderCredentials/${providerId}/keys/${keyId}/test`,
  },

  // Provider Models (Note: These endpoints don't exist in Admin API, use MODEL_MAPPINGS.DISCOVER_* instead)
  // TODO: Remove this section once all references are updated
  PROVIDER_MODELS: {
    BY_PROVIDER: (providerName: string) => `/api/provider-models/${providerName}`,
    CACHED: (providerName: string) => `/api/provider-models/${providerName}/cached`,
    REFRESH: (providerName: string) => `/api/provider-models/${providerName}/refresh`,
    TEST_CONNECTION: '/api/provider-models/test-connection',
    SUMMARY: '/api/provider-models/summary',
    DETAILS: (providerName: string, modelId: string) => `/api/provider-models/${providerName}/${modelId}`,
    CAPABILITIES: (providerName: string, modelId: string) => `/api/provider-models/${providerName}/${modelId}/capabilities`,
    SEARCH: '/api/provider-models/search',
  },

  // Model Provider Mappings
  MODEL_MAPPINGS: {
    BASE: '/api/ModelProviderMapping',
    BY_ID: (id: number) => `/api/ModelProviderMapping/${id}`,
    BY_MODEL: (modelId: string) => `/api/ModelProviderMapping/by-model/${modelId}`,
    PROVIDERS: '/api/ModelProviderMapping/providers',
    BULK: '/api/ModelProviderMapping/bulk',
    DISCOVER_PROVIDER: (providerId: number) => `/api/ModelProviderMapping/discover/provider/${providerId}`,
    DISCOVER_MODEL: (providerId: number, modelId: string) => `/api/ModelProviderMapping/discover/model/${providerId}/${modelId}`,
    TEST_CAPABILITY: (modelAlias: string, capability: string) => `/api/ModelProviderMapping/discover/capability/${modelAlias}/${capability}`,
    IMPORT: '/api/ModelProviderMapping/import',
    EXPORT: '/api/ModelProviderMapping/export',
    SUGGEST: '/api/ModelProviderMapping/suggest',
    ROUTING: (modelId: string) => `/api/ModelProviderMapping/routing/${modelId}`,
  },

  // IP Filters
  IP_FILTERS: {
    BASE: '/api/IpFilter',
    BY_ID: (id: number) => `/api/IpFilter/${id}`,
    ENABLED: '/api/IpFilter/enabled',
    SETTINGS: '/api/IpFilter/settings',
    CHECK: (ipAddress: string) => `/api/IpFilter/check/${encodeURIComponent(ipAddress)}`,
    BULK_CREATE: '/api/IpFilter/bulk',
    BULK_UPDATE: '/api/IpFilter/bulk-update',
    BULK_DELETE: '/api/IpFilter/bulk-delete',
    CREATE_TEMPORARY: '/api/IpFilter/temporary',
    EXPIRING: '/api/IpFilter/expiring',
    IMPORT: '/api/IpFilter/import',
    EXPORT: '/api/IpFilter/export',
    BLOCKED_STATS: '/api/IpFilter/blocked-stats',
  },

  // Model Costs
  MODEL_COSTS: {
    BASE: '/api/ModelCosts',
    BY_ID: (id: number) => `/api/ModelCosts/${id}`,
    BY_MODEL: (modelId: string) => `/api/ModelCosts/model/${modelId}`,
    BY_PROVIDER: (providerType: ProviderType) => `/api/ModelCosts/provider/${providerType}`,
    BATCH: '/api/ModelCosts/batch',
    IMPORT: '/api/ModelCosts/import',
    BULK_UPDATE: '/api/ModelCosts/bulk-update',
    OVERVIEW: '/api/ModelCosts/overview',
    TRENDS: '/api/ModelCosts/trends',
  },

  // Analytics & Cost Dashboard
  ANALYTICS: {
    COST_SUMMARY: '/api/CostDashboard/summary', // DEPRECATED: Use COSTS.SUMMARY instead
    COST_BY_PERIOD: '/api/CostDashboard/by-period', // DEPRECATED: Use COSTS endpoints
    COST_BY_MODEL: '/api/CostDashboard/by-model', // DEPRECATED: Use COSTS endpoints
    COST_BY_KEY: '/api/CostDashboard/by-key', // DEPRECATED: Use COSTS endpoints
    REQUEST_LOGS: '/api/Logs',
    REQUEST_LOG_BY_ID: (id: string) => `/api/Logs/${id}`,
    
    // Export management
    EXPORT_REQUEST_LOGS: '/api/analytics/export/request-logs',
    EXPORT_STATUS: (exportId: string) => `/api/analytics/export/status/${exportId}`,
    EXPORT_DOWNLOAD: (exportId: string) => `/api/analytics/export/download/${exportId}`,
  },

  // Cost Dashboard (actual endpoints)
  COSTS: {
    SUMMARY: '/api/costs/summary',
    TRENDS: '/api/costs/trends',
    MODELS: '/api/costs/models',
    VIRTUAL_KEYS: '/api/costs/virtualkeys',
  },

  // Provider Health
  HEALTH: {
    CONFIGURATIONS: '/api/ProviderHealth/configurations',
    CONFIG_BY_PROVIDER: (providerType: ProviderType) => `/api/ProviderHealth/configurations/${providerType}`,
    STATUS: '/api/ProviderHealth/status',
    STATUS_BY_PROVIDER: (providerType: ProviderType) => `/api/ProviderHealth/status/${providerType}`,
    HISTORY: '/api/ProviderHealth/history',
    HISTORY_BY_PROVIDER: (providerType: ProviderType) => `/api/ProviderHealth/history/${providerType}`,
    CHECK: (providerType: ProviderType) => `/api/ProviderHealth/check/${providerType}`,
    SUMMARY: '/api/health/providers',
    ALERTS: '/api/health/alerts',
    PERFORMANCE: (providerType: ProviderType) => `/api/health/providers/${providerType}/performance`,
  },

  // System
  SYSTEM: {
    INFO: '/api/SystemInfo/info',
    HEALTH: '/api/SystemInfo/health',
    SERVICES: '/api/health/services',
    METRICS: '/api/metrics',
    HEALTH_EVENTS: '/api/health/events',
    BACKUP: '/api/DatabaseBackup',
    RESTORE: '/api/DatabaseBackup/restore',
    NOTIFICATIONS: '/api/Notifications',
    NOTIFICATION_BY_ID: (id: number) => `/api/Notifications/${id}`,
  },

  // Comprehensive Metrics (Issue #434)
  METRICS: {
    // Real Admin API metrics endpoints
    ADMIN_BASIC: '/api/metrics',
    ADMIN_DATABASE_POOL: '/metrics/database/pool',
    
    // Real-time metrics
    REALTIME: '/api/dashboard/metrics/realtime',
  },

  // Settings
  SETTINGS: {
    GLOBAL: '/api/GlobalSettings',
    GLOBAL_BY_KEY: (key: string) => `/api/GlobalSettings/by-key/${key}`,
    BATCH_UPDATE: '/api/GlobalSettings/batch',
    AUDIO: '/api/AudioConfiguration',
    AUDIO_BY_PROVIDER: (providerType: ProviderType) => `/api/AudioConfiguration/${providerType}`,
    ROUTER: '/api/Router',
  },

  // Discovery moved to MODEL_MAPPINGS endpoints in Admin API

  // Security
  SECURITY: {
    EVENTS: '/api/admin/security/events',
    REPORT_EVENT: '/api/admin/security/events',
    EXPORT_EVENTS: '/api/admin/security/events/export',
    THREATS: '/api/admin/security/threats',
    THREAT_BY_ID: (id: string) => `/api/admin/security/threats/${id}`,
    THREAT_ANALYTICS: '/api/admin/security/threats/analytics',
    COMPLIANCE_METRICS: '/api/admin/security/compliance/metrics',
    COMPLIANCE_REPORT: '/api/admin/security/compliance/report',
  },

  // Error Queue Management
  ERROR_QUEUES: {
    BASE: '/api/admin/error-queues',
    MESSAGES: (queueName: string) => `/api/admin/error-queues/${encodeURIComponent(queueName)}/messages`,
    MESSAGE_BY_ID: (queueName: string, messageId: string) => `/api/admin/error-queues/${encodeURIComponent(queueName)}/messages/${encodeURIComponent(messageId)}`,
    STATISTICS: '/api/admin/error-queues/statistics',
    HEALTH: '/api/admin/error-queues/health',
    REPLAY: (queueName: string) => `/api/admin/error-queues/${encodeURIComponent(queueName)}/replay`,
    CLEAR: (queueName: string) => `/api/admin/error-queues/${encodeURIComponent(queueName)}/messages`,
  },

  // Configuration (Routing and Caching)
  CONFIGURATION: {
    // Routing
    ROUTING: '/api/configuration/routing',
    ROUTING_TEST: '/api/configuration/routing/test',
    LOAD_BALANCER_HEALTH: '/api/configuration/routing/health',
    ROUTING_RULES: '/api/config/routing/rules',
    ROUTING_RULE_BY_ID: (id: string) => `/api/config/routing/rules/${id}`,
    
    // Caching
    CACHING: '/api/configuration/caching',
    CACHE_POLICIES: '/api/configuration/caching/policies',
    CACHE_POLICY_BY_ID: (id: string) => `/api/configuration/caching/policies/${id}`,
    CACHE_REGIONS: '/api/configuration/caching/regions',
    CACHE_CLEAR: (regionId: string) => `/api/configuration/caching/regions/${regionId}/clear`,
    CACHE_STATISTICS: '/api/configuration/caching/statistics',
    CACHE_CONFIG: '/api/config/cache',
    CACHE_STATS: '/api/config/cache/stats',
    
    // Load Balancer
    LOAD_BALANCER: '/api/config/loadbalancer',
    
    // Performance
    PERFORMANCE: '/api/config/performance',
    PERFORMANCE_TEST: '/api/config/performance/test',
    
    // Feature Flags
    FEATURES: '/api/config/features',
    FEATURE_BY_KEY: (key: string) => `/api/config/features/${key}`,
    
    // Routing Health (Issue #437)
    ROUTING_HEALTH: '/api/config/routing/health',
    ROUTING_HEALTH_DETAILED: '/api/config/routing/health/detailed',
    ROUTING_HEALTH_HISTORY: '/api/config/routing/health/history',
    ROUTE_HEALTH_BY_ID: (routeId: string) => `/api/config/routing/health/routes/${routeId}`,
    ROUTE_PERFORMANCE_TEST: '/api/config/routing/performance/test',
    CIRCUIT_BREAKERS: '/api/config/routing/circuit-breakers',
    CIRCUIT_BREAKER_BY_ID: (breakerId: string) => `/api/config/routing/circuit-breakers/${breakerId}`,
    ROUTING_EVENTS: '/api/config/routing/events',
    ROUTING_EVENTS_SUBSCRIBE: '/api/config/routing/events/subscribe',
  },
} as const;

export const DEFAULT_PAGE_SIZE = 20;
export const MAX_PAGE_SIZE = 100;

export const CACHE_TTL = {
  SHORT: 60,         // 1 minute
  MEDIUM: 300,       // 5 minutes
  LONG: 3600,        // 1 hour
  VERY_LONG: 86400,  // 24 hours
} as const;

// Re-export HTTP_STATUS with backward compatibility aliases
export const HTTP_STATUS = {
  ...COMMON_HTTP_STATUS,
  RATE_LIMITED: COMMON_HTTP_STATUS.TOO_MANY_REQUESTS, // Alias for backward compatibility
  INTERNAL_ERROR: COMMON_HTTP_STATUS.INTERNAL_SERVER_ERROR, // Alias for backward compatibility
} as const;

export const BUDGET_DURATION = {
  TOTAL: 'Total',
  DAILY: 'Daily',
  WEEKLY: 'Weekly',
  MONTHLY: 'Monthly',
} as const;

export const FILTER_TYPE = {
  ALLOW: 'whitelist',
  DENY: 'blacklist',
} as const;

export const FILTER_MODE = {
  PERMISSIVE: 'permissive',
  RESTRICTIVE: 'restrictive',
} as const;