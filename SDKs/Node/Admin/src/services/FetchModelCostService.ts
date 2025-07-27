import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { RequestConfig } from '../client/types';
import { ENDPOINTS } from '../constants';
import { ProviderType } from '../models/providerType';
import {
  ModelCost,
  CreateModelCostDto,
  UpdateModelCostDto,
  ModelCostOverview,
  ImportResult,
} from '../models/modelCost';
import { PagedResult } from '../models/security';
import { ValidationError } from '../utils/errors';
import { z } from 'zod';

// Type aliases for better readability
interface ModelCostListParams {
  page?: number;
  pageSize?: number;
  provider?: string;
  isActive?: boolean;
}

interface ModelCostOverviewParams {
  startDate?: string;
  endDate?: string;
  groupBy?: 'provider' | 'model';
}

interface BulkUpdateRequest {
  updates: Array<{
    id: number;
    changes: Partial<UpdateModelCostDto>;
  }>;
}

// Create DTO matching C# backend structure
export interface CreateModelCostDtoBackend {
  modelIdPattern: string;
  inputTokenCost: number;
  outputTokenCost: number;
  embeddingTokenCost?: number;
  imageCostPerImage?: number;
  audioCostPerMinute?: number;
  audioCostPerKCharacters?: number;
  audioInputCostPerMinute?: number;
  audioOutputCostPerMinute?: number;
  videoCostPerSecond?: number;
  videoResolutionMultipliers?: string; // JSON string
  description?: string;
  priority?: number;
}

const createCostSchema = z.object({
  modelIdPattern: z.string().min(1),
  inputTokenCost: z.number().min(0),
  outputTokenCost: z.number().min(0),
  embeddingTokenCost: z.number().min(0).optional(),
  imageCostPerImage: z.number().min(0).optional(),
  audioCostPerMinute: z.number().min(0).optional(),
  audioCostPerKCharacters: z.number().min(0).optional(),
  audioInputCostPerMinute: z.number().min(0).optional(),
  audioOutputCostPerMinute: z.number().min(0).optional(),
  videoCostPerSecond: z.number().min(0).optional(),
  videoResolutionMultipliers: z.string().optional(), // JSON string
  description: z.string().optional(),
  priority: z.number().optional(),
});

/**
 * Type-safe Model Cost service using native fetch
 */
export class FetchModelCostService {
  constructor(private readonly client: FetchBaseApiClient) {}

  /**
   * Get all model costs with optional pagination and filtering
   */
  async list(
    params?: ModelCostListParams,
    config?: RequestConfig
  ): Promise<PagedResult<ModelCost>> {
    const queryParams = new URLSearchParams();
    if (params) {
      Object.entries(params).forEach(([key, value]) => {
        if (value !== undefined) {
          queryParams.append(key, String(value));
        }
      });
    }

    const url = queryParams.toString() 
      ? `${ENDPOINTS.MODEL_COSTS.BASE}?${queryParams.toString()}`
      : ENDPOINTS.MODEL_COSTS.BASE;

    return this.client['get']<PagedResult<ModelCost>>(url, {
      signal: config?.signal,
      timeout: config?.timeout,
      headers: config?.headers,
    });
  }

