import { BaseApiClient } from '../client/BaseApiClient';
import { ENDPOINTS, CACHE_TTL } from '../constants';
import {
  ModelCostDto,
  CreateModelCostDto,
  UpdateModelCostDto,
  ModelCostFilters,
  ModelCostCalculation,
  BulkModelCostUpdate,
  ModelCostHistory,
  CostEstimate,
  ModelCostComparison,
} from '../models/modelCost';
import { ValidationError, NotImplementedError } from '../utils/errors';
import { z } from 'zod';

const createCostSchema = z.object({
  modelId: z.string().min(1),
  inputTokenCost: z.number().min(0),
  outputTokenCost: z.number().min(0),
  currency: z.string().length(3).default('USD'),
  effectiveDate: z.string().datetime().optional(),
  expiryDate: z.string().datetime().optional(),
  providerId: z.string().optional(),
  description: z.string().max(500).optional(),
  isActive: z.boolean().optional(),
});

const calculateCostSchema = z.object({
  modelId: z.string().min(1),
  inputTokens: z.number().min(0),
  outputTokens: z.number().min(0),
});

export class ModelCostService extends BaseApiClient {
  async create(request: CreateModelCostDto): Promise<ModelCostDto> {
    try {
      createCostSchema.parse(request);
    } catch (error) {
      throw new ValidationError('Invalid model cost request', error);
    }

    const response = await this.post<ModelCostDto>(
      ENDPOINTS.MODEL_COSTS.BASE,
      request
    );

    await this.invalidateCache();
    return response;
  }

  async list(filters?: ModelCostFilters): Promise<ModelCostDto[]> {
    const params = filters
      ? {
          modelId: filters.modelId,
          providerId: filters.providerId,
          currency: filters.currency,
          isActive: filters.isActive,
          effectiveAfter: filters.effectiveAfter,
          effectiveBefore: filters.effectiveBefore,
          minInputCost: filters.minInputCost,
          maxInputCost: filters.maxInputCost,
          minOutputCost: filters.minOutputCost,
          maxOutputCost: filters.maxOutputCost,
          sortBy: filters.sortBy?.field,
          sortDirection: filters.sortBy?.direction,
        }
      : undefined;

    const cacheKey = this.getCacheKey('model-costs', params);
    return this.withCache(
      cacheKey,
      () => super.get<ModelCostDto[]>(ENDPOINTS.MODEL_COSTS.BASE, params),
      CACHE_TTL.LONG
    );
  }

  async getById(id: number): Promise<ModelCostDto> {
    const cacheKey = this.getCacheKey('model-cost', id);
    return this.withCache(
      cacheKey,
      () => super.get<ModelCostDto>(ENDPOINTS.MODEL_COSTS.BY_ID(id)),
      CACHE_TTL.LONG
    );
  }

  async getByModel(modelId: string): Promise<ModelCostDto[]> {
    const cacheKey = this.getCacheKey('model-cost-by-model', modelId);
    return this.withCache(
      cacheKey,
      () => super.get<ModelCostDto[]>(ENDPOINTS.MODEL_COSTS.BY_MODEL(modelId)),
      CACHE_TTL.LONG
    );
  }

  async update(id: number, request: UpdateModelCostDto): Promise<void> {
    await this.put(ENDPOINTS.MODEL_COSTS.BY_ID(id), request);
    await this.invalidateCache();
  }

  async deleteById(id: number): Promise<void> {
    await super.delete(ENDPOINTS.MODEL_COSTS.BY_ID(id));
    await this.invalidateCache();
  }

