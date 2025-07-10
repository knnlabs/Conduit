import { ConduitCoreClient } from '@knn_labs/conduit-core-client';
import { config } from '@/config/environment';

let coreClient: ConduitCoreClient | null = null;

/**
 * Get or create a singleton Core SDK client for server-side use
 * Uses internal Docker networking in production
 */
export function getServerCoreClient(): ConduitCoreClient {
  if (!coreClient) {
    if (!config.auth.masterKey) {
      throw new Error('CONDUIT_API_TO_API_BACKEND_AUTH_KEY is not configured');
    }

    // Use internal Docker service name for server-side connections
    const coreUrl = process.env.NODE_ENV === 'production' 
      ? 'http://api:8080'  // Docker service name
      : process.env.CONDUIT_API_BASE_URL || 'http://localhost:5000';

    coreClient = new ConduitCoreClient({
      apiKey: config.auth.masterKey, // Core API uses virtual keys for auth
      baseURL: coreUrl,
      signalR: {
        enabled: false // Disable SignalR completely - we're using simple fetch() instead
      }
    });
  }

  return coreClient;
}

/**
 * Initialize the core client
 * Should be called on server startup
 */
export async function initializeCoreClient(): Promise<void> {
  try {
    const client = getServerCoreClient();
    console.log('[CoreClient] Initialized successfully');
  } catch (error) {
    console.error('[CoreClient] Failed to initialize:', error);
    throw error;
  }
}

/**
 * Cleanup core client connections
 * Should be called on server shutdown
 */
export async function cleanupCoreClient(): Promise<void> {
  if (coreClient) {
    try {
      coreClient = null;
      console.log('[CoreClient] Cleaned up successfully');
    } catch (error) {
      console.error('[CoreClient] Cleanup error:', error);
    }
  }
}