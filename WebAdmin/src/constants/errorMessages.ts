/**
 * Re-export error message utilities from the local shared utilities.
 * All business logic now lives in the SDK for cross-project reuse.
 */
export {
  ERROR_MESSAGES,
  getDefaultErrorConfig,
  getErrorConfig,
  getErrorSeverity,
  extractRetryAfter
} from '@/lib/conduit-common';
export type {
  ErrorMessageConfig,
  OpenAIError,
  OpenAIErrorResponse
} from '@/lib/conduit-common';

/**
 * Determine the appropriate icon name for an error.
 * This remains in WebAdmin as it's UI-specific (icon names are presentation).
 */
export function getErrorIconName(statusCode: number): string {
  switch (statusCode) {
    case 401:
    case 403:
      return 'LockClosedIcon';
    case 402:
      return 'CreditCardIcon';
    case 404:
      return 'MagnifyingGlassIcon';
    case 408:
    case 504:
      return 'ClockIcon';
    case 413:
      return 'DocumentTextIcon';
    case 429:
      return 'ExclamationTriangleIcon';
    case 500:
    case 502:
    case 503:
      return 'ServerIcon';
    default:
      return 'ExclamationCircleIcon';
  }
}
