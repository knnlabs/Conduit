import { ConduitGatewayClient } from '@knn_labs/conduit-gateway-client';

let browserClient: InstanceType<typeof ConduitGatewayClient> | null = null;
let ephemeralKey: string | null = null;
let ephemeralKeyExpiry: Date | null = null;
let coreApiUrl: string | null = null;

interface EphemeralKeyResponse {
  ephemeralKey: string;
  expiresAt: string;
  expiresInSeconds: number;
  coreApiUrl: string;
}

/**
 * Get an ephemeral key for browser API access
 */
async function getEphemeralKey(): Promise<EphemeralKeyResponse> {
  const response = await fetch('/api/auth/ephemeral-key', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({
      purpose: 'discovery-api-access'
    }),
  });
  
  if (!response.ok) {
    throw new Error('Failed to get ephemeral key');
  }
  
  return response.json() as Promise<EphemeralKeyResponse>;
}

/**
 * Get or create a browser-compatible Gateway SDK client
 * This client uses ephemeral keys for secure browser access
 */
export async function getBrowserGatewayClient(): Promise<InstanceType<typeof ConduitGatewayClient>> {
  // Check if we need a new ephemeral key (expired or not set)
  const now = new Date();
  const needNewKey = !ephemeralKey ||
    !ephemeralKeyExpiry ||
    ephemeralKeyExpiry <= now;

  if (needNewKey) {
    // Get new ephemeral key
    const keyResponse = await getEphemeralKey();
    ephemeralKey = keyResponse.ephemeralKey;
    ephemeralKeyExpiry = new Date(keyResponse.expiresAt);
    coreApiUrl = keyResponse.coreApiUrl;

    // Clear existing client to force recreation with new key
    browserClient = null;
  }

  // Create client if needed or if we just got a new key
  if (!browserClient && ephemeralKey && coreApiUrl) {
    browserClient = new ConduitGatewayClient({
      apiKey: ephemeralKey,
      baseURL: coreApiUrl,
      signalR: {
        enabled: true,
        autoConnect: true,
      }
    });
  }

  if (!browserClient) {
    throw new Error('Failed to create browser client');
  }

  return browserClient;
}

// Backward-compatible alias
export const getBrowserCoreClient = getBrowserGatewayClient;

/**
 * Clear the cached browser client
 * Call this when user logs out or needs to refresh
 */
export function clearBrowserClient(): void {
  browserClient = null;
  ephemeralKey = null;
  ephemeralKeyExpiry = null;
  coreApiUrl = null;
}