// Import shared HTTP constants from Common package
import { HTTP_HEADERS, CONTENT_TYPES, HTTP_STATUS as COMMON_HTTP_STATUS } from '@knn_labs/conduit-common';

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
    CHECK_BUDGET: (id: number) => `/api/VirtualKeys/${id}/check-budget`,
    VALIDATION_INFO: (id: number) => `/api/VirtualKeys/${id}/validation-info`,
    MAINTENANCE: '/api/VirtualKeys/maintenance',
    DISCOVERY_PREVIEW: (id: number) => `/api/VirtualKeys/${id}/discovery-preview`,
  },

  // Providers - Provider ID is the canonical identifier
  PROVIDERS: {
    BASE: '/api/ProviderCredentials',
    BY_ID: (id: number) => `/api/ProviderCredentials/${id}`,
    TEST_BY_ID: (id: number) => `/api/ProviderCredentials/test/${id}`,
    TEST: '/api/ProviderCredentials/test',
  },

  // Provider Key Credentials - Manage multiple API keys per provider
  PROVIDER_KEYS: {
    BASE: (providerId: number) => `/api/ProviderCredentials/${providerId}/keys`,
    BY_ID: (providerId: number, keyId: number) => `/api/ProviderCredentials/${providerId}/keys/${keyId}`,
    SET_PRIMARY: (providerId: number, keyId: number) => `/api/ProviderCredentials/${providerId}/keys/${keyId}/set-primary`,
    TEST: (providerId: number, keyId: number) => `/api/ProviderCredentials/${providerId}/keys/${keyId}/test`,
  },

  // Model Provider Mappings
  MODEL_MAPPINGS: {
    BASE: '/api/ModelProviderMapping',
    BY_ID: (id: number) => `/api/ModelProviderMapping/${id}`,
    PROVIDERS: '/api/ModelProviderMapping/providers',
    BULK: '/api/ModelProviderMapping/bulk',
    DISCOVER: (providerId: number) => `/api/ModelProviderMapping/discover/${providerId}`,
  },

  // IP Filters
  IP_FILTERS: {
    BASE: '/api/IpFilter',
    BY_ID: (id: number) => `/api/IpFilter/${id}`,
    ENABLED: '/api/IpFilter/enabled',
    SETTINGS: '/api/IpFilter/settings',
    CHECK: (ipAddress: string) => `/api/IpFilter/check/${ipAddress}`,
  },

  // Model Costs
  MODEL_COSTS: {
    BASE: '/api/ModelCosts',
    BY_ID: (id: number) => `/api/ModelCosts/${id}`,
    BY_NAME: (costName: string) => `/api/ModelCosts/name/${costName}`,
    BY_PROVIDER: (providerId: number) => `/api/ModelCosts/provider/${providerId}`,
    IMPORT: '/api/ModelCosts/import',
    IMPORT_CSV: '/api/ModelCosts/import/csv',
    IMPORT_JSON: '/api/ModelCosts/import/json',
    EXPORT_CSV: '/api/ModelCosts/export/csv',
    EXPORT_JSON: '/api/ModelCosts/export/json',
    OVERVIEW: '/api/ModelCosts/overview',
  },

  // Analytics & Cost Dashboard
  ANALYTICS: {
    REQUEST_LOGS: '/api/Logs',
    REQUEST_LOG_BY_ID: (id: string) => `/api/Logs/${id}`,
  },

  // Cost Dashboard (actual endpoints)
  COSTS: {
    SUMMARY: '/api/costs/summary',
    TRENDS: '/api/costs/trends',
    MODELS: '/api/costs/models',
    VIRTUAL_KEYS: '/api/costs/virtualkeys',
  },

  // Audio Provider Management
  AUDIO: {
    PROVIDERS: {
      BASE: '/api/admin/audio/providers',
      BY_ID: (id: string) => `/api/admin/audio/providers/${id}`,
      BY_PROVIDER_ID: (providerId: string) => `/api/admin/audio/providers/by-id/${providerId}`,
      ENABLED: (operationType: string) => `/api/admin/audio/providers/enabled/${operationType}`,
      TEST: (id: string) => `/api/admin/audio/providers/${id}/test`,
    },
    COSTS: {
      BASE: '/api/admin/audio/costs',
      BY_ID: (id: string) => `/api/admin/audio/costs/${id}`,
      BY_PROVIDER: (providerId: string) => `/api/admin/audio/costs/by-provider/${providerId}`,
      CURRENT: '/api/admin/audio/costs/current',
    },
    USAGE: {
      BASE: '/api/admin/audio/usage',
      SUMMARY: '/api/admin/audio/usage/summary',
      BY_KEY: (virtualKey: string) => `/api/admin/audio/usage/by-key/${virtualKey}`,
      BY_PROVIDER: (providerId: string) => `/api/admin/audio/usage/by-provider/${providerId}`,
    },
    SESSIONS: {
      BASE: '/api/admin/audio/sessions',
      BY_ID: (sessionId: string) => `/api/admin/audio/sessions/${sessionId}`,
      METRICS: '/api/admin/audio/sessions/metrics',
    },
  },

  // Media Management
  MEDIA: {
    STATS: {
      BASE: '/api/admin/Media/stats',
      BY_VIRTUAL_KEY: (virtualKeyId: string) => `/api/admin/Media/stats/virtual-key/${virtualKeyId}`,
      BY_PROVIDER: '/api/admin/Media/stats/by-provider',
      BY_TYPE: '/api/admin/Media/stats/by-type',
    },
    BY_VIRTUAL_KEY: (virtualKeyId: string) => `/api/admin/Media/virtual-key/${virtualKeyId}`,
    SEARCH: '/api/admin/Media/search',
    BY_ID: (mediaId: string) => `/api/admin/Media/${mediaId}`,
    CLEANUP: {
      EXPIRED: '/api/admin/Media/cleanup/expired',
      ORPHANED: '/api/admin/Media/cleanup/orphaned',
      PRUNE: '/api/admin/Media/cleanup/prune',
    },
  },

  // Cache Monitoring
  CACHE_MONITORING: {
    STATUS: '/api/cache/monitoring/status',
    THRESHOLDS: '/api/cache/monitoring/thresholds',
    ALERTS: '/api/cache/monitoring/alerts',
    CHECK: '/api/cache/monitoring/check',
    ALERT_DEFINITIONS: '/api/cache/monitoring/alert-definitions',
    HEALTH: '/api/cache/monitoring/health',
  },

  // Database Management
  DATABASE: {
    BACKUP: '/api/database/backup',
    BACKUPS: '/api/database/backups',
    RESTORE: (backupId: string) => `/api/database/restore/${backupId}`,
    DOWNLOAD: (backupId: string) => `/api/database/download/${backupId}`,
  },

  // Configuration endpoints
  CONFIG: {
    ROUTING: '/api/config/routing',
    CACHING: {
      BASE: '/api/config/caching',
      CLEAR: (cacheId: string) => `/api/config/caching/${cacheId}/clear`,
      STATISTICS: '/api/config/caching/statistics',
      REGIONS: '/api/config/caching/regions',
      ENTRIES: (regionId: string) => `/api/config/caching/${regionId}/entries`,
      REFRESH: (regionId: string) => `/api/config/caching/${regionId}/refresh`,
      POLICY: (regionId: string) => `/api/config/caching/${regionId}/policy`,
    },
  },

  // Logs endpoints
  LOGS: {
    BASE: '/api/Logs',
    BY_ID: (id: string) => `/api/Logs/${id}`,
    MODELS: '/api/Logs/models',
    SUMMARY: '/api/Logs/summary',
  },

  // Notifications endpoints
  NOTIFICATIONS: {
    BASE: '/api/Notifications',
    BY_ID: (id: number) => `/api/Notifications/${id}`,
    UNREAD: '/api/Notifications/unread',
    MARK_READ: (id: number) => `/api/Notifications/${id}/read`,
    MARK_ALL_READ: '/api/Notifications/mark-all-read',
  },

  // Router endpoints
  ROUTER: {
    CONFIG: '/api/Router/config',
    DEPLOYMENTS: '/api/Router/deployments',
    DEPLOYMENT_BY_NAME: (deploymentName: string) => `/api/Router/deployments/${deploymentName}`,
    FALLBACKS: '/api/Router/fallbacks',
    FALLBACK_BY_MODEL: (primaryModel: string) => `/api/Router/fallbacks/${primaryModel}`,
  },

  // Security endpoints
  SECURITY: {
    EVENTS: '/api/security/events',
    THREATS: '/api/security/threats',
    COMPLIANCE: '/api/security/compliance',
  },

  // Provider Health
  HEALTH: {
    CONFIGURATIONS: '/api/ProviderHealth/configurations',
    CONFIG_BY_PROVIDER: (providerId: number) => `/api/ProviderHealth/configurations/${providerId}`,
    STATUS: '/api/ProviderHealth/status',
    STATUS_BY_ID: (providerId: number) => `/api/ProviderHealth/status/${providerId}`,
    STATUSES: '/api/ProviderHealth/statuses',
    STATUSES_BY_ID: (providerId: number) => `/api/ProviderHealth/statuses/${providerId}`,
    HISTORY_BY_PROVIDER: (providerId: number) => `/api/ProviderHealth/history/${providerId}`,
    CHECK: (providerId: number) => `/api/ProviderHealth/check/${providerId}`,
    SUMMARY: '/api/ProviderHealth/summary',
    STATISTICS: '/api/ProviderHealth/statistics',
    PURGE: '/api/ProviderHealth/purge',
    RECORDS: '/api/ProviderHealth/records',
  },

  // System
  SYSTEM: {
    INFO: '/api/SystemInfo/info',
    HEALTH: '/api/SystemInfo/health',
    SERVICES: '/api/health/services',
    NOTIFICATIONS: '/api/Notifications',
    NOTIFICATION_BY_ID: (id: number) => `/api/Notifications/${id}`,
    HEALTH_INCIDENTS: '/api/health/incidents',
    HEALTH_HISTORY: '/api/health/history',
  },

  // Comprehensive Metrics
  METRICS: {
    BASE: '/metrics',
    DATABASE_POOL: '/metrics/database/pool',
  },

  // Error Queue endpoints
  ERROR_QUEUES: {
    BASE: '/api/admin/error-queues',
    MESSAGES: (queueName: string) => `/api/admin/error-queues/${queueName}/messages`,
    MESSAGE_BY_ID: (queueName: string, messageId: string) => `/api/admin/error-queues/${queueName}/messages/${messageId}`,
    STATISTICS: '/api/admin/error-queues/statistics',
    HEALTH: '/api/admin/error-queues/health',
    REPLAY: (queueName: string) => `/api/admin/error-queues/${queueName}/replay`,
  },

  // Settings
  SETTINGS: {
    GLOBAL: '/api/GlobalSettings',
    GLOBAL_BY_ID: (id: number) => `/api/GlobalSettings/${id}`,
    GLOBAL_BY_KEY: (key: string) => `/api/GlobalSettings/by-key/${key}`,
    GLOBAL_BY_KEY_SIMPLE: '/api/GlobalSettings/by-key',
    ROUTER: '/api/Router/config',
  },

  // Admin tasks
  ADMIN_TASKS: {
    CLEANUP: '/v1/admin/tasks/cleanup',
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