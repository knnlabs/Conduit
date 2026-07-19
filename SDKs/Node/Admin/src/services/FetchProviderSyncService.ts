import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { RequestConfig } from '../client/types';
import { ENDPOINTS } from '../constants';
import {
  DriftItemDto,
  DriftItemFilter,
  DriftActionResultDto,
  BulkDriftActionResponse,
  ProviderSyncRunDto,
} from '../models/providerSync';

/**
 * Type-safe Provider metadata sync service using native fetch.
 *
 * Surfaces the OpenRouter drift-review workflow: list/get pending drift, apply or
 * dismiss individual items or in bulk, trigger a sync run, and read run history.
 */
export class FetchProviderSyncService {
  constructor(private readonly client: FetchBaseApiClient) {}

  /**
   * List drift items (defaults to Pending on the server), optionally filtered.
   */
  async listDrift(
    filter?: DriftItemFilter,
    config?: RequestConfig
  ): Promise<DriftItemDto[]> {
    const queryParams = new URLSearchParams();
    if (filter) {
      Object.entries(filter).forEach(([key, value]) => {
        if (value !== undefined) {
          queryParams.append(key, String(value));
        }
      });
    }

    const url = queryParams.toString()
      ? `${ENDPOINTS.PROVIDER_SYNC.DRIFT}?${queryParams.toString()}`
      : ENDPOINTS.PROVIDER_SYNC.DRIFT;

    return this.client['get']<DriftItemDto[]>(url, {
      signal: config?.signal,
      timeout: config?.timeout,
      headers: config?.headers,
    });
  }

  /**
   * Get a single drift item by ID.
   */
  async getDrift(id: number, config?: RequestConfig): Promise<DriftItemDto> {
    return this.client['get']<DriftItemDto>(
      ENDPOINTS.PROVIDER_SYNC.DRIFT_BY_ID(id),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Apply a drift item's proposed change. Returns a stale flag when the item was
   * rejected because Conduit's current values changed since detection.
   */
  async apply(id: number, config?: RequestConfig): Promise<DriftActionResultDto> {
    return this.client['post']<DriftActionResultDto>(
      ENDPOINTS.PROVIDER_SYNC.DRIFT_APPLY(id),
      undefined,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Dismiss a drift item without applying it.
   */
  async dismiss(id: number, config?: RequestConfig): Promise<DriftActionResultDto> {
    return this.client['post']<DriftActionResultDto>(
      ENDPOINTS.PROVIDER_SYNC.DRIFT_DISMISS(id),
      undefined,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Apply multiple drift items, returning per-item results.
   */
  async applyBulk(
    ids: number[],
    config?: RequestConfig
  ): Promise<BulkDriftActionResponse> {
    return this.client['post']<BulkDriftActionResponse, { ids: number[] }>(
      ENDPOINTS.PROVIDER_SYNC.DRIFT_BULK_APPLY,
      { ids },
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Dismiss multiple drift items, returning per-item results.
   */
  async dismissBulk(
    ids: number[],
    config?: RequestConfig
  ): Promise<BulkDriftActionResponse> {
    return this.client['post']<BulkDriftActionResponse, { ids: number[] }>(
      ENDPOINTS.PROVIDER_SYNC.DRIFT_BULK_DISMISS,
      { ids },
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Trigger a sync now. The server returns 409 if a sync is already in progress.
   */
  async run(config?: RequestConfig): Promise<ProviderSyncRunDto> {
    return this.client['post']<ProviderSyncRunDto>(
      ENDPOINTS.PROVIDER_SYNC.RUN,
      undefined,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * List recent sync runs.
   */
  async listRuns(
    page = 1,
    pageSize = 25,
    config?: RequestConfig
  ): Promise<ProviderSyncRunDto[]> {
    const queryParams = new URLSearchParams();
    queryParams.append('page', String(page));
    queryParams.append('pageSize', String(pageSize));

    return this.client['get']<ProviderSyncRunDto[]>(
      `${ENDPOINTS.PROVIDER_SYNC.RUNS}?${queryParams.toString()}`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }
}
