import * as signalR from '@microsoft/signalr';

/**
 * Base response types shared across all Conduit SDK clients
 */
interface PaginatedResponse<T> {
    items: T[];
    totalCount: number;
    pageNumber: number;
    pageSize: number;
    totalPages: number;
}
interface PagedResponse<T> {
    data: T[];
    totalCount: number;
    page: number;
    pageSize: number;
    hasNextPage: boolean;
    hasPreviousPage: boolean;
}
interface ErrorResponse {
    error: string;
    message?: string;
    details?: Record<string, unknown>;
    statusCode?: number;
}
type SortDirection = 'asc' | 'desc';
interface SortOptions {
    field: string;
    direction: SortDirection;
}
interface FilterOptions {
    search?: string;
    sortBy?: SortOptions;
    pageNumber?: number;
    pageSize?: number;
}
interface DateRange {
    startDate: string;
    endDate: string;
}
/**
 * Common usage tracking interface
 */
interface Usage {
    prompt_tokens: number;
    completion_tokens: number;
    total_tokens: number;
    is_batch?: boolean;
    image_quality?: string;
    cached_input_tokens?: number;
    cached_write_tokens?: number;
    search_units?: number;
    inference_steps?: number;
    image_count?: number;
    video_duration_seconds?: number;
    video_resolution?: string;
    audio_duration_seconds?: number;
}
/**
 * Performance metrics for API calls
 */
interface PerformanceMetrics {
    provider_name: string;
    provider_response_time_ms: number;
    total_response_time_ms: number;
    tokens_per_second?: number;
}

/**
 * Pagination and filtering types shared across Conduit SDK clients
 */
interface PaginationParams {
    page?: number;
    pageSize?: number;
}
interface SearchParams extends PaginationParams {
    search?: string;
    sortBy?: string;
    sortDirection?: 'asc' | 'desc';
}
interface TimeRangeParams {
    startDate?: string;
    endDate?: string;
    timezone?: string;
}
interface BatchOperationParams {
    batchSize?: number;
    parallel?: boolean;
    continueOnError?: boolean;
}

/**
 * Model capability definitions shared across Conduit SDK clients
 */
/**
 * Core model capabilities supported by Conduit
 */
declare enum ModelCapability {
    CHAT = "chat",
    VISION = "vision",
    IMAGE_GENERATION = "image-generation",
    IMAGE_EDIT = "image-edit",
    IMAGE_VARIATION = "image-variation",
    AUDIO_TRANSCRIPTION = "audio-transcription",
    TEXT_TO_SPEECH = "text-to-speech",
    REALTIME_AUDIO = "realtime-audio",
    EMBEDDINGS = "embeddings",
    VIDEO_GENERATION = "video-generation"
}
/**
 * Model capability metadata
 */
interface ModelCapabilityInfo {
    id: ModelCapability;
    displayName: string;
    description?: string;
    category: 'text' | 'vision' | 'audio' | 'video';
}
/**
 * Model capabilities definition for a specific model
 */
interface ModelCapabilities {
    modelId: string;
    capabilities: ModelCapability[];
    constraints?: ModelConstraints;
}
/**
 * Model-specific constraints
 */
interface ModelConstraints {
    maxTokens?: number;
    maxImages?: number;
    supportedImageSizes?: string[];
    supportedImageFormats?: string[];
    supportedAudioFormats?: string[];
    supportedVideoSizes?: string[];
    supportedLanguages?: string[];
    supportedVoices?: string[];
    maxDuration?: number;
}
/**
 * Get user-friendly display name for a capability
 */
declare function getCapabilityDisplayName(capability: ModelCapability): string;
/**
 * Get capability category
 */
declare function getCapabilityCategory(capability: ModelCapability): 'text' | 'vision' | 'audio' | 'video';

/**
 * Common error types for Conduit SDK clients
 *
 * This module provides a unified error hierarchy for both Admin and Core SDKs,
 * consolidating previously duplicated error classes.
 */
