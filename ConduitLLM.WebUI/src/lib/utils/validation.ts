/**
 * Request validation utilities for API routes
 */

export interface ValidationError {
  field: string;
  message: string;
}

export interface ValidationResult<T> {
  isValid: boolean;
  data?: T;
  errors?: ValidationError[];
}

/**
 * Validates that a value is a non-empty string
 */
export function isNonEmptyString(value: unknown): value is string {
  return typeof value === 'string' && value.trim().length > 0;
}

/**
 * Validates that a value is a positive number
 */
export function isPositiveNumber(value: unknown): value is number {
  return typeof value === 'number' && !isNaN(value) && value > 0;
}

/**
 * Validates that a value is a valid email
 */
export function isValidEmail(value: unknown): value is string {
  if (!isNonEmptyString(value)) return false;
  const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
  return emailRegex.test(value);
}

/**
 * Validates that a value is a valid URL
 */
export function isValidUrl(value: unknown): value is string {
  if (!isNonEmptyString(value)) return false;
  try {
    new URL(value);
    return true;
  } catch {
    return false;
  }
}

/**
 * Validates that a value is a valid enum value
 */
export function isValidEnumValue<T extends string>(
  value: unknown,
  enumValues: readonly T[]
): value is T {
  return typeof value === 'string' && enumValues.includes(value as T);
}

/**
 * Creates a type-safe request body validator
 */
export function createValidator<T>(
  schema: Record<keyof T, (value: unknown) => boolean>
): (body: unknown) => ValidationResult<T> {
  return (body: unknown): ValidationResult<T> => {
    if (!body || typeof body !== 'object') {
      return {
        isValid: false,
        errors: [{ field: 'body', message: 'Request body must be an object' }]
      };
    }

    const errors: ValidationError[] = [];
    const validatedData = {} as T;
    const bodyObj = body as Record<string, unknown>;

    for (const [field, validator] of Object.entries(schema) as Array<[string, (value: unknown) => boolean]>) {
      const value = bodyObj[field];
      if (!validator(value)) {
        errors.push({
          field,
          message: `Invalid value for field: ${field}`
        });
      } else {
        (validatedData as Record<string, unknown>)[field] = value;
      }
    }

    if (errors.length > 0) {
      return { isValid: false, errors };
    }

    return { isValid: true, data: validatedData };
  };
}

/**
 * Standard error response for validation failures
 */
export function validationErrorResponse(errors: ValidationError[]) {
  return {
    error: 'Validation failed',
    details: errors,
    timestamp: new Date().toISOString()
  };
}