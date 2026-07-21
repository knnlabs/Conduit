/**
 * Re-export formatting utilities from the local shared utilities.
 * All business logic now lives in the SDK for cross-project reuse.
 */
export { formatters } from '@/lib/conduit-common';
export type { DateFormatOptions, CurrencyFormatOptions, NumberFormatOptions } from '@/lib/conduit-common';
