import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { RequestConfig } from '../client/types';
import type {
  DashboardDto,
  CreateDashboardDto,
  UpdateDashboardDto,
} from '../models/monitoring';
import type { FilterOptions, PagedResponse } from '../models/common';

/**
 * Type-safe Monitoring dashboard service using native fetch
 */
export class FetchMonitoringDashboardService {
  constructor(private readonly client: FetchBaseApiClient) {}

  /**
   * List dashboards
   */
  async listDashboards(filters?: FilterOptions, config?: RequestConfig): Promise<PagedResponse<DashboardDto>> {
    const queryParams = new URLSearchParams();
    
    if (filters?.search) queryParams.append('search', filters.search);
    if (filters?.pageNumber) queryParams.append('pageNumber', filters.pageNumber.toString());
    if (filters?.pageSize) queryParams.append('pageSize', filters.pageSize.toString());

    const url = `/api/monitoring/dashboards${queryParams.toString() ? `?${queryParams.toString()}` : ''}`;

    return this.client['get']<PagedResponse<DashboardDto>>(
      url,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get dashboard by ID
   */
  async getDashboard(dashboardId: string, config?: RequestConfig): Promise<DashboardDto> {
    return this.client['get']<DashboardDto>(
      `/api/monitoring/dashboards/${dashboardId}`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Create dashboard
   */
  async createDashboard(dashboard: CreateDashboardDto, config?: RequestConfig): Promise<DashboardDto> {
    return this.client['post']<DashboardDto, CreateDashboardDto>(
      '/api/monitoring/dashboards',
      dashboard,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Update dashboard
   */
  async updateDashboard(
    dashboardId: string,
    dashboard: UpdateDashboardDto,
    config?: RequestConfig
  ): Promise<DashboardDto> {
    return this.client['put']<DashboardDto, UpdateDashboardDto>(
      `/api/monitoring/dashboards/${dashboardId}`,
      dashboard,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Delete dashboard
   */
  async deleteDashboard(dashboardId: string, config?: RequestConfig): Promise<void> {
    return this.client['delete']<void>(
      `/api/monitoring/dashboards/${dashboardId}`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Clone dashboard
   */
  async cloneDashboard(
    dashboardId: string,
    name: string,
    config?: RequestConfig
  ): Promise<DashboardDto> {
    return this.client['post']<DashboardDto, { name: string }>(
      `/api/monitoring/dashboards/${dashboardId}/clone`,
      { name },
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }
}