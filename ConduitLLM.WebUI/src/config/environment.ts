/**
 * Centralized environment configuration for the Conduit WebUI
 * This module validates and provides typed access to all environment variables
 */

// Environment variable validation errors
class EnvironmentError extends Error {
  constructor(variable: string, message?: string) {
    super(`Missing or invalid environment variable: ${variable}${message ? ` - ${message}` : ''}`);
    this.name = 'EnvironmentError';
  }
}

// Helper to get required environment variable
function _getRequiredEnv(key: string, description?: string): string {
  const value = process.env[key];
  if (!value) {
    throw new EnvironmentError(key, description);
  }
  return value;
}

// Helper to get optional environment variable with default
function getOptionalEnv(key: string, defaultValue: string): string {
  return process.env[key] || defaultValue;
}

// Helper to parse boolean environment variable
function getBooleanEnv(key: string, defaultValue: boolean): boolean {
  const value = process.env[key];
  if (!value) return defaultValue;
  return value.toLowerCase() === 'true';
}

// Helper to parse number environment variable
function getNumberEnv(key: string, defaultValue: number): number {
  const value = process.env[key];
  if (!value) return defaultValue;
  const parsed = parseInt(value, 10);
  if (isNaN(parsed)) {
    throw new EnvironmentError(key, `Expected number but got: ${value}`);
  }
  return parsed;
}

// Determine if we're running on the server
const isServer = typeof window === 'undefined';

/**
 * Application environment configuration
 */
export const config = {
  // Environment
  env: {
    nodeEnv: getOptionalEnv('NODE_ENV', 'development'),
    appEnv: getOptionalEnv('NEXT_PUBLIC_APP_ENV', 'development'),
    isDevelopment: process.env.NODE_ENV === 'development',
    isProduction: process.env.NODE_ENV === 'production',
    isTest: process.env.NODE_ENV === 'test',
  },

  // API Endpoints
  api: {
    // Server-side URLs (for internal Docker networking)
    server: isServer ? {
      adminUrl: getOptionalEnv('CONDUIT_ADMIN_API_BASE_URL', 'http://localhost:5002'),
      coreUrl: getOptionalEnv('CONDUIT_API_BASE_URL', 'http://localhost:5000'),
    } : undefined,
    // External URLs for SignalR (browser-accessible, server-side only)
    external: isServer ? {
      adminUrl: getOptionalEnv('CONDUIT_ADMIN_API_EXTERNAL_URL', 'http://localhost:5002'),
      coreUrl: getOptionalEnv('CONDUIT_API_EXTERNAL_URL', 'http://localhost:5000'),
    } : undefined,
    // API configuration
    timeout: getNumberEnv('API_TIMEOUT', 30000),
    retryAttempts: getNumberEnv('API_RETRY_ATTEMPTS', 3),
    retryDelay: getNumberEnv('API_RETRY_DELAY', 1000),
  },

  // Authentication
  auth: {
    masterKey: isServer ? getOptionalEnv('CONDUIT_API_TO_API_BACKEND_AUTH_KEY', '') : '',
    sessionSecret: isServer ? getOptionalEnv('SESSION_SECRET', '') : '',
    sessionMaxAge: getNumberEnv('SESSION_MAX_AGE', 24 * 60 * 60 * 1000), // 24 hours
  },

  // Redis Configuration
  redis: {
    url: isServer ? getOptionalEnv('REDIS_URL', 'redis://localhost:6379') : '',
    sessionPrefix: getOptionalEnv('REDIS_SESSION_PREFIX', 'conduit:session:'),
  },

  // Server Configuration
  server: {
    port: getNumberEnv('PORT', 3000),
  },

  // SignalR Configuration
  signalr: {
    autoReconnect: getBooleanEnv('NEXT_PUBLIC_SIGNALR_AUTO_RECONNECT', true),
    reconnectInterval: getNumberEnv('NEXT_PUBLIC_SIGNALR_RECONNECT_INTERVAL', 5000),
    maxReconnectAttempts: getNumberEnv('SIGNALR_MAX_RECONNECT_ATTEMPTS', 10),
    keepAliveInterval: getNumberEnv('SIGNALR_KEEP_ALIVE_INTERVAL', 15000),
  },

  // Feature Flags
  features: {
    enableRealTimeUpdates: getBooleanEnv('NEXT_PUBLIC_ENABLE_REAL_TIME_UPDATES', true),
    enableAnalytics: getBooleanEnv('NEXT_PUBLIC_ENABLE_ANALYTICS', true),
    enableDebugMode: getBooleanEnv('NEXT_PUBLIC_ENABLE_DEBUG_MODE', false),
    enableExperimentalFeatures: getBooleanEnv('NEXT_PUBLIC_ENABLE_EXPERIMENTAL', false),
  },

  // Logging Configuration
  logging: {
    level: getOptionalEnv('LOG_LEVEL', 'info'),
    enableConsole: getBooleanEnv('LOG_CONSOLE', true),
    enableFile: getBooleanEnv('LOG_FILE', false),
  },

  // Cache Configuration
  cache: {
    defaultTTL: getNumberEnv('CACHE_DEFAULT_TTL', 3600), // 1 hour
    maxSize: getOptionalEnv('CACHE_MAX_SIZE', '1GB'),
    evictionPolicy: getOptionalEnv('CACHE_EVICTION_POLICY', 'LRU'),
  },
} as const;

/**
 * Validate required environment variables
 * Call this at application startup to fail fast
 */
export function validateEnvironment(): void {
  const errors: string[] = [];

  // Server-side validations
  if (isServer) {
    // Authentication is required on server
    if (!config.auth.masterKey) {
      errors.push('CONDUIT_API_TO_API_BACKEND_AUTH_KEY is required for server-side operations');
    }
    if (!config.auth.sessionSecret || config.auth.sessionSecret === 'your-session-secret-key-change-in-production') {
      errors.push('SESSION_SECRET must be set to a secure value in production');
    }
  }

  // Production validations
  if (config.env.isProduction) {
    // Ensure proper URLs in production
    if (isServer && config.api.server) {
      if (config.api.server.adminUrl.includes('localhost') || config.api.server.adminUrl.includes('127.0.0.1')) {
        errors.push('CONDUIT_ADMIN_API_BASE_URL must not use localhost in production');
      }
      if (config.api.server.coreUrl.includes('localhost') || config.api.server.coreUrl.includes('127.0.0.1')) {
        errors.push('CONDUIT_API_BASE_URL must not use localhost in production');
      }
    }

    // Debug mode should be off in production
    if (config.features.enableDebugMode) {
      errors.push('NEXT_PUBLIC_ENABLE_DEBUG_MODE should be false in production');
    }
  }

  if (errors.length > 0) {
    throw new Error(`Environment validation failed:\n${errors.join('\n')}`);
  }
}

/**
 * Get the appropriate API URL based on context
 */
export function getAdminApiUrl(): string {
  if (!isServer) {
    throw new Error('getAdminApiUrl can only be called on the server');
  }
  return config.api.server!.adminUrl;
}

export function getCoreApiUrl(): string {
  if (!isServer) {
    throw new Error('getCoreApiUrl can only be called on the server');
  }
  return config.api.server!.coreUrl;
}

/**
 * Get SignalR hub URLs (server-side only)
 */
export function getSignalRUrl(path: string): string {
  if (!isServer) {
    throw new Error('getSignalRUrl can only be called on the server');
  }
  const baseUrl = config.api.external!.adminUrl;
  return `${baseUrl}${path}`;
}

// Export type for use in other modules
export type AppConfig = typeof config;