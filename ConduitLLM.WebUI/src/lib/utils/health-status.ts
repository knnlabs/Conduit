import type { HealthStatusDto } from '@knn_labs/conduit-admin-client';
import type { HealthCheckDetail } from '@/types/health';
import { mapHealthStatus, isNoProvidersIssue, HealthComponents } from '@/lib/constants/health';

export interface ProcessedHealthStatus {
  adminApi: 'healthy' | 'degraded' | 'unavailable';
  coreApi: 'healthy' | 'degraded' | 'unavailable';
  signalr: 'healthy' | 'degraded' | 'unavailable';
  isNoProvidersIssue: boolean;
  coreApiMessage?: string;
  adminApiDetails?: HealthStatusDto;
  coreApiDetails?: HealthStatusDto;
  adminApiChecks?: Record<string, HealthCheckDetail>;
  coreApiChecks?: Record<string, HealthCheckDetail>;
  lastChecked: Date;
}

/**
 * Processes raw health status data from the Admin API into a normalized format
 * This ensures consistent interpretation across client and server components
 */
export function processHealthStatus(
  health: HealthStatusDto,
  signalrStatus?: 'healthy' | 'degraded' | 'unavailable'
): ProcessedHealthStatus {
  // Extract provider check for Core API status
  const providerCheck = health.checks?.[HealthComponents.PROVIDERS];
  const hasNoProviders = isNoProvidersIssue(providerCheck?.description);
  
  // Use the provider check status as the Core API status
  // Since providers ARE the core functionality
  const coreApiStatus = providerCheck ? mapHealthStatus(providerCheck.status) : 'unavailable';
  
  return {
    adminApi: mapHealthStatus(health.status),
    coreApi: coreApiStatus,
    signalr: signalrStatus ?? 'unavailable',
    isNoProvidersIssue: hasNoProviders,
    coreApiMessage: providerCheck?.description,
    adminApiDetails: health,
    coreApiDetails: health, // For now, using same health data
    adminApiChecks: health.checks,
    coreApiChecks: health.checks,
    lastChecked: new Date(),
  };
}

/**
 * Creates a default health status for error cases
 */
export function createErrorHealthStatus(
  error?: string
): ProcessedHealthStatus {
  return {
    adminApi: 'unavailable',
    coreApi: 'unavailable',
    signalr: 'unavailable',
    isNoProvidersIssue: false,
    lastChecked: new Date(),
    coreApiMessage: error,
  };
}