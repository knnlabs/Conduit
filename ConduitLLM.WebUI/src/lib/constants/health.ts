/**
 * Health status constants and enums for the Conduit WebUI
 */

/**
 * Health status values returned by the health check APIs
 */
export const HealthStatus = {
  HEALTHY: 'healthy',
  DEGRADED: 'degraded',
  UNHEALTHY: 'unhealthy',
  UNAVAILABLE: 'unavailable',
} as const;

export type HealthStatusType = typeof HealthStatus[keyof typeof HealthStatus];

/**
 * Maps API health status strings to normalized status values
 */
export function mapHealthStatus(status: string | undefined): 'healthy' | 'degraded' | 'unavailable' {
  if (!status) return 'unavailable';
  
  const normalizedStatus = status.toLowerCase();
  
  switch (normalizedStatus) {
    case HealthStatus.HEALTHY:
      return 'healthy';
    case HealthStatus.DEGRADED:
    case HealthStatus.UNHEALTHY:
      return 'degraded';
    default:
      return 'unavailable';
  }
}

/**
 * Health check endpoints
 */
export const HealthEndpoints = {
  CORE_API: '/health/ready',
  ADMIN_API: '/api/systeminfo/health',
  ADMIN_API_READY: '/health/ready',
} as const;

/**
 * Health check component names
 */
export const HealthComponents = {
  DATABASE: 'database',
  PROVIDERS: 'providers',
  REDIS: 'redis',
  RABBITMQ: 'rabbitmq',
  MASSTRANSIT_BUS: 'masstransit-bus',
  SIGNALR: 'signalr',
} as const;

/**
 * Health status colors for UI display
 */
export const HealthStatusColors = {
  [HealthStatus.HEALTHY]: 'green',
  [HealthStatus.DEGRADED]: 'yellow',
  [HealthStatus.UNHEALTHY]: 'red',
  [HealthStatus.UNAVAILABLE]: 'gray',
} as const;

/**
 * Health status labels for UI display
 */
export const HealthStatusLabels = {
  [HealthStatus.HEALTHY]: 'Healthy',
  [HealthStatus.DEGRADED]: 'Degraded',
  [HealthStatus.UNHEALTHY]: 'Unhealthy',
  [HealthStatus.UNAVAILABLE]: 'Unavailable',
} as const;

/**
 * Provider-related health check messages
 */
export const ProviderHealthMessages = {
  NO_PROVIDERS: ['no enabled providers', 'no providers', 'all providers are offline'],
} as const;

/**
 * Checks if a health check description indicates no providers are configured
 */
export function isNoProvidersIssue(description: string | undefined): boolean {
  if (!description) return false;
  
  const lowerDescription = description.toLowerCase();
  return ProviderHealthMessages.NO_PROVIDERS.some(msg => lowerDescription.includes(msg));
}