declare class ConduitError extends Error {
    statusCode: number;
    code: string;
    context?: Record<string, unknown>;
    details?: unknown;
    endpoint?: string;
    method?: string;
    type?: string;
    param?: string;
    constructor(message: string, statusCode?: number, code?: string, context?: Record<string, unknown>);
    toJSON(): {
        name: string;
        message: string;
        statusCode: number;
        code: string;
        context: Record<string, unknown> | undefined;
        details: unknown;
        endpoint: string | undefined;
        method: string | undefined;
        type: string | undefined;
        param: string | undefined;
        timestamp: string;
    };
    toSerializable(): {
        name: string;
        message: string;
        statusCode: number;
        code: string;
        context: Record<string, unknown> | undefined;
        details: unknown;
        endpoint: string | undefined;
        method: string | undefined;
        type: string | undefined;
        param: string | undefined;
        timestamp: string;
        isConduitError: boolean;
    };
    static fromSerializable(data: unknown): ConduitError;
}
declare class AuthError extends ConduitError {
    constructor(message?: string, context?: Record<string, unknown>);
}
declare class AuthenticationError extends AuthError {
}
declare class AuthorizationError extends ConduitError {
    constructor(message?: string, context?: Record<string, unknown>);
}
declare class ValidationError extends ConduitError {
    field?: string;
    constructor(message?: string, context?: Record<string, unknown>);
}
declare class NotFoundError extends ConduitError {
    constructor(message?: string, context?: Record<string, unknown>);
}
declare class ConflictError extends ConduitError {
    constructor(message?: string, context?: Record<string, unknown>);
}
declare class InsufficientBalanceError extends ConduitError {
    balance?: number;
    requiredAmount?: number;
    constructor(message?: string, context?: Record<string, unknown>);
}
declare class RateLimitError extends ConduitError {
    retryAfter?: number;
    constructor(message?: string, retryAfter?: number, context?: Record<string, unknown>);
}
declare class ServerError extends ConduitError {
    constructor(message?: string, context?: Record<string, unknown>);
}
declare class NetworkError extends ConduitError {
    constructor(message?: string, context?: Record<string, unknown>);
}
declare class TimeoutError extends ConduitError {
    constructor(message?: string, context?: Record<string, unknown>);
}
declare class NotImplementedError extends ConduitError {
    constructor(message: string, context?: Record<string, unknown>);
}
declare class StreamError extends ConduitError {
    constructor(message?: string, context?: Record<string, unknown>);
}
declare function isConduitError(error: unknown): error is ConduitError;
declare function isAuthError(error: unknown): error is AuthError;
declare function isAuthorizationError(error: unknown): error is AuthorizationError;
declare function isValidationError(error: unknown): error is ValidationError;
declare function isNotFoundError(error: unknown): error is NotFoundError;
declare function isConflictError(error: unknown): error is ConflictError;
declare function isInsufficientBalanceError(error: unknown): error is InsufficientBalanceError;
declare function isRateLimitError(error: unknown): error is RateLimitError;
declare function isNetworkError(error: unknown): error is NetworkError;
declare function isStreamError(error: unknown): error is StreamError;
declare function isTimeoutError(error: unknown): error is TimeoutError;
declare function isSerializedConduitError(data: unknown): data is ReturnType<ConduitError['toSerializable']>;
declare function isHttpError(error: unknown): error is {
    response: {
        status: number;
        data: unknown;
        headers: Record<string, string>;
    };
    message: string;
    request?: unknown;
    code?: string;
};
declare function isHttpNetworkError(error: unknown): error is {
    request: unknown;
    message: string;
    code?: string;
};
declare function isErrorLike(error: unknown): error is {
    message: string;
};
declare function serializeError(error: unknown): Record<string, unknown>;
declare function deserializeError(data: unknown): Error;
declare function getErrorMessage(error: unknown): string;
declare function getErrorStatusCode(error: unknown): number;
/**
 * Handle API errors and convert them to appropriate ConduitError types
 * This function is primarily used by the Admin SDK
 */
declare function handleApiError(error: unknown, endpoint?: string, method?: string): never;
/**
 * Create an error from an ErrorResponse format
 * This function is primarily used by the Core SDK for legacy compatibility
 */
interface ErrorResponseFormat {
    error: {
        message: string;
        type?: string;
        code?: string;
        param?: string;
    };
}
declare function createErrorFromResponse(response: ErrorResponseFormat, statusCode?: number): ConduitError;

