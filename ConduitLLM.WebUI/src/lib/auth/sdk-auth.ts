import { NextRequest } from 'next/server';
import { getServerAdminClient } from '@/lib/server/adminClient';

// Simple session data for admin WebUI
export interface SDKSessionData {
  isAuthenticated: boolean;
  expiresAt: string;
  masterKeyHash?: string;  // For admin operations
}

// Simple auth result
export interface SDKAuthResult {
  isValid: boolean;
  error?: string;
  session?: SDKSessionData;
  adminClient?: ReturnType<typeof getServerAdminClient>;
}

// Extract session data from request
export function extractSessionData(request: NextRequest): SDKSessionData | null {
  try {
    const sessionCookie = request.cookies.get('conduit_session');
    if (sessionCookie) {
      try {
        return JSON.parse(sessionCookie.value);
      } catch {
        return null;
      }
    }
    return null;
  } catch {
    return null;
  }
}

// Validate session for Admin SDK operations
export async function validateAdminSession(request: NextRequest): Promise<SDKAuthResult> {
  try {
    const sessionData = extractSessionData(request);
    
    if (!sessionData) {
      return { isValid: false, error: 'No session found' };
    }

    if (!sessionData.isAuthenticated) {
      return { isValid: false, error: 'Session not authenticated' };
    }

    // Check session expiration
    if (sessionData.expiresAt && new Date(sessionData.expiresAt) < new Date()) {
      return { isValid: false, error: 'Session expired' };
    }

    // Validate master key access
    if (!sessionData.masterKeyHash) {
      return { isValid: false, error: 'Admin access not authorized' };
    }

    // Create admin client
    try {
      const adminClient = getServerAdminClient();
      return {
        isValid: true,
        session: sessionData,
        adminClient,
      };
    } catch (error) {
      return { isValid: false, error: 'Admin client initialization failed' };
    }
  } catch (error) {
    return { isValid: false, error: 'Session validation failed' };
  }
}

// Simple validation for SDK session
export async function validateSDKSession(
  request: NextRequest,
  options?: { requireAdmin?: boolean }
): Promise<SDKAuthResult> {
  // For Configuration pages, we always require admin
  return validateAdminSession(request);
}

// Create unauthorized response
export function createUnauthorizedResponse(error?: string): Response {
  return new Response(
    JSON.stringify({ error: error || 'Unauthorized' }),
    { 
      status: 401, 
      headers: { 'Content-Type': 'application/json' }
    }
  );
}

// Simple middleware helper for route handlers
export function withSDKAuth(
  handler: (
    request: NextRequest,
    context: { adminClient: ReturnType<typeof getServerAdminClient> }
  ) => Promise<Response>
) {
  return async (request: NextRequest): Promise<Response> => {
    try {
      const auth = await validateAdminSession(request);
      
      if (!auth.isValid || !auth.adminClient) {
        return createUnauthorizedResponse(auth.error);
      }

      return await handler(request, { adminClient: auth.adminClient });
    } catch (error) {
      return new Response(
        JSON.stringify({ error: 'Internal authentication error' }),
        { 
          status: 500, 
          headers: { 'Content-Type': 'application/json' }
        }
      );
    }
  };
}