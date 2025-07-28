/**
 * Formatting utilities shared across Conduit SDKs
 */

/**
 * Formats a number as currency
 */
export function formatCurrency(
  amount: number,
  currency: string = 'USD',
  locale: string = 'en-US'
): string {
  return new Intl.NumberFormat(locale, {
    style: 'currency',
    currency,
  }).format(amount);
}

/**
 * Formats a number with commas
 */
export function formatNumber(
  value: number,
  decimals: number = 0,
  locale: string = 'en-US'
): string {
  return new Intl.NumberFormat(locale, {
    minimumFractionDigits: decimals,
    maximumFractionDigits: decimals,
  }).format(value);
}

/**
 * Formats bytes to human-readable size
 */
export function formatBytes(bytes: number, decimals: number = 2): string {
  if (bytes === 0) return '0 Bytes';
  
  const k = 1024;
  const dm = decimals < 0 ? 0 : decimals;
  const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB', 'PB'];
  
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  
  return `${parseFloat((bytes / Math.pow(k, i)).toFixed(dm))} ${sizes[i]}`;
}

/**
 * Formats a percentage
 */
export function formatPercentage(value: number, decimals: number = 2): string {
  return `${(value * 100).toFixed(decimals)}%`;
}

/**
 * Truncates a string with ellipsis
 */
export function truncateString(str: string, maxLength: number, suffix: string = '...'): string {
  if (str.length <= maxLength) return str;
  return str.slice(0, maxLength - suffix.length) + suffix;
}

/**
 * Capitalizes first letter of a string
 */
export function capitalize(str: string): string {
  return str.charAt(0).toUpperCase() + str.slice(1);
}

/**
 * Converts string to title case
 */
export function toTitleCase(str: string): string {
  return str.replace(/\w\S*/g, (txt) => {
    return txt.charAt(0).toUpperCase() + txt.substr(1).toLowerCase();
  });
}

/**
 * Converts string to kebab-case
 */
export function toKebabCase(str: string): string {
  return str
    .replace(/([a-z])([A-Z])/g, '$1-$2')
    .replace(/[\s_]+/g, '-')
    .toLowerCase();
}

/**
 * Converts string to snake_case
 */
export function toSnakeCase(str: string): string {
  return str
    .replace(/([a-z])([A-Z])/g, '$1_$2')
    .replace(/[\s-]+/g, '_')
    .toLowerCase();
}

/**
 * Converts string to camelCase
 */
export function toCamelCase(str: string): string {
  return str
    .replace(/(?:^\w|[A-Z]|\b\w)/g, (word, index) => {
      return index === 0 ? word.toLowerCase() : word.toUpperCase();
    })
    .replace(/[\s-_]+/g, '');
}

/**
 * Pads a string or number with zeros
 */
export function padZero(value: string | number, length: number): string {
  return String(value).padStart(length, '0');
}

/**
 * Formats a duration in seconds to HH:MM:SS
 */
export function formatDurationHMS(seconds: number): string {
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  const secs = Math.floor(seconds % 60);
  
  const parts = [];
  if (hours > 0) parts.push(padZero(hours, 2));
  parts.push(padZero(minutes, 2));
  parts.push(padZero(secs, 2));
  
  return parts.join(':');
}

/**
 * Pluralizes a word based on count
 */
export function pluralize(
  count: number,
  singular: string,
  plural?: string
): string {
  if (count === 1) return singular;
  return plural || `${singular}s`;
}

/**
 * Formats a list of items with proper grammar
 */
export function formatList(
  items: string[],
  conjunction: string = 'and'
): string {
  if (items.length === 0) return '';
  if (items.length === 1) return items[0];
  if (items.length === 2) return `${items[0]} ${conjunction} ${items[1]}`;
  
  const lastItem = items[items.length - 1];
  const otherItems = items.slice(0, -1);
  return `${otherItems.join(', ')}, ${conjunction} ${lastItem}`;
}

/**
 * Masks sensitive data
 */
export function maskSensitive(
  value: string,
  showFirst: number = 4,
  showLast: number = 4,
  maskChar: string = '*'
): string {
  if (value.length <= showFirst + showLast) {
    return value;
  }
  
  const first = value.slice(0, showFirst);
  const last = value.slice(-showLast);
  const maskLength = Math.max(value.length - showFirst - showLast, 4);
  const mask = maskChar.repeat(maskLength);
  
  return `${first}${mask}${last}`;
}

/**
 * Formats a file path to be more readable
 */
export function formatFilePath(path: string, maxLength: number = 50): string {
  if (path.length <= maxLength) return path;
  
  const parts = path.split('/');
  if (parts.length <= 2) return truncateString(path, maxLength);
  
  const fileName = parts[parts.length - 1];
  const firstDir = parts[0] || parts[1]; // Handle absolute paths
  
  if (fileName.length + firstDir.length + 6 > maxLength) {
    return truncateString(path, maxLength);
  }
  
  return `${firstDir}/.../${fileName}`;
}