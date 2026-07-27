// Import shared HTTP constants from Common package
import { HTTP_HEADERS, CONTENT_TYPES, HTTP_STATUS as COMMON_HTTP_STATUS } from '@/lib/conduit-common';

// Re-export for backward compatibility
export { HTTP_HEADERS, CONTENT_TYPES };

/**
 * Client information constants.
 */
export const CLIENT_INFO = {
  NAME: 'conduit-webadmin',
  VERSION: 'vendored',
  USER_AGENT: 'conduit-webadmin/vendored',
} as const;

export const ENDPOINTS = {
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
  },
} as const;

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
