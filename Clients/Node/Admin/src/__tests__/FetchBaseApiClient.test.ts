import { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import { ConduitError } from '../utils/errors';
import type { ApiClientConfig } from '../client/types';

// Mock fetch globally
global.fetch = jest.fn();

// Test implementation of FetchBaseApiClient
class TestApiClient extends FetchBaseApiClient {
  // Expose protected methods for testing
  public testRequest<T>(url: string, options?: any): Promise<T> {
    return this.request<T>(url, options);
  }
  
  public testGet<T>(url: string, options?: any): Promise<T> {
    return this.get<T>(url, options);
  }
  
  public testPost<T, R>(url: string, data?: R, options?: any): Promise<T> {
    return this.post<T, R>(url, data, options);
  }
}

describe('FetchBaseApiClient', () => {
  let client: TestApiClient;
  let mockFetch: jest.MockedFunction<typeof fetch>;

  beforeEach(() => {
    const config: ApiClientConfig = {
      masterKey: 'test-master-key',
      baseUrl: 'https://admin.api.test.com',
      timeout: 30000,
    };
    
    client = new TestApiClient(config);
    mockFetch = global.fetch as jest.MockedFunction<typeof fetch>;
    mockFetch.mockClear();
  });

  describe('Basic Requests', () => {
    it('should make a successful GET request', async () => {
      const mockResponse = { data: 'test' };
      mockFetch.mockResolvedValueOnce(
        new Response(JSON.stringify(mockResponse), {
          status: 200,
          headers: { 'content-type': 'application/json' },
        })
      );

      const result = await client.testGet('/test');

      expect(mockFetch).toHaveBeenCalledWith(
        'https://admin.api.test.com/test',
        expect.objectContaining({
          method: 'GET',
          headers: expect.objectContaining({
            'X-API-Key': 'test-master-key',
            'Content-Type': 'application/json',
          }),
        })
      );
      expect(result).toEqual(mockResponse);
    });

    it('should make a successful POST request with body', async () => {
      const requestBody = { name: 'test' };
      const mockResponse = { id: 1, name: 'test' };
      
      mockFetch.mockResolvedValueOnce(
        new Response(JSON.stringify(mockResponse), {
          status: 200,
          headers: { 'content-type': 'application/json' },
        })
      );

      const result = await client.testPost('/test', requestBody);

      expect(mockFetch).toHaveBeenCalledWith(
        'https://admin.api.test.com/test',
        expect.objectContaining({
          method: 'POST',
          body: JSON.stringify(requestBody),
          headers: expect.objectContaining({
            'X-API-Key': 'test-master-key',
          }),
        })
      );
      expect(result).toEqual(mockResponse);
    });
  });

  describe('GET with parameters', () => {
    it('should handle GET with query parameters', async () => {
      mockFetch.mockResolvedValueOnce(
        new Response('{}', {
          status: 200,
          headers: { 'content-type': 'application/json' },
        })
      );

      await client.testGet('/test', { page: 1, limit: 10 });

      expect(mockFetch).toHaveBeenCalledWith(
        'https://admin.api.test.com/test?page=1&limit=10',
        expect.any(Object)
      );
    });

    it('should handle GET with options (not params)', async () => {
      mockFetch.mockResolvedValueOnce(
        new Response('{}', {
          status: 200,
          headers: { 'content-type': 'application/json' },
        })
      );

      await client.testGet('/test', { headers: { 'X-Custom': 'value' }, timeout: 5000 });

      expect(mockFetch).toHaveBeenCalledWith(
        'https://admin.api.test.com/test',
        expect.objectContaining({
          headers: expect.objectContaining({
            'X-Custom': 'value',
          }),
        })
      );
    });

    it('should handle GET with params and options (3-arg form)', async () => {
      mockFetch.mockResolvedValueOnce(
        new Response('{}', {
          status: 200,
          headers: { 'content-type': 'application/json' },
        })
      );

      await (client as any).get('/test', { page: 1 }, { headers: { 'X-Custom': 'value' } });

      expect(mockFetch).toHaveBeenCalledWith(
        'https://admin.api.test.com/test?page=1',
        expect.objectContaining({
          headers: expect.objectContaining({
            'X-Custom': 'value',
          }),
        })
      );
    });
  });

  describe('Error Handling', () => {
    it('should handle 400 validation errors', async () => {
      mockFetch.mockResolvedValueOnce(
        new Response(JSON.stringify({ error: 'Invalid request' }), {
          status: 400,
          headers: { 'content-type': 'application/json' },
        })
      );

      await expect(client.testRequest('/test')).rejects.toThrow(ConduitError);
    });

    it('should handle 401 authentication errors', async () => {
      mockFetch.mockResolvedValueOnce(
        new Response(JSON.stringify({ error: 'Unauthorized' }), {
          status: 401,
          headers: { 'content-type': 'application/json' },
        })
      );

      await expect(client.testRequest('/test')).rejects.toThrow('Unauthorized');
    });

    it('should handle network errors', async () => {
      mockFetch.mockRejectedValueOnce(new Error('Network error'));

      await expect(client.testRequest('/test')).rejects.toThrow('Network error');
    });
  });

  describe('Retry Logic', () => {
    it('should retry on network errors', async () => {
      const retryClient = new TestApiClient({
        masterKey: 'test-key',
        baseUrl: 'https://api.test.com',
        retries: {
          maxRetries: 2,
          retryDelay: 100,
          retryCondition: (error: any) => {
            return error instanceof Error && error.message.includes('network');
          },
        },
      });

      // First call fails
      mockFetch.mockRejectedValueOnce(new Error('network error'));
      
      // Second call succeeds
      mockFetch.mockResolvedValueOnce(
        new Response(JSON.stringify({ success: true }), {
          status: 200,
          headers: { 'content-type': 'application/json' },
        })
      );

      const result = await retryClient.testRequest('/test');
      
      expect(mockFetch).toHaveBeenCalledTimes(2);
      expect(result).toEqual({ success: true });
    });

    it('should respect max retries', async () => {
      const retryClient = new TestApiClient({
        masterKey: 'test-key',
        baseUrl: 'https://api.test.com',
        retries: {
          maxRetries: 1,
          retryDelay: 100,
          retryCondition: () => true,
        },
      });

      // All calls fail
      mockFetch.mockRejectedValue(new Error('network error'));

      await expect(retryClient.testRequest('/test')).rejects.toThrow('network error');
      expect(mockFetch).toHaveBeenCalledTimes(2); // Initial + 1 retry
    });
  });

  describe('Response Type Handling', () => {
    it('should handle JSON responses', async () => {
      const mockData = { test: 'data' };
      mockFetch.mockResolvedValueOnce(
        new Response(JSON.stringify(mockData), {
          status: 200,
          headers: { 'content-type': 'application/json' },
        })
      );

      const result = await client.testRequest('/test');
      expect(result).toEqual(mockData);
    });

    it('should handle text responses', async () => {
      const mockText = 'plain text response';
      mockFetch.mockResolvedValueOnce(
        new Response(mockText, {
          status: 200,
          headers: { 'content-type': 'text/plain' },
        })
      );

      const result = await client.testRequest<string>('/test', {
        responseType: 'text',
      });
      expect(result).toBe(mockText);
    });

    it('should handle empty responses', async () => {
      mockFetch.mockResolvedValueOnce(
        new Response(null, {
          status: 204,
          headers: { 'content-length': '0' },
        })
      );

      const result = await client.testRequest('/test');
      expect(result).toBeUndefined();
    });
  });

  describe('Callbacks', () => {
    it('should call onRequest callback', async () => {
      const onRequest = jest.fn();
      const callbackClient = new TestApiClient({
        masterKey: 'test-key',
        baseUrl: 'https://api.test.com',
        onRequest,
      });

      mockFetch.mockResolvedValueOnce(
        new Response('{}', {
          status: 200,
          headers: { 'content-type': 'application/json' },
        })
      );

      await callbackClient.testRequest('/test');
      
      expect(onRequest).toHaveBeenCalledWith(
        expect.objectContaining({
          method: 'GET',
          url: 'https://api.test.com/test',
        })
      );
    });

    it('should call onResponse callback', async () => {
      const onResponse = jest.fn();
      const callbackClient = new TestApiClient({
        masterKey: 'test-key',
        baseUrl: 'https://api.test.com',
        onResponse,
      });

      mockFetch.mockResolvedValueOnce(
        new Response('{"data":"test"}', {
          status: 200,
          headers: { 'content-type': 'application/json' },
        })
      );

      await callbackClient.testRequest('/test');
      
      expect(onResponse).toHaveBeenCalledWith(
        expect.objectContaining({
          status: 200,
          statusText: '',
        })
      );
    });

    it('should call onError callback', async () => {
      const onError = jest.fn();
      const callbackClient = new TestApiClient({
        masterKey: 'test-key',
        baseUrl: 'https://api.test.com',
        retries: { maxRetries: 0, retryDelay: 1000 },
        onError,
      });

      mockFetch.mockRejectedValueOnce(new Error('Network error'));

      try {
        await callbackClient.testRequest('/test');
      } catch (e) {
        // Expected
      }
      
      expect(onError).toHaveBeenCalledWith(expect.any(Error));
    });
  });

  describe('Caching', () => {
    it('should cache GET requests when cache is provided', async () => {
      const mockCache = {
        get: jest.fn().mockResolvedValue(null),
        set: jest.fn().mockResolvedValue(undefined),
        delete: jest.fn(),
        clear: jest.fn(),
      };

      const cachedClient = new TestApiClient({
        masterKey: 'test-key',
        baseUrl: 'https://api.test.com',
        cache: mockCache,
      });

      const mockData = { cached: 'data' };
      mockFetch.mockResolvedValueOnce(
        new Response(JSON.stringify(mockData), {
          status: 200,
          headers: { 'content-type': 'application/json' },
        })
      );

      // Use withCache method
      const result = await (cachedClient as any).withCache(
        'test-key',
        () => cachedClient.testGet('/test'),
        3600
      );

      expect(mockCache.get).toHaveBeenCalledWith('test-key');
      expect(mockCache.set).toHaveBeenCalledWith('test-key', mockData, 3600);
      expect(result).toEqual(mockData);
    });

    it('should return cached value when available', async () => {
      const cachedData = { cached: 'value' };
      const mockCache = {
        get: jest.fn().mockResolvedValue(cachedData),
        set: jest.fn(),
        delete: jest.fn(),
        clear: jest.fn(),
      };

      const cachedClient = new TestApiClient({
        masterKey: 'test-key',
        baseUrl: 'https://api.test.com',
        cache: mockCache,
      });

      const result = await (cachedClient as any).withCache(
        'test-key',
        () => cachedClient.testGet('/test'),
        3600
      );

      expect(mockCache.get).toHaveBeenCalledWith('test-key');
      expect(mockFetch).not.toHaveBeenCalled();
      expect(result).toEqual(cachedData);
    });
  });
});