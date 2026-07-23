// Import shared HTTP constants from Common package
import { HTTP_HEADERS, CONTENT_TYPES, HTTP_STATUS as COMMON_HTTP_STATUS } from '@/lib/conduit-common';

// Re-export for backward compatibility
export { HTTP_HEADERS, CONTENT_TYPES };

export const API_VERSION = 'v1';
export const API_PREFIX = '/api';

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
  // Model Provider Mappings
  MODEL_MAPPINGS: {
    BASE: '/v1/admin/model-provider-mappings',
    BY_ID: (id: number) => `/v1/admin/model-provider-mappings/${id}`,
    PROVIDERS: '/v1/admin/model-provider-mappings/providers',
    BULK: '/v1/admin/model-provider-mappings/bulk',
    BULK_DELETE: '/v1/admin/model-provider-mappings/bulk/delete',
    BULK_ENABLE: '/v1/admin/model-provider-mappings/bulk/enable',
    BULK_DISABLE: '/v1/admin/model-provider-mappings/bulk/disable',
  },

  // Model Costs
  MODEL_COSTS: {
    BASE: '/v1/admin/model-costs',
    BY_ID: (id: number) => `/v1/admin/model-costs/${id}`,
    BY_NAME: (costName: string) => `/v1/admin/model-costs/name/costs/${costName}`,
    BY_PROVIDER: (providerId: number) => `/v1/admin/model-costs/provider/costs/${providerId}`,
    IMPORT: '/v1/admin/model-costs/import',
    IMPORT_CSV: '/v1/admin/model-costs/import/csv',
    IMPORT_JSON: '/v1/admin/model-costs/import/json',
    EXPORT_CSV: '/v1/admin/model-costs/export/csv',
    EXPORT_JSON: '/v1/admin/model-costs/export/json',
    OVERVIEW: '/v1/admin/model-costs/overview',
  },

  // Model Management
  MODELS: {
    BASE: '/v1/admin/models',
    IMPORT_BUNDLED_CATALOG: '/v1/admin/model-catalogs/import',
    BY_ID: (id: number) => `/v1/admin/models/${id}`,
    BY_PROVIDER: (provider: string) => `/v1/admin/models/provider/models/${provider}`,
    SEARCH: '/v1/admin/models/search',
  },

  // Model Series Management
  MODEL_SERIES: {
    BASE: '/v1/admin/model-series',
    BY_ID: (id: number) => `/v1/admin/model-series/${id}`,
    MODELS: (id: number) => `/v1/admin/model-series/${id}/models`,
  },

  // Model Author Management
  MODEL_AUTHORS: {
    BASE: '/v1/admin/model-authors',
    BY_ID: (id: number) => `/v1/admin/model-authors/${id}`,
    SERIES: (id: number) => `/v1/admin/model-authors/${id}/series`,
  },

  // Model Capabilities Management
  MODEL_CAPABILITIES: {
    BASE: '/v1/admin/modelsCapabilities',
    BY_ID: (id: number) => `/v1/admin/modelsCapabilities/${id}`,
    MODELS: (id: number) => `/v1/admin/modelsCapabilities/${id}/models`,
  },

  // Unified Analytics endpoints
  ANALYTICS: {
    // Request Logs
    REQUEST_LOGS: '/api/analytics/logs',
    REQUEST_LOG_BY_ID: (id: string) => `/api/analytics/logs/${id}`,
    DISTINCT_MODELS: '/api/analytics/logs/models',

    // Cost Analytics
    COST_SUMMARY: '/api/analytics/costs/summary',
    COST_TRENDS: '/api/analytics/costs/trends',
    MODEL_COSTS: '/api/analytics/costs/models',
    VIRTUAL_KEY_COSTS: '/api/analytics/costs/virtualkeys',

    // Combined Analytics
    SUMMARY: '/api/analytics/summary',
    VIRTUAL_KEY_USAGE: (virtualKeyId: number) => `/api/analytics/virtualkeys/${virtualKeyId}/usage`,
    EXPORT: '/api/analytics/export',
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

  // Database Management
  DATABASE: {
    BACKUP: '/api/database/backup',
    BACKUPS: '/api/database/backups',
    RESTORE: (backupId: string) => `/api/database/restore/${backupId}`,
    DOWNLOAD: (backupId: string) => `/api/database/download/${backupId}`,
  },


  // Notifications endpoints
  NOTIFICATIONS: {
    BASE: '/v1/admin/notifications',
    BY_ID: (id: number) => `/v1/admin/notifications/${id}`,
    UNREAD: '/v1/admin/notifications/unread',
    MARK_READ: (id: number) => `/v1/admin/notifications/${id}/read`,
    MARK_ALL_READ: '/v1/admin/notifications/mark-all-read',
  },

  // Router endpoints
  ROUTER: {
    CONFIG: '/api/Router/config',
    DEPLOYMENTS: '/api/Router/deployments',
    DEPLOYMENT_BY_NAME: (deploymentName: string) => `/api/Router/deployments/${deploymentName}`,
    FALLBACKS: '/api/Router/fallbacks',
    FALLBACK_BY_MODEL: (primaryModel: string) => `/api/Router/fallbacks/${primaryModel}`,
  },

  // System
  SYSTEM: {
    INFO: '/v1/admin/system-metadata/info',
    HEALTH: '/v1/admin/system-metadata/health',
    SERVICES: '/v1/admin/health-status/services',
    NOTIFICATIONS: '/v1/admin/notifications',
    NOTIFICATION_BY_ID: (id: number) => `/v1/admin/notifications/${id}`,
    HEALTH_INCIDENTS: '/v1/admin/health-status/incidents',
    HEALTH_HISTORY: '/v1/admin/health-status/history',
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
