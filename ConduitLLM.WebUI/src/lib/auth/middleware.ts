import { NextRequest } from 'next/server';

export interface SessionValidationResult {
  isValid: boolean;
  error?: string;
}

export async function validateSession(request: NextRequest): Promise<SessionValidationResult> {
  try {
    // Check for session cookie (primary) or authorization header (fallback)
    const sessionCookie = request.cookies.get('conduit_session');
    const authHeader = request.headers.get('authorization');
    
    let sessionData;
    
    // Prefer cookie-based authentication (more secure)
    if (sessionCookie) {
      try {
        sessionData = JSON.parse(sessionCookie.value);
      } catch {
        return { isValid: false, error: 'Invalid session cookie' };
      }
    } else if (authHeader && authHeader.startsWith('Bearer ')) {
      // Fallback to Authorization header for API clients
      const token = authHeader.substring(7);
      try {
        sessionData = JSON.parse(atob(token));
      } catch {
        return { isValid: false, error: 'Invalid session token' };
      }
    } else {
      return { isValid: false, error: 'No session found' };
    }

    if (!sessionData || !sessionData.isAuthenticated) {
      return { isValid: false, error: 'Session not authenticated' };
    }

    // Check session expiration
    if (sessionData.expiresAt && new Date(sessionData.expiresAt) < new Date()) {
      return { isValid: false, error: 'Session expired' };
    }

    return { isValid: true };
  } catch (error) {
    console.error('Session validation error:', error);
    return { isValid: false, error: 'Session validation failed' };
  }
}

export function createUnauthorizedResponse(error: string = 'Unauthorized') {
  return new Response(JSON.stringify({ error }), {
    status: 401,
    headers: { 'Content-Type': 'application/json' }
  });
}