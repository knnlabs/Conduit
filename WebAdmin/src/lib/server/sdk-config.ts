import { ConduitAdminClient } from '@/lib/admin-api';
import { ConduitGatewayClient } from '@/lib/gateway-api';

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
  timeout: 60000, // Increased from 30s to 60s for better reliability in docker environment
  maxRetries: 3,
  
  // Disable SignalR for server-side usage
  signalR: {
    enabled: false
  }
} as const;

// Singleton instances
let adminClient: ConduitAdminClient | null = null;
let gatewayClient: InstanceType<typeof ConduitGatewayClient> | null = null;
let webAdminVirtualKey: string | null = null;

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

export async function getServerGatewayClient(): Promise<InstanceType<typeof ConduitGatewayClient>> {
  if (!gatewayClient || !webAdminVirtualKey) {
    // Validate environment at runtime
    validateEnvironment();

    // Get the WebAdmin's virtual key - this will auto-create it with $1000 if it doesn't exist
    if (!webAdminVirtualKey) {
      try {
        console.warn('[SDK] Getting or creating WebAdmin virtual key...');
        const adminClient = getServerAdminClient();
        // Use the SystemService's getWebAdminVirtualKey method which auto-creates with $1000
        webAdminVirtualKey = await adminClient.system.getWebAdminVirtualKey();
        console.warn('[SDK] WebAdmin virtual key obtained successfully');
      } catch (error) {
        console.error('[SDK] Failed to get or create WebAdmin virtual key:', error);
        throw new Error('Failed to retrieve or create WebAdmin virtual key. Ensure the database is accessible and the Admin API is running.');
      }
    }

    gatewayClient = new ConduitGatewayClient({
      apiKey: webAdminVirtualKey,
      baseURL: SDK_CONFIG.coreBaseURL,
      signalR: SDK_CONFIG.signalR,
    });
  }
  return gatewayClient;
}

// Backward-compatible alias
export const getServerCoreClient = getServerGatewayClient;

/**
 * Initialize SDK clients
 * Should be called on server startup
 */
export async function initializeSDKClients(): Promise<void> {
  try {
    getServerAdminClient();
    await getServerGatewayClient();
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
  gatewayClient = null;
  webAdminVirtualKey = null;
  console.error('[SDK] Clients cleaned up successfully');
}