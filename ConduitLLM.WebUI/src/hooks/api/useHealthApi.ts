'use client';

import { useQuery, useQueryClient } from '@tanstack/react-query';
import { getAdminClient } from '@/lib/clients/conduit';
import { reportError } from '@/lib/utils/logging';
import { BackendErrorHandler } from '@/lib/errors/BackendErrorHandler';

// Query key factory for Health API
export const healthApiKeys = {
  all: ['health-api'] as const,
  services: () => [...healthApiKeys.all, 'services'] as const,
  incidents: (days: number) => [...healthApiKeys.all, 'incidents', days] as const,
  history: (hours: number) => [...healthApiKeys.all, 'history', hours] as const,
} as const;

export interface ServiceHealth {
  id: string;
  name: string;
  status: 'healthy' | 'degraded' | 'unhealthy';
  uptime: string | { days: number };
  lastCheck: string;
  responseTime: number;
  details: Record<string, unknown>;
}

export interface HealthSummary {
  healthy: number;
  degraded: number;
  unhealthy: number;
  total: number;
}

export interface ServiceHealthData {
  timestamp: string;
  overallStatus: 'healthy' | 'degraded' | 'unhealthy';
  summary: HealthSummary;
  services: ServiceHealth[];
}

export interface Incident {
  id: string;
  title: string;
  type: 'service_degradation' | 'health_check_failure';
  severity: 'minor' | 'major' | 'critical';
  status: 'active' | 'resolved';
  startTime: string;
  endTime: string | null;
  affectedService: string;
  impact: string;
  details: {
    errorCount?: number;
    uniqueErrorTypes?: number;
    statusMessage?: string;
    responseTime?: number;
  };
}

export interface IncidentsSummary {
  type: string;
  count: number;
}

export interface IncidentsData {
  timestamp: string;
  timeRange: {
    start: string;
    end: string;
  };
  totalIncidents: number;
  activeIncidents: number;
  incidentsByType: IncidentsSummary[];
  incidentsBySeverity: IncidentsSummary[];
  incidents: Incident[];
}

export interface HealthHistoryPoint {
  timestamp: string;
  systemHealth: number;
  providerHealth: number;
  responseTime: number;
  requestVolume: number;
  errorRate: number;
}

export interface HealthHistoryData {
  timestamp: string;
  timeRange: {
    start: string;
    end: string;
  };
  intervalMinutes: number;
  history: HealthHistoryPoint[];
}

/**
 * Hook to fetch service health status
 */
export function useServiceHealth() {
  return useQuery({
    queryKey: healthApiKeys.services(),
    queryFn: async () => {
      try {
        const client = getAdminClient();
        const healthData = await client.system.getHealth();
        
        // Transform SDK response to match expected format
        const services: ServiceHealth[] = Object.entries(healthData.checks || {}).map(([name, check]) => ({
          id: name,
          name: name,
          status: check.status,
          uptime: 'Unknown',
          lastCheck: new Date().toISOString(),
          responseTime: check.duration || 0,
          details: { description: check.description, error: check.error },
        }));
        
        const summary = services.reduce((acc, service) => {
          acc[service.status] = (acc[service.status] || 0) + 1;
          acc.total++;
          return acc;
        }, { healthy: 0, degraded: 0, unhealthy: 0, total: 0 } as HealthSummary);
        
        return {
          timestamp: new Date().toISOString(),
          overallStatus: healthData.status?.toLowerCase() as 'healthy' | 'degraded' | 'unhealthy',
          summary,
          services,
        } as ServiceHealthData;
      } catch (error) {
        reportError(error as Error, 'Failed to fetch service health');
        throw BackendErrorHandler.classifyError(error);
      }
    },
    staleTime: 30000, // 30 seconds
    refetchInterval: 30000, // Refresh every 30 seconds
  });
}

/**
 * Hook to fetch incidents
 */
export function useIncidents(days: number = 7) {
  return useQuery({
    queryKey: healthApiKeys.incidents(days),
    queryFn: async () => {
      try {
        const _client = getAdminClient();
        // TODO: Replace with SDK method when incidents endpoint is added
        // For now, return mock data structure
        const now = new Date();
        const start = new Date(now.getTime() - days * 24 * 60 * 60 * 1000);
        
        return {
          timestamp: now.toISOString(),
          timeRange: {
            start: start.toISOString(),
            end: now.toISOString(),
          },
          totalIncidents: 0,
          activeIncidents: 0,
          incidentsByType: [],
          incidentsBySeverity: [],
          incidents: [],
        } as IncidentsData;
      } catch (error) {
        reportError(error as Error, 'Failed to fetch incidents');
        throw BackendErrorHandler.classifyError(error);
      }
    },
    staleTime: 60000, // 1 minute
  });
}

/**
 * Hook to fetch health history
 */
export function useHealthHistory(hours: number = 24) {
  return useQuery({
    queryKey: healthApiKeys.history(hours),
    queryFn: async () => {
      try {
        const _client = getAdminClient();
        // TODO: Replace with SDK method when health history endpoint is added
        // For now, return mock data structure
        const now = new Date();
        const start = new Date(now.getTime() - hours * 60 * 60 * 1000);
        const intervalMinutes = hours <= 24 ? 15 : 60;
        
        return {
          timestamp: now.toISOString(),
          timeRange: {
            start: start.toISOString(),
            end: now.toISOString(),
          },
          intervalMinutes,
          history: [],
        } as HealthHistoryData;
      } catch (error) {
        reportError(error as Error, 'Failed to fetch health history');
        throw BackendErrorHandler.classifyError(error);
      }
    },
    staleTime: hours <= 24 ? 60000 : 300000, // 1 min for 24h, 5 min for longer
  });
}

/**
 * Hook to invalidate health queries
 */
export function useInvalidateHealth() {
  const queryClient = useQueryClient();
  
  return {
    invalidateAll: () => queryClient.invalidateQueries({ queryKey: healthApiKeys.all }),
    invalidateServices: () => queryClient.invalidateQueries({ queryKey: healthApiKeys.services() }),
    invalidateIncidents: (days?: number) => {
      if (days !== undefined) {
        queryClient.invalidateQueries({ queryKey: healthApiKeys.incidents(days) });
      } else {
        queryClient.invalidateQueries({ 
          predicate: (query) => 
            query.queryKey[0] === 'health-api' && 
            query.queryKey[1] === 'incidents'
        });
      }
    },
    invalidateHistory: (hours?: number) => {
      if (hours !== undefined) {
        queryClient.invalidateQueries({ queryKey: healthApiKeys.history(hours) });
      } else {
        queryClient.invalidateQueries({ 
          predicate: (query) => 
            query.queryKey[0] === 'health-api' && 
            query.queryKey[1] === 'history'
        });
      }
    },
  };
}