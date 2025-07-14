/**
 * Auth mode detection and configuration
 */

export type AuthMode = 'clerk' | 'conduit';

/**
 * Determines which authentication mode to use based on environment variables
 */
export function getAuthMode(): AuthMode {
  // In middleware/client context, only NEXT_PUBLIC_ variables are available
  // We check for the public key which is sufficient to determine if Clerk is configured
  const hasClerkPublicKey = !!(
    process.env.NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY || 
    process.env.CLERK_PUBLISHABLE_KEY
  );
  
  // For server-side contexts where secret key is needed
  const hasClerkSecretKey = !!process.env.CLERK_SECRET_KEY;
  
  // If we're in a context where only public vars are available (middleware/client)
  // just check for the public key. Otherwise, check both.
  const hasClerkKeys = typeof window !== 'undefined' || !hasClerkSecretKey
    ? hasClerkPublicKey 
    : hasClerkPublicKey && hasClerkSecretKey;
  
  return hasClerkKeys ? 'clerk' : 'conduit';
}

/**
 * Check if Clerk auth is enabled
 */
export function isClerkEnabled(): boolean {
  return getAuthMode() === 'clerk';
}

/**
 * Check if Conduit auth is enabled
 */
export function isConduitAuthEnabled(): boolean {
  return getAuthMode() === 'conduit';
}

/**
 * Get Clerk configuration if enabled
 */
export function getClerkConfig() {
  if (!isClerkEnabled()) {
    return null;
  }

  return {
    publishableKey: process.env.CLERK_PUBLISHABLE_KEY!,
    secretKey: process.env.CLERK_SECRET_KEY!,
  };
}