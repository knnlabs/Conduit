'use client';

import { useMemo, useCallback } from 'react';
import { useQuery } from '@tanstack/react-query';
import { withAdminClient } from '@/lib/client/adminClient';
import type {
  SecurityEvent,
  ThreatDetection,
  ComplianceMetrics,
  SecurityOverview,
  SecurityEventFiltersState,
  QuickStats,
} from './types';

/**
 * Main hook for fetching all security dashboard data
 */
export function useSecurityDashboardData() {
  // Fetch security events
  const {
    data: eventsData,
    isLoading: eventsLoading,
    error: eventsError,
    refetch: refetchEvents,
  } = useQuery({
    queryKey: ['security-events', { pageSize: 100 }],
    queryFn: async () => {
      return withAdminClient(client =>
        client.security.getEvents({ pageSize: 100 })
      );
    },
    refetchInterval: 60000, // Refresh every minute
  });

  // Fetch threats data
  const {
    data: threats,
    isLoading: threatsLoading,
    error: threatsError,
    refetch: refetchThreats,
  } = useQuery<ThreatDetection[]>({
    queryKey: ['security-threats'],
    queryFn: async () => {
      return withAdminClient(client => client.security.getThreats());
    },
    refetchInterval: 60000,
  });

  // Fetch compliance status
  const {
    data: compliance,
    isLoading: complianceLoading,
    error: complianceError,
    refetch: refetchCompliance,
  } = useQuery<ComplianceMetrics>({
    queryKey: ['security-compliance'],
    queryFn: async () => {
      const result = await withAdminClient(client =>
        client.security.getComplianceStatus()
      );
      return result as ComplianceMetrics;
    },
    refetchInterval: 300000, // Refresh every 5 minutes
  });

  const events = useMemo(() => eventsData?.items ?? [], [eventsData]);
  const totalEvents = eventsData?.totalCount ?? 0;

  // Compute security overview from events, threats, and compliance
  const overview = useMemo((): SecurityOverview => {
    const activeThreats = threats?.filter(t => t.status === 'active') ?? [];
    const now = Date.now();
    const eventsLast24h = events.filter(e => {
      const eventTime = new Date(e.timestamp).getTime();
      return now - eventTime < 24 * 60 * 60 * 1000;
    }).length;

    // Determine threat level based on active threats
    let threatLevel: SecurityOverview['threatLevel'] = 'low';
    if (activeThreats.some(t => t.severity === 'critical')) {
      threatLevel = 'critical';
    } else if (activeThreats.some(t => t.severity === 'major')) {
      threatLevel = 'high';
    } else if (activeThreats.length > 0) {
      threatLevel = 'medium';
    }

    return {
      threatLevel,
      complianceScore: compliance?.overallScore ?? null,
      activeThreatsCount: activeThreats.length,
      eventsLast24h,
    };
  }, [events, threats, compliance]);

  // Compute quick stats from events
  const quickStats = useMemo((): QuickStats => {
    const now = Date.now();
    const last24h = events.filter(e =>
      now - new Date(e.timestamp).getTime() < 24 * 60 * 60 * 1000
    );

    return {
      failedAuthAttempts24h: last24h.filter(e => e.type === 'authentication_failure').length,
      blockedIpsCount: last24h.filter(e => e.type === 'suspicious_activity').length,
      rateLimitViolations: last24h.filter(e => e.type === 'rate_limit_exceeded').length,
      suspiciousActivityCount: last24h.filter(e => e.type === 'suspicious_activity').length,
    };
  }, [events]);

  const refetchAll = useCallback(async () => {
    await Promise.all([refetchEvents(), refetchThreats(), refetchCompliance()]);
  }, [refetchEvents, refetchThreats, refetchCompliance]);

  const isLoading = eventsLoading || threatsLoading || complianceLoading;
  const error = eventsError ?? threatsError ?? complianceError;

  return {
    events,
    totalEvents,
    threats: threats ?? [],
    compliance,
    overview,
    quickStats,
    isLoading,
    error,
    refetchAll,
  };
}

/**
 * Hook for fetching filtered security events with pagination
 */
export function useSecurityEventsFiltered(filters: SecurityEventFiltersState) {
  return useQuery({
    queryKey: ['security-events-filtered', filters],
    queryFn: async () => {
      return withAdminClient(client =>
        client.security.getEvents({
          page: filters.page,
          pageSize: filters.pageSize,
          severity: filters.severity,
          startDate: filters.startDate,
          endDate: filters.endDate,
        })
      );
    },
  });
}

/**
 * Utility hook for computing event statistics
 */
export function useEventStats(events: SecurityEvent[]) {
  return useMemo(() => {
    const now = Date.now();
    const last24h = events.filter(e =>
      now - new Date(e.timestamp).getTime() < 24 * 60 * 60 * 1000
    );
    const last7d = events.filter(e =>
      now - new Date(e.timestamp).getTime() < 7 * 24 * 60 * 60 * 1000
    );

    const bySeverity = {
      critical: events.filter(e => e.severity === 'critical').length,
      high: events.filter(e => e.severity === 'high').length,
      medium: events.filter(e => e.severity === 'medium').length,
      low: events.filter(e => e.severity === 'low').length,
    };

    const byType = {
      authentication_failure: events.filter(e => e.type === 'authentication_failure').length,
      rate_limit_exceeded: events.filter(e => e.type === 'rate_limit_exceeded').length,
      suspicious_activity: events.filter(e => e.type === 'suspicious_activity').length,
      invalid_api_key: events.filter(e => e.type === 'invalid_api_key').length,
    };

    return {
      total: events.length,
      last24h: last24h.length,
      last7d: last7d.length,
      bySeverity,
      byType,
    };
  }, [events]);
}
