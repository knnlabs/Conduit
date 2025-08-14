import { ServiceBase } from './ServiceBase';
import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';

export interface MasterEphemeralKeyMetadata {
  sourceIP?: string;
  userAgent?: string;
  purpose?: string;
  requestId?: string;
}

export interface GenerateMasterEphemeralKeyRequest {
  metadata?: MasterEphemeralKeyMetadata;
}

export interface MasterEphemeralKeyResponse {
  ephemeralMasterKey: string;
  expiresAt: string;
  expiresInSeconds: number;
}

/**
 * Service for authentication-related operations in the Admin API
 */
export class FetchAuthService extends ServiceBase {
  constructor(client: FetchBaseApiClient) {
    super(client);
  }

  /**
   * Generate a request ID for tracking
   */
  private generateRequestId(): string {
    // Node.js 16+ has crypto available on globalThis
    if (typeof globalThis !== 'undefined' && globalThis.crypto && typeof globalThis.crypto.randomUUID === 'function') {
      return globalThis.crypto.randomUUID();
    }
    
    // Fallback to timestamp-based ID for older Node.js versions
    return `req_${Date.now()}_${Math.random().toString(36).substring(2, 9)}`;
  }

  /**
   * Generate an ephemeral master key using the provided master key
   * 
   * @param masterKey - The master key to authenticate with
   * @param options - Optional metadata for the ephemeral master key request
   * @returns The ephemeral master key response with token and expiration
   * 
   * @example
   * ```typescript
   * const ephemeralMasterKey = await client.auth.generateEphemeralMasterKey(
   *   'master_key_abc123',
   *   {
   *     metadata: {
   *       purpose: 'web-ui-request',
   *       sourceIP: '192.168.1.1'
   *     }
   *   }
   * );
   * ```
   */
  async generateEphemeralMasterKey(
    masterKey: string,
    options?: GenerateMasterEphemeralKeyRequest
  ): Promise<MasterEphemeralKeyResponse> {
    // Prepare the request body
    const body: GenerateMasterEphemeralKeyRequest = {
      metadata: {
        requestId: options?.metadata?.requestId ?? this.generateRequestId(),
        ...options?.metadata
      }
    };

    // Make the request with the master key in header
    const response = await this.post<MasterEphemeralKeyResponse, GenerateMasterEphemeralKeyRequest>(
      '/api/admin/auth/ephemeral-master-key',
      body,
      undefined,
      {
        headers: {
          'X-Master-Key': masterKey
        }
      }
    );

    return response;
  }
}