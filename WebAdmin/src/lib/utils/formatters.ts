/**
 * Re-export formatting utilities from the local shared utilities.
 * All business logic now lives in the SDK for cross-project reuse.
 */
import { formatters } from '@/lib/conduit-common';

export { formatters };
export type { DateFormatOptions, CurrencyFormatOptions, NumberFormatOptions } from '@/lib/conduit-common';

export const formatDuration = formatters.responseTime;

export function formatCost(
  cost: number | null | undefined,
): string {
  if (cost === null || cost === undefined || !Number.isFinite(cost)) return '—';
  if (cost === 0) return '$0.00';
  return formatters.currency(cost);
}

export function formatRelativeTime(
  dateInput: Date | string,
  now = Date.now(),
): string {
  const date = typeof dateInput === 'string' ? new Date(dateInput) : dateInput;
  const time = date.getTime();
  if (Number.isNaN(time)) return '—';

  const diffSeconds = Math.max(0, Math.floor((now - time) / 1000));
  if (diffSeconds < 60) return 'Just now';

  const minutes = Math.floor(diffSeconds / 60);
  if (minutes < 60) return `${minutes} minute${minutes === 1 ? '' : 's'} ago`;

  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours} hour${hours === 1 ? '' : 's'} ago`;

  const days = Math.floor(hours / 24);
  if (days < 7) return `${days} day${days === 1 ? '' : 's'} ago`;
  return formatters.date(date, { relativeDays: 0 });
}
