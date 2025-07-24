import { ConduitAdminClient } from '@knn_labs/conduit-admin-client';
import { ConduitCoreClient } from '@knn_labs/conduit-core-client';

// Validate required environment variables at runtime
function validateEnvironment() {
  const requiredEnvVars = {
    CONDUIT_API_TO_API_BACKEND_AUTH_KEY: process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY,
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
    return process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY ?? '';
  },
  
  // Base URLs
  get adminBaseURL() {
    return process.env.CONDUIT_ADMIN_API_BASE_URL ?? 'http://localhost:5002';
  },
    
  get coreBaseURL() {
    return process.env.CONDUIT_API_BASE_URL ?? 'http://localhost:5000';
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
let webuiVirtualKey: string | null = null;

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

export async function getServerCoreClient(): Promise<ConduitCoreClient> {
  if (!coreClient || !webuiVirtualKey) {
    // Validate environment at runtime
    validateEnvironment();
    
    // Get the WebUI's virtual key from the Admin API
    if (!webuiVirtualKey) {
      try {
        const adminApi = getServerAdminClient();
        webuiVirtualKey = await adminApi.system.getWebUIVirtualKey();
        console.error('[SDK] WebUI virtual key fetched successfully');
      } catch (error) {
        console.error('[SDK] Failed to fetch WebUI virtual key, falling back to master key:', error);
        // Fallback to master key if we can't get the virtual key
        webuiVirtualKey = SDK_CONFIG.masterKey;
      }
    }
    
    coreClient = new ConduitCoreClient({
      apiKey: webuiVirtualKey,
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
    await getServerCoreClient();
    console.error('[SDK] Clients initialized successfully');
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
  webuiVirtualKey = null;
  console.error('[SDK] Clients cleaned up successfully');
}