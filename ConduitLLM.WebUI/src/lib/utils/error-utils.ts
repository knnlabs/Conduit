/**
 * Type-safe error handling utilities
 * These utilities help extract properties from error objects while satisfying ESLint rules
 */

import { HttpError } from '@knn_labs/conduit-admin-client';

/**
 * Safely extracts statusCode from an HttpError
 */
export function getErrorStatusCode(error: unknown): number | undefined {
  if (error instanceof HttpError) {
    return error.statusCode as number;
  }
  return undefined;
}

/**
 * Safely extracts message from any error type
 */
export function getErrorMessage(error: unknown): string {
  if (error instanceof Error) {
    return error.message;
  }
  if (typeof error === 'string') {
    return error;
  }
  if (error && typeof error === 'object' && 'message' in error) {
    const msg = (error as { message: unknown }).message;
    return typeof msg === 'string' ? msg : String(msg);
  }
  return 'Unknown error';
}

/**
 * Safely extracts details from an HttpError
 */
export function getErrorDetails(error: unknown): Record<string, unknown> {
  if (error instanceof HttpError && error.details) {
    return typeof error.details === 'object' 
      ? error.details as Record<string, unknown> 
      : { details: error.details };
  }
  return {};
}

/**
 * Safely extracts context from an HttpError
 */
export function getErrorContext(error: unknown): Record<string, unknown> {
  if (error instanceof HttpError && error.context && typeof error.context === 'object') {
    return error.context as Record<string, unknown>;
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