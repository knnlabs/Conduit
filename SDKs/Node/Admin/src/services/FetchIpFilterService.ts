import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import { ENDPOINTS, CACHE_TTL } from '../constants';
import {
  IpFilterDto,
  CreateIpFilterDto,
  UpdateIpFilterDto,
  IpFilterSettingsDto,
  UpdateIpFilterSettingsDto,
  IpCheckResult,
  IpFilterFilters,
  FilterType,
  IpFilterStatistics,
  BulkIpFilterResponse,
  IpFilterValidationResult,
} from '../models/ipFilter';
import { ValidationError, NotImplementedError } from '../utils/errors';
import { validateRequired, validateStringLength, validateEnum } from '../utils/validation';

/**
 * Validates IP address or CIDR notation
 */
function validateIpAddressOrCidr(value: string): void {
  const ipCidrRegex = /^(\d{1,3}\.){3}\d{1,3}(\/\d{1,2})?$/;
  if (!ipCidrRegex.test(value)) {
    throw new ValidationError('Invalid IP address or CIDR format (e.g., 192.168.1.1 or 192.168.1.0/24)');
  }
}

/**
 * Validates IPv4 or IPv6 address
 */
function validateIpAddress(value: string): void {
  const ipv4Regex = /^(\d{1,3}\.){3}\d{1,3}$/;
  const ipv6Regex = /^([0-9a-fA-F]{1,4}:){7}[0-9a-fA-F]{1,4}$/;

  if (!ipv4Regex.test(value) && !ipv6Regex.test(value)) {
    throw new ValidationError('Invalid IP address format');
  }
}

/**
 * Validates create IP filter request
 */
function validateCreateIpFilterRequest(request: CreateIpFilterDto): void {
  validateRequired(request, ['name', 'ipAddressOrCidr', 'filterType']);
  validateStringLength(request.name, 1, 100, 'name');
  validateIpAddressOrCidr(request.ipAddressOrCidr);
  validateEnum(request.filterType, ['whitelist', 'blacklist'] as const, 'filterType');

  if (request.description) {
    validateStringLength(request.description, 0, 500, 'description');
  }
}

export class FetchIpFilterService {
  constructor(private readonly client: FetchBaseApiClient) {}

  async create(request: CreateIpFilterDto): Promise<IpFilterDto> {
    validateCreateIpFilterRequest(request);

    const response = await this.client['post']<IpFilterDto>(
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

    const cacheKey = this.client['getCacheKey']('ip-filters', params);
    return this.client['withCache'](
      cacheKey,
      () => this.client['get']<IpFilterDto[]>(ENDPOINTS.IP_FILTERS.BASE, params),
      CACHE_TTL.SHORT
    );
  }

  async getById(id: number): Promise<IpFilterDto> {
    const cacheKey = this.client['getCacheKey']('ip-filter', id);
    return this.client['withCache'](
      cacheKey,
      () => this.client['get']<IpFilterDto>(ENDPOINTS.IP_FILTERS.BY_ID(id)),
      CACHE_TTL.SHORT
    );
  }

  async getEnabled(): Promise<IpFilterDto[]> {
    const cacheKey = 'ip-filters-enabled';
    return this.client['withCache'](
      cacheKey,
      () => this.client['get']<IpFilterDto[]>(ENDPOINTS.IP_FILTERS.ENABLED),
      CACHE_TTL.SHORT
    );
  }

  async update(id: number, request: UpdateIpFilterDto): Promise<void> {
    // Ensure the ID in the request matches the URL parameter
    request.id = id;
    await this.client['put'](ENDPOINTS.IP_FILTERS.BY_ID(id), request);
    await this.invalidateCache();
  }

  async deleteById(id: number): Promise<void> {
    await this.client['delete'](ENDPOINTS.IP_FILTERS.BY_ID(id));
    await this.invalidateCache();
  }

  async getSettings(): Promise<IpFilterSettingsDto> {
    const cacheKey = 'ip-filter-settings';
    return this.client['withCache'](
      cacheKey,
      () => this.client['get']<IpFilterSettingsDto>(ENDPOINTS.IP_FILTERS.SETTINGS),
      CACHE_TTL.SHORT
    );
  }

  async updateSettings(request: UpdateIpFilterSettingsDto): Promise<void> {
    await this.client['put'](ENDPOINTS.IP_FILTERS.SETTINGS, request);
    await this.invalidateCache();
  }

  async checkIp(ipAddress: string): Promise<IpCheckResult> {
    validateIpAddress(ipAddress);
    return this.client['get']<IpCheckResult>(ENDPOINTS.IP_FILTERS.CHECK(ipAddress));
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
    if (!this.client['cache']) return;
    await this.client['cache'].clear();
  }
}