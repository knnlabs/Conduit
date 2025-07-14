/**
 * Auth mode detection and configuration
 */

export type AuthMode = 'clerk' | 'conduit';

/**
 * Determines which authentication mode to use based on environment variables
 */
export function getAuthMode(): AuthMode {
  const hasClerkKeys = !!(
    process.env.CLERK_PUBLISHABLE_KEY && 
    process.env.CLERK_SECRET_KEY
  );
  
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