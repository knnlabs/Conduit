export class ConduitError extends Error {
  public statusCode?: number;
  public details?: unknown;
  public endpoint?: string;
  public method?: string;
  public code?: string;

  constructor(
    message: string,
    statusCode?: number,
    details?: unknown,
    endpoint?: string,
    method?: string,
    code?: string
  ) {
    super(message);
    this.name = this.constructor.name;
    this.statusCode = statusCode;
    this.details = details;
    this.endpoint = endpoint;
    this.method = method;
    this.code = code;
    Object.setPrototypeOf(this, new.target.prototype);
  }

  toJSON() {
    return {
      name: this.name,
      message: this.message,
      statusCode: this.statusCode,
      details: this.details,
      endpoint: this.endpoint,
      method: this.method,
      code: this.code,
    };
  }
}

export class AuthenticationError extends ConduitError {
  constructor(message = 'Authentication failed', details?: unknown, endpoint?: string, method?: string) {
    super(message, 401, details, endpoint, method);
  }
}

export class AuthorizationError extends ConduitError {
  constructor(message = 'Access forbidden', details?: unknown, endpoint?: string, method?: string) {
    super(message, 403, details, endpoint, method);
  }
}

export class ValidationError extends ConduitError {
  constructor(message = 'Validation failed', details?: unknown, endpoint?: string, method?: string) {
    super(message, 400, details, endpoint, method);
  }
}

export class NotFoundError extends ConduitError {
  constructor(message = 'Resource not found', details?: unknown, endpoint?: string, method?: string) {
    super(message, 404, details, endpoint, method);
  }
}

export class ConflictError extends ConduitError {
  constructor(message = 'Resource conflict', details?: unknown, endpoint?: string, method?: string) {
    super(message, 409, details, endpoint, method);
  }
}

export class RateLimitError extends ConduitError {
  public retryAfter?: number;

  constructor(message = 'Rate limit exceeded', retryAfter?: number, details?: unknown, endpoint?: string, method?: string) {
    super(message, 429, details, endpoint, method);
    this.retryAfter = retryAfter;
  }
}

export class ServerError extends ConduitError {
  constructor(message = 'Internal server error', details?: unknown, endpoint?: string, method?: string) {
    super(message, 500, details, endpoint, method);
  }
}

export class NetworkError extends ConduitError {
  constructor(message = 'Network error', details?: unknown) {
    super(message, undefined, details);
  }
}

export class TimeoutError extends ConduitError {
  constructor(message = 'Request timeout', details?: unknown) {
    super(message, 408, details);
  }
}

export class NotImplementedError extends ConduitError {
  constructor(message: string, details?: unknown) {
    super(message, 501, details);
  }
}

export function isConduitError(error: unknown): error is ConduitError {
  return error instanceof ConduitError;
}

// Type guard for axios-like errors
function isAxiosError(error: unknown): error is {
  response: { status: number; data: unknown; headers: Record<string, string> };
  message: string;
  request?: unknown;
  code?: string;
} {
  return (
    typeof error === 'object' &&
    error !== null &&
    'response' in error &&
    typeof (error as any).response === 'object'
  );
}

// Type guard for network errors
function isNetworkError(error: unknown): error is {
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
function isErrorLike(error: unknown): error is {
  message: string;
} {
  return (
    typeof error === 'object' &&
    error !== null &&
    'message' in error &&
    typeof (error as any).message === 'string'
  );
}

export function handleApiError(error: unknown, endpoint?: string, method?: string): never {
  if (isAxiosError(error)) {
    const { status, data } = error.response;
    const errorData = data as any; // We need to handle data as any since it can be anything
    const baseMessage = errorData?.error || errorData?.message || error.message;
    const details = errorData?.details || data;
    
    // Enhanced error messages with endpoint information
    const endpointInfo = endpoint && method ? ` (${method.toUpperCase()} ${endpoint})` : '';
    const enhancedMessage = `${baseMessage}${endpointInfo}`;

    switch (status) {
      case 400:
        throw new ValidationError(enhancedMessage, details, endpoint, method);
      case 401:
        throw new AuthenticationError(enhancedMessage, details, endpoint, method);
      case 403:
        throw new AuthorizationError(enhancedMessage, details, endpoint, method);
      case 404:
        throw new NotFoundError(enhancedMessage, details, endpoint, method);
      case 409:
        throw new ConflictError(enhancedMessage, details, endpoint, method);
      case 429: {
        const retryAfterHeader = error.response.headers['retry-after'];
        const retryAfter = typeof retryAfterHeader === 'string' ? parseInt(retryAfterHeader, 10) : undefined;
        throw new RateLimitError(enhancedMessage, retryAfter, details, endpoint, method);
      }
      case 500:
      case 502:
      case 503:
      case 504:
        throw new ServerError(enhancedMessage, details, endpoint, method);
      default:
        throw new ConduitError(enhancedMessage, status, details, endpoint, method);
    }
  } else if (isNetworkError(error)) {
    const endpointInfo = endpoint && method ? ` (${method.toUpperCase()} ${endpoint})` : '';
    if (error.code === 'ECONNABORTED') {
      throw new TimeoutError(`Request timeout${endpointInfo}`, { endpoint, method });
    }
    throw new NetworkError(`Network error: No response received${endpointInfo}`, {
      code: error.code,
      endpoint,
      method,
    });
  } else if (isErrorLike(error)) {
    throw new ConduitError(
      error.message,
      undefined,
      { originalError: error },
      endpoint,
      method
    );
  } else {
    throw new ConduitError(
      'Unknown error',
      undefined,
      { originalError: error },
      endpoint,
      method
    );
  }
}