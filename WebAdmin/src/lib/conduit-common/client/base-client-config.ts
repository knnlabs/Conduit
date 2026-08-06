/**
 * Base client configuration types for SDK HTTP clients
 */

import type { Logger, CacheProvider, ClientLifecycleCallbacks } from "./types";
import type { RetryStrategy } from "./retry-strategy";

/**
 * Base configuration shared by all API clients
 */
export interface BaseApiClientConfig extends ClientLifecycleCallbacks {
  /** Base URL for API requests (trailing slash will be removed) */
  baseUrl: string;

  /** Request timeout in milliseconds (default: 60000) */
  timeout?: number;

  /** Default headers included with all requests */
  defaultHeaders?: Record<string, string>;

  /** Retry strategy configuration */
  retryStrategy?: RetryStrategy;

  /** Enable debug logging (default: false) */
  debug?: boolean;

  /** Optional logger for structured logging */
  logger?: Logger;

  /** Optional cache provider for response caching */
  cache?: CacheProvider;
}

/**
 * Configuration for clients that support caching
 * @deprecated Use BaseApiClientConfig with optional cache property
 */
export interface CacheableClientConfig extends BaseApiClientConfig {
  /** Cache provider for response caching */
  cache?: CacheProvider;
}

/**
 * Configuration for clients that support logging
 * @deprecated Use BaseApiClientConfig with optional logger property
 */
export interface LoggableClientConfig extends BaseApiClientConfig {
  /** Logger instance for structured logging */
  logger?: Logger;
}

/**
 * Full-featured client configuration with all optional features
 * Used by Admin SDK which supports both caching and logging
 */
export interface FullFeaturedClientConfig extends BaseApiClientConfig {
  /** Cache provider for response caching */
  cache?: CacheProvider;
  /** Logger instance for structured logging */
  logger?: Logger;
}
