/**
 * Type definitions for formatting utilities
 */

export interface DateFormatOptions extends Intl.DateTimeFormatOptions {
  locale?: string;
  includeTime?: boolean;
  includeSeconds?: boolean;
  relativeDays?: number;
}

export interface CurrencyFormatOptions extends Intl.NumberFormatOptions {
  locale?: string;
  currency?: string;
  compact?: boolean;
  precision?: number;
}

export interface NumberFormatOptions extends Intl.NumberFormatOptions {
  locale?: string;
  compact?: boolean;
  units?: string;
}
