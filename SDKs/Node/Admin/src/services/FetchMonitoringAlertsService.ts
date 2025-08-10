import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { RequestConfig } from '../client/types';
import type {
  AlertDto,
  CreateAlertDto,
  UpdateAlertDto,
  AlertHistoryEntry,
  AlertSeverity,
  AlertStatus,
} from '../models/monitoring';
import type { FilterOptions, PagedResponse } from '../models/common';

/**
 * Type-safe Monitoring alerts service using native fetch
 */
export class FetchMonitoringAlertsService {
  constructor(private readonly client: FetchBaseApiClient) {}

  /**
   * List alerts
   */
  async listAlerts(
    filters?: FilterOptions & {
      severity?: AlertSeverity;
      status?: AlertStatus;
      metric?: string;
    },
    config?: RequestConfig
  ): Promise<PagedResponse<AlertDto>> {
    const queryParams = new URLSearchParams();
    
    if (filters?.search) queryParams.append('search', filters.search);
    if (filters?.pageNumber) queryParams.append('pageNumber', filters.pageNumber.toString());
    if (filters?.pageSize) queryParams.append('pageSize', filters.pageSize.toString());
    if (filters?.severity) queryParams.append('severity', filters.severity);
    if (filters?.status) queryParams.append('status', filters.status);
    if (filters?.metric) queryParams.append('metric', filters.metric);

    const url = `/api/monitoring/alerts${queryParams.toString() ? `?${queryParams.toString()}` : ''}`;

    return this.client['get']<PagedResponse<AlertDto>>(
      url,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get alert by ID
   */
  async getAlert(alertId: string, config?: RequestConfig): Promise<AlertDto> {
    return this.client['get']<AlertDto>(
      `/api/monitoring/alerts/${alertId}`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Create alert
   */
  async createAlert(alert: CreateAlertDto, config?: RequestConfig): Promise<AlertDto> {
    return this.client['post']<AlertDto, CreateAlertDto>(
      '/api/monitoring/alerts',
      alert,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Update alert
   */
  async updateAlert(alertId: string, alert: UpdateAlertDto, config?: RequestConfig): Promise<AlertDto> {
    return this.client['put']<AlertDto, UpdateAlertDto>(
      `/api/monitoring/alerts/${alertId}`,
      alert,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Delete alert
   */
  async deleteAlert(alertId: string, config?: RequestConfig): Promise<void> {
    return this.client['delete']<void>(
      `/api/monitoring/alerts/${alertId}`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Acknowledge alert
   */
  async acknowledgeAlert(alertId: string, notes?: string, config?: RequestConfig): Promise<AlertDto> {
    return this.client['post']<AlertDto, { notes?: string }>(
      `/api/monitoring/alerts/${alertId}/acknowledge`,
      { notes },
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Resolve alert
   */
  async resolveAlert(alertId: string, notes?: string, config?: RequestConfig): Promise<AlertDto> {
    return this.client['post']<AlertDto, { notes?: string }>(
      `/api/monitoring/alerts/${alertId}/resolve`,
      { notes },
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get alert history
   */
  async getAlertHistory(
    alertId: string,
    filters?: FilterOptions,
    config?: RequestConfig
  ): Promise<PagedResponse<AlertHistoryEntry>> {
    const queryParams = new URLSearchParams();
    
    if (filters?.search) queryParams.append('search', filters.search);
    if (filters?.pageNumber) queryParams.append('pageNumber', filters.pageNumber.toString());
    if (filters?.pageSize) queryParams.append('pageSize', filters.pageSize.toString());

    const url = `/api/monitoring/alerts/${alertId}/history${queryParams.toString() ? `?${queryParams.toString()}` : ''}`;

    return this.client['get']<PagedResponse<AlertHistoryEntry>>(
      url,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }
}