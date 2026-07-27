// Import shared HTTP constants from Common package
import { HTTP_HEADERS, CONTENT_TYPES, HTTP_STATUS } from '@/lib/conduit-common';

// Re-export for backward compatibility
export { HTTP_HEADERS, CONTENT_TYPES, HTTP_STATUS };

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
  },
} as const;

export const CACHE_TTL = {
  SHORT: 60,         // 1 minute
  MEDIUM: 300,       // 5 minutes
  LONG: 3600,        // 1 hour
  VERY_LONG: 86400,  // 24 hours
} as const;

export const BUDGET_DURATION = {
  TOTAL: 'Total',
  DAILY: 'Daily',
  WEEKLY: 'Weekly',
  MONTHLY: 'Monthly',
} as const;
