export const API_VERSION = 'v1';
export const API_PREFIX = '/api';

/**
 * HTTP method constants for type-safe method specification.
 */
export const HTTP_METHODS = {
  GET: 'GET',
  POST: 'POST',
  PUT: 'PUT',
  DELETE: 'DELETE',
  PATCH: 'PATCH',
} as const;


/**
 * HTTP header name constants.
 */
export const HTTP_HEADERS = {
  CONTENT_TYPE: 'Content-Type',
  X_API_KEY: 'X-API-Key',
  USER_AGENT: 'User-Agent',
  X_CORRELATION_ID: 'X-Correlation-Id',
  ACCEPT: 'Accept',
  CACHE_CONTROL: 'Cache-Control',
} as const;

/**
 * Content type constants.
 */
export const CONTENT_TYPES = {
  JSON: 'application/json',
  FORM_DATA: 'multipart/form-data',
  FORM_URLENCODED: 'application/x-www-form-urlencoded',
  TEXT_PLAIN: 'text/plain',
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
    BASE: '/virtualkeys',
    BY_ID: (id: number) => `/virtualkeys/${id}`,
    RESET_SPEND: (id: number) => `/virtualkeys/${id}/reset-spend`,
    VALIDATE: '/virtualkeys/validate',
    SPEND: (id: number) => `/virtualkeys/${id}/spend`,
    REFUND: (id: number) => `/virtualkeys/${id}/refund`,
    CHECK_BUDGET: (id: number) => `/virtualkeys/${id}/check-budget`,
    VALIDATION_INFO: (id: number) => `/virtualkeys/${id}/validation-info`,
    MAINTENANCE: '/virtualkeys/maintenance',
  },

  // Provider Credentials
  PROVIDERS: {
    BASE: '/providercredentials',
    BY_ID: (id: number) => `/providercredentials/${id}`,
    BY_NAME: (name: string) => `/providercredentials/name/${name}`,
    NAMES: '/providercredentials/names',
    TEST_BY_ID: (id: number) => `/providercredentials/test/${id}`,
    TEST: '/providercredentials/test',
  },

  // Provider Models
  PROVIDER_MODELS: {
    BY_PROVIDER: (providerId: string) => `/provider-models/${providerId}`,
    CACHED: (providerId: string) => `/provider-models/${providerId}/cached`,
    REFRESH: (providerId: string) => `/provider-models/${providerId}/refresh`,
    TEST_CONNECTION: '/provider-models/test-connection',
    SUMMARY: '/provider-models/summary',
    DETAILS: (provider: string, model: string) => `/provider-models/${provider}/${model}`,
    CAPABILITIES: (provider: string, model: string) => `/provider-models/${provider}/${model}/capabilities`,
    SEARCH: '/provider-models/search',
  },

  // Model Provider Mappings
  MODEL_MAPPINGS: {
    BASE: '/modelprovidermapping',
    BY_ID: (id: number) => `/modelprovidermapping/${id}`,
    BY_MODEL: (modelId: string) => `/modelprovidermapping/by-model/${modelId}`,
    PROVIDERS: '/modelprovidermapping/providers',
    BULK: '/modelprovidermapping/bulk',
    DISCOVER_PROVIDER: (providerName: string) => `/modelprovidermapping/discover/provider/${providerName}`,
    DISCOVER_MODEL: (providerName: string, modelId: string) => `/modelprovidermapping/discover/model/${providerName}/${modelId}`,
    DISCOVER_ALL: '/modelprovidermapping/discover/all',
    TEST_CAPABILITY: (modelAlias: string, capability: string) => `/modelprovidermapping/discover/capability/${modelAlias}/${capability}`,
    IMPORT: '/modelprovidermapping/import',
    EXPORT: '/modelprovidermapping/export',
    SUGGEST: '/modelprovidermapping/suggest',
    ROUTING: (modelId: string) => `/modelprovidermapping/routing/${modelId}`,
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
    BASE: '/modelcosts',
    BY_ID: (id: number) => `/modelcosts/${id}`,
    BY_MODEL: (modelId: string) => `/modelcosts/model/${modelId}`,
    BY_PROVIDER: (providerName: string) => `/modelcosts/provider/${providerName}`,
    BATCH: '/modelcosts/batch',
    IMPORT: '/modelcosts/import',
    BULK_UPDATE: '/modelcosts/bulk-update',
    OVERVIEW: '/modelcosts/overview',
    TRENDS: '/modelcosts/trends',
  },

  // Analytics & Cost Dashboard
  ANALYTICS: {
    COST_SUMMARY: '/costdashboard/summary',
    COST_BY_PERIOD: '/costdashboard/by-period',
    COST_BY_MODEL: '/costdashboard/by-model',
    COST_BY_KEY: '/costdashboard/by-key',
    REQUEST_LOGS: '/logs',
    REQUEST_LOG_BY_ID: (id: string) => `/logs/${id}`,
    
    // Specialized exports
    EXPORT_USAGE: '/api/analytics/export/usage',
    EXPORT_COST: '/api/analytics/export/cost',
    EXPORT_VIRTUAL_KEY: '/api/analytics/export/virtual-key',
    EXPORT_PROVIDER: '/api/analytics/export/provider',
    EXPORT_SECURITY: '/api/analytics/export/security',
    EXPORT_REQUEST_LOGS: '/api/analytics/export/request-logs',
    EXPORT_AUDIO_USAGE: '/api/analytics/export/audio-usage',
    
    // Export management
    EXPORT_SCHEDULES: '/api/analytics/export/schedules',
    EXPORT_SCHEDULE_BY_ID: (id: string) => `/api/analytics/export/schedules/${id}`,
    EXPORT_HISTORY: '/api/analytics/export/history',
    EXPORT_STATUS: (exportId: string) => `/api/analytics/export/status/${exportId}`,
    EXPORT_DOWNLOAD: (exportId: string) => `/api/analytics/export/download/${exportId}`,
    
    // Request log analytics
    REQUEST_LOG_STATS: '/api/analytics/request-logs/statistics',
    REQUEST_LOG_SUMMARY: '/api/analytics/request-logs/summary',
  },

  // Provider Health
  HEALTH: {
    CONFIGURATIONS: '/providerhealth/configurations',
    CONFIG_BY_PROVIDER: (provider: string) => `/providerhealth/configurations/${provider}`,
    STATUS: '/providerhealth/status',
    STATUS_BY_PROVIDER: (provider: string) => `/providerhealth/status/${provider}`,
    HISTORY: '/providerhealth/history',
    CHECK: (provider: string) => `/providerhealth/check/${provider}`,
  },

  // System
  SYSTEM: {
    INFO: '/systeminfo/info',
    HEALTH: '/systeminfo/health',
    BACKUP: '/databasebackup',
    RESTORE: '/databasebackup/restore',
    NOTIFICATIONS: '/notifications',
    NOTIFICATION_BY_ID: (id: number) => `/notifications/${id}`,
  },

  // Settings
  SETTINGS: {
    GLOBAL: '/api/GlobalSettings',
    GLOBAL_BY_KEY: (key: string) => `/api/GlobalSettings/by-key/${key}`,
    BATCH_UPDATE: '/api/GlobalSettings/batch',
    AUDIO: '/api/audio-configuration',
    AUDIO_BY_PROVIDER: (provider: string) => `/api/audio-configuration/${provider}`,
    ROUTER: '/api/router-configuration',
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

  // Configuration (Routing and Caching)
  CONFIGURATION: {
    // Routing
    ROUTING: '/api/configuration/routing',
    ROUTING_TEST: '/api/configuration/routing/test',
    LOAD_BALANCER_HEALTH: '/api/configuration/routing/health',
    
    // Caching
    CACHING: '/api/configuration/caching',
    CACHE_POLICIES: '/api/configuration/caching/policies',
    CACHE_POLICY_BY_ID: (id: string) => `/api/configuration/caching/policies/${id}`,
    CACHE_REGIONS: '/api/configuration/caching/regions',
    CACHE_CLEAR: (regionId: string) => `/api/configuration/caching/regions/${regionId}/clear`,
    CACHE_STATISTICS: '/api/configuration/caching/statistics',
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

export const HTTP_STATUS = {
  OK: 200,
  CREATED: 201,
  NO_CONTENT: 204,
  BAD_REQUEST: 400,
  UNAUTHORIZED: 401,
  FORBIDDEN: 403,
  NOT_FOUND: 404,
  CONFLICT: 409,
  RATE_LIMITED: 429,
  INTERNAL_ERROR: 500,
  BAD_GATEWAY: 502,
  SERVICE_UNAVAILABLE: 503,
  GATEWAY_TIMEOUT: 504,
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