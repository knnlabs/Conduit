import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { RequestConfig } from '../client/types';
import type { components } from '../generated/admin-api';
import { HttpMethod } from '../client/HttpMethod';
import {
  ModelCostDto,
  CreateModelCostDto,
  UpdateModelCostDto,
  ImportResult,
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
  startDate: string;
  endDate: string;
}

type ContractPagedModelCosts = components['schemas']['PagedResultOfModelCostDto'];
type ContractBulkImportResult = components['schemas']['BulkImportResult'];
type ContractModelCostOverview = components['schemas']['ModelCostOverviewDto'];
type ContractModelCost = components['schemas']['ModelCostDto'];
type ContractCreateModelCost = components['schemas']['CreateModelCostDto'];
type ContractUpdateModelCost = components['schemas']['UpdateModelCostDto'];

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
    const providerId = params?.provider === undefined ? undefined : Number(params.provider);
    if (providerId !== undefined && (!Number.isInteger(providerId) || providerId < 1))
      throw new ValidationError('provider must be a positive numeric provider ID');
    const query = { page: params?.page, pageSize: params?.pageSize, providerId,
      isActive: params?.isActive, modelType: params?.modelType };
    const queryString = new URLSearchParams(Object.entries(query).filter(([, value]) => value !== undefined).map(([key, value]) => [key, String(value)])).toString();
    const result: ContractPagedModelCosts = await this.client['executeContractRead'](
      `/api/ModelCosts${queryString ? `?${queryString}` : ''}`,
      (contractClient, options) => contractClient.GET('/api/ModelCosts', { ...options, params: { query } }), config);
    return { items: result.items ?? [], totalCount: result.totalCount ?? 0,
      page: result.currentPage ?? 1, pageSize: result.pageSize ?? 50,
      totalPages: result.totalPages ?? 0 } as PagedResult<ModelCostDto>;
  }

  /**
   * Get a specific model cost by ID
   */
  async getById(id: number, config?: RequestConfig): Promise<ModelCostDto> {
    return this.client['executeContractRead'](`/api/ModelCosts/${id}`,
      (contractClient, options) => contractClient.GET('/api/ModelCosts/{id}', { ...options, params: { path: { id } } }), config) as Promise<ModelCostDto>;
  }


  /**
   * Create a new model cost configuration
   */
  async create(
    data: CreateModelCostDto,
    config?: RequestConfig
  ): Promise<ModelCostDto> {
    validateCreateModelCostRequest(data);

    const body = data as ContractCreateModelCost;
    return this.client['executeContractOperation']<ContractModelCost, ContractCreateModelCost>('/api/ModelCosts', HttpMethod.POST,
      (contractClient, options) => contractClient.POST('/api/ModelCosts', { ...options, body }), config, body) as Promise<ModelCostDto>;
  }

  /**
   * Update an existing model cost configuration
   */
  async update(
    id: number,
    data: UpdateModelCostDto,
    config?: RequestConfig
  ): Promise<ModelCostDto> {
    const body = data as ContractUpdateModelCost;
    return this.client['executeContractOperation']<ContractModelCost, ContractUpdateModelCost>(`/api/ModelCosts/${id}`, HttpMethod.PUT,
      (contractClient, options) => contractClient.PUT('/api/ModelCosts/{id}', { ...options, params: { path: { id } }, body }), config, body) as Promise<ModelCostDto>;
  }

  /**
   * Delete a model cost configuration
   */
  async deleteById(id: number, config?: RequestConfig): Promise<void> {
    return this.client['executeContractOperation']<void>(`/api/ModelCosts/${id}`, HttpMethod.DELETE,
      (contractClient, options) => contractClient.DELETE('/api/ModelCosts/{id}', { ...options, params: { path: { id } } }), config);
  }

  /**
   * Import multiple model costs at once
   */
  async import(
    modelCosts: CreateModelCostDto[],
    config?: RequestConfig
  ): Promise<ImportResult> {
    const result: ContractBulkImportResult = await this.client['executeContractOperation']('/api/ModelCosts/import', HttpMethod.POST,
      (contractClient, options) => contractClient.POST('/api/ModelCosts/import', { ...options, body: modelCosts as ContractCreateModelCost[] }), config, modelCosts);
    return { success: result.successCount ?? 0, failed: result.failureCount ?? 0,
      errors: (result.errors ?? []).map((error, index) => ({ row: index + 1, error })) };
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
    params: ModelCostOverviewParams,
    config?: RequestConfig
  ): Promise<ContractModelCostOverview[]> {
    const query = { startDate: params.startDate, endDate: params.endDate };
    const queryString = new URLSearchParams(query).toString();
    return this.client['executeContractRead'](`/api/ModelCosts/overview?${queryString}`,
      (contractClient, options) => contractClient.GET('/api/ModelCosts/overview', { ...options, params: { query } }), config);
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


}
