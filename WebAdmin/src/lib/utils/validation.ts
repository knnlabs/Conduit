/**
 * Re-export validation utilities from @knn_labs/conduit-common.
 * All business logic now lives in the SDK for cross-project reuse.
 */
export {
  isNonEmptyString,
  isPositiveNumber,
  isValidEmail,
  isValidUrl,
  isValidEnumValue,
  createValidator
} from '@knn_labs/conduit-common';
export type { FieldValidationError as ValidationError, ValidationResult } from '@knn_labs/conduit-common';

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
