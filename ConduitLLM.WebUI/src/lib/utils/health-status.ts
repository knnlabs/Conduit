import type { HealthStatusDto } from '@knn_labs/conduit-admin-client';

/**
 * Processed health status for the WebUI
 */
export interface ProcessedHealthStatus {
  overallStatus: 'healthy' | 'degraded' | 'unhealthy';
  coreApi: 'healthy' | 'degraded' | 'unavailable';
  coreApiMessage: string;
  coreApiChecks: number;
  adminApi: 'healthy' | 'degraded' | 'unavailable';
  adminApiChecks: number;
  signalr: 'healthy' | 'degraded' | 'unavailable';
  lastChecked: Date;
  isNoProvidersIssue: boolean;
}

/**
 * Process health status from Admin API and add SignalR status
 */
export function processHealthStatus(
  adminHealth: HealthStatusDto,
  signalrStatus: 'healthy' | 'degraded' | 'unavailable' = 'unavailable'
): ProcessedHealthStatus {
  // Process admin API health
  const adminApiStatus = normalizeHealthStatus(adminHealth.status);
  const adminApiChecks = 0; // No longer tracking provider health details

  // For core API, we'll assume it's healthy if admin API is working
  // since admin API depends on core services
  const coreApiStatus = adminApiStatus === 'healthy' ? 'healthy' : 'degraded';
  const coreApiMessage = adminApiStatus === 'healthy' 
    ? 'Core API is operational' 
    : 'Core API may be experiencing issues';

  // Determine overall status
  const statuses = [adminApiStatus, coreApiStatus, signalrStatus].filter(s => s !== 'unavailable');
  const overallStatus = determineOverallStatus(statuses as ('healthy' | 'degraded')[], signalrStatus !== 'unavailable');

  // isNoProvidersIssue is always false since we no longer track provider health
  const isNoProvidersIssue = false;

  return {
    overallStatus,
    coreApi: coreApiStatus,
    coreApiMessage,
    coreApiChecks: adminApiChecks,
    adminApi: adminApiStatus,
    adminApiChecks,
    signalr: signalrStatus,
    lastChecked: new Date(),
    isNoProvidersIssue
  };
}

/**
 * Create error health status when health check fails
 */
export function createErrorHealthStatus(message: string): ProcessedHealthStatus {
  return {
    overallStatus: 'unhealthy',
    coreApi: 'unavailable',
    coreApiMessage: message,
    coreApiChecks: 0,
    adminApi: 'unavailable',
    adminApiChecks: 0,
    signalr: 'unavailable',
    lastChecked: new Date(),
    isNoProvidersIssue: false
  };
}

/**
 * Normalize health status from various formats
 */
function normalizeHealthStatus(status: string | undefined): 'healthy' | 'degraded' | 'unavailable' {
  if (!status) return 'unavailable';
  
  const normalized = status.toLowerCase();
  if (normalized === 'healthy' || normalized === 'ok') return 'healthy';
  if (normalized === 'degraded' || normalized === 'warning') return 'degraded';
  return 'unavailable';
}

/**
 * Determine overall status from component statuses
 */
function determineOverallStatus(
  statuses: ('healthy' | 'degraded' | 'unavailable')[], 
  includeSignalR: boolean
): 'healthy' | 'degraded' | 'unhealthy' {
  if (statuses.length === 0) return 'unhealthy';
  
  const hasUnavailable = statuses.some(s => s === 'unavailable');
  const hasDegraded = statuses.some(s => s === 'degraded');
  
  if (hasUnavailable) return 'unhealthy';
  if (hasDegraded) return 'degraded';
  
  // If SignalR is included and unavailable, consider it degraded
  if (includeSignalR) {
    return 'degraded';
  }
  
  return 'healthy';
}