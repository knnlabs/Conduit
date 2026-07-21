import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import { HttpMethod } from '../client/HttpMethod';
import { CACHE_TTL } from '../constants';
import type { components, paths } from '../generated/admin-api';
import type {
  IpFilterDto,
  CreateIpFilterDto,
  UpdateIpFilterDto,
  IpFilterSettingsDto,
  UpdateIpFilterSettingsDto,
  IpCheckResult,
  IpFilterFilters,
} from '../models/ipFilter';
import { ValidationError } from '../utils/errors';
import { validateRequired, validateStringLength, validateEnum } from '../utils/validation';

function isValidIpv4(ip: string): boolean {
  const parts = ip.split('.');
  if (parts.length !== 4) return false;
  return parts.every((part) => {
    const num = parseInt(part, 10);
    return !isNaN(num) && num >= 0 && num <= 255 && part === num.toString();
  });
}

function isValidIpv6(ip: string): boolean {
  if (ip.includes('::ffff:')) return isValidIpv4(ip.split('::ffff:')[1]);
  const ipv6Regex = /^(([0-9a-fA-F]{1,4}:){7}[0-9a-fA-F]{1,4}|([0-9a-fA-F]{1,4}:){1,7}:|([0-9a-fA-F]{1,4}:){1,6}:[0-9a-fA-F]{1,4}|([0-9a-fA-F]{1,4}:){1,5}(:[0-9a-fA-F]{1,4}){1,2}|([0-9a-fA-F]{1,4}:){1,4}(:[0-9a-fA-F]{1,4}){1,3}|([0-9a-fA-F]{1,4}:){1,3}(:[0-9a-fA-F]{1,4}){1,4}|([0-9a-fA-F]{1,4}:){1,2}(:[0-9a-fA-F]{1,4}){1,5}|[0-9a-fA-F]{1,4}:((:[0-9a-fA-F]{1,4}){1,6})|:((:[0-9a-fA-F]{1,4}){1,7}|:)|::)$/;
  return ipv6Regex.test(ip);
}

function isValidIp(ip: string): boolean {
  return isValidIpv4(ip) || isValidIpv6(ip);
}

function validateIpAddressOrCidr(value: string): void {
  if (!value.includes('/')) {
    if (!isValidIp(value)) throw new ValidationError('Invalid IP address format (e.g., 192.168.1.1 or 2001:db8::1)');
    return;
  }
  const [ip, prefixStr] = value.split('/');
  const prefix = parseInt(prefixStr, 10);
  if (!isValidIp(ip)) throw new ValidationError('Invalid IP address format in CIDR notation (e.g., 192.168.1.0/24 or 2001:db8::/32)');
  if (isValidIpv4(ip) && (isNaN(prefix) || prefix < 0 || prefix > 32))
    throw new ValidationError('IPv4 CIDR prefix must be between 0 and 32');
  if (!isValidIpv4(ip) && (isNaN(prefix) || prefix < 0 || prefix > 128))
    throw new ValidationError('IPv6 CIDR prefix must be between 0 and 128');
}

function validateIpAddress(value: string): void {
  if (!isValidIp(value)) throw new ValidationError('Invalid IP address format');
}

function validateCreateIpFilterRequest(request: CreateIpFilterDto): void {
  validateRequired(request, ['name', 'ipAddressOrCidr', 'filterType']);
  validateStringLength(request.name, 1, 100, 'name');
  validateIpAddressOrCidr(request.ipAddressOrCidr);
  validateEnum(request.filterType, ['whitelist', 'blacklist'] as const, 'filterType');
  if (request.description) validateStringLength(request.description, 0, 500, 'description');
}

export class FetchIpFilterService {
  constructor(private readonly client: FetchBaseApiClient) {}

  async create(request: CreateIpFilterDto): Promise<IpFilterDto> {
    validateCreateIpFilterRequest(request);
    const body: components['schemas']['CreateIpFilterDto'] = request;
    const response = await this.client['executeContractOperation']('/api/IpFilter', HttpMethod.POST,
      (client, options) => client.POST('/api/IpFilter', { ...options, body }), undefined, body);
    await this.invalidateCache();
    return response as unknown as IpFilterDto;
  }

