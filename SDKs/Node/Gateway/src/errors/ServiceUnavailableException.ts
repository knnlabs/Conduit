import { ConduitError } from '@knn_labs/conduit-common';

/**
 * Error thrown when a service is temporarily unavailable
 * Maps to HTTP 503 status code
 */
export class ServiceUnavailableException extends ConduitError {
  public readonly serviceName?: string;
  public readonly retryAfterSeconds?: number;

  constructor(message: string, serviceName?: string, retryAfterSeconds?: number) {
    super(
      message,
      503,
      'service_unavailable',
      {
        type: 'service_unavailable',
        serviceName,
        retryAfterSeconds
      }
    );
    this.serviceName = serviceName;
    this.retryAfterSeconds = retryAfterSeconds;
    this.name = 'ServiceUnavailableException';
  }
}

export function isServiceUnavailableException(error: unknown): error is ServiceUnavailableException {
  return error instanceof ServiceUnavailableException ||
    (error instanceof ConduitError && error.statusCode === 503);
}