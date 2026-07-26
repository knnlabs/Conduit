/**
 * Shared formatting helpers for the analytics request-log components.
 */

export function formatCost(cost: number | null | undefined): string {
  if (cost === null || cost === undefined) return '—';
  if (cost === 0) return '$0.00';
  return cost < 0.01 ? `$${cost.toFixed(6)}` : `$${cost.toFixed(4)}`;
}

export function formatDuration(ms: number): string {
  return ms < 1000 ? `${Math.round(ms)} ms` : `${(ms / 1000).toFixed(2)} s`;
}

export function getHttpStatusColor(statusCode: number | null): string {
  if (statusCode === null) return 'gray';
  if (statusCode >= 200 && statusCode < 300) return 'green';
  if (statusCode >= 300 && statusCode < 400) return 'blue';
  if (statusCode >= 400 && statusCode < 500) return 'orange';
  if (statusCode >= 500) return 'red';
  return 'gray';
}
