/**
 * Custom error classes for model operations
 */

/**
 * Base error class for model-related errors
 */
export class ModelError extends Error {
  constructor(message: string, public code?: string) {
    super(message);
    this.name = 'ModelError';
  }
}

/**
 * Error thrown when a provider type is invalid
 */
export class InvalidProviderTypeError extends ModelError {
  constructor(public provider: string) {
    super(`Invalid provider type: ${provider}`, 'INVALID_PROVIDER_TYPE');
    this.name = 'InvalidProviderTypeError';
  }
}

/**
 * Error thrown when a duplicate identifier is detected
 */
export class DuplicateIdentifierError extends ModelError {
  constructor(
    public identifier: string,
    public provider: string
  ) {
    super(
      `Identifier '${identifier}' already exists for provider '${provider}'`,
      'DUPLICATE_IDENTIFIER'
    );
    this.name = 'DuplicateIdentifierError';
  }
}

/**
 * Error thrown when validation fails
 */
export class ModelValidationError extends ModelError {
  constructor(
    message: string,
    public fields: Record<string, string | string[]>
  ) {
    super(message, 'VALIDATION_ERROR');
    this.name = 'ModelValidationError';
  }
  
  /**
   * Get formatted error message including field errors
   */
  getFullMessage(): string {
    const fieldErrors = Object.entries(this.fields)
      .map(([field, error]) => {
        const errorMsg = Array.isArray(error) ? error.join(', ') : error;
        return `  - ${field}: ${errorMsg}`;
      })
      .join('\n');
    
    return `${this.message}\n${fieldErrors}`;
  }
}

/**
 * Error thrown when trying to set multiple primary identifiers
 */
export class PrimaryIdentifierConflictError extends ModelError {
  constructor(public provider: string) {
    super(
      `A primary identifier already exists for provider '${provider}'`,
      'PRIMARY_IDENTIFIER_CONFLICT'
    );
    this.name = 'PrimaryIdentifierConflictError';
  }
}

/**
 * Error thrown when a required field is missing
 */
export class RequiredFieldError extends ModelError {
  constructor(public field: string) {
    super(`Required field '${field}' is missing`, 'REQUIRED_FIELD');
    this.name = 'RequiredFieldError';
  }
}

/**
 * Error thrown when a value is out of range
 */
export class OutOfRangeError extends ModelError {
  constructor(
    public field: string,
    public value: number,
    public min?: number,
    public max?: number
  ) {
    let message = `Value ${value} for field '${field}' is out of range`;
    if (min !== undefined && max !== undefined) {
      message += ` (must be between ${min} and ${max})`;
    } else if (min !== undefined) {
      message += ` (must be at least ${min})`;
    } else if (max !== undefined) {
      message += ` (must be at most ${max})`;
    }
    
    super(message, 'OUT_OF_RANGE');
    this.name = 'OutOfRangeError';
  }
}

/**
 * Error thrown when a feature is not supported by a provider
 */
export class UnsupportedFeatureError extends ModelError {
  constructor(
    public feature: string,
    public provider: string
  ) {
    super(
      `Feature '${feature}' is not supported by provider '${provider}'`,
      'UNSUPPORTED_FEATURE'
    );
    this.name = 'UnsupportedFeatureError';
  }
}