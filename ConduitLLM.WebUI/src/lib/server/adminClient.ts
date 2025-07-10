import { ConduitAdminClient } from '@knn_labs/conduit-admin-client';
import { config } from '@/config/environment';

let adminClient: ConduitAdminClient | null = null;

/**
 * Get or create a singleton Admin SDK client for server-side use
 * Uses internal Docker networking in production
 */
export function getServerAdminClient(): ConduitAdminClient {
  if (!adminClient) {
    if (!config.auth.masterKey) {
      throw new Error('CONDUIT_WEBUI_AUTH_KEY is not configured');
    }

    // Use internal Docker service name for server-side connections
    const adminUrl = process.env.NODE_ENV === 'production' 
      ? 'http://admin:8080'  // Docker service name
      : process.env.ADMIN_API_URL || 'http://localhost:8080';

    adminClient = new ConduitAdminClient({
      masterKey: config.auth.masterKey,
      adminApiUrl: adminUrl,
      options: {
        // Disable SignalR completely - we're using simple fetch() instead
        signalR: {
          enabled: false
        }
      }
    });
  }

  return adminClient;
}

/**
 * Initialize the admin client
 * Should be called on server startup
 */
export async function initializeAdminClient(): Promise<void> {
  try {
    const client = getServerAdminClient();
    console.log('[AdminClient] Initialized successfully');
  } catch (error) {
    console.error('[AdminClient] Failed to initialize:', error);
    throw error;
  }
}

/**
 * Cleanup admin client connections
 * Should be called on server shutdown
 */
export async function cleanupAdminClient(): Promise<void> {
  if (adminClient) {
    try {
      adminClient = null;
      console.log('[AdminClient] Cleaned up successfully');
    } catch (error) {
      console.error('[AdminClient] Cleanup error:', error);
    }
  }
}