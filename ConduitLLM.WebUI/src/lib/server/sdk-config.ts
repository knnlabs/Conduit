import { ConduitAdminClient } from '@knn_labs/conduit-admin-client';
import { ConduitCoreClient } from '@knn_labs/conduit-core-client';

// Validate required environment variables at runtime
function validateEnvironment() {
  const requiredEnvVars = {
    CONDUIT_API_TO_API_BACKEND_AUTH_KEY: process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY,
    CONDUIT_ADMIN_LOGIN_PASSWORD: process.env.CONDUIT_ADMIN_LOGIN_PASSWORD,
  };

  for (const [key, value] of Object.entries(requiredEnvVars)) {
    if (!value) {
      throw new Error(`Missing required environment variable: ${key}`);
    }
  }
}

// Centralized configuration - lazy evaluation
export const SDK_CONFIG = {
  // Master key for backend communication
  get masterKey() { 
    return process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY || '';
  },
  
  // Base URLs
  get adminBaseURL() {
    return process.env.NODE_ENV === 'production' 
      ? 'http://admin:8080' 
      : (process.env.CONDUIT_ADMIN_API_BASE_URL || 'http://localhost:5002');
  },
    
  get coreBaseURL() {
    return process.env.NODE_ENV === 'production' 
      ? 'http://api:8080' 
      : (process.env.CONDUIT_API_BASE_URL || 'http://localhost:5000');
  },
  
  // Common settings
  timeout: 30000,
  maxRetries: 3,
  
  // Disable SignalR for server-side usage
  signalR: {
    enabled: false
  }
} as const;

// Singleton instances
let adminClient: ConduitAdminClient | null = null;
let coreClient: ConduitCoreClient | null = null;

export function getServerAdminClient(): ConduitAdminClient {
  if (!adminClient) {
    // Validate environment at runtime
    validateEnvironment();
    
    adminClient = new ConduitAdminClient({
      baseUrl: SDK_CONFIG.adminBaseURL,
      masterKey: SDK_CONFIG.masterKey,
      timeout: SDK_CONFIG.timeout,
      retries: SDK_CONFIG.maxRetries,
    });
  }
  return adminClient;
}

export function getServerCoreClient(): ConduitCoreClient {
  if (!coreClient) {
    // Validate environment at runtime
    validateEnvironment();
    
    coreClient = new ConduitCoreClient({
      apiKey: SDK_CONFIG.masterKey,
      baseURL: SDK_CONFIG.coreBaseURL,
      signalR: SDK_CONFIG.signalR,
    });
  }
  return coreClient;
}

/**
 * Initialize SDK clients
 * Should be called on server startup
 */
export async function initializeSDKClients(): Promise<void> {
  try {
    getServerAdminClient();
    getServerCoreClient();
    console.log('[SDK] Clients initialized successfully');
  } catch (error) {
    console.error('[SDK] Failed to initialize clients:', error);
    throw error;
  }
}

/**
 * Cleanup SDK clients
 * Should be called on server shutdown
 */
export async function cleanupSDKClients(): Promise<void> {
  adminClient = null;
  coreClient = null;
  console.log('[SDK] Clients cleaned up successfully');
}