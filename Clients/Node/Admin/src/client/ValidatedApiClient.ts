import { z } from 'zod';
import { BaseApiClient } from './BaseApiClient';
import { ApiClientConfig, RequestConfig } from './types';
import { validateResponse, safeValidateResponse, ValidationResult } from '../validation/schemas';

export interface ValidatedRequestConfig extends RequestConfig {
  skipValidation?: boolean;
}

export abstract class ValidatedApiClient extends BaseApiClient {
  protected readonly validateResponses: boolean;

  constructor(config: ApiClientConfig & { validateResponses?: boolean }) {
    super(config);
    this.validateResponses = config.validateResponses ?? true;
  }

  protected async validatedRequest<T>(
    config: ValidatedRequestConfig & { method: string; url: string },
    schema?: z.ZodType<T>
  ): Promise<T> {
    const response = await this.request<unknown>(config);
    
    // Skip validation if explicitly disabled or no schema provided
    if (config.skipValidation || !schema || !this.validateResponses) {
      return response as T;
    }

    // Validate the response
    try {
      return validateResponse(schema, response);
    } catch (error) {
      this.logger?.error('Response validation failed', {
        url: config.url,
        method: config.method,
        error: error instanceof Error ? error.message : 'Unknown error',
        response,
      });
      
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
    options?: ValidatedRequestConfig
  ): Promise<T> {
    return this.validatedRequest<T>(
      {
        method: 'GET',
        url,
        params,
        ...options,
      },
      schema
    );
  }

  protected async validatedPost<T>(
    url: string,
    data?: unknown,
    schema?: z.ZodType<T>,
    options?: ValidatedRequestConfig
  ): Promise<T> {
    return this.validatedRequest<T>(
      {
        method: 'POST',
        url,
        data,
        ...options,
      },
      schema
    );
  }

  protected async validatedPut<T>(
    url: string,
    data?: unknown,
    schema?: z.ZodType<T>,
    options?: ValidatedRequestConfig
  ): Promise<T> {
    return this.validatedRequest<T>(
      {
        method: 'PUT',
        url,
        data,
        ...options,
      },
      schema
    );
  }

  protected async validatedDelete<T>(
    url: string,
    schema?: z.ZodType<T>,
    options?: ValidatedRequestConfig
  ): Promise<T> {
    return this.validatedRequest<T>(
      {
        method: 'DELETE',
        url,
        ...options,
      },
      schema
    );
  }

  protected async validatedPatch<T>(
    url: string,
    data?: unknown,
    schema?: z.ZodType<T>,
    options?: ValidatedRequestConfig
  ): Promise<T> {
    return this.validatedRequest<T>(
      {
        method: 'PATCH',
        url,
        data,
        ...options,
      },
      schema
    );
  }

  // Safe validation methods that return Result types
  protected async safeValidatedRequest<T>(
    config: ValidatedRequestConfig & { method: string; url: string },
    schema?: z.ZodType<T>
  ): Promise<ValidationResult<T> | T> {
    const response = await this.request<unknown>(config);
    
    if (config.skipValidation || !schema || !this.validateResponses) {
      return response as T;
    }

    return safeValidateResponse(schema, response);
  }
}