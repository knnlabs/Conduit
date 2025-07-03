'use client';

import { useQuery, useQueryClient } from '@tanstack/react-query';
import { getAdminClient } from '@/lib/clients/conduit';
import { reportError } from '@/lib/utils/logging';
import { apiFetch } from '@/lib/utils/fetch-wrapper';

// Query key factory for Security API
export const securityApiKeys = {
  all: ['security-api'] as const,
  events: (hours: number) => [...securityApiKeys.all, 'events', hours] as const,
  threats: () => [...securityApiKeys.all, 'threats'] as const,
  compliance: () => [...securityApiKeys.all, 'compliance'] as const,
} as const;

export interface SecurityEvent {
  timestamp: string;
  type: 'auth_failure' | 'rate_limit' | 'blocked_ip' | 'suspicious_activity';
  severity: 'warning' | 'high';
  source: string;
  virtualKeyId: string | null;
  details: string;
  statusCode: number;
}

export interface EventsSummary {
  type: string;
  count: number;
}

export interface SecurityEventsData {
  timestamp: string;
  timeRange: {
    start: string;
    end: string;
  };
  totalEvents: number;
  eventsByType: EventsSummary[];
  eventsBySeverity: EventsSummary[];
  events: SecurityEvent[];
}

export interface ThreatSource {
  ipAddress: string;
  totalFailures: number;
  daysActive: number;
  lastSeen: string;
  riskScore: number;
}

export interface ThreatDistribution {
  type: string;
  count: number;
  uniqueIPs: number;
}

export interface ThreatTrend {
  date: string;
  threats: number;
}

export interface SecurityMetrics {
  totalThreatsToday: number;
  uniqueThreatsToday: number;
  blockedIPs: number;
  complianceScore: number;
}

export interface ThreatAnalytics {
  timestamp: string;
  metrics: SecurityMetrics;
  topThreats: ThreatSource[];
  threatDistribution: ThreatDistribution[];
  threatTrend: ThreatTrend[];
}

export interface ComplianceMetrics {
  timestamp: string;
  dataProtection: {
    encryptedKeys: number;
    secureEndpoints: boolean;
    dataRetentionDays: number;
    lastAudit: string;
  };
  accessControl: {
    activeKeys: number;
    keysWithBudgets: number;
    ipWhitelistEnabled: boolean;
    rateLimitingEnabled: boolean;
  };
  monitoring: {
    logRetentionDays: number;
    requestLoggingEnabled: boolean;
    securityAlertsEnabled: boolean;
    lastSecurityReview: string;
  };
  complianceScore: number;
}

/**
 * Hook to fetch security events
 */
export function useSecurityEvents(hours: number = 24) {
  return useQuery({
    queryKey: securityApiKeys.events(hours),
    queryFn: async () => {
      try {
        const client = await getAdminClient();
        const response = await apiFetch(`/api/security/events?hours=${hours}`, {
          method: 'GET',
          headers: { 'Content-Type': 'application/json' },
        });

        if (!response.ok) {
          throw new Error(`Failed to fetch security events: ${response.statusText}`);
        }

        return response.json() as Promise<SecurityEventsData>;
      } catch (error) {
        reportError(error as Error, 'Failed to fetch security events');
        throw error;
      }
    },
    staleTime: 60000, // 1 minute
  });
}

/**
 * Hook to fetch threat analytics
 */
export function useThreatAnalytics() {
  return useQuery({
    queryKey: securityApiKeys.threats(),
    queryFn: async () => {
      try {
        const client = await getAdminClient();
        const response = await apiFetch('/api/security/threats', {
          method: 'GET',
          headers: { 'Content-Type': 'application/json' },
        });

        if (!response.ok) {
          throw new Error(`Failed to fetch threat analytics: ${response.statusText}`);
        }

        return response.json() as Promise<ThreatAnalytics>;
      } catch (error) {
        reportError(error as Error, 'Failed to fetch threat analytics');
        throw error;
      }
    },
    staleTime: 300000, // 5 minutes (cached by API)
    refetchInterval: 300000, // Refresh every 5 minutes
  });
}

/**
 * Hook to fetch compliance metrics
 */
export function useComplianceMetrics() {
  return useQuery({
    queryKey: securityApiKeys.compliance(),
    queryFn: async () => {
      try {
        const client = await getAdminClient();
        const response = await apiFetch('/api/security/compliance', {
          method: 'GET',
          headers: { 'Content-Type': 'application/json' },
        });

        if (!response.ok) {
          throw new Error(`Failed to fetch compliance metrics: ${response.statusText}`);
        }

        return response.json() as Promise<ComplianceMetrics>;
      } catch (error) {
        reportError(error as Error, 'Failed to fetch compliance metrics');
        throw error;
      }
    },
    staleTime: 600000, // 10 minutes
  });
}

/**
 * Hook to invalidate security queries
 */
export function useInvalidateSecurity() {
  const queryClient = useQueryClient();
  
  return {
    invalidateAll: () => queryClient.invalidateQueries({ queryKey: securityApiKeys.all }),
    invalidateEvents: (hours?: number) => {
      if (hours !== undefined) {
        queryClient.invalidateQueries({ queryKey: securityApiKeys.events(hours) });
      } else {
        queryClient.invalidateQueries({ 
          predicate: (query) => 
            query.queryKey[0] === 'security-api' && 
            query.queryKey[1] === 'events'
        });
      }
    },
    invalidateThreats: () => queryClient.invalidateQueries({ queryKey: securityApiKeys.threats() }),
    invalidateCompliance: () => queryClient.invalidateQueries({ queryKey: securityApiKeys.compliance() }),
  };
}