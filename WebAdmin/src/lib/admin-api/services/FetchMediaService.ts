import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { RequestConfig } from '../client/types';
import { HttpMethod } from '../client/HttpMethod';
import type {
  MediaRecord, MediaStorageStats, OverallMediaStorageStats, MediaCleanupRequest,
  MediaCleanupResponse, MediaDeleteResponse, MediaCleanupStatus, MediaCleanupEnabledResponse,
  SimpleRetentionResponse, MediaRetentionPolicy, CreateMediaRetentionPolicyRequest,
  UpdateMediaRetentionPolicyRequest,
} from '../models/media';

/** Contract-native media, cleanup, and retention operations with UI-facing adapters. */
export class FetchMediaService {
  constructor(private readonly client: FetchBaseApiClient) {}

  async getMediaByVirtualKey(id: number, config?: RequestConfig): Promise<MediaRecord[]> {
    return this.client['executeContractRead'](`/api/admin/Media/virtual-key/${id}`,
      (c, o) => c.GET('/api/admin/Media/virtual-key/{virtualKeyId}', { ...o, params: { path: { virtualKeyId: id } } }), config) as Promise<MediaRecord[]>;
  }

  async getMediaStats(type: 'overall', virtualKeyId?: never, groupId?: number, config?: RequestConfig): Promise<OverallMediaStorageStats>;
  async getMediaStats(type: 'virtual-key', virtualKeyId: number, groupId?: never, config?: RequestConfig): Promise<MediaStorageStats>;
  async getMediaStats(type: 'by-provider' | 'by-type', virtualKeyId?: never, groupId?: never, config?: RequestConfig): Promise<Record<string, number>>;
  async getMediaStats(type: 'overall' | 'virtual-key' | 'by-provider' | 'by-type' = 'overall', virtualKeyId?: number, groupId?: number, config?: RequestConfig): Promise<OverallMediaStorageStats | MediaStorageStats | Record<string, number>> {
    if (type === 'virtual-key') {
      if (!virtualKeyId) throw new Error('virtualKeyId is required for virtual-key stats');
      return this.client['executeContractRead'](`/api/admin/Media/stats/virtual-key/${virtualKeyId}`,
        (c, o) => c.GET('/api/admin/Media/stats/virtual-key/{virtualKeyId}', { ...o, params: { path: { virtualKeyId } } }), config) as Promise<MediaStorageStats>;
    }
    if (type === 'by-provider') return this.client['executeContractRead']('/api/admin/Media/stats/by-provider', (c, o) => c.GET('/api/admin/Media/stats/by-provider', o), config) as Promise<Record<string, number>>;
    if (type === 'by-type') return this.client['executeContractRead']('/api/admin/Media/stats/by-type', (c, o) => c.GET('/api/admin/Media/stats/by-type', o), config) as Promise<Record<string, number>>;
    const suffix = groupId === undefined ? '' : `?virtualKeyGroupId=${groupId}`;
    return this.client['executeContractRead'](`/api/admin/Media/stats${suffix}`,
      (c, o) => c.GET('/api/admin/Media/stats', { ...o, params: { query: { virtualKeyGroupId: groupId } } }), config) as Promise<OverallMediaStorageStats>;
  }

  async searchMedia(pattern: string, config?: RequestConfig): Promise<MediaRecord[]> {
    return this.client['executeContractRead'](`/api/admin/Media/search?pattern=${encodeURIComponent(pattern)}`,
      (c, o) => c.GET('/api/admin/Media/search', { ...o, params: { query: { pattern } } }), config) as Promise<MediaRecord[]>;
  }

  async deleteMedia(id: string, config?: RequestConfig): Promise<MediaDeleteResponse> {
    return this.client['executeContractOperation'](`/api/admin/Media/${encodeURIComponent(id)}`, HttpMethod.DELETE,
      (c, o) => c.DELETE('/api/admin/Media/{mediaId}', { ...o, params: { path: { mediaId: id } } }), config) as Promise<MediaDeleteResponse>;
  }

  async cleanupMedia(request: MediaCleanupRequest, config?: RequestConfig): Promise<MediaCleanupResponse> {
    if (request.type === 'expired') return this.client['executeContractOperation']('/api/admin/Media/cleanup/expired', HttpMethod.POST, (c, o) => c.POST('/api/admin/Media/cleanup/expired', o), config) as Promise<MediaCleanupResponse>;
    if (request.type === 'orphaned') return this.client['executeContractOperation']('/api/admin/Media/cleanup/orphaned', HttpMethod.POST, (c, o) => c.POST('/api/admin/Media/cleanup/orphaned', o), config) as Promise<MediaCleanupResponse>;
    if (request.type !== 'prune') throw new Error('Invalid cleanup type');
    const body = request.daysToKeep ? { daysToKeep: request.daysToKeep } : {};
    return this.client['executeContractOperation']('/api/admin/Media/cleanup/prune', HttpMethod.POST,
      (c, o) => c.POST('/api/admin/Media/cleanup/prune', { ...o, body }), config, body) as Promise<MediaCleanupResponse>;
  }

