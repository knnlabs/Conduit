import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { RequestConfig } from '../client/types';
import { HttpMethod } from '../client/HttpMethod';
import type { components, paths } from '../generated/admin-api';
import type { DriftItemFilter } from '../models/providerSync';

type DriftItemDto = components['schemas']['DriftItemDto'];
type DriftActionResultDto = components['schemas']['DriftActionResultDto'];
type BulkDriftActionRequest = components['schemas']['BulkDriftActionRequest'];
type BulkDriftActionResponse = components['schemas']['BulkDriftActionResponse'];
type ProviderSyncRunDto = components['schemas']['ProviderSyncRunDto'];
type DriftQuery = paths['/api/ProviderSync/drift']['get']['parameters']['query'];
type RunsQuery = paths['/api/ProviderSync/runs']['get']['parameters']['query'];

export class FetchProviderSyncService {
  constructor(private readonly client: FetchBaseApiClient) {}

  async listDrift(filter?: DriftItemFilter, config?: RequestConfig): Promise<DriftItemDto[]> {
    const query: DriftQuery = filter;
    const queryString = new URLSearchParams(Object.entries(filter ?? {}).filter(([, value]) => value !== undefined).map(([key, value]) => [key, String(value)])).toString();
    return this.client['executeContractRead'](
      `/api/ProviderSync/drift${queryString ? `?${queryString}` : ''}`,
      (contractClient, options) => contractClient.GET('/api/ProviderSync/drift', { ...options, params: { query } }),
      config,
    );
  }

  async getDrift(id: number, config?: RequestConfig): Promise<DriftItemDto> {
    return this.client['executeContractRead'](`/api/ProviderSync/drift/${id}`,
      (contractClient, options) => contractClient.GET('/api/ProviderSync/drift/{id}', { ...options, params: { path: { id } } }), config);
  }

  async apply(id: number, config?: RequestConfig): Promise<DriftActionResultDto> {
    return this.client['executeContractOperation'](`/api/ProviderSync/drift/${id}/apply`, HttpMethod.POST,
      (contractClient, options) => contractClient.POST('/api/ProviderSync/drift/{id}/apply', { ...options, params: { path: { id } } }), config);
  }

  async dismiss(id: number, config?: RequestConfig): Promise<DriftActionResultDto> {
    return this.client['executeContractOperation'](`/api/ProviderSync/drift/${id}/dismiss`, HttpMethod.POST,
      (contractClient, options) => contractClient.POST('/api/ProviderSync/drift/{id}/dismiss', { ...options, params: { path: { id } } }), config);
  }

  async applyBulk(ids: number[], config?: RequestConfig): Promise<BulkDriftActionResponse> {
    const body: BulkDriftActionRequest = { ids };
    return this.client['executeContractOperation']('/api/ProviderSync/drift/bulk/apply', HttpMethod.POST,
      (contractClient, options) => contractClient.POST('/api/ProviderSync/drift/bulk/apply', { ...options, body }), config, body);
  }

  async dismissBulk(ids: number[], config?: RequestConfig): Promise<BulkDriftActionResponse> {
    const body: BulkDriftActionRequest = { ids };
    return this.client['executeContractOperation']('/api/ProviderSync/drift/bulk/dismiss', HttpMethod.POST,
      (contractClient, options) => contractClient.POST('/api/ProviderSync/drift/bulk/dismiss', { ...options, body }), config, body);
  }

  async run(config?: RequestConfig): Promise<ProviderSyncRunDto> {
    return this.client['executeContractOperation']('/api/ProviderSync/run', HttpMethod.POST,
      (contractClient, options) => contractClient.POST('/api/ProviderSync/run', options), config);
  }

  async listRuns(page = 1, pageSize = 25, config?: RequestConfig): Promise<ProviderSyncRunDto[]> {
    const query: RunsQuery = { page, pageSize };
    return this.client['executeContractRead'](`/api/ProviderSync/runs?page=${page}&pageSize=${pageSize}`,
      (contractClient, options) => contractClient.GET('/api/ProviderSync/runs', { ...options, params: { query } }), config);
  }
}
