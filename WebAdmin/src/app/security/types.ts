/**
 * Type definitions for the Security Dashboard
 */

// Re-export SDK types for local use
export type {
  SecurityEvent,
  ThreatDetection,
  ComplianceMetrics,
  SecurityEventFilters,
} from '@knn_labs/conduit-admin-client';

/**
 * Security overview summary for the dashboard header
 */
export interface SecurityOverview {
  /** Current threat level based on active threats */
  threatLevel: 'low' | 'medium' | 'high' | 'critical';
  /** Overall compliance score (0-100), null if not available */
  complianceScore: number | null;
  /** Count of active (unresolved) threats */
  activeThreatsCount: number;
  /** Number of security events in the last 24 hours */
  eventsLast24h: number;
}

/**
 * Filter state for the security events table
 */
export interface SecurityEventFiltersState {
  /** Filter by severity level */
  severity?: 'low' | 'medium' | 'high' | 'critical';
  /** Start date for date range filter */
  startDate?: string;
  /** End date for date range filter */
  endDate?: string;
  /** Current page number */
  page: number;
  /** Number of items per page */
  pageSize: number;
}

/**
 * Quick statistics for the dashboard
 */
export interface QuickStats {
  /** Number of failed authentication attempts in the last 24 hours */
  failedAuthAttempts24h: number;
  /** Number of currently blocked IP addresses */
  blockedIpsCount: number;
  /** Number of rate limit violations in the last 24 hours */
  rateLimitViolations: number;
  /** Number of suspicious activity events in the last 24 hours */
  suspiciousActivityCount: number;
}

/**
 * Date range for filtering
 */
export interface DateRange {
  startDate: string;
  endDate: string;
}
