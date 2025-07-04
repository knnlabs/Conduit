import { ConduitAdminClient } from '@knn_labs/conduit-admin-client';
import { ConduitCoreClient } from '@knn_labs/conduit-core-client';
import { logger } from '@/lib/utils/logging';

// Configuration validation
function validateEnvironment() {
  const required = {
    CONDUIT_MASTER_KEY: process.env.CONDUIT_MASTER_KEY,
    // Use internal URLs for server-side API calls
    CONDUIT_ADMIN_API_BASE_URL: process.env.CONDUIT_ADMIN_API_BASE_URL || process.env.NEXT_PUBLIC_CONDUIT_ADMIN_API_URL,
    CONDUIT_API_BASE_URL: process.env.CONDUIT_API_BASE_URL || process.env.NEXT_PUBLIC_CONDUIT_CORE_API_URL,
  };

  const missing = Object.entries(required)
    .filter(([_, value]) => !value)
    .map(([key]) => key);

  if (missing.length > 0) {
    throw new Error(`Missing required environment variables: ${missing.join(', ')}`);
  }

  return required;
}

// SDK configuration with comprehensive options
interface SDKConfig {
  timeout?: number;
  retries?: number;
  retryDelay?: number;
  enableLogging?: boolean;
  logLevel?: 'debug' | 'info' | 'warn' | 'error';
}

const defaultSDKConfig: SDKConfig = {
  timeout: parseInt(process.env.CONDUIT_SDK_TIMEOUT || '30000'),
  retries: parseInt(process.env.CONDUIT_SDK_RETRIES || '3'),
  retryDelay: parseInt(process.env.CONDUIT_SDK_RETRY_DELAY || '1000'),
  enableLogging: process.env.CONDUIT_SDK_LOGGING === 'true',
  logLevel: (process.env.CONDUIT_SDK_LOG_LEVEL as SDKConfig['logLevel']) || 'warn',
};

// Server-side Admin client - uses master key from environment
export function createServerAdminClient(config?: Partial<SDKConfig>): ConduitAdminClient {
  const env = validateEnvironment();
  const finalConfig = { ...defaultSDKConfig, ...config };

  logger.info('Creating Conduit Admin client', { 
    url: env.CONDUIT_ADMIN_API_BASE_URL,
    config: finalConfig 
  });

  return new ConduitAdminClient({
    adminApiUrl: env.CONDUIT_ADMIN_API_BASE_URL!,
    masterKey: env.CONDUIT_MASTER_KEY!,
    options: {
      timeout: finalConfig.timeout,
      retries: finalConfig.retries,
      // TODO: SDK does not yet support:
      // - retryDelay
      // - onError, onRequest, onResponse callbacks
    }
  });
}

// Server-side Core client - uses virtual key for specific operations
export function createServerCoreClient(virtualKey: string, config?: Partial<SDKConfig>): ConduitCoreClient {
  const env = validateEnvironment();
  const finalConfig = { ...defaultSDKConfig, ...config };
  
  logger.info('Creating Conduit Core client', { 
    url: env.CONDUIT_API_BASE_URL,
    keyPrefix: virtualKey.substring(0, 8) + '...',
    config: finalConfig 
  });

  return new ConduitCoreClient({
    baseURL: env.CONDUIT_API_BASE_URL!,
    apiKey: virtualKey,
    timeout: finalConfig.timeout,
    maxRetries: finalConfig.retries,
    // TODO: SDK does not yet support:
    // - retryDelay
    // - signalR configuration
    // - onError, onRequest, onResponse callbacks
  });
}

// Client instance management with connection pooling
const clientPool = {
  admin: null as ConduitAdminClient | null,
  core: new Map<string, { client: ConduitCoreClient; lastUsed: number }>(),
};

// Cleanup stale Core clients after 5 minutes of inactivity
const CORE_CLIENT_TTL = 5 * 60 * 1000;
setInterval(() => {
  const now = Date.now();
  for (const [key, value] of clientPool.core.entries()) {
    if (now - value.lastUsed > CORE_CLIENT_TTL) {
      logger.debug('Removing stale Core client', { key });
      clientPool.core.delete(key);
    }
  }
}, 60 * 1000); // Check every minute

// Get or create Admin client singleton
export function getServerAdminClient(config?: Partial<SDKConfig>): ConduitAdminClient {
  if (!clientPool.admin) {
    clientPool.admin = createServerAdminClient(config);
  }
  return clientPool.admin;
}

// Get or create Core client with connection pooling
export function getServerCoreClient(virtualKey: string, config?: Partial<SDKConfig>): ConduitCoreClient {
  const keyHash = virtualKey.substring(0, 16); // Use prefix as cache key
  
  const existing = clientPool.core.get(keyHash);
  if (existing) {
    existing.lastUsed = Date.now();
    return existing.client;
  }
  
  const client = createServerCoreClient(virtualKey, config);
  clientPool.core.set(keyHash, { client, lastUsed: Date.now() });
  
  // Limit pool size to prevent memory leaks
  if (clientPool.core.size > 100) {
    const oldest = Array.from(clientPool.core.entries())
      .sort(([, a], [, b]) => a.lastUsed - b.lastUsed)[0];
    if (oldest) {
      clientPool.core.delete(oldest[0]);
    }
  }
  
  return client;
}

// Create a Core client for browser usage with SignalR
export function createBrowserCoreClient(virtualKey: string, config?: Partial<SDKConfig>): ConduitCoreClient {
  const baseUrl = process.env.NEXT_PUBLIC_CONDUIT_CORE_API_URL;
  
  if (!baseUrl) {
    throw new Error('NEXT_PUBLIC_CONDUIT_CORE_API_URL environment variable not configured');
  }

  const finalConfig = { ...defaultSDKConfig, ...config };

  return new ConduitCoreClient({
    baseURL: baseUrl,
    apiKey: virtualKey,
    timeout: finalConfig.timeout,
    maxRetries: finalConfig.retries,
    // TODO: SDK does not yet support:
    // - retryDelay
    // - signalR configuration
  });
}

// Invalidate cached clients (useful for configuration changes)
export function invalidateClientCache(type: 'admin' | 'core' | 'all', virtualKey?: string) {
  if (type === 'admin' || type === 'all') {
    clientPool.admin = null;
  }
  
  if (type === 'core' || type === 'all') {
    if (virtualKey) {
      const keyHash = virtualKey.substring(0, 16);
      clientPool.core.delete(keyHash);
    } else {
      clientPool.core.clear();
    }
  }
}

// Health check for SDK clients
export async function checkClientHealth(): Promise<{
  admin: boolean;
  core: boolean;
  details: Record<string, unknown>;
}> {
  const results = {
    admin: false,
    core: false,
    details: {} as Record<string, unknown>,
  };

  try {
    const adminClient = getServerAdminClient();
    await adminClient.system.getHealth();
    results.admin = true;
  } catch (error) {
    results.details.adminError = error;
    logger.error('Admin client health check failed', { error });
  }

  try {
    // Use a test key for Core health check if available
    const testKey = process.env.CONDUIT_TEST_VIRTUAL_KEY;
    if (testKey) {
      const _coreClient = getServerCoreClient(testKey);
      // TODO: SDK does not yet support health.check()
      // await coreClient.health.check();
      results.core = true;
    }
  } catch (error) {
    results.details.coreError = error;
    logger.error('Core client health check failed', { error });
  }

  return results;
}