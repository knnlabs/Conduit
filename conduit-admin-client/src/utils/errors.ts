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
  constructor(message = 'Authentication failed', details?: any, endpoint?: string, method?: string) {
    super(message, 401, details, endpoint, method);
  }
}

export class AuthorizationError extends ConduitError {
  constructor(message = 'Access forbidden', details?: any, endpoint?: string, method?: string) {
    super(message, 403, details, endpoint, method);
  }
}

export class ValidationError extends ConduitError {
  constructor(message = 'Validation failed', details?: any, endpoint?: string, method?: string) {
    super(message, 400, details, endpoint, method);
  }
}

export class NotFoundError extends ConduitError {
  constructor(message = 'Resource not found', details?: any, endpoint?: string, method?: string) {
    super(message, 404, details, endpoint, method);
  }
}

export class ConflictError extends ConduitError {
  constructor(message = 'Resource conflict', details?: any, endpoint?: string, method?: string) {
    super(message, 409, details, endpoint, method);
  }
}

export class RateLimitError extends ConduitError {
  public retryAfter?: number;

  constructor(message = 'Rate limit exceeded', retryAfter?: number, details?: any, endpoint?: string, method?: string) {
    super(message, 429, details, endpoint, method);
    this.retryAfter = retryAfter;
  }
}

export class ServerError extends ConduitError {
  constructor(message = 'Internal server error', details?: any, endpoint?: string, method?: string) {
    super(message, 500, details, endpoint, method);
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
    const baseMessage = data?.error || data?.message || error.message;
    const details = data?.details || data;
    
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
      case 429:
        const retryAfter = error.response.headers['retry-after'];
        throw new RateLimitError(enhancedMessage, retryAfter, details, endpoint, method);
      case 500:
      case 502:
      case 503:
      case 504:
        throw new ServerError(enhancedMessage, details, endpoint, method);
      default:
        throw new ConduitError(enhancedMessage, status, details, endpoint, method);
    }
  } else if (error.request) {
    const endpointInfo = endpoint && method ? ` (${method.toUpperCase()} ${endpoint})` : '';
    if (error.code === 'ECONNABORTED') {
      throw new TimeoutError(`Request timeout${endpointInfo}`, { endpoint, method });
    }
    throw new NetworkError(`Network error: No response received${endpointInfo}`, {
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