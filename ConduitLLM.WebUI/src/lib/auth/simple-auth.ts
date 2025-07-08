import { NextRequest, NextResponse } from 'next/server';

/**
 * Dead simple auth check for API routes
 * Returns JSON 401 if not authenticated, no redirects, no bullshit
 */
export function requireAuth(request: NextRequest): { isValid: boolean; response?: NextResponse } {
  const sessionCookie = request.cookies.get('conduit_session');
  
  if (!sessionCookie) {
    return {
      isValid: false,
      response: NextResponse.json({ error: 'No session' }, { status: 401 })
    };
  }
  
  try {
    const session = JSON.parse(sessionCookie.value);
    
    // Check expiration
    if (session.expiresAt && new Date(session.expiresAt) < new Date()) {
      return {
        isValid: false,
        response: NextResponse.json({ error: 'Session expired' }, { status: 401 })
      };
    }
    
    // Check if authenticated
    if (!session.isAuthenticated) {
      return {
        isValid: false,
        response: NextResponse.json({ error: 'Not authenticated' }, { status: 401 })
      };
    }
    
    return { isValid: true };
  } catch {
    return {
      isValid: false,
      response: NextResponse.json({ error: 'Invalid session' }, { status: 401 })
    };
  }
}

/**
 * Even simpler wrapper for route handlers
 */
export function withAuth(handler: (request: NextRequest) => Promise<Response>) {
  return async (request: NextRequest): Promise<Response> => {
    const auth = requireAuth(request);
    if (!auth.isValid) {
      return auth.response!;
    }
    return handler(request);
  };
}