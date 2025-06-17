import { BaseApiClient } from '../client/BaseApiClient';
import { ENDPOINTS, CACHE_TTL } from '../constants';
import {
  IpFilterDto,
  CreateIpFilterDto,
  UpdateIpFilterDto,
  IpFilterSettingsDto,
  UpdateIpFilterSettingsDto,
  IpCheckRequest,
  IpCheckResult,
  IpFilterFilters,
  IpFilterStatistics,
  BulkIpFilterRequest,
  BulkIpFilterResponse,
  IpFilterValidationResult,
  FilterType,
} from '../models/ipFilter';
import { ValidationError, NotImplementedError } from '../utils/errors';
import { z } from 'zod';

const createFilterSchema = z.object({
  name: z.string().min(1).max(100),
  cidrRange: z.string().regex(
    /^(\d{1,3}\.){3}\d{1,3}\/\d{1,2}$/,
    'Invalid CIDR format (e.g., 192.168.1.0/24)'
  ),
  filterType: z.enum(['Allow', 'Deny']),
  isEnabled: z.boolean().optional(),
  description: z.string().max(500).optional(),
});

const ipCheckSchema = z.object({
  ipAddress: z.string().ip(),
  endpoint: z.string().optional(),
});

export class IpFilterService extends BaseApiClient {
  async create(request: CreateIpFilterDto): Promise<IpFilterDto> {
    try {
      createFilterSchema.parse(request);
    } catch (error) {
      throw new ValidationError('Invalid IP filter request', error);
    }

    const response = await this.post<IpFilterDto>(
      ENDPOINTS.IP_FILTERS.BASE,
      request
    );

    await this.invalidateCache();
    return response;
  }

  async list(filters?: IpFilterFilters): Promise<IpFilterDto[]> {
    const params = filters
      ? {
          filterType: filters.filterType,
          isEnabled: filters.isEnabled,
          nameContains: filters.nameContains,
          cidrContains: filters.cidrContains,
          lastMatchedAfter: filters.lastMatchedAfter,
          lastMatchedBefore: filters.lastMatchedBefore,
          minMatchCount: filters.minMatchCount,
          sortBy: filters.sortBy?.field,
          sortDirection: filters.sortBy?.direction,
        }
      : undefined;

    const cacheKey = this.getCacheKey('ip-filters', params);
    return this.withCache(
      cacheKey,
      () => super.get<IpFilterDto[]>(ENDPOINTS.IP_FILTERS.BASE, params),
      CACHE_TTL.SHORT
    );
  }

  async getById(id: number): Promise<IpFilterDto> {
    const cacheKey = this.getCacheKey('ip-filter', id);
    return this.withCache(
      cacheKey,
      () => super.get<IpFilterDto>(ENDPOINTS.IP_FILTERS.BY_ID(id)),
      CACHE_TTL.SHORT
    );
  }

  async getEnabled(): Promise<IpFilterDto[]> {
    const cacheKey = 'ip-filters-enabled';
    return this.withCache(
      cacheKey,
      () => super.get<IpFilterDto[]>(ENDPOINTS.IP_FILTERS.ENABLED),
      CACHE_TTL.SHORT
    );
  }

  async update(id: number, request: UpdateIpFilterDto): Promise<void> {
    await this.put(ENDPOINTS.IP_FILTERS.BY_ID(id), request);
    await this.invalidateCache();
  }

  async deleteById(id: number): Promise<void> {
    await super.delete(ENDPOINTS.IP_FILTERS.BY_ID(id));
    await this.invalidateCache();
  }

  async getSettings(): Promise<IpFilterSettingsDto> {
    const cacheKey = 'ip-filter-settings';
    return this.withCache(
      cacheKey,
      () => super.get<IpFilterSettingsDto>(ENDPOINTS.IP_FILTERS.SETTINGS),
      CACHE_TTL.SHORT
    );
  }

  async updateSettings(request: UpdateIpFilterSettingsDto): Promise<void> {
    await this.put(ENDPOINTS.IP_FILTERS.SETTINGS, request);
    await this.invalidateCache();
  }

  async checkIp(ipAddress: string, endpoint?: string): Promise<IpCheckResult> {
    try {
      ipCheckSchema.parse({ ipAddress, endpoint });
    } catch (error) {
      throw new ValidationError('Invalid IP check request', error);
    }

    const request: IpCheckRequest = { ipAddress, endpoint };
    return this.post<IpCheckResult>(ENDPOINTS.IP_FILTERS.CHECK, request);
  }

  async search(query: string): Promise<IpFilterDto[]> {
    const filters: IpFilterFilters = {
      nameContains: query,
    };
    return this.list(filters);
  }

  async enableFilter(id: number): Promise<void> {
    await this.update(id, { isEnabled: true });
  }

  async disableFilter(id: number): Promise<void> {
    await this.update(id, { isEnabled: false });
  }

  async createAllowFilter(name: string, cidrRange: string, description?: string): Promise<IpFilterDto> {
    return this.create({
      name,
      cidrRange,
      filterType: 'Allow',
      isEnabled: true,
      description,
    });
  }

  async createDenyFilter(name: string, cidrRange: string, description?: string): Promise<IpFilterDto> {
    return this.create({
      name,
      cidrRange,
      filterType: 'Deny',
      isEnabled: true,
      description,
    });
  }

  async getFiltersByType(filterType: FilterType): Promise<IpFilterDto[]> {
    return this.list({ filterType });
  }

  // Stub methods
  async getStatistics(): Promise<IpFilterStatistics> {
    // STUB: This endpoint needs to be implemented in the Admin API
    throw new NotImplementedError(
      'getStatistics requires Admin API endpoint implementation. ' +
        'Consider implementing GET /api/ipfilter/statistics'
    );
  }

  async bulkCreate(_request: BulkIpFilterRequest): Promise<BulkIpFilterResponse> {
    // STUB: This endpoint needs to be implemented in the Admin API
    throw new NotImplementedError(
      'bulkCreate requires Admin API endpoint implementation. ' +
        'Consider implementing POST /api/ipfilter/bulk'
    );
  }

  async importFilters(_file: File | Blob, _format: 'csv' | 'json'): Promise<BulkIpFilterResponse> {
    // STUB: This endpoint needs to be implemented in the Admin API
    throw new NotImplementedError(
      'importFilters requires Admin API endpoint implementation. ' +
        'Consider implementing POST /api/ipfilter/import'
    );
  }

  async exportFilters(_format: 'csv' | 'json', _filterType?: FilterType): Promise<Blob> {
    // STUB: This endpoint needs to be implemented in the Admin API
    throw new NotImplementedError(
      'exportFilters requires Admin API endpoint implementation. ' +
        'Consider implementing GET /api/ipfilter/export'
    );
  }

  async validateCidr(_cidrRange: string): Promise<IpFilterValidationResult> {
    // STUB: This endpoint needs to be implemented in the Admin API
    throw new NotImplementedError(
      'validateCidr requires Admin API endpoint implementation. ' +
        'Consider implementing POST /api/ipfilter/validate-cidr'
    );
  }

  async testRules(_ipAddress: string, _proposedRules?: CreateIpFilterDto[]): Promise<{
    currentResult: IpCheckResult;
    proposedResult?: IpCheckResult;
    changes?: string[];
  }> {
    // STUB: This endpoint needs to be implemented in the Admin API
    throw new NotImplementedError(
      'testRules requires Admin API endpoint implementation. ' +
        'Consider implementing POST /api/ipfilter/test'
    );
  }

  private async invalidateCache(): Promise<void> {
    if (!this.cache) return;
    await this.cache.clear();
  }
}