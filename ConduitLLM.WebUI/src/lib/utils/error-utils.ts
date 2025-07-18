/**
 * Type-safe error handling utilities
 * These utilities help extract properties from error objects while satisfying ESLint rules
 */

import { HttpError } from '@knn_labs/conduit-admin-client';

/**
 * Safely extracts statusCode from an HttpError
 */
export function getErrorStatusCode(error: unknown): number | undefined {
  if (error instanceof HttpError && error.response) {
    return error.response.status;
  }
  return undefined;
}

/**
 * Safely extracts message from any error type
 */
export function getErrorMessage(error: unknown): string {
  // Check HttpError response data first
  if (error instanceof HttpError && error.response?.data) {
    const data = error.response.data;
    if (isObject(data)) {
      // Try common error message fields
      if (typeof data.message === 'string') return data.message;
      if (typeof data.error === 'string') return data.error;
      if (typeof data.details === 'string') return data.details;
    }
  }
  
  // Standard Error handling
  if (error instanceof Error) {
    return error.message;
  }
  
  // String errors
  if (typeof error === 'string') {
    return error;
  }
  
  // Objects with message property
  if (isObject(error) && typeof error.message === 'string') {
    return error.message;
  }
  
  return 'Unknown error';
}

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
  if (error instanceof HttpError && error.response?.data) {
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
  if (error instanceof HttpError && error.response?.data) {
    const data = error.response.data;
    if (isObject(data) && isObject(data.context)) {
      return data.context;
    }
  }
  return {};
}

/**
 * Checks if an error is an HttpError
 */
export function isHttpError(error: unknown): error is HttpError {
  return error instanceof HttpError;
}

/**
 * Gets a combined error details object with context
 */
export function getCombinedErrorDetails(error: unknown): Record<string, unknown> {
  const details = getErrorDetails(error);
  const context = getErrorContext(error);
  return { ...details, ...context };
}