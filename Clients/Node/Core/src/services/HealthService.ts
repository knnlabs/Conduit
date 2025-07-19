import { FetchBasedClient } from '../client/FetchBasedClient';
import type { ClientConfig } from '../client/types';
import type { HealthCheckResponse, HealthCheckOptions, WaitForHealthOptions } from '../models/health';

export class HealthService extends FetchBasedClient {
  constructor(config: ClientConfig) {
    super(config);
  }

  async check(options?: HealthCheckOptions): Promise<HealthCheckResponse> {
    return this.get<HealthCheckResponse>('/health', options);
  }

  async waitForHealth(options?: WaitForHealthOptions): Promise<HealthCheckResponse> {
    const timeout = options?.timeout ?? 30000;
    const pollingInterval = options?.pollingInterval ?? 1000;
    const maxAttempts = Math.floor(timeout / pollingInterval);

    for (let i = 0; i < maxAttempts; i++) {
      try {
        const response = await this.check(options);
        if (response.status === 'Healthy') {
          return response;
        }
      } catch (error) {
        // Continue trying
      }

      if (i < maxAttempts - 1) {
        await new Promise(resolve => setTimeout(resolve, pollingInterval));
      }
    }

    throw new Error(`Health check failed after ${maxAttempts} attempts`);
  }
}