import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { RequestConfig } from '../client/types';
import { z } from 'zod';

/**
 * Validation options for runtime checking
 */
export interface ValidationOptions {
  /** Whether to validate responses (default: true in development, false in production) */
  enabled?: boolean;
  /** Whether to throw on validation errors or just log warnings (default: false) */
  throwOnError?: boolean;
  /** Custom error handler */
  onValidationError?: (error: z.ZodError, response: unknown) => void;
}

/**
 * Default validation options
 */
export const defaultValidationOptions: ValidationOptions = {
  enabled: process.env.NODE_ENV !== 'production',
  throwOnError: false,
  onValidationError: (error, response) => {
    console.warn('API response validation failed:', {
      errors: error.errors,
      response,
    });
  },
};

/**
 * Validates a response against a schema
 */
export function validateResponse<T>(
  schema: z.ZodSchema<T>,
  response: unknown,
  options: ValidationOptions = defaultValidationOptions
): T {
  if (!options.enabled) {
    return response as T;
  }

  try {
    return schema.parse(response);
  } catch (error) {
    if (error instanceof z.ZodError) {
      if (options.onValidationError) {
        options.onValidationError(error, response);
      }
      
      if (options.throwOnError) {
        throw new Error(`API response validation failed: ${error.message}`);
      }
    }
    
    // Return the original response if validation fails and throwOnError is false
    return response as T;
  }
}

/**
 * Base class for all services using composition pattern
 * Services contain a client rather than extending it
 */
export abstract class ServiceBase {
  /**
   * The API client used for making requests
   */
  protected readonly client: FetchBaseApiClient;
  
  /**
   * Service-specific validation options
   */
  protected readonly validationOptions?: ValidationOptions;
  
  constructor(client: FetchBaseApiClient, validationOptions?: ValidationOptions) {
    this.client = client;
    this.validationOptions = validationOptions;
  }
  
  /**
   * Gets the effective validation options for this service
   * Merges service-specific options with client options
   */
  protected getValidationOptions(): ValidationOptions {
    const clientOptions = (this.client as any).validation || {};
    return {
      enabled: this.validationOptions?.enabled ?? clientOptions.enabled ?? (process.env.NODE_ENV !== 'production'),
      throwOnError: this.validationOptions?.throwOnError ?? clientOptions.throwOnError ?? false,
      onValidationError: this.validationOptions?.onValidationError ?? clientOptions.onValidationError,
    };
  }
  
  /**
   * Makes a validated GET request
   */
  protected async get<TResponse>(
    url: string,
    schema?: z.ZodSchema<TResponse>,
    config?: RequestConfig
  ): Promise<TResponse> {
    const response = await this.client['get']<TResponse>(url, config);
    if (schema) {
      return validateResponse(schema, response, this.getValidationOptions());
    }
    return response;
  }
  
  /**
   * Makes a validated POST request
   */
  protected async post<TResponse, TRequest = unknown>(
    url: string,
    data?: TRequest,
    schema?: z.ZodSchema<TResponse>,
    config?: RequestConfig
  ): Promise<TResponse> {
    const response = await this.client['post']<TResponse, TRequest>(url, data, config);
    if (schema) {
      return validateResponse(schema, response, this.getValidationOptions());
    }
    return response;
  }
  
  /**
   * Makes a validated PUT request
   */
  protected async put<TResponse, TRequest = unknown>(
    url: string,
    data?: TRequest,
    schema?: z.ZodSchema<TResponse>,
    config?: RequestConfig
  ): Promise<TResponse> {
    const response = await this.client['put']<TResponse, TRequest>(url, data, config);
    if (schema) {
      return validateResponse(schema, response, this.getValidationOptions());
    }
    return response;
  }
  
  /**
   * Makes a validated PATCH request
   */
  protected async patch<TResponse, TRequest = unknown>(
    url: string,
    data?: TRequest,
    schema?: z.ZodSchema<TResponse>,
    config?: RequestConfig
  ): Promise<TResponse> {
    const response = await this.client['patch']<TResponse, TRequest>(url, data, config);
    if (schema) {
      return validateResponse(schema, response, this.getValidationOptions());
    }
    return response;
  }
  
  /**
   * Makes a validated DELETE request
   */
  protected async delete<TResponse = void>(
    url: string,
    schema?: z.ZodSchema<TResponse>,
    config?: RequestConfig
  ): Promise<TResponse> {
    const response = await this.client['delete']<TResponse>(url, config);
    if (schema) {
      return validateResponse(schema, response, this.getValidationOptions());
    }
    return response;
  }
  
  /**
   * Makes an unvalidated request (for edge cases)
   */
  protected async request<TResponse = unknown, TRequest = unknown>(
    url: string,
    options: {
      method?: string;
      body?: TRequest;
      headers?: Record<string, string>;
      signal?: AbortSignal;
      timeout?: number;
      responseType?: 'json' | 'text' | 'blob' | 'arraybuffer';
    } = {}
  ): Promise<TResponse> {
    return this.client['request']<TResponse, TRequest>(url, options);
  }
}