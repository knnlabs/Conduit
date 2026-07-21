/**
 * Schema-based request body validator
 */

import type { ValidationError, ValidationResult } from "./types";

/**
 * Create a type-safe validator from a schema of per-field validation functions.
 *
 * @example
 * ```typescript
 * const validate = createValidator<{ name: string; age: number }>({
 *   name: isNonEmptyString,
 *   age: isPositiveNumber,
 * });
 * const result = validate(requestBody);
 * if (result.isValid) { ... }
 * ```
 */
export function createValidator<T>(
  schema: Record<keyof T, (value: unknown) => boolean>,
): (body: unknown) => ValidationResult<T> {
  return (body: unknown): ValidationResult<T> => {
    if (!body || typeof body !== "object") {
      return {
        isValid: false,
        errors: [{ field: "body", message: "Request body must be an object" }],
      };
    }

    const errors: ValidationError[] = [];
    const validatedData = {} as T;
    const bodyObj = body as Record<string, unknown>;

    for (const [field, validator] of Object.entries(schema)) {
      const value = bodyObj[field];
      const validatorFn = validator as (value: unknown) => boolean;
      if (!validatorFn(value)) {
        errors.push({
          field,
          message: `Invalid value for field: ${field}`,
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
