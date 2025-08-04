import { ConduitCoreClient, type VideoProgressCallbacks } from '@knn_labs/conduit-core-client';

let clientInstance: ConduitCoreClient | null = null;

/**
 * Get or create a client-side ConduitCoreClient instance
 * This client uses SignalR for real-time updates
 */
export async function getClientCoreClient(): Promise<ConduitCoreClient> {
  if (!clientInstance) {
    // For client-side, we need to get the virtual key from the server
    // This should be done through a secure endpoint that validates the user
    const response = await fetch('/api/auth/client-config');
    if (!response.ok) {
      throw new Error('Failed to get client configuration');
    }
    
    const config = await response.json() as { apiKey: string; baseURL: string };
    
    clientInstance = new ConduitCoreClient({
      apiKey: config.apiKey,
      baseURL: config.baseURL,
      signalR: {
        enabled: true,
        autoConnect: true,
      }
    });
  }
  
  return clientInstance;
}

/**
 * Clean up client resources
 */
export function cleanupClientCore(): void {
  if (clientInstance) {
    // The ConduitCoreClient type from the declaration might have signalr
    // but we need to check at runtime
    const client = clientInstance as unknown as { signalr?: { stopAllConnections: () => Promise<void> } };
    if (client.signalr && typeof client.signalr.stopAllConnections === 'function') {
      void client.signalr.stopAllConnections().catch(console.error);
    }
    clientInstance = null;
  }
}

/**
 * Generate video with progress tracking using client-side SDK
 */
export async function generateVideoWithProgress(
  request: { prompt: string; model?: string; duration?: number; size?: string; fps?: number; style?: string },
  callbacks: VideoProgressCallbacks
): Promise<{ taskId: string }> {
  const client = await getClientCoreClient();
  
  // Use the new generateWithProgress method
  const { taskId, result } = await client.videos.generateWithProgress(
    request,
    callbacks
  );
  
  // Store the result promise for later use if needed
  interface WindowWithVideoResults extends Window {
    videoGenerationResults?: Record<string, Promise<unknown>>;
  }
  const windowWithResults = window as WindowWithVideoResults;
  windowWithResults.videoGenerationResults = windowWithResults.videoGenerationResults ?? {};
  windowWithResults.videoGenerationResults[taskId] = result;
  
  return { taskId };
}