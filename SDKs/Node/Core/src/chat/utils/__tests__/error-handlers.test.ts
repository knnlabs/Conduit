/**
 * Unit tests for error-handlers utilities
 */

import {
  isOpenAIError,
  parseSSEError,
  processSSEEvent,
  handleSSEConnectionError,
  type OpenAIErrorResponse
} from '../error-handlers';

describe('error-handlers', () => {
  describe('isOpenAIError', () => {
    it('should identify valid OpenAI error format', () => {
      const errorResponse: OpenAIErrorResponse = {
        error: {
          message: 'Invalid request',
          type: 'invalid_request_error',
          code: 'invalid_parameter'
        }
      };

      expect(isOpenAIError(errorResponse)).toBe(true);
    });

    it('should reject invalid formats', () => {
      expect(isOpenAIError(null)).toBe(false);
      expect(isOpenAIError(undefined)).toBe(false);
      expect(isOpenAIError('string')).toBe(false);
      expect(isOpenAIError({})).toBe(false);
      expect(isOpenAIError({ error: {} })).toBe(false);
      expect(isOpenAIError({ error: { type: 'error' } })).toBe(false);
    });
  });

  describe('parseSSEError', () => {
    it('should parse OpenAI error and return AppError', () => {
      const errorResponse: OpenAIErrorResponse = {
        error: {
          message: 'Model not found',
          type: 'not_found_error',
          code: 'model_not_found',
          param: 'gpt-4'
        }
      };

      const result = parseSSEError(errorResponse);
      
      expect(result).not.toBeNull();
      if (result) {
        expect(result.status).toBe(404);
        expect(result.code).toBe('model_not_found');
        expect(result.title).toBe('Not Found');
        expect(result.message).toBe('The model "gpt-4" is not available. Please select a different model.');
        expect(result.isRecoverable).toBe(false);
        expect(result.severity).toBe('info');
        expect(result.iconName).toBe('MagnifyingGlassIcon');
      }
    });

    it('should map different error codes to correct status codes', () => {
      const testCases = [
        { code: 'authentication_error', expectedStatus: 401 },
        { code: 'invalid_request', expectedStatus: 400 },
        { code: 'insufficient_balance', expectedStatus: 402 },
        { code: 'permission_denied', expectedStatus: 403 },
        { code: 'rate_limit_exceeded', expectedStatus: 429 },
      ];

      testCases.forEach(({ code, expectedStatus }) => {
        const errorResponse: OpenAIErrorResponse = {
          error: {
            message: 'Test error',
            type: 'test_error',
            code
          }
        };

        const result = parseSSEError(errorResponse);
        if (result) {
          expect(result.status).toBe(expectedStatus);
        }
      });
    });

    it('should return null for non-OpenAI errors', () => {
      const result = parseSSEError({ some: 'other error' });
      expect(result).toBeNull();
    });

    it('should handle error types without codes', () => {
      const errorResponse: OpenAIErrorResponse = {
        error: {
          message: 'Rate limit exceeded',
          type: 'rate_limit_error'
        }
      };

      const result = parseSSEError(errorResponse);
      if (result) {
        expect(result.status).toBe(429);
        expect(result.code).toBe('unknown_error');
      }
    });
  });

  describe('processSSEEvent', () => {
    it('should identify [DONE] events', () => {
      const result = processSSEEvent('[DONE]');
      expect(result).toEqual({ type: 'done' });
    });

    it('should identify OpenAI errors', () => {
      const errorResponse: OpenAIErrorResponse = {
        error: {
          message: 'Test error',
          type: 'test_error'
        }
      };

      const result = processSSEEvent(errorResponse);
      expect(result.type).toBe('error');
      expect(result.error).toBeDefined();
      if (result.error) {
        expect(result.error.message).toBe('Test error');
      }
    });

    it('should identify metrics events', () => {
      const metricsData = { metrics: { tokens: 100 }, content: 'hello' };
      const result = processSSEEvent(metricsData);
      
      expect(result.type).toBe('metrics');
      expect(result.data).toBe(metricsData);
    });

    it('should identify content events', () => {
      const contentData = { choices: [{ text: 'hello' }] };
      const result = processSSEEvent(contentData);
      
      expect(result.type).toBe('content');
      expect(result.data).toBe(contentData);
    });
  });

  describe('handleSSEConnectionError', () => {
    it('should handle fetch network errors', () => {
      const networkError = new TypeError('fetch failed');
      const result = handleSSEConnectionError(networkError);
      
      expect(result.status).toBe(503);
      expect(result.code).toBe('network_error');
      expect(result.title).toBe('Connection Failed');
      expect(result.isRecoverable).toBe(true);
      expect(result.severity).toBe('error');
    });

    it('should handle abort errors', () => {
      const abortError = new DOMException('Request aborted', 'AbortError');
      const result = handleSSEConnectionError(abortError);
      
      expect(result.status).toBe(499);
      expect(result.code).toBe('request_cancelled');
      expect(result.title).toBe('Request Cancelled');
      expect(result.isRecoverable).toBe(false);
      expect(result.severity).toBe('info');
    });

    it('should handle generic errors', () => {
      const genericError = new Error('Something went wrong');
      const result = handleSSEConnectionError(genericError);
      
      expect(result.status).toBe(500);
      expect(result.code).toBe('streaming_error');
      expect(result.title).toBe('Streaming Error');
      expect(result.message).toBe('Something went wrong');
      expect(result.isRecoverable).toBe(true);
    });

    it('should handle non-Error objects', () => {
      const unknownError = 'string error';
      const result = handleSSEConnectionError(unknownError);
      
      expect(result.status).toBe(500);
      expect(result.code).toBe('streaming_error');
      expect(result.message).toBe('An error occurred during streaming');
    });
  });
});