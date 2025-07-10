/**
 * Optional WebUI authentication helper utilities
 * 
 * IMPORTANT: These utilities are NOT part of the core Admin SDK functionality.
 * They are provided as optional helpers for developers building custom WebUI
 * implementations that need to follow the same authentication patterns as the
 * official Conduit WebUI.
 * 
 * The Admin SDK itself uses the CONDUIT_API_TO_API_BACKEND_AUTH_KEY for API authentication.
 * The WebUI uses a separate CONDUIT_ADMIN_LOGIN_PASSWORD for human administrator authentication.
 */

import { createHash, randomBytes } from 'crypto';

/**
 * Session data structure for WebUI authentication
 */
export interface SessionData {
  /** Unique session identifier */
  sessionId: string;
  
  /** When the session was created */
  createdAt: string;
  
  /** When the session expires */
  expiresAt: string;
  
  /** Optional user information */
  user?: {
    id?: string;
    email?: string;
    role?: string;
  };
  
  /** Additional metadata */
  metadata?: Record<string, any>;
}

/**
 * Configuration options for WebUI auth helpers
 */
export interface WebUIAuthConfig {
  /** Session duration in milliseconds (default: 24 hours) */
  sessionDurationMs?: number;
  
  /** Hash algorithm for key comparison (default: 'sha256') */
  hashAlgorithm?: string;
  
  /** Session token length in bytes (default: 32) */
  tokenLength?: number;
}

/**
 * WebUI Authentication Helper Utilities
 * 
 * These are optional utilities for implementing WebUI authentication
 * patterns similar to the official Conduit WebUI.
 */
export class WebUIAuthHelpers {
  private config: Required<WebUIAuthConfig>;

  constructor(config?: WebUIAuthConfig) {
    this.config = {
      sessionDurationMs: config?.sessionDurationMs || 24 * 60 * 60 * 1000, // 24 hours
      hashAlgorithm: config?.hashAlgorithm || 'sha256',
      tokenLength: config?.tokenLength || 32,
    };
  }

  /**
   * Validate a provided WebUI auth key against the configured key
   * Uses constant-time comparison to prevent timing attacks
   * 
   * @param providedKey - The key provided by the user
   * @param configuredKey - The configured CONDUIT_ADMIN_LOGIN_PASSWORD
   * @returns true if the keys match, false otherwise
   */
  validateAuthKey(providedKey: string, configuredKey: string): boolean {
    if (!providedKey || !configuredKey) {
      return false;
    }

    // Hash both keys to ensure constant-time comparison
    const providedHash = createHash(this.config.hashAlgorithm)
      .update(providedKey)
      .digest();
    
    const configuredHash = createHash(this.config.hashAlgorithm)
      .update(configuredKey)
      .digest();

    // Use timing-safe comparison
    return providedHash.length === configuredHash.length &&
      providedHash.equals(configuredHash);
  }

  /**
   * Generate a cryptographically secure session token
   * 
   * @returns A secure random session token
   */
  generateSessionToken(): string {
    return randomBytes(this.config.tokenLength).toString('hex');
  }

  /**
   * Create a new session with expiration
   * 
   * @param user - Optional user information
   * @param metadata - Optional metadata
   * @returns A new session data object
   */
  createSession(user?: SessionData['user'], metadata?: Record<string, any>): SessionData {
    const now = new Date();
    const expiresAt = new Date(now.getTime() + this.config.sessionDurationMs);

    return {
      sessionId: this.generateSessionToken(),
      createdAt: now.toISOString(),
      expiresAt: expiresAt.toISOString(),
      user,
      metadata,
    };
  }

  /**
   * Parse and validate a session cookie value
   * 
   * @param cookieValue - The session cookie value (should be JSON)
   * @returns Parsed session data or null if invalid
   */
  parseSessionCookie(cookieValue: string): SessionData | null {
    try {
      if (!cookieValue) {
        return null;
      }

      const session = JSON.parse(cookieValue) as SessionData;

      // Validate required fields
      if (!session.sessionId || !session.createdAt || !session.expiresAt) {
        return null;
      }

      // Validate date formats
      const createdAt = new Date(session.createdAt);
      const expiresAt = new Date(session.expiresAt);
      
      if (isNaN(createdAt.getTime()) || isNaN(expiresAt.getTime())) {
        return null;
      }

      return session;
    } catch {
      return null;
    }
  }

  /**
   * Check if a session has expired
   * 
   * @param session - The session to check
   * @returns true if expired, false if still valid
   */
  isSessionExpired(session: SessionData): boolean {
    const expiresAt = new Date(session.expiresAt);
    return expiresAt <= new Date();
  }

  /**
   * Extend a session's expiration time
   * 
   * @param session - The session to extend
   * @returns A new session object with updated expiration
   */
  extendSession(session: SessionData): SessionData {
    const now = new Date();
    const newExpiresAt = new Date(now.getTime() + this.config.sessionDurationMs);

    return {
      ...session,
      expiresAt: newExpiresAt.toISOString(),
    };
  }

  /**
   * Create a secure cookie options object for session storage
   * 
   * @param secure - Whether to use secure flag (HTTPS only)
   * @returns Cookie options for use with cookie libraries
   */
  getCookieOptions(secure: boolean = true): {
    httpOnly: boolean;
    secure: boolean;
    sameSite: 'strict' | 'lax' | 'none';
    path: string;
    maxAge: number;
  } {
    return {
      httpOnly: true,
      secure,
      sameSite: 'strict',
      path: '/',
      maxAge: this.config.sessionDurationMs,
    };
  }

  /**
   * Hash a session token for storage
   * Useful for storing session identifiers in databases
   * 
   * @param token - The session token to hash
   * @returns Hashed token
   */
  hashSessionToken(token: string): string {
    return createHash(this.config.hashAlgorithm)
      .update(token)
      .digest('hex');
  }
}

/**
 * Default instance with standard configuration
 */
export const webUIAuthHelpers = new WebUIAuthHelpers();