import { BaseApiClient } from '../client/BaseApiClient';
import { ENDPOINTS, CACHE_TTL } from '../constants';
import {
  SecurityEvent,
  CreateSecurityEventDto,
  SecurityEventFilters,
  ThreatDetection,
  ThreatFilters,
  ThreatAnalytics,
  ComplianceMetrics,
  PagedResult,
  ThreatAction,
} from '../models/security';
import { ValidationError } from '../utils/errors';
import { z } from 'zod';

/**
 * Schema for creating a security event
 */
const createSecurityEventSchema = z.object({
  type: z.enum(['authentication_failure', 'rate_limit_exceeded', 'suspicious_activity', 'invalid_api_key']),
  severity: z.enum(['low', 'medium', 'high', 'critical']),
  source: z.string().min(1),
  virtualKeyId: z.string().optional(),
  ipAddress: z.string().optional(),
  details: z.record(z.any()),
  statusCode: z.number().optional(),
});

/**
 * Schema for security event filters
 */
const securityEventFiltersSchema = z.object({
  hours: z.number().positive().optional(),
  startDate: z.string().optional(),
  endDate: z.string().optional(),
  severity: z.enum(['low', 'medium', 'high', 'critical']).optional(),
  type: z.enum(['authentication_failure', 'rate_limit_exceeded', 'suspicious_activity', 'invalid_api_key']).optional(),
  page: z.number().positive().optional(),
  pageSize: z.number().positive().max(100).optional(),
});

/**
 * Schema for threat filters
 */
const threatFiltersSchema = z.object({
  status: z.enum(['active', 'acknowledged', 'resolved']).optional(),
  severity: z.enum(['minor', 'major', 'critical']).optional(),
  page: z.number().positive().optional(),
  pageSize: z.number().positive().max(100).optional(),
});

/**
 * Service for managing security events, threat detection, and compliance
 */
export class SecurityService extends BaseApiClient {
  /**
   * Get security events with optional filters
   * @param params - Filtering and pagination parameters
   * @returns Paged result of security events
   */
  async getEvents(params?: SecurityEventFilters): Promise<PagedResult<SecurityEvent>> {
    if (params) {
      const parsed = securityEventFiltersSchema.safeParse(params);
      if (!parsed.success) {
        throw new ValidationError('Invalid security event filters', { 
          validationErrors: parsed.error.errors,
          issues: parsed.error.format()
        });
      }
    }

    const queryParams = new URLSearchParams();
    if (params) {
      Object.entries(params).forEach(([key, value]) => {
        if (value !== undefined) {
          queryParams.append(key, value.toString());
        }
      });
    }

    const url = `${ENDPOINTS.SECURITY.EVENTS}?${queryParams.toString()}`;
    return this.withCache(
      url,
      () => this.get<PagedResult<SecurityEvent>>(url),
      CACHE_TTL.SHORT
    );
  }

  /**
   * Report a new security event
   * @param event - Security event to report
   * @returns Created security event
   */
  async reportEvent(event: CreateSecurityEventDto): Promise<SecurityEvent> {
    const parsed = createSecurityEventSchema.safeParse(event);
    if (!parsed.success) {
      throw new ValidationError('Invalid security event data', { 
        validationErrors: parsed.error.errors,
        issues: parsed.error.format()
      });
    }

    const result = await this.post<SecurityEvent>(ENDPOINTS.SECURITY.REPORT_EVENT, parsed.data);
    await this.invalidateSecurityCache();
    return result;
  }

  /**
   * Export security events in specified format
   * @param format - Export format (json or csv)
   * @param filters - Optional filters for events to export
   * @returns Blob containing exported data
   */
  async exportEvents(format: 'json' | 'csv', filters?: SecurityEventFilters): Promise<Blob> {
    if (filters) {
      const parsed = securityEventFiltersSchema.safeParse(filters);
      if (!parsed.success) {
        throw new ValidationError('Invalid security event filters', { 
          validationErrors: parsed.error.errors,
          issues: parsed.error.format()
        });
      }
    }

    const queryParams = new URLSearchParams({ format });
    if (filters) {
      Object.entries(filters).forEach(([key, value]) => {
        if (value !== undefined) {
          queryParams.append(key, value.toString());
        }
      });
    }

    const url = `${ENDPOINTS.SECURITY.EXPORT_EVENTS}?${queryParams.toString()}`;
    const response = await this.get<Blob>(url, {
      headers: { Accept: format === 'csv' ? 'text/csv' : 'application/json' },
      responseType: 'blob',
    } as any);

    return response;
  }

