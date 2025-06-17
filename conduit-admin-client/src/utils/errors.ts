export class ConduitError extends Error {
  public statusCode?: number;
  public details?: any;
  public endpoint?: string;
  public method?: string;

  constructor(
    message: string,
    statusCode?: number,
    details?: any,
    endpoint?: string,
    method?: string
  ) {
    super(message);
    this.name = this.constructor.name;
    this.statusCode = statusCode;
    this.details = details;
    this.endpoint = endpoint;
    this.method = method;
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
    };
  }
}

export class AuthenticationError extends ConduitError {
  constructor(message = 'Authentication failed', details?: any) {
    super(message, 401, details);
  }
}

export class AuthorizationError extends ConduitError {
  constructor(message = 'Access forbidden', details?: any) {
    super(message, 403, details);
  }
}

export class ValidationError extends ConduitError {
  constructor(message = 'Validation failed', details?: any) {
    super(message, 400, details);
  }
}

export class NotFoundError extends ConduitError {
  constructor(message = 'Resource not found', details?: any) {
    super(message, 404, details);
  }
}

export class ConflictError extends ConduitError {
  constructor(message = 'Resource conflict', details?: any) {
    super(message, 409, details);
  }
}

export class RateLimitError extends ConduitError {
  public retryAfter?: number;

  constructor(message = 'Rate limit exceeded', retryAfter?: number, details?: any) {
    super(message, 429, details);
    this.retryAfter = retryAfter;
  }
}

export class ServerError extends ConduitError {
  constructor(message = 'Internal server error', details?: any) {
    super(message, 500, details);
  }
}

export class NetworkError extends ConduitError {
  constructor(message = 'Network error', details?: any) {
    super(message, undefined, details);
  }
}

export class TimeoutError extends ConduitError {
  constructor(message = 'Request timeout', details?: any) {
    super(message, 408, details);
  }
}

export class NotImplementedError extends ConduitError {
  constructor(message: string, details?: any) {
    super(message, 501, details);
  }
}

export function isConduitError(error: any): error is ConduitError {
  return error instanceof ConduitError;
}

export function handleApiError(error: any, endpoint?: string, method?: string): never {
  if (error.response) {
    const { status, data } = error.response;
    const message = data?.error || data?.message || error.message;
    const details = data?.details || data;

    switch (status) {
      case 400:
        throw new ValidationError(message, details);
      case 401:
        throw new AuthenticationError(message, details);
      case 403:
        throw new AuthorizationError(message, details);
      case 404:
        throw new NotFoundError(message, details);
      case 409:
        throw new ConflictError(message, details);
      case 429:
        const retryAfter = error.response.headers['retry-after'];
        throw new RateLimitError(message, retryAfter, details);
      case 500:
      case 502:
      case 503:
      case 504:
        throw new ServerError(message, details);
      default:
        throw new ConduitError(message, status, details, endpoint, method);
    }
  } else if (error.request) {
    if (error.code === 'ECONNABORTED') {
      throw new TimeoutError('Request timeout', { endpoint, method });
    }
    throw new NetworkError('Network error: No response received', {
      code: error.code,
      endpoint,
      method,
    });
  } else {
    throw new ConduitError(
      error.message || 'Unknown error',
      undefined,
      { originalError: error },
      endpoint,
      method
    );
  }
}