/**
 * HTTP methods enum for type-safe API requests
 */
declare enum HttpMethod {
    GET = "GET",
    POST = "POST",
    PUT = "PUT",
    DELETE = "DELETE",
    PATCH = "PATCH",
    HEAD = "HEAD",
    OPTIONS = "OPTIONS"
}
/**
 * Type guard to check if a string is a valid HTTP method
 */
declare function isHttpMethod(method: string): method is HttpMethod;
/**
 * Request options with proper typing
 */
interface RequestOptions<TRequest = unknown> {
    headers?: Record<string, string>;
    signal?: AbortSignal;
    timeout?: number;
    body?: TRequest;
    params?: Record<string, string | number | boolean>;
    responseType?: 'json' | 'text' | 'blob' | 'arraybuffer';
}
/**
 * Type-safe response interface
 */
interface ApiResponse<T = unknown> {
    data: T;
    status: number;
    statusText: string;
    headers: Record<string, string>;
}
/**
 * Extended fetch options that include response type hints
 * This provides a cleaner way to handle different response types
 */
interface ExtendedRequestInit extends RequestInit {
    /**
     * Hint for how to parse the response body
     * This is not a standard fetch option but helps our client handle responses correctly
     */
    responseType?: 'json' | 'text' | 'blob' | 'arraybuffer' | 'stream';
    /**
     * Custom timeout in milliseconds
     */
    timeout?: number;
    /**
     * Request metadata for logging/debugging
     */
    metadata?: {
        /** Operation name for debugging */
        operation?: string;
        /** Start time for performance tracking */
        startTime?: number;
        /** Request ID for tracing */
        requestId?: string;
    };
}

/**
 * Response parser that handles different response types based on content-type and hints
 */
declare class ResponseParser {
    /**
     * Parses a fetch Response based on content type and response type hint
     */
    static parse<T>(response: Response, responseType?: ExtendedRequestInit['responseType']): Promise<T>;
    /**
     * Creates a clean RequestInit object without custom properties
     */
    static cleanRequestInit(init: ExtendedRequestInit): RequestInit;
}

/**
 * Common HTTP constants shared across all SDKs
 */
/**
 * HTTP headers used across SDKs
 */
declare const HTTP_HEADERS: {
    readonly CONTENT_TYPE: "Content-Type";
    readonly AUTHORIZATION: "Authorization";
    readonly X_API_KEY: "X-API-Key";
    readonly USER_AGENT: "User-Agent";
    readonly X_CORRELATION_ID: "X-Correlation-Id";
    readonly RETRY_AFTER: "Retry-After";
    readonly ACCEPT: "Accept";
    readonly CACHE_CONTROL: "Cache-Control";
};
type HttpHeader = typeof HTTP_HEADERS[keyof typeof HTTP_HEADERS];
/**
 * Content types
 */
declare const CONTENT_TYPES: {
    readonly JSON: "application/json";
    readonly FORM_DATA: "multipart/form-data";
    readonly FORM_URLENCODED: "application/x-www-form-urlencoded";
    readonly TEXT_PLAIN: "text/plain";
    readonly TEXT_STREAM: "text/event-stream";
};
type ContentType = typeof CONTENT_TYPES[keyof typeof CONTENT_TYPES];
/**
 * HTTP status codes
 */
declare const HTTP_STATUS: {
    readonly OK: 200;
    readonly CREATED: 201;
    readonly NO_CONTENT: 204;
    readonly BAD_REQUEST: 400;
    readonly UNAUTHORIZED: 401;
    readonly FORBIDDEN: 403;
    readonly NOT_FOUND: 404;
    readonly CONFLICT: 409;
    readonly TOO_MANY_REQUESTS: 429;
    readonly RATE_LIMITED: 429;
    readonly INTERNAL_SERVER_ERROR: 500;
    readonly INTERNAL_ERROR: 500;
    readonly BAD_GATEWAY: 502;
    readonly SERVICE_UNAVAILABLE: 503;
    readonly GATEWAY_TIMEOUT: 504;
};
type HttpStatusCode = typeof HTTP_STATUS[keyof typeof HTTP_STATUS];
/**
 * Error codes for network errors
 */