  async list(filters?: IpFilterFilters): Promise<IpFilterDto[]> {
    const params = filters ? {
      filterType: filters.filterType, isEnabled: filters.isEnabled,
      nameContains: filters.nameContains, ipAddressOrCidrContains: filters.ipAddressOrCidrContains,
      lastMatchedAfter: filters.lastMatchedAfter, lastMatchedBefore: filters.lastMatchedBefore,
      minMatchCount: filters.minMatchCount, sortBy: filters.sortBy?.field,
      sortDirection: filters.sortBy?.direction,
    } : undefined;
    const entries = Object.entries(params ?? {}).filter(([, value]) => value !== undefined);
    const query = Object.fromEntries(entries);
    const suffix = entries.length ? `?${new URLSearchParams(entries.map(([key, value]) => [key, String(value)])).toString()}` : '';
    const cacheKey = this.client['getCacheKey']('ip-filters', params);
    return this.client['withCache'](cacheKey, () =>
      this.client['executeContractRead'](`/api/IpFilter${suffix}`, (client, options) => {
        // These compatibility filters are intentionally passed through even though the API
        // neither documents nor applies them today.
        return client.GET('/api/IpFilter', {
          ...options, params: { query },
        } as never);
      }) as Promise<IpFilterDto[]>, CACHE_TTL.SHORT);
  }

  async getById(id: number): Promise<IpFilterDto> {
    const cacheKey = this.client['getCacheKey']('ip-filter', id);
    return this.client['withCache'](cacheKey, () => this.client['executeContractRead'](`/api/IpFilter/${id}`,
      (client, options) => client.GET('/api/IpFilter/{id}', { ...options, params: { path: { id } } })) as Promise<IpFilterDto>, CACHE_TTL.SHORT);
  }

  async getEnabled(): Promise<IpFilterDto[]> {
    return this.client['withCache']('ip-filters-enabled', () => this.client['executeContractRead']('/api/IpFilter/enabled',
      (client, options) => client.GET('/api/IpFilter/enabled', options)) as Promise<IpFilterDto[]>, CACHE_TTL.SHORT);
  }

  async listByVirtualKey(virtualKeyId: number): Promise<IpFilterDto[]> {
    const cacheKey = this.client['getCacheKey']('ip-filters-by-vkey', virtualKeyId);
    return this.client['withCache'](cacheKey, () => this.client['executeContractRead'](`/api/IpFilter/by-virtual-key/${virtualKeyId}`,
      (client, options) => client.GET('/api/IpFilter/by-virtual-key/{virtualKeyId}', {
        ...options, params: { path: { virtualKeyId } },
      })) as Promise<IpFilterDto[]>, CACHE_TTL.SHORT);
  }

  async update(id: number, request: UpdateIpFilterDto): Promise<void> {
    request.id = id;
    const body = request as unknown as components['schemas']['UpdateIpFilterDto'];
    await this.client['executeContractOperation'](`/api/IpFilter/${id}`, HttpMethod.PUT,
      (client, options) => client.PUT('/api/IpFilter/{id}', { ...options, params: { path: { id } }, body }), undefined, body);
    await this.invalidateCache();
  }

  async deleteById(id: number): Promise<void> {
    await this.client['executeContractOperation'](`/api/IpFilter/${id}`, HttpMethod.DELETE,
      (client, options) => client.DELETE('/api/IpFilter/{id}', { ...options, params: { path: { id } } }));
    await this.invalidateCache();
  }

  async getSettings(): Promise<IpFilterSettingsDto> {
    return this.client['withCache']('ip-filter-settings', () => this.client['executeContractRead']('/api/IpFilter/settings',
      (client, options) => client.GET('/api/IpFilter/settings', options)) as Promise<IpFilterSettingsDto>, CACHE_TTL.SHORT);
  }

  async updateSettings(request: UpdateIpFilterSettingsDto): Promise<void> {
    type SettingsBody = paths['/api/IpFilter/settings']['put']['requestBody']['content']['application/json'];
    // Preserve the partial facade until the backend stops binding the full settings response model.
    const body = request as SettingsBody;
    await this.client['executeContractOperation']('/api/IpFilter/settings', HttpMethod.PUT,
      (client, options) => client.PUT('/api/IpFilter/settings', { ...options, body }), undefined, body);
    await this.invalidateCache();
  }

  async checkIp(ipAddress: string): Promise<IpCheckResult> {
    validateIpAddress(ipAddress);
    return this.client['executeContractRead'](`/api/IpFilter/check/${encodeURIComponent(ipAddress)}`,
      (client, options) => client.GET('/api/IpFilter/check/{ipAddress}', {
        ...options, params: { path: { ipAddress } },
      })) as Promise<IpCheckResult>;
  }

  async enableFilter(id: number): Promise<void> { await this.update(id, { id, isEnabled: true }); }
  async disableFilter(id: number): Promise<void> { await this.update(id, { id, isEnabled: false }); }

  private async invalidateCache(): Promise<void> {
    if (this.client['cache']) await this.client['cache'].clear();
  }
}
