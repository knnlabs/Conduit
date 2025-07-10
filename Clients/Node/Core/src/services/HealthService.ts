import { BaseClient } from '../client/BaseClient';
import {
  HealthCheckResponse,
  HealthCheckItem,
  HealthSummary,
  WaitForHealthOptions
} from '../models/health';
import axios from 'axios';

/**
 * Service for monitoring system health and performing health checks against the Conduit Core API
 */
export class HealthService {
  constructor(private client: BaseClient) {}

  /**
   * Performs a liveness check to verify the API is responsive
   * Note: Core API has dedicated health endpoints at root level (no /v1 prefix)
   * 
   * @returns Promise<HealthCheckResponse> Health check response indicating if the API is alive
   */
  async getLiveness(): Promise<HealthCheckResponse> {
    try {
      // Create a separate axios instance without authentication for health checks
      const healthClient = axios.create({
        baseURL: this.client['config'].baseURL,
        timeout: 5000
      });
      
      const response = await healthClient.get('/health/live');
      return response.data as HealthCheckResponse;
    } catch (error) {
      return {
        status: 'Unhealthy',
        totalDuration: 0,
        checks: [
          {
            name: 'liveness',
            status: 'Unhealthy',
            description: `Liveness check failed: ${error instanceof Error ? error.message : 'Unknown error'}`,
            duration: 0
          }
        ]
      };
    }
  }

  /**
   * Performs a readiness check to verify the API and its dependencies are ready to serve requests
   * Note: Core API has dedicated health endpoints at root level (no /v1 prefix)
   * 
   * @returns Promise<HealthCheckResponse> Health check response indicating readiness status
   */
  async getReadiness(): Promise<HealthCheckResponse> {
    try {
      // Create a separate axios instance without authentication for health checks
      const healthClient = axios.create({
        baseURL: this.client['config'].baseURL,
        timeout: 5000
      });
      
      const response = await healthClient.get('/health/ready');
      return response.data as HealthCheckResponse;
    } catch (error) {
      return {
        status: 'Unhealthy',
        totalDuration: 0,
        checks: [
          {
            name: 'readiness',
            status: 'Unhealthy',
            description: `Readiness check failed: ${error instanceof Error ? error.message : 'Unknown error'}`,
            duration: 0
          }
        ]
      };
    }
  }

  /**
   * Performs a comprehensive health check of all system components
   * Note: Core API health endpoint is at root level (no /v1 prefix and no authentication required)
   * 
   * @returns Promise<HealthCheckResponse> Detailed health check response for all system components
   */
  async getFullHealth(): Promise<HealthCheckResponse> {
    try {
      // Create a separate axios instance without authentication for health checks
      const healthClient = axios.create({
        baseURL: this.client['config'].baseURL,
        timeout: 5000
      });
      
      const response = await healthClient.get('/health');
      return response.data as HealthCheckResponse;
    } catch (error) {
      return {
        status: 'Unhealthy',
        totalDuration: 0,
        checks: [
          {
            name: 'full_health',
            status: 'Unhealthy',
            description: `Full health check failed: ${error instanceof Error ? error.message : 'Unknown error'}`,
            duration: 0
          }
        ]
      };
    }
  }

  /**
   * Checks if the system is currently healthy based on the full health check
   * 
   * @returns Promise<boolean> True if the system is healthy, false otherwise
   */
  async isSystemHealthy(): Promise<boolean> {
    try {
      const health = await this.getFullHealth();
      return health.status === 'Healthy';
    } catch {
      return false;
    }
  }

  /**
   * Checks if the system is ready to serve requests
   * 
   * @returns Promise<boolean> True if the system is ready, false otherwise
   */
  async isSystemReady(): Promise<boolean> {
    try {
      const readiness = await this.getReadiness();
      return readiness.status === 'Healthy';
    } catch {
      return false;
    }
  }

  /**
   * Gets all unhealthy components from the full health check
   * 
   * @returns Promise<HealthCheckItem[]> List of unhealthy health check items
   */
  async getUnhealthyComponents(): Promise<HealthCheckItem[]> {
    try {
      const health = await this.getFullHealth();
      return health.checks?.filter(c => c.status !== 'Healthy') || [];
    } catch {
      return [];
    }
  }

  /**
   * Gets a specific health check by name
   * 
   * @param checkName - The name of the health check to retrieve
   * @returns Promise<HealthCheckItem | null> The health check item, or null if not found
   */
  async getHealthCheck(checkName: string): Promise<HealthCheckItem | null> {
    if (!checkName?.trim()) {
      throw new Error('Check name cannot be null or empty');
    }

    try {
      const health = await this.getFullHealth();
      return health.checks?.find(c => c.name.toLowerCase() === checkName.toLowerCase()) || null;
    } catch {
      return null;
    }
  }

  /**
   * Gets the database health status
   * 
   * @returns Promise<HealthCheckItem | null> Database health check item, or null if not found
   */
  async getDatabaseHealth(): Promise<HealthCheckItem | null> {
    return this.getHealthCheck('database');
  }

