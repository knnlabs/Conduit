import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { RequestConfig } from '../client/types';
import { HttpMethod } from '../client/HttpMethod';
import type { components, paths } from '../generated/admin-api';
import {
  parseCriticalResponse,
  virtualKeyIssueSchema,
  virtualKeyValidationSchema,
} from '@/lib/api-transport/critical-response-validation';

type VirtualKeyDto = components['schemas']['VirtualKeyDto'];
type CreateVirtualKeyRequestDto = components['schemas']['CreateVirtualKeyRequestDto'];
type CreateVirtualKeyResponseDto = components['schemas']['CreateVirtualKeyResponseDto'];
type UpdateVirtualKeyRequestDto = components['schemas']['UpdateVirtualKeyRequestDto'];
type VirtualKeyValidationResponseDto = components['schemas']['VirtualKeyValidationResult'];
type VirtualKeyDiscoveryPreviewDto = components['schemas']['VirtualKeyDiscoveryPreviewDto'];
type ListQuery = paths['/api/VirtualKeys']['get']['parameters']['query'];

export interface VirtualKeyListResponseDto {
  items: VirtualKeyDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export class FetchVirtualKeyService {
  constructor(private readonly client: FetchBaseApiClient) {}

  async list(page = 1, pageSize = 10, virtualKeyGroupId?: number, config?: RequestConfig): Promise<VirtualKeyListResponseDto> {
    const query: ListQuery = { virtualKeyGroupId };
    const suffix = virtualKeyGroupId === undefined ? '' : `?virtualKeyGroupId=${virtualKeyGroupId}`;
    const allItems = await this.client['executeContractRead'](`/api/VirtualKeys${suffix}`,
      (client, options) => client.GET('/api/VirtualKeys', { ...options, params: { query } }), config);
    const totalCount = allItems.length;
    return {
      items: allItems.slice((page - 1) * pageSize, page * pageSize),
      totalCount,
      page,
      pageSize,
      totalPages: Math.ceil(totalCount / pageSize),
    };
  }

  async get(id: string, config?: RequestConfig): Promise<VirtualKeyDto> {
    const numericId = Number(id);
    return this.client['executeContractRead'](`/api/VirtualKeys/${numericId}`,
      (client, options) => client.GET('/api/VirtualKeys/{id}', { ...options, params: { path: { id: numericId } } }), config);
  }

  async create(data: CreateVirtualKeyRequestDto, config?: RequestConfig): Promise<CreateVirtualKeyResponseDto> {
    const response = await this.client['executeContractOperation']('/api/VirtualKeys', HttpMethod.POST,
      (client, options) => client.POST('/api/VirtualKeys', { ...options, body: data }), config, data);
    return parseCriticalResponse(virtualKeyIssueSchema, response, 'Admin virtual-key issuance') as CreateVirtualKeyResponseDto;
  }

  async update(id: string, data: UpdateVirtualKeyRequestDto, config?: RequestConfig): Promise<void> {
    const numericId = Number(id);
    await this.client['executeContractOperation'](`/api/VirtualKeys/${numericId}`, HttpMethod.PUT,
      (client, options) => client.PUT('/api/VirtualKeys/{id}', { ...options, params: { path: { id: numericId } }, body: data }), config, data);
  }

  async delete(id: string, config?: RequestConfig): Promise<void> {
    const numericId = Number(id);
    await this.client['executeContractOperation'](`/api/VirtualKeys/${numericId}`, HttpMethod.DELETE,
      (client, options) => client.DELETE('/api/VirtualKeys/{id}', { ...options, params: { path: { id: numericId } } }), config);
  }

  async validate(key: string, config?: RequestConfig): Promise<VirtualKeyValidationResponseDto> {
    const body: components['schemas']['ValidateVirtualKeyRequest'] = { key };
    const response = await this.client['executeContractOperation']('/api/VirtualKeys/validate', HttpMethod.POST,
      (client, options) => client.POST('/api/VirtualKeys/validate', { ...options, body }), config, body);
    return parseCriticalResponse(virtualKeyValidationSchema, response, 'Admin virtual-key validation');
  }

  async maintenance(config?: RequestConfig): Promise<void> {
    await this.client['executeContractOperation']('/api/VirtualKeys/maintenance', HttpMethod.POST,
      (client, options) => client.POST('/api/VirtualKeys/maintenance', options), config);
  }

  async previewDiscovery(id: string, capability?: string, config?: RequestConfig): Promise<VirtualKeyDiscoveryPreviewDto> {
    const numericId = Number(id);
    const suffix = capability ? `?capability=${encodeURIComponent(capability)}` : '';
    return this.client['executeContractRead'](`/api/VirtualKeys/${numericId}/discovery-preview${suffix}`,
      (client, options) => client.GET('/api/VirtualKeys/{id}/discovery-preview', {
        ...options, params: { path: { id: numericId }, query: { capability } },
      }), config);
  }

  isKeyValid(key: VirtualKeyDto): boolean {
    return key.isEnabled === true && (!key.expiresAt || new Date(key.expiresAt) >= new Date());
  }
}
