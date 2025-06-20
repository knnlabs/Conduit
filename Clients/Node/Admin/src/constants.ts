export const API_VERSION = 'v1';
export const API_PREFIX = '/api';

export const ENDPOINTS = {
  // Virtual Keys
  VIRTUAL_KEYS: {
    BASE: '/virtualkeys',
    BY_ID: (id: number) => `/virtualkeys/${id}`,
    RESET_SPEND: (id: number) => `/virtualkeys/${id}/reset-spend`,
    VALIDATE: '/virtualkeys/validate',
    SPEND: (id: number) => `/virtualkeys/${id}/spend`,
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
  },

  // Model Provider Mappings
  MODEL_MAPPINGS: {
    BASE: '/modelprovidermapping',
    BY_ID: (id: number) => `/modelprovidermapping/${id}`,
    BY_MODEL: (modelId: string) => `/modelprovidermapping/by-model/${modelId}`,
    PROVIDERS: '/modelprovidermapping/providers',
  },

  // IP Filters
  IP_FILTERS: {
    BASE: '/ipfilter',
    BY_ID: (id: number) => `/ipfilter/${id}`,
    ENABLED: '/ipfilter/enabled',
    SETTINGS: '/ipfilter/settings',
    CHECK: '/ipfilter/check',
  },

  // Model Costs
  MODEL_COSTS: {
    BASE: '/modelcosts',
    BY_ID: (id: number) => `/modelcosts/${id}`,
    BY_MODEL: (modelId: string) => `/modelcosts/model/${modelId}`,
    BATCH: '/modelcosts/batch',
  },

  // Analytics & Cost Dashboard
  ANALYTICS: {
    COST_SUMMARY: '/costdashboard/summary',
    COST_BY_PERIOD: '/costdashboard/by-period',
    COST_BY_MODEL: '/costdashboard/by-model',
    COST_BY_KEY: '/costdashboard/by-key',
    REQUEST_LOGS: '/logs',
    REQUEST_LOG_BY_ID: (id: string) => `/logs/${id}`,
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
    GLOBAL: '/globalsettings',
    GLOBAL_BY_KEY: (key: string) => `/globalsettings/${key}`,
    AUDIO: '/audioconfiguration',
    AUDIO_BY_PROVIDER: (provider: string) => `/audioconfiguration/${provider}`,
    ROUTER: '/router/configuration',
  },

  // Discovery (note: these endpoints don't use /api prefix)
  DISCOVERY: {
    MODELS: '/v1/discovery/models',
    PROVIDER_MODELS: (provider: string) => `/v1/discovery/providers/${provider}/models`,
    MODEL_CAPABILITY: (model: string, capability: string) => `/v1/discovery/models/${model}/capabilities/${capability}`,
    BULK_CAPABILITIES: '/v1/discovery/bulk/capabilities',
    BULK_MODELS: '/v1/discovery/bulk/models',
    REFRESH: '/v1/discovery/refresh',
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
  ALLOW: 'Allow',
  DENY: 'Deny',
} as const;

export const FILTER_MODE = {
  PERMISSIVE: 'permissive',
  RESTRICTIVE: 'restrictive',
} as const;