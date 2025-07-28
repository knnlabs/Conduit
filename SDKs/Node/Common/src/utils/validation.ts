/**
 * Common validation utilities shared across Conduit SDKs
 */

import { ValidationError } from '../errors';

/**
 * Validates email format
 */
export function isValidEmail(email: string): boolean {
  const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
  return emailRegex.test(email);
}

/**
 * Validates URL format
 */
export function isValidUrl(url: string): boolean {
  try {
    new URL(url);
    return true;
  } catch {
    return false;
  }
}

/**
 * Validates API key format
 */
export function isValidApiKey(apiKey: string): boolean {
  // Standard format: sk-{32+ alphanumeric characters}
  const apiKeyRegex = /^sk-[a-zA-Z0-9]{32,}$/;
  return apiKeyRegex.test(apiKey);
}

/**
 * Validates ISO date string
 */
export function isValidIsoDate(date: string): boolean {
  const isoDateRegex = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{3})?Z?$/;
  if (!isoDateRegex.test(date)) {
    return false;
  }
  
  const parsed = new Date(date);
  return !isNaN(parsed.getTime());
}

/**
 * Validates UUID format
 */
export function isValidUuid(uuid: string): boolean {
  const uuidRegex = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
  return uuidRegex.test(uuid);
}

/**
 * Validates that a value is not null or undefined
 */
export function assertDefined<T>(value: T | null | undefined, name: string): T {
  if (value === null || value === undefined) {
    throw new ValidationError(`${name} is required`);
  }
  return value;
}

/**
 * Validates that a string is not empty
 */
export function assertNotEmpty(value: string | null | undefined, name: string): string {
  const defined = assertDefined(value, name);
  if (defined.trim().length === 0) {
    throw new ValidationError(`${name} cannot be empty`);
  }
  return defined;
}

/**
 * Validates that a number is within a range
 */
export function assertInRange(value: number, min: number, max: number, name: string): number {
  if (value < min || value > max) {
    throw new ValidationError(`${name} must be between ${min} and ${max}`);
  }
  return value;
}

/**
 * Validates that a value is one of allowed values
 */
export function assertOneOf<T>(value: T, allowed: readonly T[], name: string): T {
  if (!allowed.includes(value)) {
    throw new ValidationError(`${name} must be one of: ${allowed.join(', ')}`);
  }
  return value;
}

/**
 * Validates array length
 */
export function assertArrayLength<T>(
  array: T[],
  min: number,
  max: number,
  name: string
): T[] {
  if (array.length < min || array.length > max) {
    throw new ValidationError(`${name} must have between ${min} and ${max} items`);
  }
  return array;
}

/**
 * Validates that an object has required properties
 */
export function assertHasProperties<T extends Record<string, unknown>>(
  obj: T,
  required: (keyof T)[],
  name: string
): T {
  const missing = required.filter(prop => !(prop in obj));
  if (missing.length > 0) {
    throw new ValidationError(`${name} is missing required properties: ${missing.join(', ')}`);
  }
  return obj;
}

/**
 * Sanitizes a string by removing potentially dangerous characters
 */
export function sanitizeString(str: string, maxLength?: number): string {
  // Remove control characters and trim
  let sanitized = str.replace(/[\x00-\x1F\x7F]/g, '').trim();
  
  // Limit length if specified
  if (maxLength && sanitized.length > maxLength) {
    sanitized = sanitized.substring(0, maxLength);
  }
  
  return sanitized;
}

/**
 * Type guard to check if value is a non-empty string
 */
export function isNonEmptyString(value: unknown): value is string {
  return typeof value === 'string' && value.trim().length > 0;
}

/**
 * Type guard to check if value is a positive number
 */
export function isPositiveNumber(value: unknown): value is number {
  return typeof value === 'number' && value > 0 && isFinite(value);
}

/**
 * Type guard to check if value is a valid enum value
 */
export function isEnumValue<T extends Record<string, string | number>>(
  value: unknown,
  enumObject: T
): value is T[keyof T] {
  return Object.values(enumObject).includes(value as T[keyof T]);
}

/**
 * Validates JSON string
 */
export function isValidJson(str: string): boolean {
  try {
    JSON.parse(str);
    return true;
  } catch {
    return false;
  }
}

/**
 * Validates base64 string
 */
export function isValidBase64(str: string): boolean {
  const base64Regex = /^[A-Za-z0-9+/]*(={0,2})$/;
  if (!base64Regex.test(str)) {
    return false;
  }
  
  // Check if length is valid
  return str.length % 4 === 0;
}

/**
 * Creates a validation function that checks multiple conditions
 */
export function createValidator<T>(
  validators: Array<(value: T) => boolean | string>
): (value: T) => void {
  return (value: T) => {
    for (const validator of validators) {
      const result = validator(value);
      if (typeof result === 'string') {
        throw new ValidationError(result);
      }
      if (!result) {
        throw new ValidationError('Validation failed');
      }
    }
  };
}