declare const ERROR_CODES: {
    readonly CONNECTION_ABORTED: "ECONNABORTED";
    readonly TIMEOUT: "ETIMEDOUT";
    readonly CONNECTION_RESET: "ECONNRESET";
    readonly NETWORK_UNREACHABLE: "ENETUNREACH";
    readonly CONNECTION_REFUSED: "ECONNREFUSED";
    readonly HOST_NOT_FOUND: "ENOTFOUND";
};
type ErrorCode = typeof ERROR_CODES[keyof typeof ERROR_CODES];
/**
 * Default timeout values in milliseconds
 */
declare const TIMEOUTS: {
    readonly DEFAULT_REQUEST: 60000;
    readonly SHORT_REQUEST: 10000;
    readonly LONG_REQUEST: 300000;
    readonly STREAMING: 0;
};
type TimeoutValue = typeof TIMEOUTS[keyof typeof TIMEOUTS];
/**
 * Retry configuration defaults
 */
declare const RETRY_CONFIG: {
    readonly DEFAULT_MAX_RETRIES: 3;
    readonly INITIAL_DELAY: 1000;
    readonly MAX_DELAY: 30000;
    readonly BACKOFF_FACTOR: 2;
};
type RetryConfigValue = typeof RETRY_CONFIG[keyof typeof RETRY_CONFIG];

/**
 * SignalR hub connection states
 */
declare enum HubConnectionState {
    Disconnected = "Disconnected",
    Connecting = "Connecting",
    Connected = "Connected",
    Disconnecting = "Disconnecting",
    Reconnecting = "Reconnecting"
}
/**
 * SignalR logging levels
 */
declare enum SignalRLogLevel {
    Trace = 0,
    Debug = 1,
    Information = 2,
    Warning = 3,
    Error = 4,
    Critical = 5,
    None = 6
}
/**
 * HTTP transport types for SignalR
 */
declare enum HttpTransportType {
    None = 0,
    WebSockets = 1,
    ServerSentEvents = 2,
    LongPolling = 4
}
/**
 * Default transport configuration
 */
declare const DefaultTransports: number;
/**
 * Base SignalR connection options
 */
interface SignalRConnectionOptions {
    /**
     * Logging level
     */
    logLevel?: SignalRLogLevel;
    /**
     * Transport types to use
     */
    transport?: HttpTransportType;
    /**
     * Headers to include with requests
     */
    headers?: Record<string, string>;
    /**
     * Access token factory for authentication
     */
    accessTokenFactory?: () => string | Promise<string>;
    /**
     * Close timeout in milliseconds
     */
    closeTimeout?: number;
    /**
     * Reconnection delay intervals in milliseconds
     */
    reconnectionDelay?: number[];
    /**
     * Server timeout in milliseconds
     */
    serverTimeout?: number;
    /**
     * Keep-alive interval in milliseconds
     */
    keepAliveInterval?: number;
}
/**
 * Authentication configuration for SignalR connections
 */
interface SignalRAuthConfig {
    /**
     * Authentication token or key
     */
    authToken: string;
    /**
     * Authentication type (e.g., 'master', 'virtual')
     */
    authType: 'master' | 'virtual';
    /**
     * Additional headers for authentication
     */
    additionalHeaders?: Record<string, string>;
}
/**
 * SignalR hub method argument types for type safety
 */
type SignalRPrimitive = string | number | boolean | null | undefined;
type SignalRValue = SignalRPrimitive | SignalRArgs | SignalRPrimitive[];
interface SignalRArgs {
    [key: string]: SignalRValue;
}

/**
 * Base configuration for SignalR connections
 */
interface BaseSignalRConfig {
    /**
     * Base URL for the SignalR hub
     */
    baseUrl: string;
    /**
     * Authentication configuration
     */
    auth: SignalRAuthConfig;
    /**
     * Connection options
     */
    options?: SignalRConnectionOptions;
    /**
     * User agent string
     */
    userAgent?: string;
}
/**
 * Base class for SignalR hub connections with automatic reconnection and error handling.
 * This abstract class provides common functionality for both Admin and Core SDKs.
 */
