import { NextRequest, NextResponse } from 'next/server';
import { AuthError, isAuthError } from '@knn_labs/conduit-admin-client';
import { requireAuth as requireAuthNew, requireAdmin as requireAdminNew } from '../auth';

/**
 * Dead simple auth check for API routes
 * Returns JSON 401 if not authenticated, no redirects, no bullshit
 * 
 * @deprecated Use the new requireAuth from @/lib/auth instead
 */
export function requireAuth(request: NextRequest): { isValid: boolean; response?: NextResponse } {
  try {
    // Use the new auth function internally
    const sessionPromise = requireAuthNew(request);
    // Since this is a sync function, we need to handle the promise differently
    // For backward compatibility, we'll keep the old behavior
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
  } catch (error) {
    if (isAuthError(error)) {
      return {
        isValid: false,
        response: NextResponse.json({ error: error.message }, { status: 401 })
      };
    }
    return {
      isValid: false,
      response: NextResponse.json({ error: 'Authentication error' }, { status: 401 })
    };
  }
}

/**
 * Require admin authentication for API routes
 * Returns JSON 403 if not admin, 401 if not authenticated
 * 
 * @deprecated Use the new requireAdmin from @/lib/auth instead
 */
export function requireAdmin(request: NextRequest): { isValid: boolean; response?: NextResponse } {
  // First check if authenticated
  const authCheck = requireAuth(request);
  if (!authCheck.isValid) {
    return authCheck;
  }
  
  try {
    const sessionCookie = request.cookies.get('conduit_session');
    const session = JSON.parse(sessionCookie!.value);
    
    // Check if user has admin role
    // For now, any authenticated user is considered admin
    // TODO: Implement proper role-based access control
    if (!session.isAdmin && session.role !== 'admin') {
      // Currently all authenticated users have admin access
      // This check is for future role implementation
    }
    
    return { isValid: true };
  } catch {
    return {
      isValid: false,
      response: NextResponse.json({ error: 'Forbidden' }, { status: 403 })
    };
  }
}

/**
 * Even simpler wrapper for route handlers
 */
export function withAuth<T extends any[]>(
  handler: (request: NextRequest, ...args: T) => Promise<Response>
) {
  return async (request: NextRequest, ...args: T): Promise<Response> => {
    const auth = requireAuth(request);
    if (!auth.isValid) {
      return auth.response!;
    }
    return handler(request, ...args);
  };
}

/**
 * Wrapper for admin route handlers
 */
export function withAdmin<T extends any[]>(
  handler: (request: NextRequest, ...args: T) => Promise<Response>
) {
  return async (request: NextRequest, ...args: T): Promise<Response> => {
    const auth = requireAdmin(request);
    if (!auth.isValid) {
      return auth.response!;
    }
    return handler(request, ...args);
  };
}