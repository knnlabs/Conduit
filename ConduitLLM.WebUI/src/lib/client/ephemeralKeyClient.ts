/**
 * Client for managing ephemeral API keys for direct browser-to-Core API communication
 */

interface EphemeralKeyResponse {
  ephemeralKey: string;
  expiresAt: string;
  expiresInSeconds: number;
  coreApiUrl: string;
}

interface EphemeralKeyCache {
  key: string;
  expiresAt: Date;
  coreApiUrl: string;
}

class EphemeralKeyClient {
  private cache: EphemeralKeyCache | null = null;
  private refreshPromise: Promise<EphemeralKeyCache> | null = null;
  
  /**
   * Get the Core API URL from environment or use default
   */
  private getCoreApiUrl(): string {
    // In production, this would be your actual Core API URL
    // For development, the Core API is exposed on localhost:5000
    return typeof window !== 'undefined' 
      ? (process.env.NEXT_PUBLIC_CORE_API_URL ?? 'http://localhost:5000')
      : 'http://localhost:5000';
  }

  /**
   * Request a new ephemeral key from the WebUI backend
   */
  private async requestNewKey(purpose?: string): Promise<EphemeralKeyCache> {
    const response = await fetch('/api/auth/ephemeral-key', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ purpose: purpose ?? 'chat-streaming' }),
    });

    if (!response.ok) {
      throw new Error(`Failed to get ephemeral key: ${response.statusText}`);
    }

    const data = await response.json() as EphemeralKeyResponse;
    
    return {
      key: data.ephemeralKey,
      expiresAt: new Date(data.expiresAt),
      coreApiUrl: data.coreApiUrl,
    };
  }

  /**
   * Get a valid ephemeral key, either from cache or by requesting a new one
   * Automatically handles expiration and refresh
   */
  async getKey(purpose?: string): Promise<{ key: string; coreApiUrl: string }> {
    // If we're already refreshing, wait for that to complete
    if (this.refreshPromise) {
      const result = await this.refreshPromise;
      return { key: result.key, coreApiUrl: result.coreApiUrl };
    }

    // Check if we have a cached key that's still valid
    // We consider it expired 30 seconds before actual expiry to avoid edge cases
    if (this.cache) {
      const now = new Date();
      const expiryBuffer = new Date(this.cache.expiresAt.getTime() - 30000); // 30 seconds buffer
      
      if (now < expiryBuffer) {
        return { key: this.cache.key, coreApiUrl: this.cache.coreApiUrl };
      }
    }

    // Need to refresh the key
    try {
      this.refreshPromise = this.requestNewKey(purpose);
      this.cache = await this.refreshPromise;
      return { key: this.cache.key, coreApiUrl: this.cache.coreApiUrl };
    } finally {
      this.refreshPromise = null;
    }
  }

  /**
   * Clear the cached key (useful after errors)
   */
  clearCache(): void {
    this.cache = null;
    this.refreshPromise = null;
  }

  /**
   * Make a direct request to the Core API using an ephemeral key
   * Automatically handles key refresh on 401 errors
   */
  async makeDirectRequest(
    endpoint: string,
    options: RequestInit & { retryOnAuth?: boolean } = { retryOnAuth: true }
  ): Promise<Response> {
    const { key, coreApiUrl } = await this.getKey();
    
    const headers = new Headers(options.headers);
    headers.set('X-Ephemeral-Key', key);
    
    const requestOptions: RequestInit = {
      ...options,
      headers,
    };

    const response = await fetch(`${coreApiUrl}${endpoint}`, requestOptions);

    // If we get a 401 and retry is enabled, clear cache and try once more with a new key
    if (response.status === 401 && options.retryOnAuth !== false) {
      console.warn('Ephemeral key rejected, requesting new key and retrying...');
      this.clearCache();
      
      const { key: newKey, coreApiUrl: newCoreApiUrl } = await this.getKey();
      const newHeaders = new Headers(requestOptions.headers);
      newHeaders.set('X-Ephemeral-Key', newKey);
      requestOptions.headers = newHeaders;
      
      return fetch(`${newCoreApiUrl}${endpoint}`, requestOptions);
    }

    return response;
  }

  /**
   * Create a streaming request to the Core API
   * Returns the Response object for SSE streaming
   */
  async createStreamingRequest(
    endpoint: string,
    body: unknown,
    signal?: AbortSignal
  ): Promise<Response> {
    return this.makeDirectRequest(endpoint, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Accept': 'text/event-stream',
      },
      body: JSON.stringify(body),
      signal,
    });
  }
}

// Export singleton instance
export const ephemeralKeyClient = new EphemeralKeyClient();

// Export the class for testing
export { EphemeralKeyClient };