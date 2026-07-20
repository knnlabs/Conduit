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
 * Validates IPv4 address format
 */
function isValidIpv4(ip: string): boolean {
  const parts = ip.split('.');
  if (parts.length !== 4) return false;
  return parts.every((part) => {
    const num = parseInt(part, 10);
    return !isNaN(num) && num >= 0 && num <= 255 && part === num.toString();
  });
}

/**
 * Validates IPv6 address format (simplified - accepts common formats)
 */
function isValidIpv6(ip: string): boolean {
  // Handle IPv4-mapped IPv6
  if (ip.includes('::ffff:')) {
    const ipv4Part = ip.split('::ffff:')[1];
    return isValidIpv4(ipv4Part);
  }

  // Basic IPv6 validation - allows shorthand with ::
  const ipv6Regex =
    /^(([0-9a-fA-F]{1,4}:){7}[0-9a-fA-F]{1,4}|([0-9a-fA-F]{1,4}:){1,7}:|([0-9a-fA-F]{1,4}:){1,6}:[0-9a-fA-F]{1,4}|([0-9a-fA-F]{1,4}:){1,5}(:[0-9a-fA-F]{1,4}){1,2}|([0-9a-fA-F]{1,4}:){1,4}(:[0-9a-fA-F]{1,4}){1,3}|([0-9a-fA-F]{1,4}:){1,3}(:[0-9a-fA-F]{1,4}){1,4}|([0-9a-fA-F]{1,4}:){1,2}(:[0-9a-fA-F]{1,4}){1,5}|[0-9a-fA-F]{1,4}:((:[0-9a-fA-F]{1,4}){1,6})|:((:[0-9a-fA-F]{1,4}){1,7}|:)|::)$/;
  return ipv6Regex.test(ip);
}

/**
 * Validates IP address (IPv4 or IPv6)
 */
function isValidIp(ip: string): boolean {
  return isValidIpv4(ip) || isValidIpv6(ip);
}

/**
 * Validates IP address or CIDR notation (IPv4 or IPv6)
 */
function validateIpAddressOrCidr(value: string): void {
  if (value.includes('/')) {
    // CIDR notation
    const [ip, prefixStr] = value.split('/');
    const prefix = parseInt(prefixStr, 10);

    if (!isValidIp(ip)) {
      throw new ValidationError(
        'Invalid IP address format in CIDR notation (e.g., 192.168.1.0/24 or 2001:db8::/32)'
      );
    }

    // Validate prefix length based on IP version
    if (isValidIpv4(ip)) {
      if (isNaN(prefix) || prefix < 0 || prefix > 32) {
        throw new ValidationError('IPv4 CIDR prefix must be between 0 and 32');
      }
    } else {
      if (isNaN(prefix) || prefix < 0 || prefix > 128) {
        throw new ValidationError('IPv6 CIDR prefix must be between 0 and 128');
      }
    }
  } else {
    // Plain IP address
    if (!isValidIp(value)) {
      throw new ValidationError(
        'Invalid IP address format (e.g., 192.168.1.1 or 2001:db8::1)'
      );
    }
  }
}

/**
 * Validates IPv4 or IPv6 address (no CIDR allowed)
 */
