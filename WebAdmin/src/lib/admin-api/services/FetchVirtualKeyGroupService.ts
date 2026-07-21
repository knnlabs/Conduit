import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { RequestConfig } from '../client/types';
import { HttpMethod } from '../client/HttpMethod';
import type { components, paths } from '../generated/admin-api';
import type { TransactionHistoryParams } from '../models/virtualKey';
import { balanceAdjustmentSchema, parseCriticalResponse } from '@/lib/api-transport/critical-response-validation';

type VirtualKeyGroupDto = components['schemas']['VirtualKeyGroupDto'];
type CreateVirtualKeyGroupRequestDto = components['schemas']['CreateVirtualKeyGroupRequestDto'];
type UpdateVirtualKeyGroupRequestDto = components['schemas']['UpdateVirtualKeyGroupRequestDto'];
type AdjustBalanceDto = components['schemas']['AdjustBalanceDto'];
type VirtualKeyDto = components['schemas']['VirtualKeyDto'];
type PagedGroups = components['schemas']['PagedResultOfVirtualKeyGroupDto'];
type PagedTransactions = components['schemas']['PagedResultOfVirtualKeyGroupTransactionDto'];
type ListQuery = paths['/api/VirtualKeyGroups']['get']['parameters']['query'];

export interface ListGroupsParams { page?: number; pageSize?: number }

export class FetchVirtualKeyGroupService {
  constructor(private readonly client: FetchBaseApiClient) {}

  async list(params?: ListGroupsParams, config?: RequestConfig): Promise<PagedGroups> {
    const query: ListQuery = params;
    const qs = new URLSearchParams(Object.entries(params ?? {}).map(([k, v]) => [k, String(v)])).toString();
    return this.client['executeContractRead'](`/api/VirtualKeyGroups${qs ? `?${qs}` : ''}`,
      (client, options) => client.GET('/api/VirtualKeyGroups', { ...options, params: { query } }), config);
  }

  async get(id: number, config?: RequestConfig): Promise<VirtualKeyGroupDto> {
    return this.client['executeContractRead'](`/api/VirtualKeyGroups/${id}`,
      (client, options) => client.GET('/api/VirtualKeyGroups/{id}', { ...options, params: { path: { id } } }), config);
  }

  async create(data: CreateVirtualKeyGroupRequestDto, config?: RequestConfig): Promise<VirtualKeyGroupDto> {
    return this.client['executeContractOperation']('/api/VirtualKeyGroups', HttpMethod.POST,
      (client, options) => client.POST('/api/VirtualKeyGroups', { ...options, body: data }), config, data);
  }

  async update(id: number, data: UpdateVirtualKeyGroupRequestDto, config?: RequestConfig): Promise<void> {
    await this.client['executeContractOperation'](`/api/VirtualKeyGroups/${id}`, HttpMethod.PUT,
      (client, options) => client.PUT('/api/VirtualKeyGroups/{id}', { ...options, params: { path: { id } }, body: data }), config, data);
  }

  async adjustBalance(id: number, data: AdjustBalanceDto, config?: RequestConfig): Promise<VirtualKeyGroupDto> {
    const response = await this.client['executeContractOperation'](`/api/VirtualKeyGroups/${id}/adjust-balance`, HttpMethod.POST,
      (client, options) => client.POST('/api/VirtualKeyGroups/{id}/adjust-balance', {
        ...options, params: { path: { id } }, body: data,
      }), config, data);
    return parseCriticalResponse(balanceAdjustmentSchema, response, 'Admin virtual-key-group balance adjustment') as VirtualKeyGroupDto;
  }

  async delete(id: number, config?: RequestConfig): Promise<void> {
    await this.client['executeContractOperation'](`/api/VirtualKeyGroups/${id}`, HttpMethod.DELETE,
      (client, options) => client.DELETE('/api/VirtualKeyGroups/{id}', { ...options, params: { path: { id } } }), config);
  }

  async getKeys(id: number, config?: RequestConfig): Promise<VirtualKeyDto[]> {
    return this.client['executeContractRead'](`/api/VirtualKeyGroups/${id}/keys`,
      (client, options) => client.GET('/api/VirtualKeyGroups/{id}/keys', { ...options, params: { path: { id } } }), config);
  }

  async getTransactionHistory(id: number, params?: TransactionHistoryParams, config?: RequestConfig): Promise<PagedTransactions> {
    const query = params;
    const qs = new URLSearchParams(Object.entries(params ?? {}).map(([k, v]) => [k, String(v)])).toString();
    return this.client['executeContractRead'](`/api/VirtualKeyGroups/${id}/transactions${qs ? `?${qs}` : ''}`,
      (client, options) => client.GET('/api/VirtualKeyGroups/{id}/transactions', {
        ...options, params: { path: { id }, query },
      }), config);
  }
}
