import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import { HttpMethod } from '../client/HttpMethod';
import type { RequestConfig } from '../client/types';
import { ENDPOINTS } from '../constants';
import type {
  IpWhitelistDto,
  SecurityEventParams,
  SecurityEventPage,
  SecurityEventExtended,
  ThreatSummaryDto,
  ActiveThreat,
  AccessPolicy,
  PolicyRule,
  CreateAccessPolicyDto,
  UpdateAccessPolicyDto,
  AuditLogParams,
  AuditLogPage,
  ExportParams,
  ExportResult,
} from '../models/securityExtended';
import type {
  SecurityEvent,
  CreateSecurityEventDto,
  SecurityEventFilters,
  ThreatDetection,
  ThreatFilters,
  ThreatAnalytics,
  ComplianceMetrics,
  PagedResult,
} from '../models/security';

// Define ComplianceReport type locally
interface ComplianceReport {
  startDate: string;
  endDate: string;
  overallScore: number;
  categories: {
    dataProtection: { score: number; issues: string[] };
    accessControl: { score: number; issues: string[] };
    auditCompliance: { score: number; issues: string[] };
    threatResponse: { score: number; issues: string[] };
  };
  recommendations: string[];
  generatedAt: string;
}

/**
 * Type-safe Security service using native fetch
 */
export class FetchSecurityService {
  constructor(private readonly client: FetchBaseApiClient) {}

  // IP Management

