import { ConduitError } from '@knn_labs/conduit-common';

/**
 * Error thrown when a requested model is not found or not configured
 * Maps to HTTP 404 status code
 */
export class ModelNotFoundException extends ConduitError {
  public readonly modelName: string;

  constructor(modelName: string, message?: string) {
    super(
      message || `Model '${modelName}' not found. Please check your model configuration.`,
      404,
      'model_not_found',
      {
        type: 'invalid_request_error',
        param: 'model',
        modelName
      }
    );
    this.modelName = modelName;
    this.name = 'ModelNotFoundException';
  }
}

export function isModelNotFoundException(error: unknown): error is ModelNotFoundException {
  return error instanceof ModelNotFoundException ||
    (error instanceof ConduitError && error.code === 'model_not_found');
}