import { ConduitAdminClient } from '@knn_labs/conduit-admin-client';
import { useAuthStore } from '@/stores/useAuthStore';
import { reportError } from '@/lib/utils/logging';
import { config, getAdminApiUrl } from '@/config';

// Server-side admin client configuration - uses environment variable
export function createServerAdminClient(): ConduitAdminClient {
  const masterKey = config.auth.masterKey;
  
  if (!masterKey) {
    throw new Error('CONDUIT_API_TO_API_BACKEND_AUTH_KEY environment variable is not set');
  }

  const adminApiUrl = getAdminApiUrl();
  
  // Client configuration validated - masterKey presence confirmed
  
  return new ConduitAdminClient({
    adminApiUrl,
    masterKey,
    options: {
      timeout: config.api.timeout,
    }
  });
}

// Note: Client-side admin clients have been removed for security.
// All API operations should go through server-side API routes.

// Singleton instances for common usage
let serverAdminClientInstance: ConduitAdminClient | null = null;

// Server-side admin client getter (for API routes)
export function getAdminClient(): ConduitAdminClient {
  // Check if we're in a server environment (API route)
  if (typeof window === 'undefined') {
    // Server-side: use environment variable
    if (!serverAdminClientInstance) {
      serverAdminClientInstance = createServerAdminClient();
    }
    return serverAdminClientInstance;
  } else {
    throw new Error('Admin client cannot be created on the client side. Use server-side API routes instead.');
  }
}

// Helper to check if we can create clients
export function canCreateAdminClient(): boolean {
  // On server-side, check environment variable
  if (typeof window === 'undefined') {
    return !!config.auth.masterKey;
  }
  
  // On client-side, always false
  return false;
}

// Error handling helpers
export function isAuthError(error: unknown): boolean {
  const errorObj = error as { status?: number };
  return errorObj?.status === 401 || errorObj?.status === 403;
}

export function isNetworkError(error: unknown): boolean {
  const errorObj = error as { name?: string; code?: string; message?: string };
  return errorObj?.name === 'NetworkError' || 
         errorObj?.code === 'NETWORK_ERROR' ||
         errorObj?.message?.includes('Failed to fetch') ||
         errorObj?.message?.includes('Network request failed') ||
         !navigator.onLine;
}

export function isRateLimitError(error: unknown): boolean {
  const errorObj = error as { status?: number };
  return errorObj?.status === 429;
}

export function isServerError(error: unknown): boolean {
  const errorObj = error as { status?: number };
  return errorObj?.status !== undefined && errorObj.status >= 500 && errorObj.status < 600;
}

export function isValidationError(error: unknown): boolean {
  const errorObj = error as { status?: number };
  return errorObj?.status === 400 || errorObj?.status === 422;
}

export function getErrorMessage(error: unknown, context: string): string {
  if (isAuthError(error)) {
    return 'Your session has expired. Please log in again.';
  }
  
  if (isNetworkError(error)) {
    if (!navigator.onLine) {
      return 'You appear to be offline. Please check your internet connection.';
    }
    return 'Unable to connect to the server. Please check your connection and try again.';
  }
  
  if (isRateLimitError(error)) {
    return 'Too many requests. Please wait a moment before trying again.';
  }
  
  if (isServerError(error)) {
    return 'The server is experiencing issues. Please try again later.';
  }
  
  if (isValidationError(error)) {
    // Try to extract validation details
    const errorObj = error as { response?: { data?: { errors?: unknown } }; details?: unknown; message?: string };
    const details = errorObj?.response?.data?.errors || errorObj?.details;
    if (details && typeof details === 'object') {
      const messages = Object.values(details).flat().join(', ');
      return `Validation failed: ${messages}`;
    }
    return errorObj?.message || 'Invalid request data.';
  }
  
  // Generic error with context
  const errorObj = error as { message?: string };
  return errorObj?.message || `${context} failed. Please try again.`;
}

export function handleClientError(error: unknown, context: string): never {
  reportError(error, context);
  
  if (isAuthError(error)) {
    // Clear auth and redirect to login
    useAuthStore.getState().logout();
    window.location.href = '/login';
  }
  
  const message = getErrorMessage(error, context);
  throw new Error(message);
}

// Retry configuration for different error types
export function shouldRetry(error: unknown, attempt: number): boolean {
  const maxAttempts = 3;
  
  if (attempt >= maxAttempts) return false;
  
  // Don't retry auth errors or validation errors
  if (isAuthError(error) || isValidationError(error)) {
    return false;
  }
  
  // Retry network errors and server errors
  if (isNetworkError(error) || isServerError(error)) {
    return true;
  }
  
  // Retry rate limit errors with exponential backoff
  if (isRateLimitError(error)) {
    return attempt < 2; // Only retry rate limits twice
  }
  
  return false;
}

export function getRetryDelay(attempt: number, error?: unknown): number {
  // Base delay of 1 second
  let delay = 1000;
  
  // Exponential backoff
  delay *= Math.pow(2, attempt);
  
  // Add some jitter to prevent thundering herd
  delay += Math.random() * 1000;
  
  // Rate limit errors should wait longer
  if (error && isRateLimitError(error)) {
    delay = Math.max(delay, 5000); // At least 5 seconds
  }
  
  // Cap at 30 seconds
  return Math.min(delay, 30000);
}