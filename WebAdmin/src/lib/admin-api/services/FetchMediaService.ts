import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { RequestConfig } from '../client/types';
import { ENDPOINTS } from '../constants';
import type {
  MediaRecord,
  MediaStorageStats,
  OverallMediaStorageStats,
  MediaCleanupRequest,
  MediaCleanupResponse,
  MediaDeleteResponse,
  MediaCleanupStatus,
  MediaCleanupEnabledResponse,
  SimpleRetentionResponse,
  MediaRetentionPolicy,
  CreateMediaRetentionPolicyRequest,
  UpdateMediaRetentionPolicyRequest,
} from '../models/media';

/**
 * Type-safe Media service using native fetch
 *
 * Provides comprehensive media management functionality including:
 * - Media record retrieval by virtual key
 * - Storage statistics and analytics
 * - Media search capabilities
 * - Media cleanup operations
 * - Media deletion
 *
 * All operations are fully typed and follow the same patterns as other Admin SDK services.
 */
export class FetchMediaService {
  constructor(private readonly client: FetchBaseApiClient) {}

  /**
   * Get media records for a specific virtual key
   *
   * @param virtualKeyId - The virtual key ID to get media for
   * @param config - Optional request configuration
   * @returns Promise resolving to array of media records
   *
   * @example
   * ```typescript
   * const media = await client.media.getMediaByVirtualKey(123);
   * console.warn(`Found ${media.length} media files`);
   * ```
   */
  async getMediaByVirtualKey(
    virtualKeyId: number,
    config?: RequestConfig
  ): Promise<MediaRecord[]> {
    return this.client['get']<MediaRecord[]>(
      ENDPOINTS.MEDIA.BY_VIRTUAL_KEY(virtualKeyId.toString()),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get media storage statistics
   *
   * @param type - Type of stats to retrieve ('overall', 'by-provider', 'by-type', 'virtual-key')
   * @param virtualKeyId - Required when type is 'virtual-key'
   * @param virtualKeyGroupId - Optional filter by virtual key group ID (only for 'overall' type)
   * @param config - Optional request configuration
   * @returns Promise resolving to stats based on type
   *
   * @example
   * ```typescript
   * // Get overall stats
   * const overallStats = await client.media.getMediaStats('overall');
   *
   * // Get stats for specific virtual key
   * const keyStats = await client.media.getMediaStats('virtual-key', 123);
   *
   * // Get stats by provider
   * const providerStats = await client.media.getMediaStats('by-provider');
   * ```
   */
  async getMediaStats(
    type: 'overall',
    virtualKeyId?: never,
    virtualKeyGroupId?: number,
    config?: RequestConfig
  ): Promise<OverallMediaStorageStats>;
  async getMediaStats(
    type: 'virtual-key',
    virtualKeyId: number,
    virtualKeyGroupId?: never,
    config?: RequestConfig
  ): Promise<MediaStorageStats>;
  async getMediaStats(
    type: 'by-provider' | 'by-type',
    virtualKeyId?: never,
    virtualKeyGroupId?: never,
    config?: RequestConfig
  ): Promise<Record<string, number>>;
  async getMediaStats(
    type: 'overall' | 'by-provider' | 'by-type' | 'virtual-key' = 'overall',
    virtualKeyId?: number,
    virtualKeyGroupId?: number,
    config?: RequestConfig
  ): Promise<OverallMediaStorageStats | MediaStorageStats | Record<string, number>> {
    let endpoint: string;

    switch (type) {
      case 'by-provider':
        endpoint = ENDPOINTS.MEDIA.STATS.BY_PROVIDER;
        break;
      case 'by-type':
        endpoint = ENDPOINTS.MEDIA.STATS.BY_TYPE;
        break;
      case 'virtual-key':
        if (!virtualKeyId) {
          throw new Error('virtualKeyId is required for virtual-key stats');
        }
        endpoint = ENDPOINTS.MEDIA.STATS.BY_VIRTUAL_KEY(virtualKeyId.toString());
        break;
      case 'overall':
      default: {
        // Add virtualKeyGroupId param if provided
        const params = virtualKeyGroupId ? `?virtualKeyGroupId=${virtualKeyGroupId}` : '';
        endpoint = `${ENDPOINTS.MEDIA.STATS.BASE}${params}`;
        break;
      }
    }

    return this.client['get'](endpoint, {
      signal: config?.signal,
      timeout: config?.timeout,
      headers: config?.headers,
    });
  }

  /**
   * Search for media records by storage key pattern
   *
   * @param pattern - Pattern to search for in storage keys
   * @param config - Optional request configuration
   * @returns Promise resolving to array of matching media records
   *
   * @example
   * ```typescript
   * const results = await client.media.searchMedia('image-*');
   * console.warn(`Found ${results.length} matching files`);
   * ```
   */
  async searchMedia(
    pattern: string,
    config?: RequestConfig
  ): Promise<MediaRecord[]> {
    const params = new URLSearchParams({
      pattern: pattern
    });

    return this.client['get']<MediaRecord[]>(
      `${ENDPOINTS.MEDIA.SEARCH}?${params.toString()}`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Delete a specific media record
   *
   * @param mediaId - ID of the media record to delete
   * @param config - Optional request configuration
   * @returns Promise resolving to deletion response
   *
   * @example
   * ```typescript
   * const response = await client.media.deleteMedia('media-123');
   * console.warn(response.message);
   * ```
   */
  async deleteMedia(
    mediaId: string,
    config?: RequestConfig
  ): Promise<MediaDeleteResponse> {
    return this.client['delete']<MediaDeleteResponse>(
      ENDPOINTS.MEDIA.BY_ID(mediaId),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Cleanup media records using various strategies
   *
   * @param request - Cleanup request specifying type and optional parameters
   * @param config - Optional request configuration
   * @returns Promise resolving to cleanup response with count of deleted items
   *
   * @example
   * ```typescript
   * // Clean up expired media
   * const expiredResult = await client.media.cleanupMedia({ type: 'expired' });
   *
   * // Clean up orphaned media
   * const orphanedResult = await client.media.cleanupMedia({ type: 'orphaned' });
   *
   * // Prune old media (keep last 30 days)
   * const pruneResult = await client.media.cleanupMedia({
   *   type: 'prune',
   *   daysToKeep: 30
   * });
   *
   * console.warn(`Deleted ${pruneResult.deletedCount} files`);
   * ```
   */
  async cleanupMedia(
    request: MediaCleanupRequest,
    config?: RequestConfig
  ): Promise<MediaCleanupResponse> {
    let endpoint: string;
    let body: unknown = undefined;

    switch (request.type) {
      case 'expired':
        endpoint = ENDPOINTS.MEDIA.CLEANUP.EXPIRED;
        break;
      case 'orphaned':
        endpoint = ENDPOINTS.MEDIA.CLEANUP.ORPHANED;
        break;
      case 'prune':
        endpoint = ENDPOINTS.MEDIA.CLEANUP.PRUNE;
        body = request.daysToKeep ? { daysToKeep: request.daysToKeep } : undefined;
        break;
      default:
        throw new Error('Invalid cleanup type');
    }

    return this.client['post']<MediaCleanupResponse>(
      endpoint,
      body,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get the status of the media cleanup background service
   *
   * @param config - Optional request configuration
   * @returns Promise resolving to cleanup service status including last run info, budget usage, and configuration
   *
   * @example
   * ```typescript
   * const status = await client.media.getCleanupServiceStatus();
   * console.warn(`Last run: ${status.lastRunTimeUtc}`);
   * console.warn(`Budget used: ${status.monthlyBudgetUsedPercent}%`);
   * console.warn(`Enabled: ${status.isEnabled}`);
   * ```
   */
  async getCleanupServiceStatus(
    config?: RequestConfig
  ): Promise<MediaCleanupStatus> {
    return this.client['get']<MediaCleanupStatus>(
      ENDPOINTS.MEDIA.CLEANUP_SERVICE.STATUS,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get whether the media cleanup service is currently enabled
   *
   * @param config - Optional request configuration
   * @returns Promise resolving to enabled state
   *
   * @example
   * ```typescript
   * const response = await client.media.getCleanupServiceEnabled();
   * console.warn(`Cleanup service enabled: ${response.enabled}`);
   * ```
   */
  async getCleanupServiceEnabled(
    config?: RequestConfig
  ): Promise<MediaCleanupEnabledResponse> {
    return this.client['get']<MediaCleanupEnabledResponse>(
      ENDPOINTS.MEDIA.CLEANUP_SERVICE.ENABLED,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Enable or disable the media cleanup background service at runtime
   *
   * @param enabled - Whether to enable or disable the service
   * @param config - Optional request configuration
   * @returns Promise resolving to confirmation response
   *
   * @example
   * ```typescript
   * // Enable the cleanup service
   * const response = await client.media.setCleanupServiceEnabled(true);
   * console.warn(response.message);
   *
   * // Disable the cleanup service
   * const response = await client.media.setCleanupServiceEnabled(false);
   * console.warn(response.message);
   * ```
   */
  async setCleanupServiceEnabled(
    enabled: boolean,
    config?: RequestConfig
  ): Promise<MediaCleanupEnabledResponse> {
    return this.client['post']<MediaCleanupEnabledResponse>(
      ENDPOINTS.MEDIA.CLEANUP_SERVICE.ENABLED,
      { enabled },
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  // ========================================
  // Simple Retention Override Methods
  // ========================================

  /**
   * Get the current simple retention override setting
   * When set, all media is deleted after the specified number of days regardless of account balance.
   *
   * @param config - Optional request configuration
   * @returns Promise resolving to simple retention response
   *
   * @example
   * ```typescript
   * const response = await client.media.getSimpleRetentionOverride();
   * if (response.isOverrideActive) {
   *   console.warn(`All media will be deleted after ${response.retentionDays} days`);
   * } else {
   *   console.warn('Using policy-based retention');
   * }
   * ```
   */
  async getSimpleRetentionOverride(
    config?: RequestConfig
  ): Promise<SimpleRetentionResponse> {
    return this.client['get']<SimpleRetentionResponse>(
      ENDPOINTS.MEDIA.CLEANUP_SERVICE.SIMPLE_RETENTION,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Set or clear the simple retention override
   * When set, all media is deleted after the specified number of days regardless of account balance.
   * Pass null to clear the override and return to policy-based retention.
   *
   * @param retentionDays - Number of days (1-365) or null to clear
   * @param config - Optional request configuration
   * @returns Promise resolving to simple retention response
   *
   * @example
   * ```typescript
   * // Set simple retention to 30 days for all media
   * const response = await client.media.setSimpleRetentionOverride(30);
   * console.warn(response.message);
   *
   * // Clear override and use policy-based retention
   * const cleared = await client.media.setSimpleRetentionOverride(null);
   * console.warn(cleared.message);
   * ```
   */
  async setSimpleRetentionOverride(
    retentionDays: number | null,
    config?: RequestConfig
  ): Promise<SimpleRetentionResponse> {
    return this.client['post']<SimpleRetentionResponse>(
      ENDPOINTS.MEDIA.CLEANUP_SERVICE.SIMPLE_RETENTION,
      { retentionDays },
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  // ========================================
  // Retention Policy Management Methods
  // ========================================

  /**
   * Get all media retention policies
   *
   * @param config - Optional request configuration
   * @returns Promise resolving to array of retention policies
   *
   * @example
   * ```typescript
   * const policies = await client.media.getRetentionPolicies();
   * const defaultPolicy = policies.find(p => p.isDefault);
   * console.warn(`Default policy: ${defaultPolicy?.name}`);
   * ```
   */
  async getRetentionPolicies(
    config?: RequestConfig
  ): Promise<MediaRetentionPolicy[]> {
    return this.client['get']<MediaRetentionPolicy[]>(
      ENDPOINTS.MEDIA.RETENTION_POLICIES.BASE,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get a specific retention policy by ID
   *
   * @param id - The policy ID
   * @param config - Optional request configuration
   * @returns Promise resolving to the retention policy
   *
   * @example
   * ```typescript
   * const policy = await client.media.getRetentionPolicy(1);
   * console.warn(`Policy "${policy.name}" retains for ${policy.positiveBalanceRetentionDays} days`);
   * ```
   */
  async getRetentionPolicy(
    id: number,
    config?: RequestConfig
  ): Promise<MediaRetentionPolicy> {
    return this.client['get']<MediaRetentionPolicy>(
      ENDPOINTS.MEDIA.RETENTION_POLICIES.BY_ID(id),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Create a new retention policy
   *
   * @param policy - The policy to create
   * @param config - Optional request configuration
   * @returns Promise resolving to the created policy
   *
   * @example
   * ```typescript
   * const policy = await client.media.createRetentionPolicy({
   *   name: 'Extended Retention',
   *   description: 'Extended retention for premium users',
   *   positiveBalanceRetentionDays: 90,
   *   zeroBalanceRetentionDays: 30,
   *   negativeBalanceRetentionDays: 7,
   * });
   * console.warn(`Created policy: ${policy.name} (ID: ${policy.id})`);
   * ```
   */
  async createRetentionPolicy(
    policy: CreateMediaRetentionPolicyRequest,
    config?: RequestConfig
  ): Promise<MediaRetentionPolicy> {
    return this.client['post']<MediaRetentionPolicy>(
      ENDPOINTS.MEDIA.RETENTION_POLICIES.BASE,
      policy,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Update an existing retention policy
   *
   * @param id - The policy ID
   * @param policy - The policy updates (partial)
   * @param config - Optional request configuration
   * @returns Promise resolving to the updated policy
   *
   * @example
   * ```typescript
   * const updated = await client.media.updateRetentionPolicy(1, {
   *   positiveBalanceRetentionDays: 120,
   * });
   * console.warn(`Updated retention to ${updated.positiveBalanceRetentionDays} days`);
   * ```
   */
  async updateRetentionPolicy(
    id: number,
    policy: UpdateMediaRetentionPolicyRequest,
    config?: RequestConfig
  ): Promise<MediaRetentionPolicy> {
    return this.client['put']<MediaRetentionPolicy>(
      ENDPOINTS.MEDIA.RETENTION_POLICIES.BY_ID(id),
      policy,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Delete a retention policy
   *
   * @param id - The policy ID to delete
   * @param config - Optional request configuration
   * @returns Promise resolving when deletion is complete
   *
   * @example
   * ```typescript
   * await client.media.deleteRetentionPolicy(2);
   * console.warn('Policy deleted');
   * ```
   */
  async deleteRetentionPolicy(
    id: number,
    config?: RequestConfig
  ): Promise<void> {
    await this.client['delete']<void>(
      ENDPOINTS.MEDIA.RETENTION_POLICIES.BY_ID(id),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Set a policy as the default retention policy
   * Only one policy can be the default at a time.
   *
   * @param id - The policy ID to set as default
   * @param config - Optional request configuration
   * @returns Promise resolving to confirmation message
   *
   * @example
   * ```typescript
   * const result = await client.media.setDefaultRetentionPolicy(1);
   * console.warn(result.message);
   * ```
   */
  async setDefaultRetentionPolicy(
    id: number,
    config?: RequestConfig
  ): Promise<{ message: string }> {
    return this.client['post']<{ message: string }>(
      ENDPOINTS.MEDIA.RETENTION_POLICIES.SET_DEFAULT(id),
      {},
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }
}
