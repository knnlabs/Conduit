import type { FetchBasedClient } from '../client/FetchBasedClient';
import type { HealthCheckResponse, HealthCheckOptions, WaitForHealthOptions } from '../models/health';

export class HealthService {
  constructor(private client: FetchBasedClient) {}

  async check(options?: HealthCheckOptions): Promise<HealthCheckResponse> {
    return this.client['get']<HealthCheckResponse>('/health', options);
  }

  async waitForHealth(options?: WaitForHealthOptions): Promise<HealthCheckResponse> {
    const maxAttempts = options?.maxAttempts || 30;
    const delayMs = options?.delayMs || 1000;

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
        await new Promise(resolve => setTimeout(resolve, delayMs));
      }
    }

    throw new Error(`Health check failed after ${maxAttempts} attempts`);
  }
}