  async calculateCost(
    modelId: string,
    inputTokens: number,
    outputTokens: number
  ): Promise<ModelCostCalculation> {
    try {
      calculateCostSchema.parse({ modelId, inputTokens, outputTokens });
    } catch (error) {
      throw new ValidationError('Invalid cost calculation request', error);
    }

    // Get active cost for the model
    const costs = await this.getByModel(modelId);
    const activeCost = costs.find(c => c.isActive);

    if (!activeCost) {
      throw new ValidationError(`No active cost configuration found for model: ${modelId}`);
    }

    const inputCost = (inputTokens / 1000) * activeCost.inputTokenCost;
    const outputCost = (outputTokens / 1000) * activeCost.outputTokenCost;

    return {
      modelId,
      inputTokens,
      outputTokens,
      inputCost,
      outputCost,
      totalCost: inputCost + outputCost,
      currency: activeCost.currency,
      costPerThousandInputTokens: activeCost.inputTokenCost,
      costPerThousandOutputTokens: activeCost.outputTokenCost,
    };
  }

  async getCurrentCost(modelId: string): Promise<ModelCostDto | null> {
    const costs = await this.getByModel(modelId);
    return costs.find(c => c.isActive) || null;
  }

  async updateCosts(models: string[], inputCost: number, outputCost: number): Promise<void> {
    const updates = await Promise.all(
      models.map(async (modelId) => {
        const costs = await this.getByModel(modelId);
        const activeCost = costs.find(c => c.isActive);
        if (activeCost) {
          return this.update(activeCost.id, {
            inputTokenCost: inputCost,
            outputTokenCost: outputCost,
          });
        } else {
          return this.create({
            modelId,
            inputTokenCost: inputCost,
            outputTokenCost: outputCost,
            isActive: true,
          });
        }
      })
    );

    await Promise.all(updates);
  }

  // Stub methods
  async bulkUpdate(_request: BulkModelCostUpdate): Promise<{
    updated: ModelCostDto[];
    failed: { modelId: string; error: string }[];
  }> {
    // STUB: This endpoint needs to be implemented in the Admin API
    throw new NotImplementedError(
      'bulkUpdate requires Admin API endpoint implementation. ' +
        'Consider implementing POST /api/modelcosts/bulk-update'
    );
  }

  async getHistory(_modelId: string): Promise<ModelCostHistory> {
    // STUB: This endpoint needs to be implemented in the Admin API
    throw new NotImplementedError(
      'getHistory requires Admin API endpoint implementation. ' +
        'Consider implementing GET /api/modelcosts/history/{modelId}'
    );
  }

  async estimateCosts(_scenarios: {
    name: string;
    inputTokens: number;
    outputTokens: number;
  }[], _models: string[]): Promise<CostEstimate> {
    // STUB: This endpoint needs to be implemented in the Admin API
    throw new NotImplementedError(
      'estimateCosts requires Admin API endpoint implementation. ' +
        'Consider implementing POST /api/modelcosts/estimate'
    );
  }

  async compareCosts(
    _baseModel: string,
    _comparisonModels: string[],
    _inputTokens: number,
    _outputTokens: number
  ): Promise<ModelCostComparison> {
    // STUB: This endpoint needs to be implemented in the Admin API
    throw new NotImplementedError(
      'compareCosts requires Admin API endpoint implementation. ' +
        'Consider implementing POST /api/modelcosts/compare'
    );
  }

  async importCosts(_file: File | Blob, _format: 'csv' | 'json'): Promise<{
    imported: number;
    updated: number;
    failed: { row: number; error: string }[];
  }> {
    // STUB: This endpoint needs to be implemented in the Admin API
    throw new NotImplementedError(
      'importCosts requires Admin API endpoint implementation. ' +
        'Consider implementing POST /api/modelcosts/import'
    );
  }

  async exportCosts(_format: 'csv' | 'json', _activeOnly?: boolean): Promise<Blob> {
    // STUB: This endpoint needs to be implemented in the Admin API
    throw new NotImplementedError(
      'exportCosts requires Admin API endpoint implementation. ' +
        'Consider implementing GET /api/modelcosts/export'
    );
  }

  private async invalidateCache(): Promise<void> {
    if (!this.cache) return;
    await this.cache.clear();
  }
}