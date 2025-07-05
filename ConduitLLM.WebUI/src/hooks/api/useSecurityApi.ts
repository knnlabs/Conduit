'use client';

import { useQuery, useQueryClient, useMutation } from '@tanstack/react-query';
import { getAdminClient } from '@/lib/clients/conduit';
import { reportError } from '@/lib/utils/logging';
import { BackendErrorHandler } from '@/lib/errors/BackendErrorHandler';
import { notifications } from '@mantine/notifications';
import type { CreateSecurityEventDto, SecurityEvent as SDKSecurityEvent } from '@knn_labs/conduit-admin-client';

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
        const client = getAdminClient();
        const endDate = new Date().toISOString();
        const startDate = new Date(Date.now() - hours * 60 * 60 * 1000).toISOString();
        
        const result = await client.security.getEvents({
          startDate,
          endDate,
          pageSize: 100, // Get more events
        });
        
        // Transform SDK response to match expected format
        const eventsByType = result.items.reduce((acc, event) => {
          const type = event.type;
          const existing = acc.find(e => e.type === type);
          if (existing) {
            existing.count++;
          } else {
            acc.push({ type, count: 1 });
          }
          return acc;
        }, [] as EventsSummary[]);
        
        const eventsBySeverity = result.items.reduce((acc, event) => {
          const severity = event.severity;
          const existing = acc.find(e => e.type === severity);
          if (existing) {
            existing.count++;
          } else {
            acc.push({ type: severity, count: 1 });
          }
          return acc;
        }, [] as EventsSummary[]);
        
        const transformedData: SecurityEventsData = {
          timestamp: new Date().toISOString(),
          timeRange: { start: startDate, end: endDate },
          totalEvents: result.totalCount,
          eventsByType,
          eventsBySeverity,
          events: result.items.map(event => ({
            timestamp: event.timestamp,
            type: event.type as 'auth_failure' | 'rate_limit' | 'blocked_ip' | 'suspicious_activity',
            severity: event.severity as 'warning' | 'high',
            source: event.source,
            virtualKeyId: event.virtualKeyId || null,
            details: event.details?.message || JSON.stringify(event.details),
            statusCode: event.statusCode || 0,
          })),
        };
        
        return transformedData;
      } catch (error) {
        const backendError = BackendErrorHandler.classifyError(error);
        reportError(new Error(BackendErrorHandler.getUserFriendlyMessage(backendError)), 'Failed to fetch security events');
        throw backendError;
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
        const client = getAdminClient();
        const analytics = await client.security.getThreatAnalytics();
        
        // Transform SDK response to match expected format
        const transformedData: ThreatAnalytics = {
          timestamp: new Date().toISOString(),
          metrics: {
            totalThreatsToday: analytics.metrics.activeThreats,
            uniqueThreatsToday: analytics.metrics.suspiciousActivity,
            blockedIPs: analytics.metrics.blockedRequests,
            complianceScore: 85, // TODO: Get from actual compliance data
          },
          topThreats: analytics.topThreats.slice(0, 5).map((threat, index) => ({
            ipAddress: `${threat.type}-source-${index}`,
            totalFailures: threat.count,
            daysActive: 7, // TODO: Calculate from actual data
            lastSeen: new Date().toISOString(),
            riskScore: threat.count > 100 ? 90 : threat.count > 50 ? 70 : 50,
          })),
          threatDistribution: analytics.topThreats.map(threat => ({
            type: threat.type,
            count: threat.count,
            uniqueIPs: Math.ceil(threat.count / 5), // Estimate
          })),
          threatTrend: analytics.threatTrend.map(trend => ({
            date: trend.date,
            threats: trend.count,
          })),
        };
        
        return transformedData;
      } catch (error) {
        const backendError = BackendErrorHandler.classifyError(error);
        reportError(new Error(BackendErrorHandler.getUserFriendlyMessage(backendError)), 'Failed to fetch threat analytics');
        throw backendError;
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
        const client = getAdminClient();
        const metrics = await client.security.getComplianceMetrics();
        
        // Transform SDK response to match expected format
        const transformedData: ComplianceMetrics = {
          timestamp: metrics.lastAssessment,
          dataProtection: {
            encryptedKeys: 100, // TODO: Get from actual data
            secureEndpoints: true, // TODO: Get from actual data
            dataRetentionDays: 90, // TODO: Get from actual data
            lastAudit: metrics.lastAssessment,
          },
          accessControl: {
            activeKeys: 50, // TODO: Get from actual data
            keysWithBudgets: 45, // TODO: Get from actual data
            ipWhitelistEnabled: true, // TODO: Get from actual data
            rateLimitingEnabled: true, // TODO: Get from actual data
          },
          monitoring: {
            logRetentionDays: 30, // TODO: Get from actual data
            requestLoggingEnabled: true, // TODO: Get from actual data
            securityAlertsEnabled: true, // TODO: Get from actual data
            lastSecurityReview: metrics.lastAssessment,
          },
          complianceScore: metrics.overallScore,
        };
        
        return transformedData;
      } catch (error) {
        const backendError = BackendErrorHandler.classifyError(error);
        reportError(new Error(BackendErrorHandler.getUserFriendlyMessage(backendError)), 'Failed to fetch compliance metrics');
        throw backendError;
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

/**
 * Hook to create a security event
 */
export function useCreateSecurityEvent() {
  const queryClient = useQueryClient();
  const client = getAdminClient();

  return useMutation<SDKSecurityEvent, Error, CreateSecurityEventDto>({
    mutationFn: async (eventData: CreateSecurityEventDto) => {
      return await client.security.reportEvent(eventData);
    },
    onSuccess: (data) => {
      // Invalidate security events queries to refresh the data
      queryClient.invalidateQueries({ 
        predicate: (query) => 
          query.queryKey[0] === 'security-api' && 
          query.queryKey[1] === 'events'
      });
      
      notifications.show({
        title: 'Security Event Created',
        message: `Security event of type "${data.type}" has been reported successfully`,
        color: 'green',
      });
    },
    onError: (error: Error) => {
      const backendError = BackendErrorHandler.classifyError(error);
      const message = BackendErrorHandler.getUserFriendlyMessage(backendError);
      
      notifications.show({
        title: 'Failed to Create Security Event',
        message,
        color: 'red',
      });
      
      reportError(error, 'Failed to create security event');
    },
  });
}