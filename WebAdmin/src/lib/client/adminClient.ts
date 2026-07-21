import { ConduitAdminClient } from '@/lib/admin-api';
import { useCallback } from 'react';
import {
  parseCriticalResponse,
  webAdminEphemeralKeySchema,
} from '@/lib/api-transport/critical-response-validation';

/**
 * Ephemeral master key response from WebAdmin backend
 */
interface EphemeralMasterKeyResponse {
  ephemeralMasterKey: string;
  expiresAt: string;
  expiresInSeconds: number;
  adminApiUrl: string;
}

/**
 * Creates a fresh local Admin API client with ephemeral master-key authentication.
 * Each call generates a new single-use ephemeral key for maximum security
 */
export async function createAdminClient(): Promise<ConduitAdminClient> {
  // Generate fresh ephemeral master key from WebAdmin backend
  const response = await fetch('/api/auth/ephemeral-master-key', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({
      purpose: 'frontend-admin-api-call'
    }),
  });

  if (!response.ok) {
    const errorText = await response.text().catch(() => 'Unknown error');
    throw new Error(`Failed to generate ephemeral master key: ${response.status} ${errorText}`);
  }

  const keyData = parseCriticalResponse(
    webAdminEphemeralKeySchema,
    await response.json(),
    'WebAdmin ephemeral master-key issuance',
  ) satisfies EphemeralMasterKeyResponse;

  // Create the local Admin client with an ephemeral key.
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
 * Executes an operation with a fresh local Admin client.
 * Automatically handles ephemeral key generation and client creation
 * 
 * @param operation - Function that uses the local Admin client
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
 * Custom hook for using the local Admin client in React components.
 * Provides a function to execute operations with fresh ephemeral keys
 */
export function useAdminClient() {
  const executeWithAdmin = useCallback(async <T>(
    operation: (client: ConduitAdminClient) => Promise<T>
  ): Promise<T> => {
    return withAdminClient(operation);
  }, []); // Empty deps - function never needs to change since it only calls withAdminClient

  return { executeWithAdmin };
}
