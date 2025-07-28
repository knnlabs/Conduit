/**
 * Date and time utility functions shared across Conduit SDKs
 */

/**
 * Formats a date to ISO string with UTC timezone
 */
export function toIsoString(date: Date | string | number): string {
  const dateObj = date instanceof Date ? date : new Date(date);
  return dateObj.toISOString();
}

/**
 * Parses an ISO date string to Date object
 */
export function parseIsoDate(dateStr: string): Date {
  const date = new Date(dateStr);
  if (isNaN(date.getTime())) {
    throw new Error(`Invalid date string: ${dateStr}`);
  }
  return date;
}

/**
 * Gets current timestamp in ISO format
 */
export function getCurrentTimestamp(): string {
  return new Date().toISOString();
}

/**
 * Calculates time difference in milliseconds
 */
export function getTimeDifference(start: Date | string, end: Date | string = new Date()): number {
  const startTime = start instanceof Date ? start.getTime() : new Date(start).getTime();
  const endTime = end instanceof Date ? end.getTime() : new Date(end).getTime();
  return endTime - startTime;
}

/**
 * Formats duration in milliseconds to human-readable string
 */
export function formatDuration(ms: number): string {
  if (ms < 1000) {
    return `${ms}ms`;
  }
  
  const seconds = Math.floor(ms / 1000);
  const minutes = Math.floor(seconds / 60);
  const hours = Math.floor(minutes / 60);
  const days = Math.floor(hours / 24);
  
  if (days > 0) {
    return `${days}d ${hours % 24}h`;
  }
  if (hours > 0) {
    return `${hours}h ${minutes % 60}m`;
  }
  if (minutes > 0) {
    return `${minutes}m ${seconds % 60}s`;
  }
  
  return `${seconds}s`;
}

/**
 * Adds time to a date
 */
export function addTime(
  date: Date | string,
  amount: number,
  unit: 'seconds' | 'minutes' | 'hours' | 'days'
): Date {
  const dateObj = date instanceof Date ? new Date(date) : new Date(date);
  
  switch (unit) {
    case 'seconds':
      dateObj.setSeconds(dateObj.getSeconds() + amount);
      break;
    case 'minutes':
      dateObj.setMinutes(dateObj.getMinutes() + amount);
      break;
    case 'hours':
      dateObj.setHours(dateObj.getHours() + amount);
      break;
    case 'days':
      dateObj.setDate(dateObj.getDate() + amount);
      break;
  }
  
  return dateObj;
}

/**
 * Checks if a date is within a range
 */
export function isDateInRange(
  date: Date | string,
  start: Date | string,
  end: Date | string
): boolean {
  const dateTime = date instanceof Date ? date.getTime() : new Date(date).getTime();
  const startTime = start instanceof Date ? start.getTime() : new Date(start).getTime();
  const endTime = end instanceof Date ? end.getTime() : new Date(end).getTime();
  
  return dateTime >= startTime && dateTime <= endTime;
}

/**
 * Gets the start of a time period
 */
export function getStartOf(
  date: Date | string,
  period: 'day' | 'week' | 'month' | 'year'
): Date {
  const dateObj = date instanceof Date ? new Date(date) : new Date(date);
  
  switch (period) {
    case 'day':
      dateObj.setHours(0, 0, 0, 0);
      break;
    case 'week':
      const day = dateObj.getDay();
      const diff = dateObj.getDate() - day;
      dateObj.setDate(diff);
      dateObj.setHours(0, 0, 0, 0);
      break;
    case 'month':
      dateObj.setDate(1);
      dateObj.setHours(0, 0, 0, 0);
      break;
    case 'year':
      dateObj.setMonth(0, 1);
      dateObj.setHours(0, 0, 0, 0);
      break;
  }
  
  return dateObj;
}

/**
 * Gets the end of a time period
 */
export function getEndOf(
  date: Date | string,
  period: 'day' | 'week' | 'month' | 'year'
): Date {
  const dateObj = date instanceof Date ? new Date(date) : new Date(date);
  
  switch (period) {
    case 'day':
      dateObj.setHours(23, 59, 59, 999);
      break;
    case 'week':
      const day = dateObj.getDay();
      const diff = dateObj.getDate() - day + 6;
      dateObj.setDate(diff);
      dateObj.setHours(23, 59, 59, 999);
      break;
    case 'month':
      dateObj.setMonth(dateObj.getMonth() + 1, 0);
      dateObj.setHours(23, 59, 59, 999);
      break;
    case 'year':
      dateObj.setMonth(11, 31);
      dateObj.setHours(23, 59, 59, 999);
      break;
  }
  
  return dateObj;
}

/**
 * Formats a date for API requests (YYYY-MM-DD)
 */
export function formatApiDate(date: Date | string): string {
  const dateObj = date instanceof Date ? date : new Date(date);
  const year = dateObj.getFullYear();
  const month = String(dateObj.getMonth() + 1).padStart(2, '0');
  const day = String(dateObj.getDate()).padStart(2, '0');
  
  return `${year}-${month}-${day}`;
}

/**
 * Parses a Unix timestamp to Date
 */
export function fromUnixTimestamp(timestamp: number): Date {
  // Check if timestamp is in seconds or milliseconds
  const isSeconds = timestamp < 10000000000;
  return new Date(isSeconds ? timestamp * 1000 : timestamp);
}

/**
 * Converts Date to Unix timestamp (seconds)
 */
export function toUnixTimestamp(date: Date | string): number {
  const dateObj = date instanceof Date ? date : new Date(date);
  return Math.floor(dateObj.getTime() / 1000);
}