  /**
   * Get IP whitelist configuration
   */
  async getIpWhitelist(config?: RequestConfig): Promise<IpWhitelistDto> {
    return this.client['get']<IpWhitelistDto>(
      '/api/security/ip-whitelist',
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Add IPs to whitelist
   */
  async addToIpWhitelist(ips: string[], config?: RequestConfig): Promise<void> {
    return this.client['post']<void>(
      '/api/security/ip-whitelist',
      { ips },
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Remove IPs from whitelist
   */
  async removeFromIpWhitelist(ips: string[], config?: RequestConfig): Promise<void> {
    // Use a custom request since we need to send a body with DELETE
    const headers = {
      'Content-Type': 'application/json',
      ...config?.headers,
    };
    
    return this.client['request']<void>(
      '/api/security/ip-whitelist',
      {
        method: HttpMethod.DELETE,
        headers,
        body: JSON.stringify({ ips }),
        signal: config?.signal,
        timeout: config?.timeout,
      }
    );
  }

  // Security Events

  /**
   * Get security events with filtering
   */
  async getSecurityEvents(params?: SecurityEventParams, config?: RequestConfig): Promise<SecurityEventPage> {
    const queryParams = new URLSearchParams();
    
    if (params) {
      if (params.pageNumber) queryParams.append('page', params.pageNumber.toString());
      if (params.pageSize) queryParams.append('pageSize', params.pageSize.toString());
      if (params.startDate) queryParams.append('startDate', params.startDate);
      if (params.endDate) queryParams.append('endDate', params.endDate);
      if (params.severity) queryParams.append('severity', params.severity);
      if (params.type) queryParams.append('type', params.type);
      if (params.status) queryParams.append('status', params.status);
    }

    const queryString = queryParams.toString();
    const url = queryString ? `/api/security/events?${queryString}` : '/api/security/events';

    return this.client['get']<SecurityEventPage>(
      url,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get security events using existing endpoint and types
   */
  async getEvents(filters?: SecurityEventFilters, config?: RequestConfig): Promise<PagedResult<SecurityEvent>> {
    const queryParams = new URLSearchParams();
    
    if (filters) {
      if (filters.page) queryParams.append('page', filters.page.toString());
      if (filters.pageSize) queryParams.append('pageSize', filters.pageSize.toString());
      if (filters.hours) queryParams.append('hours', filters.hours.toString());
      if (filters.startDate) queryParams.append('startDate', filters.startDate);
      if (filters.endDate) queryParams.append('endDate', filters.endDate);
      if (filters.severity) queryParams.append('severity', filters.severity);
      if (filters.type) queryParams.append('type', filters.type);
    }

    const queryString = queryParams.toString();
    const url = queryString ? `${ENDPOINTS.SECURITY.EVENTS}?${queryString}` : ENDPOINTS.SECURITY.EVENTS;

    return this.client['get']<PagedResult<SecurityEvent>>(
      url,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get a specific security event by ID
   */
  async getSecurityEventById(id: string, config?: RequestConfig): Promise<SecurityEventExtended> {
    return this.client['get']<SecurityEventExtended>(
      `/api/security/events/${id}`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Acknowledge a security event
   */
  async acknowledgeSecurityEvent(id: string, config?: RequestConfig): Promise<void> {
    return this.client['post']<void>(
      `/api/security/events/${id}/acknowledge`,
      {},
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Report a new security event
   */
  async reportEvent(event: CreateSecurityEventDto, config?: RequestConfig): Promise<SecurityEvent> {
    return this.client['post']<SecurityEvent, CreateSecurityEventDto>(
      ENDPOINTS.SECURITY.REPORT_EVENT,
      event,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Export security events
   */
  async exportEvents(params: ExportParams, config?: RequestConfig): Promise<ExportResult> {
    return this.client['post']<ExportResult, ExportParams>(
      ENDPOINTS.SECURITY.EXPORT_EVENTS,
      params,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  // Threat Detection

  /**
   * Get threat summary
   */
  async getThreatSummary(config?: RequestConfig): Promise<ThreatSummaryDto> {
    return this.client['get']<ThreatSummaryDto>(
      '/api/security/threats',
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get active threats
   */
  async getActiveThreats(config?: RequestConfig): Promise<ActiveThreat[]> {
    return this.client['get']<ActiveThreat[]>(
      '/api/security/threats/active',
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get threats using existing endpoint
   */
  async getThreats(filters?: ThreatFilters, config?: RequestConfig): Promise<PagedResult<ThreatDetection>> {
    const queryParams = new URLSearchParams();
    
    if (filters) {
      if (filters.page) queryParams.append('page', filters.page.toString());
      if (filters.pageSize) queryParams.append('pageSize', filters.pageSize.toString());
      if (filters.status) queryParams.append('status', filters.status);
      if (filters.severity) queryParams.append('severity', filters.severity);
    }

    const queryString = queryParams.toString();
    const url = queryString ? `${ENDPOINTS.SECURITY.THREATS}?${queryString}` : ENDPOINTS.SECURITY.THREATS;

    return this.client['get']<PagedResult<ThreatDetection>>(
      url,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Update threat status
   */
  async updateThreatStatus(
    id: string, 
    action: 'acknowledge' | 'resolve' | 'ignore',
    config?: RequestConfig
  ): Promise<void> {
    return this.client['put']<void>(
      ENDPOINTS.SECURITY.THREAT_BY_ID(id),
      { action },
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get threat analytics
   */
  async getThreatAnalytics(config?: RequestConfig): Promise<ThreatAnalytics> {
    return this.client['get']<ThreatAnalytics>(
      ENDPOINTS.SECURITY.THREAT_ANALYTICS,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  // Access Control

  /**
   * Get access policies
   */
  async getAccessPolicies(config?: RequestConfig): Promise<AccessPolicy[]> {
    return this.client['get']<AccessPolicy[]>(
      '/api/security/policies',
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Create access policy
   */
  async createAccessPolicy(
    policy: CreateAccessPolicyDto, 
    config?: RequestConfig
  ): Promise<AccessPolicy> {
    return this.client['post']<AccessPolicy, CreateAccessPolicyDto>(
      '/api/security/policies',
      policy,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Update access policy
   */
  async updateAccessPolicy(
    id: string, 
    policy: UpdateAccessPolicyDto, 
    config?: RequestConfig
  ): Promise<AccessPolicy> {
    return this.client['put']<AccessPolicy, UpdateAccessPolicyDto>(
      `/api/security/policies/${id}`,
      policy,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Delete access policy
   */
  async deleteAccessPolicy(id: string, config?: RequestConfig): Promise<void> {
    return this.client['delete']<void>(
      `/api/security/policies/${id}`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  // Audit Logs

  /**
   * Get audit logs
   */
  async getAuditLogs(params?: AuditLogParams, config?: RequestConfig): Promise<AuditLogPage> {
    const queryParams = new URLSearchParams();
    
    if (params) {
      if (params.pageNumber) queryParams.append('page', params.pageNumber.toString());
      if (params.pageSize) queryParams.append('pageSize', params.pageSize.toString());
      if (params.startDate) queryParams.append('startDate', params.startDate);
      if (params.endDate) queryParams.append('endDate', params.endDate);
      if (params.action) queryParams.append('action', params.action);
      if (params.userId) queryParams.append('userId', params.userId);
      if (params.resourceType) queryParams.append('resourceType', params.resourceType);
      if (params.resourceId) queryParams.append('resourceId', params.resourceId);
    }

    const queryString = queryParams.toString();
    const url = queryString ? `/api/security/audit-logs?${queryString}` : '/api/security/audit-logs';

    return this.client['get']<AuditLogPage>(
      url,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Export audit logs
   */
  async exportAuditLogs(params: ExportParams, config?: RequestConfig): Promise<ExportResult> {
    return this.client['post']<ExportResult, ExportParams>(
      '/api/security/audit-logs/export',
      params,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  // Compliance

  /**
   * Get compliance metrics
   */
  async getComplianceMetrics(config?: RequestConfig): Promise<ComplianceMetrics> {
    return this.client['get']<ComplianceMetrics>(
      ENDPOINTS.SECURITY.COMPLIANCE_METRICS,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get compliance report
   */
  async getComplianceReport(
    startDate: string, 
    endDate: string, 
    config?: RequestConfig
  ): Promise<ComplianceReport> {
    const queryParams = new URLSearchParams({
      startDate,
      endDate,
    });

    return this.client['get']<ComplianceReport>(
      `${ENDPOINTS.SECURITY.COMPLIANCE_REPORT}?${queryParams}`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  // Helper methods

  /**
   * Validate IP address or CIDR notation
   */
  validateIpAddress(ip: string): boolean {
    // IPv4 validation
    const ipv4Regex = /^(\d{1,3}\.){3}\d{1,3}$/;
    const cidrv4Regex = /^(\d{1,3}\.){3}\d{1,3}\/\d{1,2}$/;
    
    // IPv6 validation (simplified)
    const ipv6Regex = /^([\da-fA-F]{1,4}:){7}[\da-fA-F]{1,4}$/;
    const cidrv6Regex = /^([\da-fA-F]{1,4}:){7}[\da-fA-F]{1,4}\/\d{1,3}$/;
    
    return ipv4Regex.test(ip) || cidrv4Regex.test(ip) || 
           ipv6Regex.test(ip) || cidrv6Regex.test(ip);
  }

  /**
   * Calculate security score based on metrics
   */
  calculateSecurityScore(metrics: {
    blockedAttempts: number;
    suspiciousActivities: number;
    activeThreats: number;
    failedAuthentications: number;
  }): number {
    const baseScore = 100;
    const deductions = {
      blockedAttempts: Math.min(metrics.blockedAttempts * 0.5, 20),
      suspiciousActivities: Math.min(metrics.suspiciousActivities * 2, 30),
      activeThreats: Math.min(metrics.activeThreats * 10, 40),
      failedAuthentications: Math.min(metrics.failedAuthentications * 0.1, 10),
    };
    
    const totalDeduction = Object.values(deductions).reduce((sum, val) => sum + val, 0);
    return Math.max(0, baseScore - totalDeduction);
  }

  /**
   * Group security events by type
   */
  groupEventsByType(events: SecurityEventExtended[]): Record<string, SecurityEventExtended[]> {
    return events.reduce((acc, event) => {
      if (!acc[event.type]) {
        acc[event.type] = [];
      }
      acc[event.type].push(event);
      return acc;
    }, {} as Record<string, SecurityEventExtended[]>);
  }

  /**
   * Get severity color for UI display
   */
  getSeverityColor(severity: 'low' | 'medium' | 'high' | 'critical'): string {
    const colors = {
      low: '#10B981',      // green
      medium: '#F59E0B',   // amber
      high: '#EF4444',     // red
      critical: '#7C3AED', // purple
    };
    return colors[severity];
  }

  /**
   * Format threat level for display
   */
  formatThreatLevel(level: 'low' | 'medium' | 'high' | 'critical'): string {
    return `${level.charAt(0).toUpperCase() + level.slice(1)} Risk`;
  }

  /**
   * Check if an IP is in a CIDR range
   */
  isIpInRange(ip: string, cidr: string): boolean {
    // This is a simplified implementation
    // In production, use a proper IP range checking library
    const [range, bits] = cidr.split('/');
    if (!bits) return ip === range;
    
    // Convert IPs to numbers for comparison
    const ipToNumber = (ip: string): number => {
      return ip.split('.').reduce((acc, octet) => (acc << 8) + parseInt(octet), 0) >>> 0;
    };
    
    const mask = (0xffffffff << (32 - parseInt(bits))) >>> 0;
    const ipNum = ipToNumber(ip);
    const rangeNum = ipToNumber(range);
    
    return (ipNum & mask) === (rangeNum & mask);
  }

  /**
   * Generate policy recommendation based on current threats
   */
  generatePolicyRecommendation(threats: ActiveThreat[]): PolicyRule[] {
    const recommendations: PolicyRule[] = [];
    
    // Group threats by source
    const threatsBySource = threats.reduce((acc, threat) => {
      if (!acc[threat.source]) {
        acc[threat.source] = [];
      }
      acc[threat.source].push(threat);
      return acc;
    }, {} as Record<string, ActiveThreat[]>);
    
    // Generate recommendations
    Object.entries(threatsBySource).forEach(([source, sourceThreats]) => {
      if (sourceThreats.length >= 3) {
        recommendations.push({
          condition: {
            field: 'source_ip',
            operator: 'equals',
            value: source,
          },
          action: 'deny',
          metadata: {
            reason: `Multiple threats detected from ${source}`,
            threatCount: sourceThreats.length,
          },
        });
      }
    });
    
    return recommendations;
  }
}