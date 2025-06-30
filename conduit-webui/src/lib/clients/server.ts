import { ConduitAdminClient } from '@knn_labs/conduit-admin-client';
import { ConduitCoreClient } from '@knn_labs/conduit-core-client';

// Server-side Admin client - uses master key from environment
export function createServerAdminClient(): ConduitAdminClient {
  const masterKey = process.env.CONDUIT_MASTER_KEY;
  const baseUrl = process.env.NEXT_PUBLIC_CONDUIT_ADMIN_API_URL;
  
  if (!masterKey) {
    throw new Error('CONDUIT_MASTER_KEY environment variable not configured');
  }
  
  if (!baseUrl) {
    throw new Error('NEXT_PUBLIC_CONDUIT_ADMIN_API_URL environment variable not configured');
  }

  return new ConduitAdminClient({
    adminApiUrl: baseUrl,
    masterKey,
    options: {
      timeout: 30000,
    }
  });
}

// Server-side Core client - uses virtual key for specific operations
export function createServerCoreClient(virtualKey: string): ConduitCoreClient {
  const baseUrl = process.env.NEXT_PUBLIC_CONDUIT_CORE_API_URL;
  
  if (!baseUrl) {
    throw new Error('NEXT_PUBLIC_CONDUIT_CORE_API_URL environment variable not configured');
  }

  return new ConduitCoreClient({
    baseURL: baseUrl,
    apiKey: virtualKey,
    timeout: 30000,
  });
}

// Singleton instance for server-side use
let serverAdminClient: ConduitAdminClient | null = null;

export function getServerAdminClient(): ConduitAdminClient {
  if (!serverAdminClient) {
    serverAdminClient = createServerAdminClient();
  }
  return serverAdminClient;
}

// Error handling for server-side API calls
export function handleServerError(error: any, context: string): never {
  console.error(`Server API error (${context}):`, error);
  
  // Return user-friendly error without exposing internal details
  const message = error?.status === 404 
    ? 'Resource not found'
    : error?.status >= 500 
    ? 'Internal server error'
    : error?.message || 'API request failed';
    
  throw new Error(message);
}