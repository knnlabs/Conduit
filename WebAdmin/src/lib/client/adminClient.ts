import { ConduitAdminClient } from '@knn_labs/conduit-admin-client';

/**
 * Ephemeral master key response from WebUI backend
 */
interface EphemeralMasterKeyResponse {
  ephemeralMasterKey: string;
  expiresAt: string;
  expiresInSeconds: number;
  adminApiUrl: string;
}

/**
 * Creates a fresh Admin SDK client with ephemeral master key authentication
 * Each call generates a new single-use ephemeral key for maximum security
 */
export async function createAdminClient(): Promise<ConduitAdminClient> {
  // Generate fresh ephemeral master key from WebUI backend
  const response = await fetch('/api/auth/ephemeral-master-key', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({
      purpose: 'frontend-admin-sdk-call'
    }),
  });

  if (!response.ok) {
    const errorText = await response.text().catch(() => 'Unknown error');
    throw new Error(`Failed to generate ephemeral master key: ${response.status} ${errorText}`);
  }

  const keyData = await response.json() as EphemeralMasterKeyResponse;

  // Create Admin SDK client with ephemeral key
  // IMPORTANT: No retries because ephemeral master keys are single-use!
  // If a request fails, a new ephemeral key must be generated
  return new ConduitAdminClient({
    baseUrl: keyData.adminApiUrl,
    masterKey: keyData.ephemeralMasterKey,
    timeout: 60000, // 60 second timeout
    retries: 0, // Disabled - ephemeral keys are single-use
  });
}

/**
 * Executes an operation with a fresh Admin SDK client
 * Automatically handles ephemeral key generation and client creation
 * 
 * @param operation - Function that uses the Admin SDK client
 * @returns Promise resolving to the operation result
 * 
 * @example
 * ```typescript
 * // Get system health
 * const health = await withAdminClient(client => client.system.getHealth());
 * 
 * // Create virtual key
 * const virtualKey = await withAdminClient(client => 
 *   client.virtualKeys.create({ keyName: 'Test Key', virtualKeyGroupId: 1 })
 * );
 * ```
 */
export async function withAdminClient<T>(
  operation: (client: ConduitAdminClient) => Promise<T>
): Promise<T> {
  const adminClient = await createAdminClient();
  return operation(adminClient);
}

/**
 * Custom hook for using Admin SDK in React components
 * Provides a function to execute operations with fresh ephemeral keys
 */
export function useAdminClient() {
  const executeWithAdmin = async <T>(
    operation: (client: ConduitAdminClient) => Promise<T>
  ): Promise<T> => {
    return withAdminClient(operation);
  };

  return { executeWithAdmin };
}