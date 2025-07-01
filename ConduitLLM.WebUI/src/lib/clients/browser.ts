import { ConduitCoreClient } from '@knn_labs/conduit-core-client';
import { logger } from '@/lib/utils/logging';

// Browser-side client configuration
interface BrowserClientConfig {
  virtualKey: string;
  signalREnabled?: boolean;
  autoConnect?: boolean;
  onConnectionChange?: (connected: boolean) => void;
}

// Create a browser-optimized Core client with SignalR support
export function createBrowserClient(config: BrowserClientConfig): ConduitCoreClient {
  const baseUrl = process.env.NEXT_PUBLIC_CONDUIT_CORE_API_URL;
  
  if (!baseUrl) {
    throw new Error('Core API URL not configured');
  }

  logger.info('Creating browser Core client', {
    url: baseUrl,
    signalREnabled: config.signalREnabled ?? true,
  });

  return new ConduitCoreClient({
    baseURL: baseUrl,
    apiKey: config.virtualKey,
    timeout: 30000,
    maxRetries: 3,
    // TODO: SDK does not yet support:
    // - retryDelay
    // - signalR configuration
  });
}

// Hook-friendly client factory for React components
export function useBrowserClient(virtualKey: string | null): ConduitCoreClient | null {
  if (!virtualKey) return null;

  // This is a simplified version - in production, you'd want to:
  // 1. Use React.useMemo to memoize the client
  // 2. Handle cleanup on unmount
  // 3. Integrate with your state management
  return createBrowserClient({ virtualKey });
}