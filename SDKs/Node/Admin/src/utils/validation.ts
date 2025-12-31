/**
 * Simple validation utilities to replace Zod dependency
 * Provides runtime validation for common use cases
 */

import { ValidationError } from '@knn_labs/conduit-common';

/**
 * Validates that required fields are present and not null/undefined
 *
 * @param data - The object to validate
 * @param fields - Array of field names that are required
 * @throws {ValidationError} If any required field is missing
 *
 * @example
 * ```typescript
 * validateRequired(request, ['name', 'email']);
 * ```
 */
export function validateRequired<T extends object>(
  data: T,
  fields: (keyof T)[]
): void {
  for (const field of fields) {
    if (data[field] === undefined || data[field] === null) {
      throw new ValidationError(`Field '${String(field)}' is required`);
    }
  }
}

/**
 * Validates a date range to ensure start is before end
 *
 * @param range - Object with startDate and endDate
 * @throws {ValidationError} If dates are invalid or start is after end
 *
 * @example
 * ```typescript
 * validateDateRange({ startDate: '2024-01-01', endDate: '2024-12-31' });
 * ```
 */
export function validateDateRange(range: { startDate?: string; endDate?: string }): void {
  if (!range.startDate || !range.endDate) {
    throw new ValidationError('Start date and end date are required');
  }

  // Validate date format (ISO 8601)
  const startDate = new Date(range.startDate);
  const endDate = new Date(range.endDate);

  if (isNaN(startDate.getTime())) {
    throw new ValidationError('Invalid start date format');
  }

  if (isNaN(endDate.getTime())) {
    throw new ValidationError('Invalid end date format');
  }

  if (startDate > endDate) {
    throw new ValidationError('Start date must be before end date');
  }
}

/**
 * Validates an email address format
 *
 * @param email - The email address to validate
 * @throws {ValidationError} If email format is invalid
 *
 * @example
 * ```typescript
 * validateEmail('user@example.com');
 * ```
 */
export function validateEmail(email: string): void {
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
    throw new ValidationError('Invalid email format');
  }
}

/**
 * Validates that a string is not empty after trimming
 *
 * @param value - The string to validate
 * @param fieldName - Name of the field for error messages
 * @throws {ValidationError} If string is empty
 *
 * @example
 * ```typescript
 * validateNonEmptyString(name, 'name');
 * ```
 */
export function validateNonEmptyString(value: string, fieldName: string): void {
  if (!value || value.trim().length === 0) {
    throw new ValidationError(`${fieldName} cannot be empty`);
  }
}

/**
 * Validates that a number is within a specified range
 *
 * @param value - The number to validate
 * @param min - Minimum allowed value (inclusive)
 * @param max - Maximum allowed value (inclusive)
 * @param fieldName - Name of the field for error messages
 * @throws {ValidationError} If number is out of range
 *
 * @example
 * ```typescript
 * validateNumberRange(age, 0, 150, 'age');
 * ```
 */
export function validateNumberRange(
  value: number,
  min: number,
  max: number,
  fieldName: string
): void {
  if (value < min || value > max) {
    throw new ValidationError(`${fieldName} must be between ${min} and ${max}`);
  }
}

/**
 * Validates that a string length is within a specified range
 *
 * @param value - The string to validate
 * @param minLength - Minimum allowed length
 * @param maxLength - Maximum allowed length
 * @param fieldName - Name of the field for error messages
 * @throws {ValidationError} If string length is out of range
 *
 * @example
 * ```typescript
 * validateStringLength(description, 1, 500, 'description');
 * ```
 */
export function validateStringLength(
  value: string,
  minLength: number,
  maxLength: number,
  fieldName: string
): void {
  if (value.length < minLength) {
    throw new ValidationError(`${fieldName} must be at least ${minLength} characters`);
  }
  if (value.length > maxLength) {
    throw new ValidationError(`${fieldName} cannot exceed ${maxLength} characters`);
  }
}

/**
 * Validates that a value is one of the allowed options
 *
 * @param value - The value to validate
 * @param allowedValues - Array of allowed values
 * @param fieldName - Name of the field for error messages
 * @throws {ValidationError} If value is not in allowed list
 *
 * @example
 * ```typescript
 * validateEnum(status, ['active', 'inactive', 'pending'], 'status');
 * ```
 */
export function validateEnum<T>(
  value: T,
  allowedValues: readonly T[],
  fieldName: string
): void {
  if (!allowedValues.includes(value)) {
    throw new ValidationError(
      `${fieldName} must be one of: ${allowedValues.join(', ')}`
    );
  }
}

/**
 * Validates that a value is a valid URL
 *
 * @param value - The URL string to validate
 * @param fieldName - Name of the field for error messages
 * @throws {ValidationError} If URL is invalid
 *
 * @example
 * ```typescript
 * validateUrl(apiEndpoint, 'apiEndpoint');
 * ```
 */
export function validateUrl(value: string, fieldName: string): void {
  try {
    new URL(value);
  } catch {
    throw new ValidationError(`${fieldName} must be a valid URL`);
  }
}

/**
 * Validates that an array is not empty
 *
 * @param value - The array to validate
 * @param fieldName - Name of the field for error messages
 * @throws {ValidationError} If array is empty
 *
 * @example
 * ```typescript
 * validateNonEmptyArray(items, 'items');
 * ```
 */
export function validateNonEmptyArray<T>(value: T[], fieldName: string): void {
  if (!Array.isArray(value) || value.length === 0) {
    throw new ValidationError(`${fieldName} must contain at least one item`);
  }
}
