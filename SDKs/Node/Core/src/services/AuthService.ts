import { BaseService } from './BaseService';
import type { FetchBasedClient } from '../client/FetchBasedClient';

export interface EphemeralKeyMetadata {
  sourceIP?: string;
  userAgent?: string;
  purpose?: string;
  requestId?: string;
}

export interface GenerateEphemeralKeyRequest {
  metadata?: EphemeralKeyMetadata;
}

export interface EphemeralKeyResponse {
  ephemeralKey: string;
  expiresAt: string;
  expiresInSeconds: number;
}

/**
 * Service for authentication-related operations
 */
export class AuthService extends BaseService {
  constructor(client: FetchBasedClient) {
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
   * Generate an ephemeral key using the provided virtual key
   * 
   * @param virtualKey - The virtual key to authenticate with
   * @param options - Optional metadata for the ephemeral key request
   * @returns The ephemeral key response with token and expiration
   * 
   * @example
   * ```typescript
   * const ephemeralKey = await client.auth.generateEphemeralKey(
   *   'vk_abc123',
   *   {
   *     metadata: {
   *       purpose: 'web-ui-request',
   *       sourceIP: '192.168.1.1'
   *     }
   *   }
   * );
   * ```
   */
  async generateEphemeralKey(
    virtualKey: string,
    options?: GenerateEphemeralKeyRequest
  ): Promise<EphemeralKeyResponse> {
    // Prepare the request body
    const body: GenerateEphemeralKeyRequest = {
      metadata: {
        requestId: options?.metadata?.requestId ?? this.generateRequestId(),
        ...options?.metadata
      }
    };

    // Make the request with the virtual key as Bearer token
    const response = await this.clientAdapter.post<EphemeralKeyResponse, GenerateEphemeralKeyRequest>(
      '/v1/auth/ephemeral-key',
      body,
      {
        headers: {
          'Authorization': `Bearer ${virtualKey}`
        }
      }
    );

    return response;
  }
}