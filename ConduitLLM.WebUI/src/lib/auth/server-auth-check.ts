import { redirect } from 'next/navigation';
import { getAuthMode } from './auth-mode';
import { currentUser } from '@clerk/nextjs/server';

/**
 * Server-side function to ensure user is an admin
 * Redirects to unauthorized page if not admin
 */
export async function requireAdmin() {
  const authMode = getAuthMode();
  
  if (authMode === 'clerk') {
    const user = await currentUser();
    
    if (!user) {
      redirect('/sign-in');
    }
    
    // Check if user has admin status in their metadata
    const publicMetadata = user.publicMetadata as Record<string, any>;
    const isAdmin = publicMetadata?.conduitAdmin === true || publicMetadata?.role === 'admin';
    
    if (!isAdmin) {
      // User is authenticated but not an admin
      redirect('/unauthorized');
    }
    
    return { user, isAdmin: true };
  }
  
  // For Conduit auth, being authenticated means being admin
  // This is handled by the middleware
  return { user: null, isAdmin: true };
}

/**
 * Server-side function to check if user is admin without redirecting
 */
export async function checkAdmin() {
  const authMode = getAuthMode();
  
  if (authMode === 'clerk') {
    const user = await currentUser();
    
    if (!user) {
      return { user: null, isAdmin: false };
    }
    
    const publicMetadata = user.publicMetadata as Record<string, any>;
    const isAdmin = publicMetadata?.conduitAdmin === true || publicMetadata?.role === 'admin';
    
    return { user, isAdmin };
  }
  
  // For Conduit auth, check is handled by middleware
  return { user: null, isAdmin: true };
}