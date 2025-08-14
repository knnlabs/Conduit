import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { RequestConfig } from '../client/types';
import { ENDPOINTS } from '../constants';
import { ProviderType } from '../models/providerType';
import {
  ModelCostDto,
  CreateModelCostDto,
  UpdateModelCostDto,
  ModelCostOverview,
  ImportResult,
  CreateModelCostMappingDto,
  UpdateModelCostMappingDto,
  ModelCostMappingDto,
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


// Validation schemas
const createCostSchema = z.object({
  costName: z.string().min(1).max(255),
  modelProviderMappingIds: z.array(z.number()),
  pricingModel: z.number().optional(), // PricingModel enum
  pricingConfiguration: z.string().optional(), // JSON configuration
  modelType: z.string().default('chat'),
  inputCostPerMillionTokens: z.number().min(0),
  outputCostPerMillionTokens: z.number().min(0),
  embeddingCostPerMillionTokens: z.number().min(0).optional(),
  imageCostPerImage: z.number().min(0).optional(),
  audioCostPerMinute: z.number().min(0).optional(),
  audioCostPerKCharacters: z.number().min(0).optional(),
  audioInputCostPerMinute: z.number().min(0).optional(),
  audioOutputCostPerMinute: z.number().min(0).optional(),
  videoCostPerSecond: z.number().min(0).optional(),
  videoResolutionMultipliers: z.string().optional(), // JSON string
  imageResolutionMultipliers: z.string().optional(), // JSON string
  imageQualityMultipliers: z.string().optional(), // JSON string
  description: z.string().optional(),
  priority: z.number().optional(),
  batchProcessingMultiplier: z.number().min(0).max(1).optional(),
  supportsBatchProcessing: z.boolean().optional(),
  cachedInputCostPerMillionTokens: z.number().min(0).optional(),
  cachedInputWriteCostPerMillionTokens: z.number().min(0).optional(),
  costPerSearchUnit: z.number().min(0).optional(),
  costPerInferenceStep: z.number().min(0).optional(),
  defaultInferenceSteps: z.number().min(1).optional(),
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
  ): Promise<PagedResult<ModelCostDto>> {
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

    return this.client['get']<PagedResult<ModelCostDto>>(url, {
      signal: config?.signal,
      timeout: config?.timeout,
      headers: config?.headers,
    });
  }

  /**
   * Get a specific model cost by ID
   */
  async getById(id: number, config?: RequestConfig): Promise<ModelCostDto> {
    return this.client['get']<ModelCostDto>(
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
   * @deprecated Provider type is no longer used for cost lookups - costs are mapped to specific models
   */
  async getByProvider(
    providerType: ProviderType,
    config?: RequestConfig
  ): Promise<ModelCostDto[]> {
    // This endpoint may not work as expected with the new model
    return this.client['get']<ModelCostDto[]>(
      ENDPOINTS.MODEL_COSTS.BY_PROVIDER(providerType),
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
    data: CreateModelCostDto,
    config?: RequestConfig
  ): Promise<ModelCostDto> {
    try {
      createCostSchema.parse(data);
    } catch (error) {
      throw new ValidationError('Invalid model cost data', { validationError: error });
    }

    return this.client['post']<ModelCostDto, CreateModelCostDto>(
      ENDPOINTS.MODEL_COSTS.BASE,
      data,
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
  ): Promise<ModelCostDto> {
    return this.client['put']<ModelCostDto, UpdateModelCostDto>(
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
    modelCosts: CreateModelCostDto[],
    config?: RequestConfig
  ): Promise<ImportResult> {
    return this.client['post']<ImportResult, CreateModelCostDto[]>(
      ENDPOINTS.MODEL_COSTS.IMPORT,
      modelCosts,
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
  async bulkUpdate(): Promise<ModelCostDto[]> {
    throw new Error('Bulk update endpoint no longer exists. Update model costs individually.');
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
   * Helper method to calculate cost for given token usage
   */
  calculateTokenCost(
    cost: ModelCostDto,
    inputTokens: number,
    outputTokens: number
  ): { inputCost: number; outputCost: number; totalCost: number } {
    // Costs are now stored as cost per million tokens
    const inputCost = (inputTokens / 1_000_000) * cost.inputCostPerMillionTokens;
    const outputCost = (outputTokens / 1_000_000) * cost.outputCostPerMillionTokens;
    
    return {
      inputCost,
      outputCost,
      totalCost: inputCost + outputCost,
    };
  }


  // Model Cost Mapping Methods

  /**
   * Create model cost mappings - link a cost to specific models
   */
  async createMappings(
    data: CreateModelCostMappingDto,
    config?: RequestConfig
  ): Promise<ModelCostMappingDto[]> {
    return this.client['post']<ModelCostMappingDto[], CreateModelCostMappingDto>(
      `/api/ModelCostMappings`,
      data,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Update model cost mappings - replaces all mappings for a cost
   */
  async updateMappings(
    data: UpdateModelCostMappingDto,
    config?: RequestConfig
  ): Promise<ModelCostMappingDto[]> {
    return this.client['put']<ModelCostMappingDto[], UpdateModelCostMappingDto>(
      `/api/ModelCostMappings`,
      data,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get all mappings for a specific model cost
   */
  async getMappingsByCostId(
    modelCostId: number,
    config?: RequestConfig
  ): Promise<ModelCostMappingDto[]> {
    return this.client['get']<ModelCostMappingDto[]>(
      `/api/ModelCostMappings/by-cost/${modelCostId}`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Delete a specific model cost mapping
   */
  async deleteMapping(
    mappingId: number,
    config?: RequestConfig
  ): Promise<void> {
    return this.client['delete']<void>(
      `/api/ModelCostMappings/${mappingId}`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }
}