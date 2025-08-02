import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { RequestConfig } from '../client/types';
import { ENDPOINTS } from '../constants';

/**
 * Type-safe Configuration service using native fetch
 */
export class FetchConfigurationService {
  constructor(private readonly client: FetchBaseApiClient) {}

  // Working endpoints

  async getRoutingConfiguration(config?: RequestConfig): Promise<unknown> {
    return this.client['get']<unknown>(
      ENDPOINTS.CONFIG.ROUTING,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  async updateRoutingConfiguration(
    data: unknown,
    config?: RequestConfig
  ): Promise<unknown> {
    return this.client['put']<unknown>(
      ENDPOINTS.CONFIG.ROUTING,
      data,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  async getCachingConfiguration(config?: RequestConfig): Promise<unknown> {
    return this.client['get']<unknown>(
      ENDPOINTS.CONFIG.CACHING.BASE,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  async updateCachingConfiguration(
    data: unknown,
    config?: RequestConfig
  ): Promise<unknown> {
    return this.client['put']<unknown>(
      ENDPOINTS.CONFIG.CACHING.BASE,
      data,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  async getCacheStatistics(config?: RequestConfig): Promise<unknown> {
    return this.client['get']<unknown>(
      ENDPOINTS.CONFIG.CACHING.STATISTICS,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  async getCacheRegions(config?: RequestConfig): Promise<string[]> {
    return this.client['get']<string[]>(
      ENDPOINTS.CONFIG.CACHING.REGIONS,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  async getCacheEntries(regionId: string, config?: RequestConfig): Promise<unknown> {
    return this.client['get'](
      ENDPOINTS.CONFIG.CACHING.ENTRIES(regionId),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  async refreshCacheRegion(regionId: string, config?: RequestConfig): Promise<void> {
    return this.client['post']<void>(
      ENDPOINTS.CONFIG.CACHING.REFRESH(regionId),
      {},
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  async updateCachePolicy(regionId: string, policy: unknown, config?: RequestConfig): Promise<void> {
    return this.client['put']<void>(
      ENDPOINTS.CONFIG.CACHING.POLICY(regionId),
      policy,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  // This is what the WebUI expects
  async clearCacheByRegion(cacheId: string, config?: RequestConfig): Promise<unknown> {
    return this.client['post']<unknown>(
      ENDPOINTS.CONFIG.CACHING.CLEAR(cacheId),
      {},
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  // Also provide the original name
  async clearCacheRegion(cacheId: string, config?: RequestConfig): Promise<unknown> {
    return this.clearCacheByRegion(cacheId, config);
  }

}