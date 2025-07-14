import { currentUser } from '@clerk/nextjs/server';
import { User } from '@clerk/nextjs/server';

/**
 * Check if the current user is a Conduit admin based on Clerk metadata
 */
export async function isClerkAdmin(): Promise<boolean> {
  try {
    const user = await currentUser();
    
    if (!user) {
      return false;
    }

    // Check publicMetadata for admin status
    const publicMetadata = user.publicMetadata as Record<string, any>;
    
    return publicMetadata?.conduitAdmin === true || publicMetadata?.role === 'admin';
  } catch (error) {
    console.error('Error checking Clerk admin status:', error);
    return false;
  }
}

/**
 * Get the current Clerk user if authenticated
 */
export async function getClerkUser(): Promise<User | null> {
  try {
    return await currentUser();
  } catch (error) {
    console.error('Error getting Clerk user:', error);
    return null;
  }
}

/**
 * Check if user is authenticated with Clerk
 * Note: This should be called from server components or API routes
 */
export async function isClerkAuthenticated(): Promise<boolean> {
  try {
    const user = await currentUser();
    return !!user;
  } catch (error) {
    console.error('Error checking Clerk authentication:', error);
    return false;
  }
}