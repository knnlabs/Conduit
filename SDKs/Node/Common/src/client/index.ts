// Client configuration types
export {
  Logger,
  CacheProvider,
  RetryConfig,
  HttpError,
  RequestConfigInfo,
  ResponseInfo,
  ClientLifecycleCallbacks,
  BaseClientOptions
} from './types';

// Base API client
export { BaseApiClient, BaseClientConfig, BaseRequestOptions } from './BaseApiClient';

// SignalR configuration
export { SignalRConfig } from './signalr-config';

// Re-export SignalR connection options from the main SignalR module
export { SignalRConnectionOptions } from '../signalr/types';