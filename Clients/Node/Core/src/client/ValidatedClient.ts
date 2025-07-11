import { z } from 'zod';
import { BaseClient } from './BaseClient';
import { ClientConfig, RequestOptions } from './types';
import { validateResponse, safeValidateResponse, ValidationResult } from '../validation/schemas';

export interface ValidatedRequestOptions extends RequestOptions {
  skipValidation?: boolean;
}

export abstract class ValidatedClient extends BaseClient {
  protected readonly validateResponses: boolean;

  constructor(config: ClientConfig & { validateResponses?: boolean }) {
    super(config);
    this.validateResponses = config.validateResponses ?? true;
  }

  protected async validatedRequest<T>(
    config: { method: string; url: string; data?: unknown; params?: Record<string, unknown> },
    schema?: z.ZodType<T>,
    options?: ValidatedRequestOptions
  ): Promise<T> {
    const response = await this.request<unknown>(
      {
        method: config.method,
        url: config.url,
        data: config.data,
        params: config.params,
      },
      options
    );
    
    // Skip validation if explicitly disabled or no schema provided
    if (options?.skipValidation || !schema || !this.validateResponses) {
      return response as T;
    }

    // Validate the response
    try {
      return validateResponse(schema, response);
    } catch (error) {
      if (this.config.debug) {
        console.error('[Conduit] Response validation failed', {
          url: config.url,
          method: config.method,
          error: error instanceof Error ? error.message : 'Unknown error',
          response,
        });
      }
      
      // Re-throw with more context
      if (error instanceof Error) {
        error.message = `${error.message} (${config.method} ${config.url})`;
      }
      throw error;
    }
  }

  protected async validatedGet<T>(
    url: string,
    params?: Record<string, unknown>,
    schema?: z.ZodType<T>,
    options?: ValidatedRequestOptions
  ): Promise<T> {
    return this.validatedRequest<T>(
      {
        method: 'GET',
        url,
        params,
      },
      schema,
      options
    );
  }

  protected async validatedPost<T>(
    url: string,
    data?: unknown,
    schema?: z.ZodType<T>,
    options?: ValidatedRequestOptions
  ): Promise<T> {
    return this.validatedRequest<T>(
      {
        method: 'POST',
        url,
        data,
      },
      schema,
      options
    );
  }

  protected async validatedPut<T>(
    url: string,
    data?: unknown,
    schema?: z.ZodType<T>,
    options?: ValidatedRequestOptions
  ): Promise<T> {
    return this.validatedRequest<T>(
      {
        method: 'PUT',
        url,
        data,
      },
      schema,
      options
    );
  }

  protected async validatedDelete<T>(
    url: string,
    schema?: z.ZodType<T>,
    options?: ValidatedRequestOptions
  ): Promise<T> {
    return this.validatedRequest<T>(
      {
        method: 'DELETE',
        url,
      },
      schema,
      options
    );
  }

  protected async validatedPatch<T>(
    url: string,
    data?: unknown,
    schema?: z.ZodType<T>,
    options?: ValidatedRequestOptions
  ): Promise<T> {
    return this.validatedRequest<T>(
      {
        method: 'PATCH',
        url,
        data,
      },
      schema,
      options
    );
  }

  // Safe validation methods that return Result types
  protected async safeValidatedRequest<T>(
    config: { method: string; url: string; data?: unknown; params?: Record<string, unknown> },
    schema?: z.ZodType<T>,
    options?: ValidatedRequestOptions
  ): Promise<ValidationResult<T> | T> {
    const response = await this.request<unknown>(
      {
        method: config.method,
        url: config.url,
        data: config.data,
        params: config.params,
      },
      options
    );
    
    if (options?.skipValidation || !schema || !this.validateResponses) {
      return response as T;
    }

    return safeValidateResponse(schema, response);
  }
}