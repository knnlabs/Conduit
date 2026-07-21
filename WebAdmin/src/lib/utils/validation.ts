/**
 * Re-export validation utilities from the local shared utilities.
 * All business logic now lives in the SDK for cross-project reuse.
 */
export {
  isNonEmptyString,
  isPositiveNumber,
  isValidEmail,
  isValidUrl,
  isValidEnumValue,
  createValidator
} from '@/lib/conduit-common';
export type { FieldValidationError as ValidationError, ValidationResult } from '@/lib/conduit-common';

/**
 * Standard error response for validation failures
 */
export function validationErrorResponse(errors: Array<{ field: string; message: string }>) {
  return {
    error: 'Validation failed',
    details: errors,
    timestamp: new Date().toISOString()
  };
}