  /**
   * Get a specific model cost by ID
   */
  async getById(id: number, config?: RequestConfig): Promise<ModelCost> {
    return this.client['get']<ModelCost>(
      ENDPOINTS.MODEL_COSTS.BY_ID(id),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get model costs by provider type
   */
  async getByProvider(
    providerType: ProviderType,
    config?: RequestConfig
  ): Promise<ModelCost[]> {
    return this.client['get']<ModelCost[]>(
      ENDPOINTS.MODEL_COSTS.BY_PROVIDER(providerType),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get model cost by pattern
   */
  async getByPattern(
    pattern: string,
    config?: RequestConfig
  ): Promise<ModelCost | null> {
    return this.client['get']<ModelCost | null>(
      `/api/modelcosts/pattern/${encodeURIComponent(pattern)}`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Create a new model cost configuration
   */
  async create(
    data: CreateModelCostDto | CreateModelCostDtoBackend,
    config?: RequestConfig
  ): Promise<ModelCost> {
    // Transform from old format to new format if needed
    const backendData: CreateModelCostDtoBackend = 'modelIdPattern' in data
      ? data
      : {
          modelIdPattern: data.modelId,
          inputTokenCost: data.inputTokenCost,
          outputTokenCost: data.outputTokenCost,
          description: data.description,
          priority: 0,
        };

    try {
      createCostSchema.parse(backendData);
    } catch (error) {
      throw new ValidationError('Invalid model cost data', { validationError: error });
    }

    return this.client['post']<ModelCost, CreateModelCostDtoBackend>(
      ENDPOINTS.MODEL_COSTS.BASE,
      backendData,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Update an existing model cost configuration
   */
  async update(
    id: number,
    data: UpdateModelCostDto,
    config?: RequestConfig
  ): Promise<ModelCost> {
    return this.client['put']<ModelCost, UpdateModelCostDto>(
      ENDPOINTS.MODEL_COSTS.BY_ID(id),
      data,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Delete a model cost configuration
   */
  async deleteById(id: number, config?: RequestConfig): Promise<void> {
    return this.client['delete']<void>(
      ENDPOINTS.MODEL_COSTS.BY_ID(id),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Import multiple model costs at once
   */
  async import(
    modelCosts: (CreateModelCostDto | CreateModelCostDtoBackend)[],
    config?: RequestConfig
  ): Promise<ImportResult> {
    // Transform to backend format if needed
    const backendData = modelCosts.map(cost => {
      if ('modelIdPattern' in cost) {
        return cost;
      }
      return {
        modelIdPattern: cost.modelId,
        inputTokenCost: cost.inputTokenCost,
        outputTokenCost: cost.outputTokenCost,
        description: cost.description,
        priority: 0,
      } as CreateModelCostDtoBackend;
    });

    return this.client['post']<ImportResult, CreateModelCostDtoBackend[]>(
      ENDPOINTS.MODEL_COSTS.IMPORT,
      backendData,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Bulk update multiple model costs
   */
  async bulkUpdate(
    updates: BulkUpdateRequest['updates'],
    config?: RequestConfig
  ): Promise<ModelCost[]> {
    return this.client['post']<ModelCost[], BulkUpdateRequest>(
      ENDPOINTS.MODEL_COSTS.BULK_UPDATE,
      { updates },
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get model cost overview with aggregation
   */
  async getOverview(
    params?: ModelCostOverviewParams,
    config?: RequestConfig
  ): Promise<ModelCostOverview[]> {
    const queryParams = new URLSearchParams();
    if (params) {
      Object.entries(params).forEach(([key, value]) => {
        if (value !== undefined) {
          queryParams.append(key, String(value));
        }
      });
    }

    const url = queryParams.toString()
      ? `${ENDPOINTS.MODEL_COSTS.OVERVIEW}?${queryParams.toString()}`
      : ENDPOINTS.MODEL_COSTS.OVERVIEW;

    return this.client['get']<ModelCostOverview[]>(url, {
      signal: config?.signal,
      timeout: config?.timeout,
      headers: config?.headers,
    });
  }

  /**
   * Helper method to check if a model matches a pattern
   */
  doesModelMatchPattern(modelId: string, pattern: string): boolean {
    if (pattern.endsWith('*')) {
      const prefix = pattern.slice(0, -1);
      return modelId.startsWith(prefix);
    }
    return modelId === pattern;
  }

  /**
   * Helper method to find the best matching cost for a model
   */
  async findBestMatch(
    modelId: string,
    costs: ModelCost[]
  ): Promise<ModelCost | null> {
    // First try exact match
    const exactMatch = costs.find(c => c.modelIdPattern === modelId);
    if (exactMatch) return exactMatch;

    // Then try pattern matches, sorted by specificity (longest prefix)
    const patternMatches = costs
      .filter(c => c.modelIdPattern.endsWith('*') && this.doesModelMatchPattern(modelId, c.modelIdPattern))
      .sort((a, b) => b.modelIdPattern.length - a.modelIdPattern.length);

    return patternMatches[0] ?? null;
  }

  /**
   * Helper method to calculate cost for given token usage
   */
  calculateTokenCost(
    cost: ModelCost,
    inputTokens: number,
    outputTokens: number
  ): { inputCost: number; outputCost: number; totalCost: number } {
    const inputCostPerMillion = cost.inputCostPerMillionTokens ?? 0;
    const outputCostPerMillion = cost.outputCostPerMillionTokens ?? 0;
    
    const inputCost = (inputTokens / 1000000) * inputCostPerMillion;
    const outputCost = (outputTokens / 1000000) * outputCostPerMillion;
    
    return {
      inputCost,
      outputCost,
      totalCost: inputCost + outputCost,
    };
  }

  /**
   * Helper method to get cost type from model ID
   */
  getCostType(modelId: string): 'text' | 'embedding' | 'image' | 'audio' | 'video' {
    if (modelId.includes('embed')) return 'embedding';
    if (modelId.includes('dall-e') || modelId.includes('stable-diffusion')) return 'image';
    if (modelId.includes('whisper') || modelId.includes('tts')) return 'audio';
    if (modelId.includes('video')) return 'video';
    return 'text';
  }
}