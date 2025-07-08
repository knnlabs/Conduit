export class ConduitError extends Error {
  public statusCode: number;
  public code: string;
  public context?: Record<string, unknown>;
  public type?: string;
  public param?: string;

  constructor(
    message: string,
    statusCode: number = 500,
    code: string = 'INTERNAL_ERROR',
    context?: Record<string, unknown>
  ) {
    super(message);
    this.name = this.constructor.name;
    this.statusCode = statusCode;
    this.code = code;
    this.context = context;
    
    // Preserve additional properties from context
    if (context) {
      this.type = context.type as string | undefined;
      this.param = context.param as string | undefined;
    }
    
    // Ensure proper prototype chain for instanceof checks
    Object.setPrototypeOf(this, new.target.prototype);
    
    // Capture stack trace for better debugging
    if (Error.captureStackTrace) {
      Error.captureStackTrace(this, this.constructor);
    }
  }

  toJSON() {
    return {
      name: this.name,
      message: this.message,
      statusCode: this.statusCode,
      code: this.code,
      type: this.type,
      param: this.param,
      context: this.context,
      timestamp: new Date().toISOString(),
    };
  }
  
  // Helper method for Next.js serialization
  toSerializable() {
    return {
      isConduitError: true,
      ...this.toJSON(),
    };
  }
  
  // Static method to reconstruct from serialized error
  static fromSerializable(data: any): ConduitError {
    if (!data || !data.isConduitError) {
      throw new Error('Invalid serialized ConduitError');
    }
    
    const error = new ConduitError(
      data.message,
      data.statusCode,
      data.code,
      data.context
    );
    
    // Restore additional properties
    if (data.type !== undefined) error.type = data.type;
    if (data.param !== undefined) error.param = data.param;
    
    return error;
  }
}

export class AuthError extends ConduitError {
  constructor(message = 'Authentication failed', context?: Record<string, unknown>) {
    super(message, 401, 'AUTH_ERROR', context);
  }
}

// Alias for backward compatibility
export class AuthenticationError extends AuthError {}

export class RateLimitError extends ConduitError {
  public readonly retryAfter?: number;

  constructor(message = 'Rate limit exceeded', retryAfter?: number, context?: Record<string, unknown>) {
    super(message, 429, 'RATE_LIMIT_ERROR', { ...context, retryAfter });
    this.retryAfter = retryAfter;
  }
}

export class ValidationError extends ConduitError {
  public field?: string;
  
  constructor(message: string, context?: Record<string, unknown>) {
    super(message, 400, 'VALIDATION_ERROR', context);
    this.field = context?.field as string | undefined;
  }
}

export class NotFoundError extends ConduitError {
  constructor(message = 'Resource not found', context?: Record<string, unknown>) {
    super(message, 404, 'NOT_FOUND', context);
  }
}

export class NetworkError extends ConduitError {
  constructor(message = 'Network request failed', context?: Record<string, unknown>) {
    super(message, 0, 'NETWORK_ERROR', context);
  }
}

export class StreamError extends ConduitError {
  constructor(message = 'Stream processing failed', context?: Record<string, unknown>) {
    super(message, 500, 'STREAM_ERROR', context);
  }
}

export class TimeoutError extends ConduitError {
  constructor(message = 'Request timeout', context?: Record<string, unknown>) {
    super(message, 408, 'TIMEOUT_ERROR', context);
  }
}

export class ServerError extends ConduitError {
  constructor(message = 'Internal server error', context?: Record<string, unknown>) {
    super(message, 500, 'SERVER_ERROR', context);
  }
}

// Type guards
export function isConduitError(error: unknown): error is ConduitError {
  return error instanceof ConduitError;
}

export function isAuthError(error: unknown): error is AuthError {
  return error instanceof AuthError || error instanceof AuthenticationError;
}

export function isValidationError(error: unknown): error is ValidationError {
  return error instanceof ValidationError;
}

export function isNotFoundError(error: unknown): error is NotFoundError {
  return error instanceof NotFoundError;
}

export function isRateLimitError(error: unknown): error is RateLimitError {
  return error instanceof RateLimitError;
}

export function isNetworkError(error: unknown): error is NetworkError {
  return error instanceof NetworkError;
}

export function isStreamError(error: unknown): error is StreamError {
  return error instanceof StreamError;
}

// Helper to check if an error is serialized ConduitError
export function isSerializedConduitError(data: unknown): data is ReturnType<ConduitError['toSerializable']> {
  return (
    typeof data === 'object' &&
    data !== null &&
    'isConduitError' in data &&
    (data as any).isConduitError === true
  );
}

// Next.js-specific utilities for error serialization across server/client boundaries
export function serializeError(error: unknown): Record<string, any> {
  if (isConduitError(error)) {
    return error.toSerializable();
  }
  
  if (error instanceof Error) {
    return {
      isError: true,
      name: error.name,
      message: error.message,
      stack: process.env.NODE_ENV === 'development' ? error.stack : undefined,
    };
  }
  
  return {
    isError: true,
    message: String(error),
  };
}

export function deserializeError(data: unknown): Error {
  if (isSerializedConduitError(data)) {
    return ConduitError.fromSerializable(data);
  }
  
  if (typeof data === 'object' && data !== null && 'isError' in data) {
    const errorData = data as any;
    const error = new Error(errorData.message);
    if (errorData.name) error.name = errorData.name;
    if (errorData.stack) error.stack = errorData.stack;
    return error;
  }
  
  return new Error('Unknown error');
}

// Helper for Next.js error boundaries
export function getErrorMessage(error: unknown): string {
  if (isConduitError(error)) {
    return error.message;
  }
  
  if (error instanceof Error) {
    return error.message;
  }
  
  return 'An unexpected error occurred';
}

// Helper for Next.js error pages
export function getErrorStatusCode(error: unknown): number {
  if (isConduitError(error)) {
    return error.statusCode;
  }
  
  return 500;
}

// Legacy compatibility - create an error from ErrorResponse format
import type { ErrorResponse } from '../models/common';

export function createErrorFromResponse(response: ErrorResponse, statusCode?: number): ConduitError {
  const context: Record<string, unknown> = {
    type: response.error.type,
    param: response.error.param,
  };
  
  return new ConduitError(
    response.error.message,
    statusCode || 500,
    response.error.code || 'API_ERROR',
    context
  );
}