  /**
   * Get detected threats with optional filters
   * @param params - Filtering and pagination parameters
   * @returns Paged result of detected threats
   */
  async getThreats(params?: ThreatFilters): Promise<PagedResult<ThreatDetection>> {
    if (params) {
      const parsed = threatFiltersSchema.safeParse(params);
      if (!parsed.success) {
        throw new ValidationError('Invalid threat filters', { 
          validationErrors: parsed.error.errors,
          issues: parsed.error.format()
        });
      }
    }

    const queryParams = new URLSearchParams();
    if (params) {
      Object.entries(params).forEach(([key, value]) => {
        if (value !== undefined) {
          queryParams.append(key, value.toString());
        }
      });
    }

    const url = `${ENDPOINTS.SECURITY.THREATS}?${queryParams.toString()}`;
    return this.withCache(
      url,
      () => this.get<PagedResult<ThreatDetection>>(url),
      CACHE_TTL.SHORT
    );
  }

  /**
   * Update the status of a detected threat
   * @param threatId - ID of the threat to update
   * @param action - Action to take on the threat
   * @returns Updated threat detection
   */
  async updateThreatStatus(threatId: string, action: ThreatAction): Promise<ThreatDetection> {
    if (!threatId || threatId.trim() === '') {
      throw new ValidationError('Threat ID is required');
    }

    if (!['acknowledge', 'resolve', 'ignore'].includes(action)) {
      throw new ValidationError('Invalid threat action');
    }

    const result = await this.patch<ThreatDetection>(
      ENDPOINTS.SECURITY.THREAT_BY_ID(threatId),
      { action }
    );
    
    await this.invalidateSecurityCache();
    return result;
  }

  /**
   * Get threat analytics and statistics
   * @returns Threat analytics data
   */
  async getThreatAnalytics(): Promise<ThreatAnalytics> {
    return this.withCache(
      ENDPOINTS.SECURITY.THREAT_ANALYTICS,
      () => this.get<ThreatAnalytics>(ENDPOINTS.SECURITY.THREAT_ANALYTICS),
      CACHE_TTL.MEDIUM
    );
  }

  /**
   * Get compliance metrics
   * @returns Current compliance metrics
   */
  async getComplianceMetrics(): Promise<ComplianceMetrics> {
    return this.withCache(
      ENDPOINTS.SECURITY.COMPLIANCE_METRICS,
      () => this.get<ComplianceMetrics>(ENDPOINTS.SECURITY.COMPLIANCE_METRICS),
      CACHE_TTL.LONG
    );
  }

  /**
   * Get compliance report in specified format
   * @param format - Export format (json or pdf)
   * @returns Blob containing compliance report
   */
  async getComplianceReport(format: 'json' | 'pdf'): Promise<Blob> {
    if (!['json', 'pdf'].includes(format)) {
      throw new ValidationError('Invalid report format. Must be json or pdf');
    }

    const url = `${ENDPOINTS.SECURITY.COMPLIANCE_REPORT}?format=${format}`;
    const response = await this.get<Blob>(url, {
      headers: { Accept: format === 'pdf' ? 'application/pdf' : 'application/json' },
      responseType: 'blob',
    } as any);

    return response;
  }

  /**
   * Invalidate security-related cache entries
   */
  private async invalidateSecurityCache(): Promise<void> {
    if (!this.cache) return;
    
    // Clear all security-related cache entries
    const keysToInvalidate = [
      ENDPOINTS.SECURITY.EVENTS,
      ENDPOINTS.SECURITY.THREATS,
      ENDPOINTS.SECURITY.THREAT_ANALYTICS,
      ENDPOINTS.SECURITY.COMPLIANCE_METRICS,
    ];

    for (const key of keysToInvalidate) {
      await this.cache.delete(key);
    }
  }
}