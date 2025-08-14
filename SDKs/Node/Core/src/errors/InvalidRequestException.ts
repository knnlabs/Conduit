import { ConduitError } from '@knn_labs/conduit-common';

/**
 * Error thrown when a request contains invalid parameters or is malformed
 * Maps to HTTP 400 status code
 */
export class InvalidRequestException extends ConduitError {
  public readonly errorCode?: string;
  public readonly param?: string;

  constructor(message: string, errorCode?: string, param?: string) {
    super(
      message,
      400,
      errorCode ?? 'invalid_request',
      {
        type: 'invalid_request_error',
        errorCode,
        param
      }
    );
    this.errorCode = errorCode;
    this.param = param;
    this.name = 'InvalidRequestException';
  }
}

export function isInvalidRequestException(error: unknown): error is InvalidRequestException {
  return error instanceof InvalidRequestException ||
    (error instanceof ConduitError && error.statusCode === 400 && error.type === 'invalid_request_error');
}