function validateIpAddress(value: string): void {
  if (!isValidIp(value)) {
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

  /**
   * Lists the IP filters scoped to a specific virtual key (per-key allow/deny rules).
   */
  async listByVirtualKey(virtualKeyId: number): Promise<IpFilterDto[]> {
    const cacheKey = this.client['getCacheKey']('ip-filters-by-vkey', virtualKeyId);
    return this.client['withCache'](
      cacheKey,
      () =>
        this.client['get']<IpFilterDto[]>(
          ENDPOINTS.IP_FILTERS.BY_VIRTUAL_KEY(virtualKeyId)
        ),
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


  /**
   * Get statistics about IP filter rules (computed client-side from current rules)
   */
  async getStatistics(): Promise<IpFilterStatistics> {
    const allFilters = await this.list();
    const enabledFilters = allFilters.filter((f) => f.isEnabled);

    return {
      totalRules: allFilters.length,
      enabledRules: enabledFilters.length,
      disabledRules: allFilters.length - enabledFilters.length,
      whitelistRules: allFilters.filter((f) => f.filterType === 'whitelist').length,
      blacklistRules: allFilters.filter((f) => f.filterType === 'blacklist').length,
      // Note: These fields would require server-side tracking
      totalBlockedRequests: 0,
      lastUpdated: allFilters.reduce(
        (latest, f) => (new Date(f.updatedAt) > new Date(latest) ? f.updatedAt : latest),
        allFilters[0]?.updatedAt ?? new Date().toISOString()
      ),
    };
  }

  /**
   * Import filters from a file (requires server-side implementation)
   * @throws NotImplementedError - Server endpoint not yet available
   */
  async importFilters(_file: File | Blob, _format: 'csv' | 'json'): Promise<BulkIpFilterResponse> {
    // This operation requires server-side processing for validation and bulk insert
    throw new NotImplementedError(
      'importFilters requires Admin API endpoint implementation. ' +
        'Consider implementing POST /api/ipfilter/import'
    );
  }

  /**
   * Export filters to JSON or CSV format (computed client-side)
   */
  async exportFilters(format: 'csv' | 'json', filterType?: FilterType): Promise<Blob> {
    let filters = await this.list();

    // Filter by type if specified
    if (filterType) {
      filters = filters.filter((f) => f.filterType === filterType);
    }

    if (format === 'json') {
      const jsonData = JSON.stringify(filters, null, 2);
      return new Blob([jsonData], { type: 'application/json' });
    } else {
      // CSV format
      const headers = [
        'id',
        'filterType',
        'ipAddressOrCidr',
        'name',
        'description',
        'isEnabled',
        'createdAt',
        'updatedAt',
      ];
      const csvRows = [headers.join(',')];

      for (const filter of filters) {
        const row = [
          filter.id,
          filter.filterType,
          `"${filter.ipAddressOrCidr}"`,
          `"${filter.name ?? ''}"`,
          `"${(filter.description ?? '').replace(/"/g, '""')}"`,
          filter.isEnabled,
          filter.createdAt,
          filter.updatedAt,
        ];
        csvRows.push(row.join(','));
      }

      const csvData = csvRows.join('\n');
      return new Blob([csvData], { type: 'text/csv' });
    }
  }

  /**
   * Validate CIDR notation (computed client-side)
   */
  async validateCidr(cidrRange: string): Promise<IpFilterValidationResult> {
    try {
      validateIpAddressOrCidr(cidrRange);

      // Parse CIDR components
      const hasCidr = cidrRange.includes('/');
      const [ip, prefixStr] = hasCidr ? cidrRange.split('/') : [cidrRange, undefined];
      const prefix = prefixStr ? parseInt(prefixStr, 10) : undefined;
      const isIpv6Address = isValidIpv6(ip);

      return {
        isValid: true,
        ipAddress: ip,
        prefixLength: prefix,
        isIpv6: isIpv6Address,
        normalizedCidr: cidrRange,
      };
    } catch (error) {
      return {
        isValid: false,
        error: error instanceof Error ? error.message : 'Invalid CIDR format',
      };
    }
  }

  /**
   * Test how rules would affect an IP address
   */
  async testRules(
    ipAddress: string,
    _proposedRules?: CreateIpFilterDto[]
  ): Promise<{
    currentResult: IpCheckResult;
    proposedResult?: IpCheckResult;
    changes?: string[];
  }> {
    validateIpAddress(ipAddress);

    // Get current result from the server
    const currentResult = await this.checkIp(ipAddress);

    // Note: Testing proposed rules would require server-side implementation
    // For now, we only return the current result
    if (_proposedRules && _proposedRules.length > 0) {
      return {
        currentResult,
        proposedResult: undefined,
        changes: [
          'Note: Testing proposed rules requires server-side implementation. ' +
            'Only current rule evaluation is available.',
        ],
      };
    }

    return { currentResult };
  }

  private async invalidateCache(): Promise<void> {
    if (!this.client['cache']) return;
    await this.client['cache'].clear();
  }
}