declare abstract class BaseSignalRConnection {
    protected connection?: signalR.HubConnection;
    protected readonly config: BaseSignalRConfig;
    protected connectionReadyPromise: Promise<void>;
    private connectionReadyResolve?;
    private connectionReadyReject?;
    private disposed;
    /**
     * Gets the hub path for this connection type.
     */
    protected abstract get hubPath(): string;
    constructor(config: BaseSignalRConfig);
    /**
     * Gets whether the connection is established and ready for use.
     */
    get isConnected(): boolean;
    /**
     * Gets the current connection state.
     */
    get state(): HubConnectionState;
    /**
     * Event handlers
     */
    onConnected?: () => Promise<void>;
    onDisconnected?: (error?: Error) => Promise<void>;
    onReconnecting?: (error?: Error) => Promise<void>;
    onReconnected?: (connectionId?: string) => Promise<void>;
    /**
     * Establishes the SignalR connection.
     */
    protected getConnection(): Promise<signalR.HubConnection>;
    /**
     * Configures hub-specific event handlers. Override in derived classes.
     */
    protected abstract configureHubHandlers(connection: signalR.HubConnection): void;
    /**
     * Maps transport type enum to SignalR transport.
     */
    protected mapTransportType(transport: HttpTransportType): signalR.HttpTransportType;
    /**
     * Maps log level enum to SignalR log level.
     */
    protected mapLogLevel(level: SignalRLogLevel): signalR.LogLevel;
    /**
     * Builds headers for the connection based on configuration.
     */
    private buildHeaders;
    /**
     * Waits for the connection to be ready.
     */
    waitForReady(): Promise<void>;
    /**
     * Invokes a method on the hub with proper error handling.
     */
    protected invoke<T = void>(methodName: string, ...args: unknown[]): Promise<T>;
    /**
     * Sends a message to the hub without expecting a response.
     */
    protected send(methodName: string, ...args: unknown[]): Promise<void>;
    /**
     * Disconnects the SignalR connection.
     */
    disconnect(): Promise<void>;
    /**
     * Disposes of the connection and cleans up resources.
     */
    dispose(): Promise<void>;
}

/**
 * Logger interface for client logging
 */
interface Logger {
    debug(message: string, ...args: unknown[]): void;
    info(message: string, ...args: unknown[]): void;
    warn(message: string, ...args: unknown[]): void;
    error(message: string, ...args: unknown[]): void;
}
/**
 * Cache provider interface for client-side caching
 */
interface CacheProvider {
    get<T>(key: string): Promise<T | null>;
    set<T>(key: string, value: T, ttl?: number): Promise<void>;
    delete(key: string): Promise<void>;
    clear(): Promise<void>;
}
/**
 * Base retry configuration interface
 *
 * Note: The Admin and Core SDKs have different retry strategies:
 * - Admin SDK uses simple fixed delay retry
 * - Core SDK uses exponential backoff
 *
 * This base interface supports both patterns.
 */
interface RetryConfig {
    /**
     * Maximum number of retry attempts
     */
    maxRetries: number;
    /**
     * For Admin SDK: Fixed delay between retries in milliseconds
     * For Core SDK: Initial delay for exponential backoff
     */
    retryDelay?: number;
    /**
     * For Core SDK: Initial delay for exponential backoff
     */
    initialDelay?: number;
    /**
     * For Core SDK: Maximum delay between retries
     */
    maxDelay?: number;
    /**
     * For Core SDK: Backoff multiplication factor
     */
    factor?: number;
    /**
     * Custom retry condition function
     */
    retryCondition?: (error: unknown) => boolean;
}
/**
 * HTTP error class
 */
declare class HttpError extends Error {
    code?: string;
    response?: {
        status: number;
        data: unknown;
        headers: Record<string, string>;
    };
    request?: unknown;
    config?: {
        url?: string;
        method?: string;
        _retry?: number;
    };
    constructor(message: string, code?: string);
}
/**
 * Request configuration information
 */
interface RequestConfigInfo {
    method: string;
    url: string;
    headers: Record<string, string>;
    data?: unknown;
    params?: Record<string, unknown>;
}
/**
 * Response information
 */
interface ResponseInfo {
    status: number;
    statusText: string;
    headers: Record<string, string>;
    data: unknown;
    config: RequestConfigInfo;
}
/**
 * Base client lifecycle callbacks
 */
