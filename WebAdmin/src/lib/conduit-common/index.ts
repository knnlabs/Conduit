/**
 * the local shared utilities - Shared types for Conduit SDK clients
 */

// Base types
export * from "./types/base";

// Pagination types
export * from "./types/pagination";

// Capability types
export * from "./types/capabilities";

// Error types and utilities
export * from "./errors";

// HTTP types and utilities
export * from "./http";

// SignalR types and base classes
export * from "./signalr";

// Client configuration types
export * from "./client";

// Explicit exports for types that might get tree-shaken
export type {
  Logger,
  CacheProvider,
  RequestConfigInfo,
  ResponseInfo,
} from "./client/types";
export { HttpError } from "./client/types";
export type { SignalRConfig } from "./client/signalr-config";
export type { SignalRConnectionOptions } from "./signalr/types";

// Explicit exports for BaseApiClient (may get tree-shaken)
export { BaseApiClient } from "./client/BaseApiClient";
export type { BaseRequestOptions } from "./client/BaseApiClient";
export type {
  BaseApiClientConfig,
  CacheableClientConfig,
  LoggableClientConfig,
  FullFeaturedClientConfig,
} from "./client/base-client-config";
export {
  RetryStrategyType,
  calculateRetryDelay,
  getMaxRetries,
  shouldRetryWithStrategy,
  DEFAULT_RETRY_STRATEGIES,
} from "./client/retry-strategy";
export type {
  RetryStrategy,
  FixedDelayConfig,
  ExponentialBackoffConfig,
  CustomDelaysConfig,
} from "./client/retry-strategy";

// Formatting utilities
export * from "./formatting";

// Validation utilities (type guards, model patterns, form validators)
export * from "./validation";
