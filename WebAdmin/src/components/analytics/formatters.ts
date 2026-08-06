/**
 * Shared formatting helpers for the analytics request-log components.
 */

export { formatCost, formatDuration, formatters } from '@/lib/utils/formatters';

export function getHttpStatusColor(statusCode: number | null): string {
  if (statusCode === null) return 'gray';
  if (statusCode >= 200 && statusCode < 300) return 'green';
  if (statusCode >= 300 && statusCode < 400) return 'blue';
  if (statusCode >= 400 && statusCode < 500) return 'orange';
  if (statusCode >= 500) return 'red';
  return 'gray';
}
