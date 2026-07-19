'use client';

import { withAdminClient } from '@/lib/client/adminClient';
import { useAdminMutation } from '@/hooks/useAdminMutation';
import type {
  DriftItemDto,
  DriftItemFilter,
  DriftActionResultDto,
  BulkDriftActionResponse,
  ProviderSyncRunDto,
} from '@knn_labs/conduit-admin-client';

export const DRIFT_QUERY_KEY = 'provider-sync-drift';
export const RUNS_QUERY_KEY = 'provider-sync-runs';

/**
 * Fetch drift items (defaults to Pending on the server), optionally filtered.
 * Standalone async function for use inside useQuery.
 */
export async function fetchDrift(filter?: DriftItemFilter): Promise<DriftItemDto[]> {
  return withAdminClient((client) => client.providerSync.listDrift(filter));
}

/**
 * Fetch recent sync runs for the history panel.
 */
export async function fetchSyncRuns(page = 1, pageSize = 25): Promise<ProviderSyncRunDto[]> {
  return withAdminClient((client) => client.providerSync.listRuns(page, pageSize));
}

// --- Mutation hooks using useAdminMutation ---

function applyResultMessage(result: DriftActionResultDto): string {
  if (result.success) return 'Drift applied';
  if (result.stale) return 'Skipped: values changed since detection — re-run sync';
  return `Not applied: ${result.error ?? 'unknown error'}`;
}

export function useApplyDrift() {
  return useAdminMutation<DriftActionResultDto, number>({
    mutationFn: (id) => (client) => client.providerSync.apply(id),
    successMessage: applyResultMessage,
    invalidateKeys: [DRIFT_QUERY_KEY],
  });
}

export function useDismissDrift() {
  return useAdminMutation<DriftActionResultDto, number>({
    mutationFn: (id) => (client) => client.providerSync.dismiss(id),
    successMessage: 'Drift item dismissed',
    invalidateKeys: [DRIFT_QUERY_KEY],
  });
}

export function useBulkApplyDrift() {
  return useAdminMutation<BulkDriftActionResponse, number[]>({
    mutationFn: (ids) => (client) => client.providerSync.applyBulk(ids),
    successMessage: (result) =>
      `Applied ${result.succeededCount}${result.failedCount > 0 ? `, ${result.failedCount} skipped/failed` : ''}`,
    invalidateKeys: [DRIFT_QUERY_KEY],
  });
}

export function useBulkDismissDrift() {
  return useAdminMutation<BulkDriftActionResponse, number[]>({
    mutationFn: (ids) => (client) => client.providerSync.dismissBulk(ids),
    successMessage: (result) =>
      `Dismissed ${result.succeededCount}${result.failedCount > 0 ? `, ${result.failedCount} failed` : ''}`,
    invalidateKeys: [DRIFT_QUERY_KEY],
  });
}

export function useRunSync() {
  return useAdminMutation<ProviderSyncRunDto, void>({
    mutationFn: () => (client) => client.providerSync.run(),
    successMessage: (run) =>
      `Sync ${run.status.toLowerCase()}: ${run.itemsCreated} new, ${run.itemsUpdated} updated, ${run.itemsAutoResolved} auto-resolved`,
    invalidateKeys: [DRIFT_QUERY_KEY, RUNS_QUERY_KEY],
  });
}
