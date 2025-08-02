import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { RequestConfig } from '../client/types';
import { ENDPOINTS } from '../constants';
import type {
  SecurityEvent,
  SecurityEventFilters,
  ThreatDetection,
  PagedResult,
} from '../models/security';

/**
 * Service for security-related operations
 * NOTE: This service has limited functionality. Most security endpoints have been removed.
 */
export class FetchSecurityService {
  constructor(private readonly client: FetchBaseApiClient) {}

  /**
   * Get security events with optional filtering
   */
  async getEvents(
    filter?: SecurityEventFilters,
    config?: RequestConfig
  ): Promise<PagedResult<SecurityEvent>> {
    const queryParams = new URLSearchParams();
    if (filter) {
      Object.entries(filter).forEach(([key, value]) => {
        if (value !== undefined) {
          queryParams.append(key, String(value));
        }
      });
    }

    const url = queryParams.toString()
      ? `${ENDPOINTS.SECURITY.EVENTS}?${queryParams.toString()}`
      : ENDPOINTS.SECURITY.EVENTS;

    return this.client['get']<PagedResult<SecurityEvent>>(url, {
      signal: config?.signal,
      timeout: config?.timeout,
      headers: config?.headers,
    });
  }

  /**
   * Get threat detection status
   */
  async getThreats(config?: RequestConfig): Promise<ThreatDetection[]> {
    return this.client['get']<ThreatDetection[]>(
      ENDPOINTS.SECURITY.THREATS,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get compliance status
   */
  async getComplianceStatus(config?: RequestConfig): Promise<unknown> {
    return this.client['get']<unknown>(
      ENDPOINTS.SECURITY.COMPLIANCE,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }
}