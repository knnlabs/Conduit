/**
 * Health check related constants for the WebUI
 */

export const HEALTH_STATUS = {
  HEALTHY: 'healthy',
  DEGRADED: 'degraded',
  UNHEALTHY: 'unhealthy',
  UNAVAILABLE: 'unavailable'
} as const;

export type HealthStatusType = typeof HEALTH_STATUS[keyof typeof HEALTH_STATUS];

export const HEALTH_CHECK_INTERVALS = {
  /** Fast polling for active monitoring (5 seconds) */
  FAST: 5000,
  /** Normal polling for background monitoring (30 seconds) */
  NORMAL: 30000,
  /** Slow polling for low-priority checks (60 seconds) */
  SLOW: 60000,
  /** Manual only - no automatic polling */
  MANUAL: 0
} as const;

export const HEALTH_ENDPOINTS = {
  CORE_API: '/health',
  CORE_API_READY: '/health/ready',
  ADMIN_API: '/api/system/health'
} as const;

export const HEALTH_COMPONENT_NAMES = {
  CORE_API: 'core-api',
  ADMIN_API: 'admin-api',
  SIGNALR: 'signalr',
  DATABASE: 'database',
  REDIS: 'redis',
  RABBITMQ: 'rabbitmq'
} as const;

export const HEALTH_COLORS = {
  [HEALTH_STATUS.HEALTHY]: 'text-green-600',
  [HEALTH_STATUS.DEGRADED]: 'text-yellow-600',
  [HEALTH_STATUS.UNHEALTHY]: 'text-red-600',
  [HEALTH_STATUS.UNAVAILABLE]: 'text-gray-500'
} as const;

export const HEALTH_BACKGROUNDS = {
  [HEALTH_STATUS.HEALTHY]: 'bg-green-100',
  [HEALTH_STATUS.DEGRADED]: 'bg-yellow-100',
  [HEALTH_STATUS.UNHEALTHY]: 'bg-red-100',
  [HEALTH_STATUS.UNAVAILABLE]: 'bg-gray-100'
} as const;

export const HEALTH_ICONS = {
  [HEALTH_STATUS.HEALTHY]: '✓',
  [HEALTH_STATUS.DEGRADED]: '⚠',
  [HEALTH_STATUS.UNHEALTHY]: '✗',
  [HEALTH_STATUS.UNAVAILABLE]: '○'
} as const;