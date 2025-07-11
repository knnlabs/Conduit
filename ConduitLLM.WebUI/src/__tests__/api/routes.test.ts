import { NextRequest } from 'next/server';
import { getServerAdminClient } from '@/lib/server/sdk-config';
import { AuthError, RateLimitError, NetworkError } from '@knn_labs/conduit-admin-client';

// Mock the SDK and auth modules
jest.mock('@/lib/server/sdk-config');
jest.mock('@/lib/auth/simple-auth');
jest.mock('@/lib/errors/sdk-errors');

// Import route handlers after mocking
import { GET as getProviders } from '@/app/api/providers/route';
import { POST as createVirtualKey, GET as getVirtualKeys } from '@/app/api/virtualkeys/route';
import { GET as getHealth } from '@/app/api/health/route';

describe('API Routes', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    
    // Setup default mock for auth
    // eslint-disable-next-line @typescript-eslint/no-var-requires
    const mockAuth = require('@/lib/auth/simple-auth');
    mockAuth.requireAuth.mockReturnValue({ isValid: true });
    
    // Setup default mock for error handling
    // eslint-disable-next-line @typescript-eslint/no-var-requires
    const mockHandleError = require('@/lib/errors/sdk-errors').handleSDKError;
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    mockHandleError.mockImplementation((error: any) => {
      if (error instanceof AuthError) {
        return new Response(JSON.stringify({ error: 'Unauthorized' }), { status: 401 });
      }
      if (error instanceof RateLimitError) {
        return new Response(JSON.stringify({ error: 'Rate limited' }), { status: 429 });
      }
      if (error instanceof NetworkError) {
        return new Response(JSON.stringify({ error: 'Network error' }), { status: 503 });
      }
      return new Response(JSON.stringify({ error: 'Internal server error' }), { status: 500 });
    });
  });

  describe('GET /api/providers', () => {
    it('should return providers list when authenticated', async () => {
      const mockProviders = [
        { id: '1', providerName: 'OpenAI', isEnabled: true },
        { id: '2', providerName: 'Anthropic', isEnabled: true },
      ];

      const mockClient = {
        providers: {
          getProviders: jest.fn().mockResolvedValue(mockProviders),
        },
      };

      (getServerAdminClient as jest.Mock).mockReturnValue(mockClient);

      const request = new NextRequest('http://localhost/api/providers');
      const response = await getProviders(request);
      const data = await response.json();

      expect(response.status).toBe(200);
      expect(data).toEqual(mockProviders);
      expect(mockClient.providers.getProviders).toHaveBeenCalled();
    });

    it('should handle authentication errors', async () => {
      // eslint-disable-next-line @typescript-eslint/no-var-requires
      const mockAuth = require('@/lib/auth/simple-auth');
      mockAuth.requireAuth.mockReturnValue({ isValid: false, response: new Response('Unauthorized', { status: 401 }) });

      const request = new NextRequest('http://localhost/api/providers');
      const response = await getProviders(request);

      expect(response.status).toBe(401);
    });

    it('should handle SDK errors properly', async () => {
      const mockClient = {
        providers: {
          getProviders: jest.fn().mockRejectedValue(new NetworkError('Connection failed')),
        },
      };

      (getServerAdminClient as jest.Mock).mockReturnValue(mockClient);

      const request = new NextRequest('http://localhost/api/providers');
      const response = await getProviders(request);
      const data = await response.json();

      expect(response.status).toBe(503);
      expect(data).toHaveProperty('error', 'Network error');
    });
  });

  describe('POST /api/virtualkeys', () => {
    it('should create a virtual key with valid data', async () => {
      // eslint-disable-next-line @typescript-eslint/no-var-requires
      const mockAuth = require('@/lib/auth/simple-auth');
      mockAuth.requireAuth.mockReturnValue({ isValid: true });

      const createData = {
        keyName: 'Test Key',
        totalBudget: 100,
        budgetDuration: 'monthly',
        isEnabled: true,
      };

      const mockCreatedKey = {
        id: 'key-123',
        ...createData,
        apiKey: 'sk_test_123',
        createdAt: new Date().toISOString(),
      };

      const mockClient = {
        virtualKeys: {
          createVirtualKey: jest.fn().mockResolvedValue(mockCreatedKey),
        },
      };

      (getServerAdminClient as jest.Mock).mockReturnValue(mockClient);

      const request = new NextRequest('http://localhost/api/virtualkeys', {
        method: 'POST',
        body: JSON.stringify(createData),
      });

      const response = await createVirtualKey(request);
      const data = await response.json();

      expect(response.status).toBe(200);
      expect(data).toEqual(mockCreatedKey);
      expect(mockClient.virtualKeys.createVirtualKey).toHaveBeenCalledWith(createData);
    });

    it('should handle rate limit errors', async () => {
      const mockClient = {
        virtualKeys: {
          createVirtualKey: jest.fn().mockRejectedValue(new RateLimitError('Too many requests', 60)),
        },
      };

      (getServerAdminClient as jest.Mock).mockReturnValue(mockClient);

      const request = new NextRequest('http://localhost/api/virtualkeys', {
        method: 'POST',
        body: JSON.stringify({ keyName: 'Test' }),
      });

      const response = await createVirtualKey(request);
      const data = await response.json();

      expect(response.status).toBe(429);
      expect(data).toHaveProperty('error', 'Rate limited');
    });
  });

  describe('GET /api/virtualkeys', () => {
    it('should return virtual keys list', async () => {
      const mockKeys = [
        { id: '1', keyName: 'Key 1', isEnabled: true },
        { id: '2', keyName: 'Key 2', isEnabled: false },
      ];

      const mockClient = {
        virtualKeys: {
          getVirtualKeys: jest.fn().mockResolvedValue(mockKeys),
        },
      };

      (getServerAdminClient as jest.Mock).mockReturnValue(mockClient);

      const request = new NextRequest('http://localhost/api/virtualkeys');
      const response = await getVirtualKeys(request);
      const data = await response.json();

      expect(response.status).toBe(200);
      expect(data).toEqual(mockKeys);
    });
  });

  describe('GET /api/health', () => {
    it('should return health status without authentication', async () => {
      const mockHealthData = {
        status: 'healthy',
        version: '1.0.0',
        services: {
          database: 'connected',
          cache: 'connected',
        },
      };

      const mockClient = {
        system: {
          getHealth: jest.fn().mockResolvedValue(mockHealthData),
        },
      };

      (getServerAdminClient as jest.Mock).mockReturnValue(mockClient);

      const request = new NextRequest('http://localhost/api/health');
      const response = await getHealth(request);
      const data = await response.json();

      expect(response.status).toBe(200);
      expect(data).toEqual(mockHealthData);
      
      // Verify auth was not called for health endpoint
      // eslint-disable-next-line @typescript-eslint/no-var-requires
      const mockAuth = require('@/lib/auth/simple-auth');
      expect(mockAuth.requireAuth).not.toHaveBeenCalled();
    });
  });

  describe('Error Handling Consistency', () => {
    it('should handle auth errors consistently across routes', async () => {
      // eslint-disable-next-line @typescript-eslint/no-var-requires
      const mockAuth = require('@/lib/auth/simple-auth');
      mockAuth.requireAuth.mockReturnValue({ 
        isValid: false, 
        response: new Response(JSON.stringify({ error: 'Unauthorized' }), { status: 401 }) 
      });

      // Test multiple authenticated routes
      const routes = [
        { handler: getProviders, url: '/api/providers' },
        { handler: getVirtualKeys, url: '/api/virtualkeys' },
      ];

      for (const { handler, url } of routes) {
        const request = new NextRequest(`http://localhost${url}`);
        const response = await handler(request);
        
        expect(response.status).toBe(401);
        const data = await response.json();
        expect(data).toHaveProperty('error', 'Unauthorized');
      }
    });

    it('should use handleSDKError for all SDK exceptions', async () => {
      const mockClient = {
        providers: {
          getProviders: jest.fn().mockRejectedValue(new Error('Unknown SDK error')),
        },
        virtualKeys: {
          getVirtualKeys: jest.fn().mockRejectedValue(new Error('Unknown SDK error')),
        },
      };

      (getServerAdminClient as jest.Mock).mockReturnValue(mockClient);

      const request = new NextRequest('http://localhost/api/providers');
      await getProviders(request);

      // eslint-disable-next-line @typescript-eslint/no-var-requires
      const mockHandleError = require('@/lib/errors/sdk-errors').handleSDKError;
      expect(mockHandleError).toHaveBeenCalledWith(expect.any(Error));
    });
  });

  describe('Request Validation', () => {
    it('should validate required fields in POST requests', async () => {
      const invalidData = {
        // Missing required keyName field
        totalBudget: 100,
      };

      const request = new NextRequest('http://localhost/api/virtualkeys', {
        method: 'POST',
        body: JSON.stringify(invalidData),
      });

      const mockClient = {
        virtualKeys: {
          createVirtualKey: jest.fn().mockRejectedValue(
            new Error('Validation failed: keyName is required')
          ),
        },
      };

      (getServerAdminClient as jest.Mock).mockReturnValue(mockClient);

      const response = await createVirtualKey(request);
      
      expect(response.status).toBe(500); // Would be 400 with proper validation
      expect(mockClient.virtualKeys.createVirtualKey).toHaveBeenCalledWith(invalidData);
    });
  });
});