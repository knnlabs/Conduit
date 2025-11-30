import { ConduitError } from '@knn_labs/conduit-common';

/**
 * Error thrown when a request payload exceeds the maximum allowed size
 * Maps to HTTP 413 status code
 */
export class PayloadTooLargeException extends ConduitError {
  public readonly payloadSize?: number;
  public readonly maximumSize?: number;

  constructor(message: string, payloadSize?: number, maximumSize?: number) {
    super(
      message,
      413,
      'payload_too_large',
      {
        type: 'invalid_request_error',
        payloadSize,
        maximumSize
      }
    );
    this.payloadSize = payloadSize;
    this.maximumSize = maximumSize;
    this.name = 'PayloadTooLargeException';
  }
}

export function isPayloadTooLargeException(error: unknown): error is PayloadTooLargeException {
  return error instanceof PayloadTooLargeException ||
    (error instanceof ConduitError && error.statusCode === 413);
}