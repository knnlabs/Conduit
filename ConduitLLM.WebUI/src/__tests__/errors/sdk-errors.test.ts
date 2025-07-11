import { NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { 
  ConduitError,
  AuthError, 
  RateLimitError, 
  NetworkError,
  ValidationError,
  NotFoundError,
  ConflictError,
} from '@knn_labs/conduit-admin-client';

describe('SDK Error Handling', () => {
  describe('handleSDKError', () => {
    it('should handle RateLimitError with retry-after header', () => {
      const error = new RateLimitError('Too many requests', 60);
      const response = handleSDKError(error);
      
      expect(response).toBeInstanceOf(NextResponse);
      expect(response.status).toBe(429);
      expect(response.headers.get('Retry-After')).toBe('60');
      expect(response.headers.get('X-RateLimit-Limit')).toBeTruthy();
    });

    it('should handle AuthError with 401 status', () => {
      const error = new AuthError('Invalid credentials', {
        code: 'INVALID_CREDENTIALS',
      });
      const response = handleSDKError(error);
      
      expect(response.status).toBe(401);
      expect(response.headers.get('Content-Type')).toBe('application/json');
    });

    it('should handle NetworkError with 503 status', () => {
      const error = new NetworkError('Connection failed');
      const response = handleSDKError(error);
      
      expect(response.status).toBe(503);
    });

    it('should handle ValidationError with 400 status', () => {
      const error = new ValidationError('Invalid input', {
        fields: {
          keyName: 'Required field',
          budget: 'Must be positive',
        },
      });
      const response = handleSDKError(error);
      
      expect(response.status).toBe(400);
    });

    it('should handle NotFoundError with 404 status', () => {
      const error = new NotFoundError('Resource not found', {
        resourceType: 'VirtualKey',
        resourceId: '123',
      });
      const response = handleSDKError(error);
      
      expect(response.status).toBe(404);
    });

    it('should handle ConflictError with 409 status', () => {
      const error = new ConflictError('Resource already exists', {
        conflictingField: 'keyName',
      });
      const response = handleSDKError(error);
      
      expect(response.status).toBe(409);
    });

    it('should handle generic ConduitError with custom status code', () => {
      const error = new ConduitError('Custom error', {
        statusCode: 418, // I'm a teapot
        code: 'TEAPOT',
      });
      const response = handleSDKError(error);
      
      expect(response.status).toBe(418);
    });

    it('should handle ConduitError without status code as 500', () => {
      const error = new ConduitError('Server error');
      const response = handleSDKError(error);
      
      expect(response.status).toBe(500);
    });

    it('should handle unknown errors as 500', () => {
      const error = new Error('Unknown error');
      const response = handleSDKError(error);
      
      expect(response.status).toBe(500);
    });

    it('should handle non-Error objects as 500', () => {
      const error = 'String error';
      const response = handleSDKError(error);
      
      expect(response.status).toBe(500);
    });

    it('should handle null/undefined as 500', () => {
      const response1 = handleSDKError(null);
      const response2 = handleSDKError(undefined);
      
      expect(response1.status).toBe(500);
      expect(response2.status).toBe(500);
    });

    it('should include error details in response body', async () => {
      const error = new ValidationError('Validation failed', {
        fields: {
          email: 'Invalid email format',
          password: 'Too short',
        },
      });
      const response = handleSDKError(error);
      const body = await response.json();
      
      expect(body).toEqual({
        error: 'Validation failed',
        details: {
          fields: {
            email: 'Invalid email format',
            password: 'Too short',
          },
        },
      });
    });

    it('should not expose sensitive information in errors', async () => {
      const error = new Error('Connection to database failed at postgres://user:password@localhost/db');
      const response = handleSDKError(error);
      const body = await response.json();
      
      // Should not include the full error message with credentials
      expect(body.error).not.toContain('password');
      expect(body.error).toBe('Internal server error');
    });

    it('should log errors to console in development', () => {
      const originalEnv = process.env.NODE_ENV;
      process.env.NODE_ENV = 'development';
      
      const consoleSpy = jest.spyOn(console, 'error').mockImplementation();
      const error = new Error('Test error');
      
      handleSDKError(error);
      
      expect(consoleSpy).toHaveBeenCalledWith('[SDK Error]:', error);
      
      consoleSpy.mockRestore();
      process.env.NODE_ENV = originalEnv;
    });

    it('should include request ID in error response if available', async () => {
      const error = new NetworkError('Request failed', {
        requestId: 'req_123456',
      });
      const response = handleSDKError(error);
      const body = await response.json();
      
      expect(response.headers.get('X-Request-ID')).toBe('req_123456');
      expect(body.requestId).toBe('req_123456');
    });
  });

  describe('Error Response Format', () => {
    it('should have consistent error response structure', async () => {
      const errors = [
        new AuthError('Auth failed'),
        new ValidationError('Validation failed'),
        new NetworkError('Network failed'),
        new Error('Unknown error'),
      ];

      for (const error of errors) {
        const response = handleSDKError(error);
        const body = await response.json();
        
        // All errors should have at least an error field
        expect(body).toHaveProperty('error');
        expect(typeof body.error).toBe('string');
        
        // Should be valid JSON
        expect(() => JSON.stringify(body)).not.toThrow();
      }
    });

    it('should handle circular references in error details', async () => {
      const circularObj: any = { a: 1 };
      circularObj.self = circularObj;
      
      const error = new ValidationError('Circular error', {
        details: circularObj,
      });
      
      // Should not throw when handling circular references
      const response = handleSDKError(error);
      const body = await response.json();
      
      expect(body).toHaveProperty('error');
      expect(response.status).toBe(400);
    });
  });

  describe('Error Type Detection', () => {
    it('should properly detect SDK error types using instanceof', () => {
      const authError = new AuthError('Unauthorized');
      const rateLimitError = new RateLimitError('Too many requests', 60);
      const networkError = new NetworkError('Network failed');
      const validationError = new ValidationError('Invalid data');
      
      expect(authError instanceof AuthError).toBe(true);
      expect(authError instanceof ConduitError).toBe(true);
      
      expect(rateLimitError instanceof RateLimitError).toBe(true);
      expect(rateLimitError instanceof ConduitError).toBe(true);
      
      expect(networkError instanceof NetworkError).toBe(true);
      expect(networkError instanceof ConduitError).toBe(true);
      
      expect(validationError instanceof ValidationError).toBe(true);
      expect(validationError instanceof ConduitError).toBe(true);
    });

    it('should handle SDK errors with custom error codes', async () => {
      const error = new ConduitError('Custom SDK error', {
        code: 'CUSTOM_ERROR_CODE',
        statusCode: 422,
      });
      
      const response = handleSDKError(error);
      const body = await response.json();
      
      expect(response.status).toBe(422);
      expect(body.code).toBe('CUSTOM_ERROR_CODE');
    });
  });

  describe('Error Metadata', () => {
    it('should preserve error metadata in responses', async () => {
      const error = new NotFoundError('Virtual key not found', {
        resourceType: 'VirtualKey',
        resourceId: 'vk_123',
        searchCriteria: { name: 'Test Key' },
      });
      
      const response = handleSDKError(error);
      const body = await response.json();
      
      expect(body).toMatchObject({
        error: 'Virtual key not found',
        details: {
          resourceType: 'VirtualKey',
          resourceId: 'vk_123',
          searchCriteria: { name: 'Test Key' },
        },
      });
    });

    it('should handle errors with stack traces in development', async () => {
      const originalEnv = process.env.NODE_ENV;
      process.env.NODE_ENV = 'development';
      
      const error = new Error('Test error with stack');
      const response = handleSDKError(error);
      const body = await response.json();
      
      // In development, might include stack trace
      expect(body).toHaveProperty('error');
      
      process.env.NODE_ENV = originalEnv;
    });

    it('should not include stack traces in production', async () => {
      const originalEnv = process.env.NODE_ENV;
      process.env.NODE_ENV = 'production';
      
      const error = new Error('Test error with stack');
      const response = handleSDKError(error);
      const body = await response.json();
      
      // In production, should not include stack trace
      expect(body).not.toHaveProperty('stack');
      
      process.env.NODE_ENV = originalEnv;
    });
  });
});