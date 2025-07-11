import { cleanupSDKClients } from '@/lib/server/sdk-config';
import { ConduitAdminClient } from '@knn_labs/conduit-admin-client';
import { ConduitCoreClient } from '@knn_labs/conduit-core-client';

describe('SDK Integration', () => {
  const originalEnv = process.env;

  beforeEach(() => {
    // Reset environment
    process.env = { ...originalEnv };
    // Clear module cache to reset singleton instances
    jest.resetModules();
    // Clean up any existing clients
    cleanupSDKClients();
  });

  afterEach(() => {
    process.env = originalEnv;
    cleanupSDKClients();
  });

  describe('SDK Configuration', () => {
    it('should use correct configuration values', () => {
      process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY = 'test-master-key';
      process.env.CONDUIT_ADMIN_LOGIN_PASSWORD = 'test-admin-password';
      process.env.NODE_ENV = 'development';
      process.env.CONDUIT_ADMIN_API_BASE_URL = 'http://localhost:5002';
      process.env.CONDUIT_API_BASE_URL = 'http://localhost:5000';

      // Re-import to get fresh config
      // eslint-disable-next-line @typescript-eslint/no-var-requires
      const { SDK_CONFIG: config } = require('@/lib/server/sdk-config');

      expect(config.masterKey).toBe('test-master-key');
      expect(config.adminBaseURL).toBe('http://localhost:5002');
      expect(config.coreBaseURL).toBe('http://localhost:5000');
      expect(config.timeout).toBe(30000);
      expect(config.maxRetries).toBe(3);
      expect(config.signalR.enabled).toBe(false);
    });

    it('should use production URLs when NODE_ENV is production', () => {
      process.env.NODE_ENV = 'production';
      process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY = 'prod-key';
      process.env.CONDUIT_ADMIN_LOGIN_PASSWORD = 'prod-password';

      // eslint-disable-next-line @typescript-eslint/no-var-requires
      const { SDK_CONFIG: config } = require('@/lib/server/sdk-config');

      expect(config.adminBaseURL).toBe('http://admin:8080');
      expect(config.coreBaseURL).toBe('http://api:8080');
    });

    it('should use default URLs when environment variables are not set', () => {
      process.env.NODE_ENV = 'development';
      process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY = 'test-key';
      process.env.CONDUIT_ADMIN_LOGIN_PASSWORD = 'test-password';
      delete process.env.CONDUIT_ADMIN_API_BASE_URL;
      delete process.env.CONDUIT_API_BASE_URL;

      // eslint-disable-next-line @typescript-eslint/no-var-requires
      const { SDK_CONFIG: config } = require('@/lib/server/sdk-config');

      expect(config.adminBaseURL).toBe('http://localhost:5002');
      expect(config.coreBaseURL).toBe('http://localhost:5000');
    });
  });

  describe('SDK Client Creation', () => {
    beforeEach(() => {
      process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY = 'test-key';
      process.env.CONDUIT_ADMIN_LOGIN_PASSWORD = 'test-password';
    });

    it('should create singleton admin client', () => {
      const { getServerAdminClient: getClient } = require('@/lib/server/sdk-config');
      
      const client1 = getClient();
      const client2 = getClient();
      
      expect(client1).toBe(client2); // Same instance
      expect(client1).toBeInstanceOf(ConduitAdminClient);
    });

    it('should create singleton core client', () => {
      const { getServerCoreClient: getClient } = require('@/lib/server/sdk-config');
      
      const client1 = getClient();
      const client2 = getClient();
      
      expect(client1).toBe(client2); // Same instance
      expect(client1).toBeInstanceOf(ConduitCoreClient);
    });

    it('should validate environment variables when creating clients', () => {
      delete process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY;

      const { getServerAdminClient: getClient } = require('@/lib/server/sdk-config');

      expect(() => {
        getClient();
      }).toThrow('Missing required environment variable: CONDUIT_API_TO_API_BACKEND_AUTH_KEY');
    });

    it('should validate all required environment variables', () => {
      process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY = 'test-key';
      delete process.env.CONDUIT_ADMIN_LOGIN_PASSWORD;

      const { getServerAdminClient: getClient } = require('@/lib/server/sdk-config');

      expect(() => {
        getClient();
      }).toThrow('Missing required environment variable: CONDUIT_ADMIN_LOGIN_PASSWORD');
    });
  });

  describe('SDK Client Cleanup', () => {
    it('should properly cleanup clients', async () => {
      process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY = 'test-key';
      process.env.CONDUIT_ADMIN_LOGIN_PASSWORD = 'test-password';

      const { 
        getServerAdminClient, 
        getServerCoreClient, 
        cleanupSDKClients: cleanup 
      } = require('@/lib/server/sdk-config');

      // Create clients
      const adminClient1 = getServerAdminClient();
      const coreClient1 = getServerCoreClient();

      // Cleanup
      await cleanup();

      // Get new clients after cleanup
      const adminClient2 = getServerAdminClient();
      const coreClient2 = getServerCoreClient();

      // Should be different instances after cleanup
      expect(adminClient1).not.toBe(adminClient2);
      expect(coreClient1).not.toBe(coreClient2);
    });
  });

  describe('Backward Compatibility', () => {
    beforeEach(() => {
      process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY = 'test-key';
      process.env.CONDUIT_ADMIN_LOGIN_PASSWORD = 'test-password';
    });

    it('should work through re-exported functions in adminClient.ts', () => {
      const { getServerAdminClient } = require('@/lib/server/adminClient');
      const client = getServerAdminClient();
      
      expect(client).toBeInstanceOf(ConduitAdminClient);
    });

    it('should work through re-exported functions in coreClient.ts', () => {
      const { getServerCoreClient } = require('@/lib/server/coreClient');
      const client = getServerCoreClient();
      
      expect(client).toBeInstanceOf(ConduitCoreClient);
    });
  });

  describe('Environment Variable Lazy Evaluation', () => {
    it('should not throw error at import time when env vars are missing', () => {
      delete process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY;
      delete process.env.CONDUIT_ADMIN_LOGIN_PASSWORD;

      // This should not throw
      expect(() => {
        require('@/lib/server/sdk-config');
      }).not.toThrow();

      // But accessing config should work with empty strings
      // eslint-disable-next-line @typescript-eslint/no-var-requires
      const { SDK_CONFIG: config } = require('@/lib/server/sdk-config');
      expect(config.masterKey).toBe('');
    });

    it('should only validate env vars when creating clients', () => {
      delete process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY;

      const { SDK_CONFIG: config, getServerAdminClient } = require('@/lib/server/sdk-config');
      
      // Accessing config should work
      expect(config.masterKey).toBe('');
      
      // But creating client should fail
      expect(() => {
        getServerAdminClient();
      }).toThrow('Missing required environment variable');
    });
  });

  describe('SDK Client Configuration', () => {
    beforeEach(() => {
      process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY = 'test-key';
      process.env.CONDUIT_ADMIN_LOGIN_PASSWORD = 'test-password';
      
      // Mock the SDK constructors
      jest.mock('@knn_labs/conduit-admin-client', () => ({
        ConduitAdminClient: jest.fn().mockImplementation((config) => ({
          _config: config,
        })),
      }));
      
      jest.mock('@knn_labs/conduit-core-client', () => ({
        ConduitCoreClient: jest.fn().mockImplementation((config) => ({
          _config: config,
        })),
      }));
    });

    it('should pass correct configuration to admin client', () => {
      const { getServerAdminClient, SDK_CONFIG } = require('@/lib/server/sdk-config');
      const client = getServerAdminClient();

      expect(client._config).toEqual({
        masterKey: SDK_CONFIG.masterKey,
        adminApiUrl: SDK_CONFIG.adminBaseURL,
        options: {
          signalR: SDK_CONFIG.signalR,
        },
      });
    });

    it('should pass correct configuration to core client', () => {
      const { getServerCoreClient, SDK_CONFIG } = require('@/lib/server/sdk-config');
      const client = getServerCoreClient();

      expect(client._config).toEqual({
        apiKey: SDK_CONFIG.masterKey,
        baseURL: SDK_CONFIG.coreBaseURL,
        signalR: SDK_CONFIG.signalR,
      });
    });
  });
});