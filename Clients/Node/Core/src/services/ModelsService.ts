import type { FetchBasedClient } from '../client/FetchBasedClient';
import { HttpMethod } from '../client/HttpMethod';
import type { RequestOptions } from '../client/types';
import type { Model, ModelsResponse } from '../models/models';

export class ModelsService {
  private cachedModels?: Model[];
  private cacheExpiry?: number;
  private readonly cacheTTL = 5 * 60 * 1000; // 5 minutes

  constructor(private readonly client: FetchBasedClient) {}

  async list(options?: RequestOptions & { useCache?: boolean }): Promise<Model[]> {
    if (options?.useCache !== false && this.isCacheValid()) {
      return this.cachedModels as Model[];
    }

    const response = await this.client['request']<ModelsResponse>(
      {
        method: HttpMethod.GET,
        url: '/v1/models',
      },
      options
    );

    this.cachedModels = response.data;
    this.cacheExpiry = Date.now() + this.cacheTTL;

    return response.data;
  }

  async get(modelId: string, options?: RequestOptions): Promise<Model | null> {
    const models = await this.list(options);
    return models.find(model => model.id === modelId) || null;
  }

  async exists(modelId: string, options?: RequestOptions): Promise<boolean> {
    const model = await this.get(modelId, options);
    return model !== null;
  }

  clearCache(): void {
    this.cachedModels = undefined;
    this.cacheExpiry = undefined;
  }

  private isCacheValid(): boolean {
    return !!(this.cachedModels && this.cacheExpiry && Date.now() < this.cacheExpiry);
  }
}