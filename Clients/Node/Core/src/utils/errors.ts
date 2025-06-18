import type { ErrorResponse } from '../models/common';

export class ConduitError extends Error {
  public readonly statusCode?: number;
  public readonly code?: string;
  public readonly type?: string;
  public readonly param?: string;

  constructor(message: string, statusCode?: number, code?: string, type?: string, param?: string) {
    super(message);
    this.name = 'ConduitError';
    this.statusCode = statusCode;
    this.code = code;
    this.type = type;
    this.param = param;
    Object.setPrototypeOf(this, ConduitError.prototype);
  }

  static fromErrorResponse(response: ErrorResponse, statusCode?: number): ConduitError {
    return new ConduitError(
      response.error.message,
      statusCode,
      response.error.code || undefined,
      response.error.type,
      response.error.param || undefined
    );
  }
}

export class AuthenticationError extends ConduitError {
  constructor(message: string = 'Authentication failed') {
    super(message, 401, 'authentication_error', 'invalid_request_error');
    this.name = 'AuthenticationError';
    Object.setPrototypeOf(this, AuthenticationError.prototype);
  }
}

export class RateLimitError extends ConduitError {
  public readonly retryAfter?: number;

  constructor(message: string = 'Rate limit exceeded', retryAfter?: number) {
    super(message, 429, 'rate_limit_error', 'rate_limit_error');
    this.name = 'RateLimitError';
    this.retryAfter = retryAfter;
    Object.setPrototypeOf(this, RateLimitError.prototype);
  }
}

export class ValidationError extends ConduitError {
  constructor(message: string, param?: string) {
    super(message, 400, 'validation_error', 'invalid_request_error', param);
    this.name = 'ValidationError';
    Object.setPrototypeOf(this, ValidationError.prototype);
  }
}

export class NetworkError extends ConduitError {
  constructor(message: string = 'Network request failed') {
    super(message);
    this.name = 'NetworkError';
    Object.setPrototypeOf(this, NetworkError.prototype);
  }
}

export class StreamError extends ConduitError {
  constructor(message: string = 'Stream processing failed') {
    super(message);
    this.name = 'StreamError';
    Object.setPrototypeOf(this, StreamError.prototype);
  }
}