  /**
   * Gets the Redis cache health status
   * 
   * @returns Promise<HealthCheckItem | null> Redis health check item, or null if not found
   */
  async getRedisHealth(): Promise<HealthCheckItem | null> {
    return this.getHealthCheck('redis');
  }

  /**
   * Gets the RabbitMQ message queue health status
   * 
   * @returns Promise<HealthCheckItem | null> RabbitMQ health check item, or null if not found
   */
  async getRabbitMQHealth(): Promise<HealthCheckItem | null> {
    return this.getHealthCheck('rabbitmq');
  }

  /**
   * Gets the provider health status
   * 
   * @returns Promise<HealthCheckItem | null> Provider health check item, or null if not found
   */
  async getProvidersHealth(): Promise<HealthCheckItem | null> {
    return this.getHealthCheck('providers');
  }

  /**
   * Gets the system resources health status
   * 
   * @returns Promise<HealthCheckItem | null> System resources health check item, or null if not found
   */
  async getSystemResourcesHealth(): Promise<HealthCheckItem | null> {
    return this.getHealthCheck('system_resources');
  }

  /**
   * Gets the SignalR health status
   * 
   * @returns Promise<HealthCheckItem | null> SignalR health check item, or null if not found
   */
  async getSignalRHealth(): Promise<HealthCheckItem | null> {
    return this.getHealthCheck('signalr');
  }

  /**
   * Waits for the system to become healthy within a specified timeout
   * 
   * @param options - Wait options including timeout and polling interval
   * @returns Promise<boolean> True if the system became healthy within the timeout, false otherwise
   */
  async waitForHealthy(options: WaitForHealthOptions): Promise<boolean> {
    const { timeout, pollingInterval = 5000 } = options;
    const deadline = Date.now() + timeout;

    while (Date.now() < deadline) {
      try {
        if (await this.isSystemHealthy()) {
          return true;
        }
      } catch {
        // Continue polling on error
      }

      await new Promise(resolve => setTimeout(resolve, pollingInterval));
    }

    return false;
  }

  /**
   * Waits for the system to become ready within a specified timeout
   * 
   * @param options - Wait options including timeout and polling interval
   * @returns Promise<boolean> True if the system became ready within the timeout, false otherwise
   */
  async waitForReady(options: WaitForHealthOptions): Promise<boolean> {
    const { timeout, pollingInterval = 2000 } = options;
    const deadline = Date.now() + timeout;

    while (Date.now() < deadline) {
      try {
        if (await this.isSystemReady()) {
          return true;
        }
      } catch {
        // Continue polling on error
      }

      await new Promise(resolve => setTimeout(resolve, pollingInterval));
    }

    return false;
  }

  /**
   * Gets a summary of the current health status with key metrics
   * 
   * @returns Promise<HealthSummary> Health summary object
   */
  async getHealthSummary(): Promise<HealthSummary> {
    try {
      const health = await this.getFullHealth();
      const checks = health.checks || [];

      const healthyCount = checks.filter(c => c.status === 'Healthy').length;
      const degradedCount = checks.filter(c => c.status === 'Degraded').length;
      const unhealthyCount = checks.filter(c => c.status === 'Unhealthy').length;

      return {
        overallStatus: health.status,
        totalDuration: health.totalDuration,
        checkCounts: {
          total: checks.length,
          healthy: healthyCount,
          degraded: degradedCount,
          unhealthy: unhealthyCount
        },
        healthPercentage: checks.length > 0 ? (healthyCount / checks.length) * 100 : 100,
        components: checks.map(c => ({
          name: c.name,
          status: c.status,
          duration: c.duration,
          hasData: Boolean(c.data && Object.keys(c.data).length > 0)
        }))
      };
    } catch (error) {
      return {
        overallStatus: 'Unhealthy',
        totalDuration: 0,
        checkCounts: { total: 0, healthy: 0, degraded: 0, unhealthy: 1 },
        healthPercentage: 0,
        components: []
      };
    }
  }

  /**
   * Checks if the system is currently healthy based on configurable thresholds
   * 
   * @param options - Health check criteria
   * @returns Promise<boolean> True if the system is healthy based on the specified thresholds
   */
  async isSystemHealthyAdvanced(options: {
    maxErrorRate?: number;
    maxResponseTime?: number;
    minProviderHealthPercentage?: number;
  } = {}): Promise<boolean> {
    const {
      maxResponseTime = 2000.0,
      minProviderHealthPercentage = 80.0
    } = options;

    try {
      const health = await this.getFullHealth();
      
      // Check overall status first
      if (health.status !== 'Healthy') {
        return false;
      }

      // Check response time
      if (health.totalDuration > maxResponseTime) {
        return false;
      }

      // Check provider health percentage
      const providers = health.checks?.filter(c => c.name === 'providers') || [];
      if (providers.length > 0) {
        const healthyProviders = providers.filter(p => p.status === 'Healthy').length;
        const providerHealthPercentage = (healthyProviders / providers.length) * 100;
        if (providerHealthPercentage < minProviderHealthPercentage) {
          return false;
        }
      }

      return true;
    } catch {
      return false;
    }
  }
}