  async getCleanupServiceStatus(config?: RequestConfig): Promise<MediaCleanupStatus> {
    return this.client['executeContractRead']('/api/admin/media-cleanup/status', (c, o) => c.GET('/api/admin/media-cleanup/status', o), config) as Promise<MediaCleanupStatus>;
  }
  async getCleanupServiceEnabled(config?: RequestConfig): Promise<MediaCleanupEnabledResponse> {
    return this.client['executeContractRead']('/api/admin/media-cleanup/enabled', (c, o) => c.GET('/api/admin/media-cleanup/enabled', o), config) as Promise<MediaCleanupEnabledResponse>;
  }
  async setCleanupServiceEnabled(enabled: boolean, config?: RequestConfig): Promise<MediaCleanupEnabledResponse> {
    const body = { enabled };
    return this.client['executeContractOperation']('/api/admin/media-cleanup/enabled', HttpMethod.POST,
      (c, o) => c.POST('/api/admin/media-cleanup/enabled', { ...o, body }), config, body) as Promise<MediaCleanupEnabledResponse>;
  }
  async getSimpleRetentionOverride(config?: RequestConfig): Promise<SimpleRetentionResponse> {
    return this.client['executeContractRead']('/api/admin/media-cleanup/simple-retention', (c, o) => c.GET('/api/admin/media-cleanup/simple-retention', o), config) as Promise<SimpleRetentionResponse>;
  }
  async setSimpleRetentionOverride(retentionDays: number | null, config?: RequestConfig): Promise<SimpleRetentionResponse> {
    const body = { retentionDays };
    return this.client['executeContractOperation']('/api/admin/media-cleanup/simple-retention', HttpMethod.POST,
      (c, o) => c.POST('/api/admin/media-cleanup/simple-retention', { ...o, body }), config, body) as Promise<SimpleRetentionResponse>;
  }
  async getRetentionPolicies(config?: RequestConfig): Promise<MediaRetentionPolicy[]> {
    return this.client['executeContractRead']('/api/admin/media-retention/policies', (c, o) => c.GET('/api/admin/media-retention/policies', o), config) as Promise<MediaRetentionPolicy[]>;
  }
  async getRetentionPolicy(id: number, config?: RequestConfig): Promise<MediaRetentionPolicy> {
    return this.client['executeContractRead'](`/api/admin/media-retention/policies/${id}`,
      (c, o) => c.GET('/api/admin/media-retention/policies/{id}', { ...o, params: { path: { id } } }), config) as Promise<MediaRetentionPolicy>;
  }
  async createRetentionPolicy(data: CreateMediaRetentionPolicyRequest, config?: RequestConfig): Promise<MediaRetentionPolicy> {
    return this.client['executeContractOperation']('/api/admin/media-retention/policies', HttpMethod.POST,
      (c, o) => c.POST('/api/admin/media-retention/policies', { ...o, body: data }), config, data) as Promise<MediaRetentionPolicy>;
  }
  async updateRetentionPolicy(id: number, data: UpdateMediaRetentionPolicyRequest, config?: RequestConfig): Promise<MediaRetentionPolicy> {
    return this.client['executeContractOperation'](`/api/admin/media-retention/policies/${id}`, HttpMethod.PUT,
      (c, o) => c.PUT('/api/admin/media-retention/policies/{id}', { ...o, params: { path: { id } }, body: data }), config, data) as Promise<MediaRetentionPolicy>;
  }
  async deleteRetentionPolicy(id: number, config?: RequestConfig): Promise<void> {
    return this.client['executeContractOperation'](`/api/admin/media-retention/policies/${id}`, HttpMethod.DELETE,
      (c, o) => c.DELETE('/api/admin/media-retention/policies/{id}', { ...o, params: { path: { id } } }), config);
  }
  async setDefaultRetentionPolicy(id: number, config?: RequestConfig): Promise<{ message: string }> {
    return this.client['executeContractOperation'](`/api/admin/media-retention/policies/${id}/set-default`, HttpMethod.POST,
      (c, o) => c.POST('/api/admin/media-retention/policies/{id}/set-default', { ...o, params: { path: { id } } }), config) as Promise<{ message: string }>;
  }
}
