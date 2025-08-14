/**
 * Safe logging utilities for production environments
 * Prevents sensitive data from being logged to console
 */

/* global performance */

// Sensitive keys that should never be logged
const SENSITIVE_KEYS = [
  'masterkey', 'master_key', 'password', 'secret', 'apikey', 'api_key',
  'token', 'auth', 'authorization', 'credential', 'key', 'privatekey',
  'private_key', 'sessionid', 'session_id', 'cookie', 'bearer'
];

/**
 * Check if a string contains sensitive information
 */
function isSensitiveKey(key: string): boolean {
  const lowerKey = key.toLowerCase();
  return SENSITIVE_KEYS.some(sensitiveKey => lowerKey.includes(sensitiveKey));
}

/**
 * Sanitize an object by masking sensitive values
 */
function sanitizeObject(obj: unknown, depth = 0): unknown {
  if (depth > 10) return '[Max Depth Reached]'; // Prevent infinite recursion
  
  if (obj === null || obj === undefined) return obj;
  
  if (typeof obj === 'string') {
    // Don't log very long strings that might contain sensitive data
    if (obj.length > 500) {
      return `[String Length: ${obj.length}]`;
    }
    return obj;
  }
  
  if (typeof obj !== 'object') return obj;
  
  if (Array.isArray(obj)) {
    return obj.map(item => sanitizeObject(item, depth + 1));
  }
  
  const sanitized: Record<string, unknown> = {};
  
  for (const [key, value] of Object.entries(obj as Record<string, unknown>)) {
    if (isSensitiveKey(key)) {
      sanitized[key] = typeof value === 'string' && value.length > 0 
        ? `[REDACTED: ${value.length} chars]` 
        : '[REDACTED]';
    } else {
      sanitized[key] = sanitizeObject(value, depth + 1);
    }
  }
  
  return sanitized;
}

/**
 * Safe console.log that automatically sanitizes sensitive data
 */
export function safeLog(message: string, ...args: unknown[]) {
  if (process.env.NODE_ENV === 'production' && !process.env.NEXT_PUBLIC_ENABLE_DEBUG_LOGS) {
    return; // Don't log in production unless explicitly enabled
  }
  
  const sanitizedArgs = args.map(arg => sanitizeObject(arg));
  console.warn(`[Conduit DEBUG] ${message}`, ...sanitizedArgs);
}

/**
 * Safe console.warn that automatically sanitizes sensitive data
 */
export function safeWarn(message: string, ...args: unknown[]) {
  const sanitizedArgs = args.map(arg => sanitizeObject(arg));
  console.warn(`[Conduit] ${message}`, ...sanitizedArgs);
}

/**
 * Safe console.error that automatically sanitizes sensitive data
 */
export function safeError(message: string, ...args: unknown[]) {
  const sanitizedArgs = args.map(arg => sanitizeObject(arg));
  console.error(`[Conduit] ${message}`, ...sanitizedArgs);
}

/**
 * Debug logging that only works in development
 */
export function debugLog(message: string, ...args: unknown[]) {
  if (process.env.NODE_ENV === 'development') {
    safeLog(`[DEBUG] ${message}`, ...args);
  }
}

/**
 * Log performance metrics safely
 */
export function perfLog(operation: string, duration: number, metadata?: unknown) {
  if (process.env.NODE_ENV === 'development' || process.env.NEXT_PUBLIC_ENABLE_PERF_LOGS) {
    safeLog(`[PERF] ${operation}: ${duration}ms`, metadata ? sanitizeObject(metadata) : undefined);
  }
}

/**
 * Create a performance timer
 */
export function createPerfTimer(operation: string) {
  const start = typeof performance !== 'undefined' ? performance.now() : Date.now();
  
  return {
    end: (metadata?: unknown) => {
      const end = typeof performance !== 'undefined' ? performance.now() : Date.now();
      const duration = end - start;
      perfLog(operation, duration, metadata);
      return duration;
    }
  };
}

/**
 * Safe error reporting that removes sensitive data
 */
export function reportError(error: unknown, context?: string, metadata?: unknown) {
  const errorObj = error instanceof Error ? error : new Error(String(error));
  const sanitizedMetadata = metadata ? sanitizeObject(metadata) : undefined;
  
  safeError(
    context ? `Error in ${context}` : 'Unexpected error',
    {
      message: errorObj.message,
      name: errorObj.name,
      stack: process.env.NODE_ENV === 'development' ? errorObj.stack : '[Stack trace hidden in production]',
      metadata: sanitizedMetadata
    }
  );
  
  // In production, you might want to send this to an error reporting service
  if (process.env.NODE_ENV === 'production' && process.env.NEXT_PUBLIC_ERROR_REPORTING_URL) {
    // Send to error reporting service (implementation depends on your service)
    // Example: Sentry, LogRocket, etc.
  }
}

/**
 * Generate a unique request ID for tracking
 */
export function generateRequestId(): string {
  return `req_${Date.now()}_${Math.random().toString(36).substring(2, 9)}`;
}

/**
 * Logger object that provides a consistent logging interface
 */
export const logger = {
  info: safeLog,
  warn: safeWarn,
  error: safeError,
  debug: debugLog,
  perf: perfLog,
};