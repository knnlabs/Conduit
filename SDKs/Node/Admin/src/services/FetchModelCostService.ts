import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { RequestConfig } from '../client/types';
import { ENDPOINTS } from '../constants';
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
import { PagedResult } from '../models/common-types';
import { ValidationError } from '../utils/errors';
import { validateRequired, validateStringLength, validateNonEmptyArray, validateNumberRange } from '../utils/validation';

// Type aliases for better readability
interface ModelCostListParams {
  page?: number;
  pageSize?: number;
  provider?: string;
  isActive?: boolean;
  /** Filter by model type (chat, image, video, embedding, audio) */
  modelType?: string;
}

interface ModelCostOverviewParams {
  startDate?: string;
  endDate?: string;
  groupBy?: 'provider' | 'model';
}

/**
 * Validates create model cost request
 */
function validateCreateModelCostRequest(data: CreateModelCostDto): void {
  // Validate required fields
  validateRequired(data, ['costName', 'modelProviderTypeAssociationIds', 'inputCostPerMillionTokens', 'outputCostPerMillionTokens']);
  validateStringLength(data.costName, 1, 255, 'costName');
  validateNonEmptyArray(data.modelProviderTypeAssociationIds ?? [], 'modelProviderTypeAssociationIds');

  // Validate cost values are non-negative
  if (data.inputCostPerMillionTokens < 0) {
    throw new ValidationError('inputCostPerMillionTokens must be non-negative');
  }
  if (data.outputCostPerMillionTokens < 0) {
    throw new ValidationError('outputCostPerMillionTokens must be non-negative');
  }

  // Validate optional number fields if provided
  const optionalNumberFields = [
    'embeddingCostPerMillionTokens',
    'audioCostPerMinute',
    'audioCostPerThousandCharacters',
    'cachedInputCostPerMillionTokens',
    'cachedInputWriteCostPerMillionTokens',
    'costPerSearchUnit',
  ] as const;

  for (const field of optionalNumberFields) {
    const value = data[field as keyof CreateModelCostDto];
    if (value !== undefined && value !== null && typeof value === 'number' && value < 0) {
      throw new ValidationError(`${field} must be non-negative`);
    }
  }

  // Validate batch processing multiplier is between 0 and 1
  if (data.batchProcessingMultiplier !== undefined && data.batchProcessingMultiplier !== null) {
    validateNumberRange(data.batchProcessingMultiplier, 0, 1, 'batchProcessingMultiplier');
  }
}

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
   * Create a new model cost configuration
   */
  async create(
    data: CreateModelCostDto,
    config?: RequestConfig
  ): Promise<ModelCostDto> {
    validateCreateModelCostRequest(data);

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