interface ClientLifecycleCallbacks {
    /**
     * Callback invoked on any error
     */
    onError?: (error: Error) => void;
    /**
     * Callback invoked before each request
     */
    onRequest?: (config: RequestConfigInfo) => void | Promise<void>;
    /**
     * Callback invoked after each response
     */
    onResponse?: (response: ResponseInfo) => void | Promise<void>;
}
/**
 * Base client configuration options
 */
interface BaseClientOptions extends ClientLifecycleCallbacks {
    /**
     * Request timeout in milliseconds
     */
    timeout?: number;
    /**
     * Retry configuration
     */
    retries?: number | RetryConfig;
    /**
     * Logger instance for client logging
     */
    logger?: Logger;
    /**
     * Cache provider for response caching
     */
    cache?: CacheProvider;
    /**
     * Custom headers to include with all requests
     */
    headers?: Record<string, string>;
    /**
     * Custom retry delays in milliseconds (overrides retry config)
     * @default [1000, 2000, 4000, 8000, 16000]
     */
    retryDelay?: number[];
    /**
     * Custom function to validate response status
     */
    validateStatus?: (status: number) => boolean;
    /**
     * Enable debug mode
     */
    debug?: boolean;
}

/**
 * SignalR client configuration
 */
interface SignalRConfig {
    /**
     * Whether SignalR is enabled
     * @default true
     */
    enabled?: boolean;
    /**
     * Whether to automatically connect on client initialization
     * @default true
     */
    autoConnect?: boolean;
    /**
     * Reconnection delays in milliseconds (exponential backoff)
     * @default [0, 2000, 10000, 30000]
     */
    reconnectDelay?: number[];
    /**
     * SignalR logging level
     * @default SignalRLogLevel.Information
     */
    logLevel?: SignalRLogLevel;
    /**
     * HTTP transport type
     * @default HttpTransportType.WebSockets | HttpTransportType.ServerSentEvents | HttpTransportType.LongPolling
     */
    transport?: HttpTransportType;
    /**
     * Custom headers for SignalR connections
     */
    headers?: Record<string, string>;
    /**
     * Connection timeout in milliseconds
     * @default 30000
     */
    connectionTimeout?: number;
}

export { type ApiResponse, AuthError, AuthenticationError, AuthorizationError, type BaseClientOptions, type BaseSignalRConfig, BaseSignalRConnection, type BatchOperationParams, CONTENT_TYPES, type CacheProvider, type ClientLifecycleCallbacks, ConduitError, ConflictError, type ContentType, type DateRange, DefaultTransports, ERROR_CODES, type ErrorCode, type ErrorResponse, type ErrorResponseFormat, type ExtendedRequestInit, type FilterOptions, HTTP_HEADERS, HTTP_STATUS, HttpError, type HttpHeader, HttpMethod, type HttpStatusCode, HttpTransportType, HubConnectionState, InsufficientBalanceError, type Logger, type ModelCapabilities, ModelCapability, type ModelCapabilityInfo, type ModelConstraints, NetworkError, NotFoundError, NotImplementedError, type PagedResponse, type PaginatedResponse, type PaginationParams, type PerformanceMetrics, RETRY_CONFIG, RateLimitError, type RequestConfigInfo, type RequestOptions, type ResponseInfo, ResponseParser, type RetryConfig, type RetryConfigValue, type SearchParams, ServerError, type SignalRArgs, type SignalRAuthConfig, type SignalRConfig, type SignalRConnectionOptions, SignalRLogLevel, type SignalRValue, type SortDirection, type SortOptions, StreamError, TIMEOUTS, type TimeRangeParams, TimeoutError, type TimeoutValue, type Usage, ValidationError, createErrorFromResponse, deserializeError, getCapabilityCategory, getCapabilityDisplayName, getErrorMessage, getErrorStatusCode, handleApiError, isAuthError, isAuthorizationError, isConduitError, isConflictError, isErrorLike, isHttpError, isHttpMethod, isHttpNetworkError, isInsufficientBalanceError, isNetworkError, isNotFoundError, isRateLimitError, isSerializedConduitError, isStreamError, isTimeoutError, isValidationError, serializeError };
