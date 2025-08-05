/**
 * Common error types for Conduit SDK clients
 * 
 * This module provides a unified error hierarchy for both Admin and Core SDKs,
 * consolidating previously duplicated error classes.
 */

export class ConduitError extends Error {
  public statusCode: number;
  public code: string;
  public context?: Record<string, unknown>;
  
  // Admin SDK specific fields
  public details?: unknown;
  public endpoint?: string;
  public method?: string;
  
  // Core SDK specific fields
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
    
    // Preserve additional context from the constructor pattern
    if (context) {
      // Admin SDK fields
      this.details = context.details;
      this.endpoint = context.endpoint as string | undefined;
      this.method = context.method as string | undefined;
      
      // Core SDK fields
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
      context: this.context,
      details: this.details,
      endpoint: this.endpoint,
      method: this.method,
      type: this.type,
      param: this.param,
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
  static fromSerializable(data: unknown): ConduitError {
    if (!data || typeof data !== 'object' || !('isConduitError' in data) || !(data as { isConduitError: unknown }).isConduitError) {
      throw new Error('Invalid serialized ConduitError');
    }
    
    const errorData = data as unknown as {
      message: string;
      statusCode: number;
      code: string;
      context?: Record<string, unknown>;
      details?: unknown;
      endpoint?: string;
      method?: string;
      type?: string;
      param?: string;
    };
    
    const error = new ConduitError(
      errorData.message,
      errorData.statusCode,
      errorData.code,
      errorData.context
    );
    
    // Restore additional properties
    if (errorData.details !== undefined) error.details = errorData.details;
    if (errorData.endpoint !== undefined) error.endpoint = errorData.endpoint;
    if (errorData.method !== undefined) error.method = errorData.method;
    if (errorData.type !== undefined) error.type = errorData.type;
    if (errorData.param !== undefined) error.param = errorData.param;
    
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

export class AuthorizationError extends ConduitError {
  constructor(message = 'Access forbidden', context?: Record<string, unknown>) {
    super(message, 403, 'AUTHORIZATION_ERROR', context);
  }
}

export class ValidationError extends ConduitError {
  public field?: string;
  
  constructor(message = 'Validation failed', context?: Record<string, unknown>) {
    super(message, 400, 'VALIDATION_ERROR', context);
    this.field = context?.field as string | undefined;
  }
}

export class NotFoundError extends ConduitError {
  constructor(message = 'Resource not found', context?: Record<string, unknown>) {
    super(message, 404, 'NOT_FOUND', context);
  }
}

export class ConflictError extends ConduitError {
  constructor(message = 'Resource conflict', context?: Record<string, unknown>) {
    super(message, 409, 'CONFLICT_ERROR', context);
  }
}

export class InsufficientBalanceError extends ConduitError {
  public balance?: number;
  public requiredAmount?: number;

  constructor(message = 'Insufficient balance to complete request', context?: Record<string, unknown>) {
    super(message, 402, 'INSUFFICIENT_BALANCE', context);
    this.balance = context?.balance as number | undefined;
    this.requiredAmount = context?.requiredAmount as number | undefined;
  }
}

export class RateLimitError extends ConduitError {
  public retryAfter?: number;

  constructor(message = 'Rate limit exceeded', retryAfter?: number, context?: Record<string, unknown>) {
    super(message, 429, 'RATE_LIMIT_ERROR', { ...context, retryAfter });
    this.retryAfter = retryAfter;
  }
}

export class ServerError extends ConduitError {
  constructor(message = 'Internal server error', context?: Record<string, unknown>) {
    super(message, 500, 'SERVER_ERROR', context);
  }
}

export class NetworkError extends ConduitError {
  constructor(message = 'Network error', context?: Record<string, unknown>) {
    super(message, 0, 'NETWORK_ERROR', context);
  }
}

export class TimeoutError extends ConduitError {
  constructor(message = 'Request timeout', context?: Record<string, unknown>) {
    super(message, 408, 'TIMEOUT_ERROR', context);
  }
}

export class NotImplementedError extends ConduitError {
  constructor(message: string, context?: Record<string, unknown>) {
    super(message, 501, 'NOT_IMPLEMENTED', context);
  }
}

export class StreamError extends ConduitError {
  constructor(message = 'Stream processing failed', context?: Record<string, unknown>) {
    super(message, 500, 'STREAM_ERROR', context);
  }
}

// Type guards
export function isConduitError(error: unknown): error is ConduitError {
  return error instanceof ConduitError;
}

export function isAuthError(error: unknown): error is AuthError {
  return error instanceof AuthError || error instanceof AuthenticationError;
}

export function isAuthorizationError(error: unknown): error is AuthorizationError {
  return error instanceof AuthorizationError;
}

export function isValidationError(error: unknown): error is ValidationError {
  return error instanceof ValidationError;
}

export function isNotFoundError(error: unknown): error is NotFoundError {
  return error instanceof NotFoundError;
}

export function isConflictError(error: unknown): error is ConflictError {
  return error instanceof ConflictError;
}

export function isInsufficientBalanceError(error: unknown): error is InsufficientBalanceError {
  return error instanceof InsufficientBalanceError;
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

export function isTimeoutError(error: unknown): error is TimeoutError {
  return error instanceof TimeoutError;
}

// Helper to check if an error is serialized ConduitError
export function isSerializedConduitError(data: unknown): data is ReturnType<ConduitError['toSerializable']> {
  return (
    typeof data === 'object' &&
    data !== null &&
    'isConduitError' in data &&
    (data as { isConduitError: unknown }).isConduitError === true
  );
}

// Type guard for HTTP errors
export function isHttpError(error: unknown): error is {
  response: { status: number; data: unknown; headers: Record<string, string> };
  message: string;
  request?: unknown;
  code?: string;
} {
  return (
    typeof error === 'object' &&
    error !== null &&
    'response' in error &&
    typeof (error as { response: unknown }).response === 'object'
  );
}

// Type guard for network errors
export function isHttpNetworkError(error: unknown): error is {
  request: unknown;
  message: string;
  code?: string;
} {
  return (
    typeof error === 'object' &&
    error !== null &&
    'request' in error &&
    !('response' in error)
  );
}

// Type guard for generic errors
export function isErrorLike(error: unknown): error is {
  message: string;
} {
  return (
    typeof error === 'object' &&
    error !== null &&
    'message' in error &&
    typeof (error as { message: unknown }).message === 'string'
  );
}

// Next.js-specific utilities for error serialization across server/client boundaries
export function serializeError(error: unknown): Record<string, unknown> {
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
    const errorData = data as {
      message?: string;
      name?: string;
      stack?: string;
      isError: boolean;
    };
    const error = new Error(errorData.message || 'Unknown error');
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

/**
 * Handle API errors and convert them to appropriate ConduitError types
 * This function is primarily used by the Admin SDK
 */
export function handleApiError(error: unknown, endpoint?: string, method?: string): never {
  const context: Record<string, unknown> = {
    endpoint,
    method,
  };

  if (isHttpError(error)) {
    const { status, data } = error.response;
    const errorData = data as { error?: string; message?: string; details?: unknown } | null;
    const baseMessage = errorData?.error || errorData?.message || error.message;
    
    // Enhanced error messages with endpoint information
    const endpointInfo = endpoint && method ? ` (${method.toUpperCase()} ${endpoint})` : '';
    const enhancedMessage = `${baseMessage}${endpointInfo}`;
    
    // Add details to context
    context.details = errorData?.details || data;

    switch (status) {
      case 400:
        throw new ValidationError(enhancedMessage, context);
      case 401:
        throw new AuthError(enhancedMessage, context);
      case 402:
        throw new InsufficientBalanceError(enhancedMessage, context);
      case 403:
        throw new AuthorizationError(enhancedMessage, context);
      case 404:
        throw new NotFoundError(enhancedMessage, context);
      case 409:
        throw new ConflictError(enhancedMessage, context);
      case 429: {
        const retryAfterHeader = error.response.headers['retry-after'];
        const retryAfter = typeof retryAfterHeader === 'string' ? parseInt(retryAfterHeader, 10) : undefined;
        throw new RateLimitError(enhancedMessage, retryAfter, context);
      }
      case 500:
      case 502:
      case 503:
      case 504:
        throw new ServerError(enhancedMessage, context);
      default:
        throw new ConduitError(enhancedMessage, status, `HTTP_${status}`, context);
    }
  } else if (isHttpNetworkError(error)) {
    const endpointInfo = endpoint && method ? ` (${method.toUpperCase()} ${endpoint})` : '';
    context.code = error.code;
    
    if (error.code === 'ECONNABORTED') {
      throw new TimeoutError(`Request timeout${endpointInfo}`, context);
    }
    throw new NetworkError(`Network error: No response received${endpointInfo}`, context);
  } else if (isErrorLike(error)) {
    context.originalError = error;
    throw new ConduitError(error.message, 500, 'UNKNOWN_ERROR', context);
  } else {
    context.originalError = error;
    throw new ConduitError('Unknown error', 500, 'UNKNOWN_ERROR', context);
  }
}

/**
 * Create an error from an ErrorResponse format
 * This function is primarily used by the Core SDK for legacy compatibility
 */
export interface ErrorResponseFormat {
  error: {
    message: string;
    type?: string;
    code?: string;
    param?: string;
  };
}

export function createErrorFromResponse(response: ErrorResponseFormat, statusCode?: number): ConduitError {
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