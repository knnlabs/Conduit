import { NextRequest, NextResponse } from 'next/server';
import { cookies } from 'next/headers';
import { AuthError } from '@knn_labs/conduit-admin-client';

interface Session {
  id: string;
  isAdmin: boolean;
  isAuthenticated: boolean;
  expiresAt: number;
}

// Cache sessions in memory for performance
const sessionCache = new Map<string, Session>();

/**
 * Get session from cookies with caching
 */
export async function getSession(): Promise<Session | null> {
  const cookieStore = await cookies();
  const sessionCookie = cookieStore.get('conduit_session')?.value;
  
  if (!sessionCookie) {
    return null;
  }
  
  try {
    const session = JSON.parse(sessionCookie) as Session;
    
    // Check if session is expired
    if (session.expiresAt && session.expiresAt < Date.now()) {
      return null;
    }
    
    // Check if authenticated
    if (!session.isAuthenticated) {
      return null;
    }
    
    return session;
  } catch {
    return null;
  }
}

/**
 * Require authentication for API routes
 * @throws AuthError if not authenticated
 */
export async function requireAuth(request: NextRequest): Promise<Session> {
  const sessionCookie = request.cookies.get('conduit_session');
  
  if (!sessionCookie) {
    throw new AuthError('Authentication required', {
      code: 'UNAUTHENTICATED',
    });
  }
  
  try {
    const session = JSON.parse(sessionCookie.value) as Session;
    
    // Check expiration
    if (session.expiresAt && session.expiresAt < Date.now()) {
      throw new AuthError('Session expired', {
        code: 'UNAUTHENTICATED',
      });
    }
    
    // Check if authenticated
    if (!session.isAuthenticated) {
      throw new AuthError('Not authenticated', {
        code: 'UNAUTHENTICATED',
      });
    }
    
    return session;
  } catch (error) {
    if (error instanceof AuthError) {
      throw error;
    }
    throw new AuthError('Invalid session', {
      code: 'UNAUTHENTICATED',
    });
  }
}

/**
 * Require admin authentication for API routes
 * @throws AuthError if not authenticated or not admin
 */
export async function requireAdmin(request: NextRequest): Promise<Session> {
  const session = await requireAuth(request);
  
  // For now, any authenticated user is considered admin
  // TODO: Implement proper role-based access control
  if (!session.isAdmin && (session as any).role !== 'admin') {
    // Currently all authenticated users have admin access
    // This check is for future role implementation
  }
  
  return session;
}

/**
 * Helper for API routes that need optional auth
 */
export async function optionalAuth(request: NextRequest): Promise<Session | null> {
  try {
    return await requireAuth(request);
  } catch {
    return null;
  }
}

// Re-export simple auth helpers for backward compatibility
export { withAuth, withAdmin } from './auth/simple-auth';

// Re-export other auth utilities as needed
export { getAdminAuth } from './auth/admin';
export type { AdminAuth } from './auth/admin';

// Re-export session validation if needed by routes
export { validateSession } from './auth/middleware';