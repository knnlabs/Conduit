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
  // Bulk operations removed - create filters individually



  // Removed non-existent endpoints

  private async invalidateCache(): Promise<void> {
    if (!this.cache) return;
    await this.cache.clear();
  }
}