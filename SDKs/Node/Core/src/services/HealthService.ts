import { BaseService } from './BaseService';
import type { HealthCheckResponse, SimpleHealthStatus, HealthCheckItem } from '../models/health';

export class HealthService extends BaseService {
  async checkHealth(): Promise<SimpleHealthStatus> {
    const response = await this.clientAdapter.get<{ status: string }>('/health');
    return {
      status: response.status === 'Healthy' ? 'healthy' : 'unhealthy'
    };
  }

  async checkReady(): Promise<HealthCheckResponse> {
    // The /health endpoint returns a basic health check
    // This is a more detailed endpoint for readiness checks
    try {
      const response = await this.clientAdapter.get<HealthCheckResponse>('/health/ready');
      
      // If the ready endpoint exists, use it
      return this.normalizeHealthResponse(response);
    } catch {
      // Fallback to basic health check if ready endpoint doesn't exist
      const basicHealth = await this.checkHealth();
      return {
        status: basicHealth.status,
        checks: [{
          name: 'core-api',
          status: basicHealth.status,
          duration: 0,
          description: 'Core API health check'
        }],
        lastChecked: new Date().toISOString(),
        totalDuration: 0
      };
    }
  }

  private normalizeHealthResponse(response: Partial<HealthCheckResponse> & { status?: string; checks?: Array<Partial<HealthCheckItem> & { status?: string }> }): HealthCheckResponse {
    // Handle different health check response formats
    if (response.status && Array.isArray(response.checks)) {
      return {
        status: this.normalizeStatus(response.status),
        checks: response.checks.map((check) => ({
          name: check.name ?? 'unknown',
          status: this.normalizeStatus(check.status ?? 'unknown'),
          duration: check.duration ?? 0,
          data: check.data,
          description: check.description,
          exception: check.exception,
          tags: check.tags
        })),
        lastChecked: response.lastChecked ?? new Date().toISOString(),
        totalDuration: response.totalDuration ?? 0,
        message: response.message
      };
    }

    // Handle simple status response
    return {
      status: this.normalizeStatus(response.status ?? 'unknown'),
      checks: [],
      lastChecked: new Date().toISOString(),
      totalDuration: 0
    };
  }

  private normalizeStatus(status: string): 'healthy' | 'degraded' | 'unhealthy' {
    const normalized = status.toLowerCase();
    if (normalized === 'healthy' || normalized === 'ok') return 'healthy';
    if (normalized === 'degraded' || normalized === 'warning') return 'degraded';
    return 'unhealthy';
  }
}