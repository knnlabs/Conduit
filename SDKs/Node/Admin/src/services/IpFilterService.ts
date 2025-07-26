import { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import { ENDPOINTS, CACHE_TTL } from '../constants';
import {
  IpFilterDto,
  CreateIpFilterDto,
  UpdateIpFilterDto,
  IpFilterSettingsDto,
  UpdateIpFilterSettingsDto,
  IpCheckResult,
  IpFilterFilters,
  IpFilterStatistics,
  BulkIpFilterResponse,
  IpFilterValidationResult,
  FilterType,
  CreateTemporaryIpFilterDto,
  BulkOperationResult,
  IpFilterImport,
  IpFilterImportResult,
  BlockedRequestStats,
} from '../models/ipFilter';
import { ValidationError, NotImplementedError } from '../utils/errors';
import { z } from 'zod';

const createFilterSchema = z.object({
  name: z.string().min(1).max(100),
  ipAddressOrCidr: z.string().regex(
    /^(\d{1,3}\.){3}\d{1,3}(\/\d{1,2})?$/,
    'Invalid IP address or CIDR format (e.g., 192.168.1.1 or 192.168.1.0/24)'
  ),
  filterType: z.enum(['whitelist', 'blacklist']),
  isEnabled: z.boolean().optional(),
  description: z.string().max(500).optional(),
});

const ipCheckSchema = z.object({
  ipAddress: z.string().ipv4().or(z.string().ipv6()),
  endpoint: z.string().optional(),
});

export class IpFilterService extends FetchBaseApiClient {
  async create(request: CreateIpFilterDto): Promise<IpFilterDto> {
    try {
      createFilterSchema.parse(request);
    } catch (error) {
      throw new ValidationError('Invalid IP filter request', { validationError: error });
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
          ipAddressOrCidrContains: filters.ipAddressOrCidrContains,
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
    // Ensure the ID in the request matches the URL parameter
    request.id = id;
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

  async checkIp(ipAddress: string): Promise<IpCheckResult> {
    try {
      ipCheckSchema.parse({ ipAddress });
    } catch (error) {
      throw new ValidationError('Invalid IP check request', { validationError: error });
    }

    return this.get<IpCheckResult>(ENDPOINTS.IP_FILTERS.CHECK(ipAddress));
  }

  async search(query: string): Promise<IpFilterDto[]> {
    const filters: IpFilterFilters = {
      nameContains: query,
    };
    return this.list(filters);
  }

  async enableFilter(id: number): Promise<void> {
    await this.update(id, { id, isEnabled: true });
  }

  async disableFilter(id: number): Promise<void> {
    await this.update(id, { id, isEnabled: false });
  }

  async createAllowFilter(name: string, ipAddressOrCidr: string, description?: string): Promise<IpFilterDto> {
    return this.create({
      name,
      ipAddressOrCidr,
      filterType: 'whitelist',
      isEnabled: true,
      description,
    });
  }

  async createDenyFilter(name: string, ipAddressOrCidr: string, description?: string): Promise<IpFilterDto> {
    return this.create({
      name,
      ipAddressOrCidr,
      filterType: 'blacklist',
      isEnabled: true,
      description,
    });
  }

  async getFiltersByType(filterType: FilterType): Promise<IpFilterDto[]> {
    return this.list({ filterType });
  }

  // Bulk operations
  async bulkCreate(rules: CreateIpFilterDto[]): Promise<BulkOperationResult> {
    if (!Array.isArray(rules) || rules.length === 0) {
      throw new ValidationError('Rules array is required and must not be empty');
    }

    const response = await this.post<BulkOperationResult>(
      ENDPOINTS.IP_FILTERS.BULK_CREATE,
      { rules }
    );

    await this.invalidateCache();
    return response;
  }

  async bulkUpdate(operation: 'enable' | 'disable', ruleIds: string[]): Promise<IpFilterDto[]> {
    if (!['enable', 'disable'].includes(operation)) {
      throw new ValidationError('Operation must be either "enable" or "disable"');
    }

    if (!Array.isArray(ruleIds) || ruleIds.length === 0) {
      throw new ValidationError('Rule IDs array is required and must not be empty');
    }

    const response = await this.put<IpFilterDto[]>(
      ENDPOINTS.IP_FILTERS.BULK_UPDATE,
      { operation, ruleIds }
    );

    await this.invalidateCache();
    return response;
  }

  async bulkDelete(ruleIds: string[]): Promise<BulkOperationResult> {
    if (!Array.isArray(ruleIds) || ruleIds.length === 0) {
      throw new ValidationError('Rule IDs array is required and must not be empty');
    }

    const response = await this.post<BulkOperationResult>(
      ENDPOINTS.IP_FILTERS.BULK_DELETE,
      { ruleIds }
    );

    await this.invalidateCache();
    return response;
  }

  // Temporary rules
  async createTemporary(rule: CreateTemporaryIpFilterDto): Promise<IpFilterDto> {
    const temporarySchema = createFilterSchema.extend({
      expiresAt: z.string().refine((val) => {
        const date = new Date(val);
        return !isNaN(date.getTime()) && date > new Date();
      }, 'expiresAt must be a valid future date'),
      reason: z.string().optional(),
    });

    try {
      temporarySchema.parse(rule);
    } catch (error) {
      throw new ValidationError('Invalid temporary IP filter request', { validationError: error });
    }

    const response = await this.post<IpFilterDto>(
      ENDPOINTS.IP_FILTERS.CREATE_TEMPORARY,
      rule
    );

    await this.invalidateCache();
    return response;
  }

  async getExpiring(withinHours: number): Promise<IpFilterDto[]> {
    if (withinHours <= 0) {
      throw new ValidationError('withinHours must be a positive number');
    }

    const queryParams = new URLSearchParams({ withinHours: withinHours.toString() });
    const url = `${ENDPOINTS.IP_FILTERS.EXPIRING}?${queryParams.toString()}`;
    
    return this.get<IpFilterDto[]>(url);
  }

  // Import/Export
  async import(rules: IpFilterImport[]): Promise<IpFilterImportResult> {
    if (!Array.isArray(rules) || rules.length === 0) {
      throw new ValidationError('Rules array is required and must not be empty');
    }

    const response = await this.post<IpFilterImportResult>(
      ENDPOINTS.IP_FILTERS.IMPORT,
      { rules }
    );

    await this.invalidateCache();
    return response;
  }

  async export(format: 'json' | 'csv'): Promise<Blob> {
    if (!['json', 'csv'].includes(format)) {
      throw new ValidationError('Format must be either "json" or "csv"');
    }

    const queryParams = new URLSearchParams({ format });
    const url = `${ENDPOINTS.IP_FILTERS.EXPORT}?${queryParams.toString()}`;

    const response = await this.get<Blob>(url, {
      headers: { Accept: format === 'csv' ? 'text/csv' : 'application/json' },
      responseType: 'blob',
    });

    return response;
  }

  // Analytics
  async getBlockedRequestStats(params: { 
    startDate?: string; 
    endDate?: string; 
    groupBy?: 'rule' | 'country' | 'hour' 
  }): Promise<BlockedRequestStats> {
    const queryParams = new URLSearchParams();
    if (params.startDate) queryParams.append('startDate', params.startDate);
    if (params.endDate) queryParams.append('endDate', params.endDate);
    if (params.groupBy) queryParams.append('groupBy', params.groupBy);

    const url = `${ENDPOINTS.IP_FILTERS.BLOCKED_STATS}?${queryParams.toString()}`;
    
    return this.withCache(
      url,
      () => this.get<BlockedRequestStats>(url),
      CACHE_TTL.SHORT
    );
  }

  // Legacy stub methods for backward compatibility
  async getStatistics(): Promise<IpFilterStatistics> {
    // STUB: This endpoint needs to be implemented in the Admin API
    throw new NotImplementedError(
      'getStatistics requires Admin API endpoint implementation. ' +
        'Consider implementing GET /api/ipfilter/statistics'
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