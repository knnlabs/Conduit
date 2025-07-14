import { getAuthMode } from './auth-mode';
import { isClerkAdmin, isClerkAuthenticated } from './clerk-helpers';
import { validateSession } from './middleware';
import { NextRequest } from 'next/server';
import { auth } from '@clerk/nextjs/server';

/**
 * Unified authentication check that works with both Clerk and Conduit auth
 */
export async function isAuthenticated(request?: NextRequest): Promise<boolean> {
  const authMode = getAuthMode();
  
  if (authMode === 'clerk') {
    return await isClerkAuthenticated();
  }
  
  // For Conduit auth, check if we have a valid session
  if (request) {
    const result = await validateSession(request);
    return result.isValid;
  }
  
  return false;
}

/**
 * Unified admin check that works with both Clerk and Conduit auth
 */
export async function isAdmin(request?: NextRequest): Promise<boolean> {
  const authMode = getAuthMode();
  
  if (authMode === 'clerk') {
    return await isClerkAdmin();
  }
  
  // For Conduit auth, all authenticated users are admins
  return await isAuthenticated(request);
}

/**
 * Require authentication for API routes (throws on failure)
 */
export async function requireAuth(request: NextRequest): Promise<void> {
  const authMode = getAuthMode();
  
  if (authMode === 'clerk') {
    const { userId } = await auth();
    if (!userId) {
      throw new Error('Unauthorized');
    }
    
    // Check if user is admin
    const adminStatus = await isClerkAdmin();
    if (!adminStatus) {
      throw new Error('Unauthorized: Admin access required');
    }
  } else {
    // Use existing Conduit auth
    const result = await validateSession(request);
    if (!result.isValid) {
      throw new Error(result.error || 'Unauthorized');
    }
  }
}

/**
 * Require admin access for API routes (throws on failure)
 */
export async function requireAdmin(request: NextRequest): Promise<void> {
  // For now, requireAdmin is the same as requireAuth
  // since all WebUI access is admin-only
  await requireAuth(request);
}