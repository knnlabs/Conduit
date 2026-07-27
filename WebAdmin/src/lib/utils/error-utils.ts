/**
 * Type-safe error handling utilities
 * These utilities help extract properties from error objects while satisfying ESLint rules
 */

import {
  getErrorMessage,
  getErrorStatusCode,
  isHttpError,
} from '@/lib/conduit-common';

export {
  getErrorMessage,
  getErrorStatusCode,
  isHttpError,
};

/**
 * Type guard to check if a value is a valid object
 */
function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

/**
 * Safely extracts details from an HttpError
 */
export function getErrorDetails(error: unknown): Record<string, unknown> {
  if (isHttpError(error) && error.response.data) {
    const data = error.response.data;
    if (isObject(data)) {
      return data;
    }
    return { details: data };
  }
  return {};
}

/**
 * Safely extracts context from an HttpError
 * Since HttpError doesn't have a context property, we extract from response data
 */
export function getErrorContext(error: unknown): Record<string, unknown> {
  if (isHttpError(error) && error.response.data) {
    const data = error.response.data;
    if (isObject(data) && isObject(data.context)) {
      return data.context;
    }
  }
  return {};
}

/**
 * Gets a combined error details object with context
 */
export function getCombinedErrorDetails(error: unknown): Record<string, unknown> {
  const details = getErrorDetails(error);
  const context = getErrorContext(error);
  return { ...details, ...context };
}
