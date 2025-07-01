import { BaseApiClient } from '../client/BaseApiClient';
import { ENDPOINTS, CACHE_TTL, DEFAULT_PAGE_SIZE, BUDGET_DURATION } from '../constants';
import {
  VirtualKeyDto,
  CreateVirtualKeyRequest,
  CreateVirtualKeyResponse,
  UpdateVirtualKeyRequest,
  VirtualKeyValidationRequest,
  VirtualKeyValidationResult,
  UpdateSpendRequest,
  RefundSpendRequest,
  CheckBudgetRequest,
  CheckBudgetResponse,
  VirtualKeyValidationInfo,
  VirtualKeyMaintenanceRequest,
  VirtualKeyMaintenanceResponse,
  VirtualKeyFilters,
  VirtualKeyStatistics,
} from '../models/virtualKey';
import { ValidationError, NotImplementedError } from '../utils/errors';
import { z } from 'zod';

const createVirtualKeySchema = z.object({
  keyName: z.string().min(1).max(100),
  allowedModels: z.string().optional(),
  maxBudget: z.number().min(0).max(1000000).optional(),
  budgetDuration: z.enum([BUDGET_DURATION.TOTAL, BUDGET_DURATION.DAILY, BUDGET_DURATION.WEEKLY, BUDGET_DURATION.MONTHLY]).optional(),
  expiresAt: z.string().datetime().optional(),
  metadata: z.string().optional(),
  rateLimitRpm: z.number().min(0).optional(),
  rateLimitRpd: z.number().min(0).optional(),
});

export class VirtualKeyService extends BaseApiClient {
  async create(request: CreateVirtualKeyRequest): Promise<CreateVirtualKeyResponse> {
    try {
      createVirtualKeySchema.parse(request);
    } catch (error) {
      throw new ValidationError('Invalid virtual key request', error);
    }

    const response = await this.post<CreateVirtualKeyResponse>(
      ENDPOINTS.VIRTUAL_KEYS.BASE,
      request
    );

    await this.invalidateCache();
    return response;
  }

  async list(filters?: VirtualKeyFilters): Promise<VirtualKeyDto[]> {
    const params = {
      pageNumber: filters?.pageNumber || 1,
      pageSize: filters?.pageSize || DEFAULT_PAGE_SIZE,
      search: filters?.search,
      sortBy: filters?.sortBy?.field,
      sortDirection: filters?.sortBy?.direction,
      isEnabled: filters?.isEnabled,
      hasExpired: filters?.hasExpired,
      budgetDuration: filters?.budgetDuration,
      minBudget: filters?.minBudget,
      maxBudget: filters?.maxBudget,
      allowedModels: filters?.allowedModels,
      createdAfter: filters?.createdAfter,
      createdBefore: filters?.createdBefore,
      lastUsedAfter: filters?.lastUsedAfter,
      lastUsedBefore: filters?.lastUsedBefore,
    };

    const cacheKey = this.getCacheKey('virtual-keys', params);
    return this.withCache<VirtualKeyDto[]>(
      cacheKey,
      () => super.get<VirtualKeyDto[]>(ENDPOINTS.VIRTUAL_KEYS.BASE, params),
      CACHE_TTL.SHORT
    );
  }

  async getById(id: number): Promise<VirtualKeyDto> {
    const cacheKey = this.getCacheKey('virtual-key', id);
    return this.withCache<VirtualKeyDto>(
      cacheKey,
      () => super.get<VirtualKeyDto>(ENDPOINTS.VIRTUAL_KEYS.BY_ID(id)),
      CACHE_TTL.MEDIUM
    );
  }

  async update(id: number, request: UpdateVirtualKeyRequest): Promise<void> {
    await this.put(ENDPOINTS.VIRTUAL_KEYS.BY_ID(id), request);
    await this.invalidateCache();
  }

  async deleteById(id: number): Promise<void> {
    await super.delete(ENDPOINTS.VIRTUAL_KEYS.BY_ID(id));
    await this.invalidateCache();
  }

  async search(query: string): Promise<VirtualKeyDto[]> {
    const filters: VirtualKeyFilters = {
      search: query,
      pageSize: 100,
    };
    return this.list(filters);
  }

  async resetSpend(id: number): Promise<void> {
    await this.post(ENDPOINTS.VIRTUAL_KEYS.RESET_SPEND(id));
    await this.cache?.delete(this.getCacheKey('virtual-key', id));
  }

  async validate(key: string): Promise<VirtualKeyValidationResult> {
    const request: VirtualKeyValidationRequest = { key };
    return this.post<VirtualKeyValidationResult>(
      ENDPOINTS.VIRTUAL_KEYS.VALIDATE,
      request
    );
  }

  async updateSpend(id: number, request: UpdateSpendRequest): Promise<void> {
    await this.post(ENDPOINTS.VIRTUAL_KEYS.SPEND(id), request);
    await this.cache?.delete(this.getCacheKey('virtual-key', id));
  }

  async refundSpend(id: number, request: RefundSpendRequest): Promise<void> {
    if (!request.amount || request.amount <= 0) {
      throw new ValidationError('Refund amount must be greater than zero');
    }
    if (!request.reason) {
      throw new ValidationError('Refund reason is required');
    }
    
    await this.post(ENDPOINTS.VIRTUAL_KEYS.REFUND(id), request);
    await this.cache?.delete(this.getCacheKey('virtual-key', id));
  }

  async checkBudget(id: number, estimatedCost: number): Promise<CheckBudgetResponse> {
    const request: CheckBudgetRequest = { estimatedCost };
    return this.post<CheckBudgetResponse>(
      ENDPOINTS.VIRTUAL_KEYS.CHECK_BUDGET(id),
      request
    );
  }

  async getValidationInfo(id: number): Promise<VirtualKeyValidationInfo> {
    return super.get<VirtualKeyValidationInfo>(
      ENDPOINTS.VIRTUAL_KEYS.VALIDATION_INFO(id)
    );
  }

  async performMaintenance(
    request?: VirtualKeyMaintenanceRequest
  ): Promise<VirtualKeyMaintenanceResponse> {
    return this.post<VirtualKeyMaintenanceResponse>(
      ENDPOINTS.VIRTUAL_KEYS.MAINTENANCE,
      request || {}
    );
  }

  async getStatistics(): Promise<VirtualKeyStatistics> {
    // STUB: This endpoint needs to be implemented in the Admin API
    throw new NotImplementedError(
      'getStatistics requires Admin API endpoint implementation. ' +
      'The WebUI currently calculates statistics client-side by fetching all keys.'
    );
  }

  async bulkCreate(_requests: CreateVirtualKeyRequest[]): Promise<CreateVirtualKeyResponse[]> {
    // STUB: This endpoint needs to be implemented in the Admin API
    throw new NotImplementedError(
      'bulkCreate requires Admin API endpoint implementation. ' +
      'Consider implementing POST /api/virtualkeys/bulk for batch creation.'
    );
  }

  async exportKeys(_format: 'csv' | 'json'): Promise<Blob> {
    // STUB: This endpoint needs to be implemented in the Admin API
    throw new NotImplementedError(
      'exportKeys requires Admin API endpoint implementation. ' +
      'Consider implementing GET /api/virtualkeys/export with format parameter.'
    );
  }

  private async invalidateCache(): Promise<void> {
    if (!this.cache) return;
    
    // For now, clear all cache
    await this.cache.clear();
  }
}