// Client configuration types
export type {
  Logger,
  CacheProvider,
  RetryConfig,
  HttpError,
  RequestConfigInfo,
  ResponseInfo,
  ClientLifecycleCallbacks,
  BaseClientOptions,
} from "./types";

// SignalR configuration
export type { SignalRConfig } from "./signalr-config";

// Re-export SignalR connection options from the main SignalR module
export type { SignalRConnectionOptions } from "../signalr/types";

// Base API client
export { BaseApiClient, type BaseRequestOptions } from "./BaseApiClient";

// Base client configuration types
export type {
  BaseApiClientConfig,
  CacheableClientConfig,
  LoggableClientConfig,
  FullFeaturedClientConfig,
} from "./base-client-config";

// Retry strategy types and utilities
export {
  RetryStrategyType,
  calculateRetryDelay,
  getMaxRetries,
  shouldRetryWithStrategy,
  DEFAULT_RETRY_STRATEGIES,
} from "./retry-strategy";

export type {
  RetryStrategy,
  FixedDelayConfig,
  ExponentialBackoffConfig,
  CustomDelaysConfig,
} from "./retry-strategy";
