import { ConduitAdminClient } from '@knn_labs/conduit-admin-client';
import { ConduitCoreClient } from '@knn_labs/conduit-core-client';
import { useAuthStore } from '@/stores/useAuthStore';
import { reportError } from '@/lib/utils/logging';

// Admin client configuration - uses master key authentication
export function createAdminClient(): ConduitAdminClient {
  const { user } = useAuthStore.getState();
  
  if (!user?.masterKey) {
    throw new Error('No master key available for admin client');
  }

  return new ConduitAdminClient({
    adminApiUrl: process.env.NEXT_PUBLIC_CONDUIT_ADMIN_API_URL!,
    masterKey: user.masterKey,
    options: {
      timeout: 30000,
    }
  });
}

// Core client configuration - uses virtual key authentication
export function createCoreClient(virtualKey?: string): ConduitCoreClient {
  if (!virtualKey) {
    throw new Error('Virtual key required for core client');
  }

  return new ConduitCoreClient({
    baseURL: process.env.NEXT_PUBLIC_CONDUIT_CORE_API_URL!,
    apiKey: virtualKey,
    timeout: 30000,
  });
}

// Singleton instances for common usage
let adminClientInstance: ConduitAdminClient | null = null;

export function getAdminClient(): ConduitAdminClient {
  try {
    // Always create a fresh client to ensure we have the latest auth state
    adminClientInstance = createAdminClient();
    return adminClientInstance;
  } catch (error) {
    console.error('Failed to create admin client:', error);
    throw error;
  }
}

// Helper to check if we can create clients
export function canCreateAdminClient(): boolean {
  const { user } = useAuthStore.getState();
  return !!(user?.masterKey && user.isAuthenticated);
}

export function canCreateCoreClient(virtualKey?: string): boolean {
  return !!virtualKey;
}

// Error handling helpers
export function isAuthError(error: any): boolean {
  return error?.status === 401 || error?.status === 403;
}

export function isNetworkError(error: any): boolean {
  return error?.name === 'NetworkError' || 
         error?.code === 'NETWORK_ERROR' ||
         error?.message?.includes('Failed to fetch') ||
         error?.message?.includes('Network request failed') ||
         !navigator.onLine;
}

export function isRateLimitError(error: any): boolean {
  return error?.status === 429;
}

export function isServerError(error: any): boolean {
  return error?.status >= 500 && error?.status < 600;
}

export function isValidationError(error: any): boolean {
  return error?.status === 400 || error?.status === 422;
}

export function getErrorMessage(error: any, context: string): string {
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
    const details = error?.response?.data?.errors || error?.details;
    if (details && typeof details === 'object') {
      const messages = Object.values(details).flat().join(', ');
      return `Validation failed: ${messages}`;
    }
    return error?.message || 'Invalid request data.';
  }
  
  // Generic error with context
  return error?.message || `${context} failed. Please try again.`;
}

export function handleClientError(error: any, context: string): never {
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
export function shouldRetry(error: any, attempt: number): boolean {
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

export function getRetryDelay(attempt: number, error?: any): number {
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