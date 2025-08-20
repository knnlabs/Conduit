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
        throw new Error(`Invalid cleanup type: ${request.type}`);
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
}