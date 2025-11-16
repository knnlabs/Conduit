/**
 * Comprehensive formatting utilities for consistent data presentation
 * across the ConduitLLM WebUI application.
 */

export interface DateFormatOptions extends Intl.DateTimeFormatOptions {
  locale?: string;
  includeTime?: boolean;
  includeSeconds?: boolean;
  relativeDays?: number; // Show "today", "yesterday" for recent dates
}

export interface CurrencyFormatOptions extends Intl.NumberFormatOptions {
  locale?: string;
  currency?: string;
  compact?: boolean; // Use compact notation for large numbers
  precision?: number; // Override decimal places
}

export interface NumberFormatOptions extends Intl.NumberFormatOptions {
  locale?: string;
  compact?: boolean;
  units?: string; // Append units like "requests", "tokens", etc.
}

/**
 * Centralized formatting utilities with comprehensive options
 */
export const formatters = {
  /**
   * Format dates with intelligent defaults and extensive customization
   */
  date: (
    dateInput: string | Date | null | undefined, 
    options: DateFormatOptions = {}
  ): string => {
    if (!dateInput) return 'Never';
    
    const date = typeof dateInput === 'string' ? new Date(dateInput) : dateInput;
    
    // Validate date
    if (isNaN(date.getTime())) {
      console.warn('Invalid date input:', dateInput);
      return 'Invalid Date';
    }

    const {
      locale = 'en-US',
      includeTime = true,
      includeSeconds = false,
      relativeDays = 7,
      ...intlOptions
    } = options;

    // Handle relative dates for recent timestamps
    if (relativeDays > 0) {
      const now = new Date();
      const diffDays = Math.floor((now.getTime() - date.getTime()) / (1000 * 60 * 60 * 24));
      
      if (diffDays === 0) return `Today at ${formatters.time(date, { locale })}`;
      if (diffDays === 1) return `Yesterday at ${formatters.time(date, { locale })}`;
      if (diffDays < relativeDays) return `${diffDays} days ago`;
    }

    // Default format options
    const defaultOptions: Intl.DateTimeFormatOptions = {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      ...(includeTime && {
        hour: '2-digit',
        minute: '2-digit',
        ...(includeSeconds && { second: '2-digit' })
      }),
      ...intlOptions
    };

    return date.toLocaleDateString(locale, defaultOptions);
  },

  /**
   * Format time only
   */
  time: (
    dateInput: string | Date | null | undefined,
    options: { locale?: string; includeSeconds?: boolean } = {}
  ): string => {
    if (!dateInput) return '--:--';
    
    const date = typeof dateInput === 'string' ? new Date(dateInput) : dateInput;
    if (isNaN(date.getTime())) return '--:--';

    const { locale = 'en-US', includeSeconds = false } = options;
    
    return date.toLocaleTimeString(locale, {
      hour: '2-digit',
      minute: '2-digit',
      ...(includeSeconds && { second: '2-digit' })
    });
  },

  /**
   * Format dates without time component
   */
  dateOnly: (
    dateInput: string | Date | null | undefined,
    options: { locale?: string } = {}
  ): string => {
    return formatters.date(dateInput, { 
      ...options, 
      includeTime: false 
    });
  },

  /**
   * Format currency with intelligent defaults and customization
   */
  currency: (
    amount: number | null | undefined,
    options: CurrencyFormatOptions = {}
  ): string => {
    if (amount === null || amount === undefined || isNaN(amount)) {
      return '$0.00';
    }

    const {
      locale = 'en-US',
      currency = 'USD',
      compact = false,
      precision,
      ...intlOptions
    } = options;

    // Determine appropriate precision based on context
    // If precision is explicitly provided, use it
    // Otherwise, use 6 decimals for micro-transactions, 4 for everything else
    const minimumFractionDigits = precision ?? (amount < 0.01 ? 6 : 4);
    const maximumFractionDigits = precision ?? (amount < 0.01 ? 6 : 4);

    const formatOptions: Intl.NumberFormatOptions = {
      style: 'currency',
      currency,
      minimumFractionDigits,
      maximumFractionDigits,
      ...(compact && amount >= 1000 && { notation: 'compact' }),
      ...intlOptions
    };

    return new Intl.NumberFormat(locale, formatOptions).format(amount);
  },

  /**
   * Format large currency amounts with compact notation
   */
  compactCurrency: (
    amount: number | null | undefined,
    options: CurrencyFormatOptions = {}
  ): string => {
    return formatters.currency(amount, { ...options, compact: true });
  },

  /**
   * Format percentages with consistent precision
   */
  percentage: (
    value: number | null | undefined,
    total?: number | null  ,
    options: { decimals?: number; locale?: string } = {}
  ): string => {
    const { decimals = 1, locale = 'en-US' } = options;

    if (value === null || value === undefined || isNaN(value)) {
      return '0%';
    }

    let percentage: number;
    if (total !== undefined && total !== null && !isNaN(total)) {
      if (total === 0) return '0%';
      percentage = (value / total) * 100;
    } else {
      percentage = value * 100; // Assume value is already a ratio
    }

    return new Intl.NumberFormat(locale, {
      style: 'percent',
      minimumFractionDigits: decimals,
      maximumFractionDigits: decimals
    }).format(percentage / 100);
  },

  /**
   * Format file sizes with appropriate units
   */
  fileSize: (
    bytes: number | null | undefined,
    options: { decimals?: number; binary?: boolean } = {}
  ): string => {
    if (bytes === null || bytes === undefined || isNaN(bytes) || bytes < 0) {
      return '0 B';
    }

    const { decimals = 1, binary = false } = options;
    const base = binary ? 1024 : 1000;
    const units = binary 
      ? ['B', 'KiB', 'MiB', 'GiB', 'TiB', 'PiB']
      : ['B', 'KB', 'MB', 'GB', 'TB', 'PB'];

    if (bytes === 0) return '0 B';

    const exp = Math.floor(Math.log(bytes) / Math.log(base));
    const unitIndex = Math.min(exp, units.length - 1);
    const value = bytes / Math.pow(base, unitIndex);

    return `${value.toFixed(unitIndex === 0 ? 0 : decimals)} ${units[unitIndex]}`;
  },

  /**
   * Format numbers with thousand separators and optional units
   */
  number: (
    value: number | null | undefined,
    options: NumberFormatOptions & { units?: string } = {}
  ): string => {
    if (value === null || value === undefined || isNaN(value)) {
      return '0';
    }

    const { locale = 'en-US', compact = false, units, ...intlOptions } = options;

    const formatOptions: Intl.NumberFormatOptions = {
      ...(compact && value >= 1000 && { notation: 'compact' }),
      ...intlOptions
    };

    const formatted = new Intl.NumberFormat(locale, formatOptions).format(value);
    return units ? `${formatted} ${units}` : formatted;
  },

  /**
   * Format duration from milliseconds to human readable
   */
  duration: (
    milliseconds: number | null | undefined,
    options: { format?: 'long' | 'short' | 'compact' } = {}
  ): string => {
    if (milliseconds === null || milliseconds === undefined || isNaN(milliseconds) || milliseconds < 0) {
      return '0ms';
    }

    const { format = 'short' } = options;

    const seconds = Math.floor(milliseconds / 1000);
    const minutes = Math.floor(seconds / 60);
    const hours = Math.floor(minutes / 60);
    const days = Math.floor(hours / 24);

    if (format === 'compact') {
      if (days > 0) return `${days}d`;
      if (hours > 0) return `${hours}h`;
      if (minutes > 0) return `${minutes}m`;
      if (seconds > 0) return `${seconds}s`;
      return `${milliseconds}ms`;
    }

    if (format === 'long') {
      const parts = [];
      if (days > 0) parts.push(`${days} day${days !== 1 ? 's' : ''}`);
      if (hours % 24 > 0) parts.push(`${hours % 24} hour${hours % 24 !== 1 ? 's' : ''}`);
      if (minutes % 60 > 0) parts.push(`${minutes % 60} minute${minutes % 60 !== 1 ? 's' : ''}`);
      if (seconds % 60 > 0) parts.push(`${seconds % 60} second${seconds % 60 !== 1 ? 's' : ''}`);
      return parts.join(', ') || '0 seconds';
    }

    // Short format (default)
    if (days > 0) return `${days}d ${hours % 24}h`;
    if (hours > 0) return `${hours}h ${minutes % 60}m`;
    if (minutes > 0) return `${minutes}m ${seconds % 60}s`;
    if (seconds > 0) return `${seconds}s`;
    return `${milliseconds}ms`;
  },

  /**
   * Format API response times with appropriate units
   */
  responseTime: (milliseconds: number | null | undefined): string => {
    if (milliseconds === null || milliseconds === undefined || isNaN(milliseconds)) {
      return '--';
    }

    if (milliseconds < 1000) {
      return `${Math.round(milliseconds)}ms`;
    }

    const seconds = milliseconds / 1000;
    return `${seconds.toFixed(1)}s`;
  },

  /**
   * Format large numbers with short notation (1.2M, 500K, etc)
   */
  shortNumber: (
    value: number | null | undefined,
    options: { decimals?: number; locale?: string } = {}
  ): string => {
    if (value === null || value === undefined || isNaN(value)) {
      return '0';
    }

    const { decimals = 1 } = options;

    if (Math.abs(value) < 1000) {
      return Math.round(value).toString();
    }

    const suffixes = ['', 'K', 'M', 'B', 'T'];
    const absValue = Math.abs(value);
    const exp = Math.min(Math.floor(Math.log10(absValue) / 3), suffixes.length - 1);
    const shortValue = absValue / Math.pow(1000, exp);
    
    const formatted = shortValue.toFixed(decimals).replace(/\.0+$/, '');
    const suffix = suffixes[exp];
    
    return value < 0 ? `-${formatted}${suffix}` : `${formatted}${suffix}`;
  }
};