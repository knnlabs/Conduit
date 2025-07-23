import { RetryConfig as RetryConfig$1, ResponseInfo as ResponseInfo$1, RequestOptions, HttpMethod, FilterOptions, DateRange, PagedResponse, ConduitError } from '@knn_labs/conduit-common';

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
 * SignalR client configuration
 */
interface SignalRConfig {
    enabled?: boolean;
    autoConnect?: boolean;
    reconnectDelay?: number[];
    logLevel?: number;
    transport?: number;
    headers?: Record<string, string>;
    connectionTimeout?: number;
}
/**
 * Request configuration info for callbacks
 */
interface RequestConfigInfo {
    method: string;
    url: string;
    headers: Record<string, string>;
    data?: unknown;
    params?: Record<string, unknown>;
}
interface RetryConfig extends RetryConfig$1 {
    maxRetries: number;
    retryDelay: number;
    retryCondition?: (error: unknown) => boolean;
}
interface ConduitConfig {
    masterKey: string;
    adminApiUrl: string;
    conduitApiUrl?: string;
    options?: {
        timeout?: number;
        retries?: number | RetryConfig;
        logger?: Logger;
        cache?: CacheProvider;
        headers?: Record<string, string>;
        validateStatus?: (status: number) => boolean;
        signalR?: SignalRConfig;
        /**
         * Custom retry delays in milliseconds
         * @default [1000, 2000, 4000, 8000, 16000]
         */
        retryDelay?: number[];
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
    };
}
interface RequestConfig {
    method?: string;
    url?: string;
    data?: unknown;
    params?: Record<string, unknown>;
    headers?: Record<string, string>;
    timeout?: number;
    responseType?: 'json' | 'text' | 'blob' | 'arraybuffer' | 'document' | 'stream';
    signal?: AbortSignal;
}
interface ApiClientConfig {
    baseUrl: string;
    masterKey: string;
    timeout?: number;
    retries?: number | RetryConfig;
    logger?: Logger;
    cache?: CacheProvider;
    defaultHeaders?: Record<string, string>;
    retryDelay?: number[];
    onError?: (error: Error) => void;
    onRequest?: (config: RequestConfigInfo) => void | Promise<void>;
    onResponse?: (response: ResponseInfo) => void | Promise<void>;
}
interface ResponseInfo extends ResponseInfo$1 {
    status: number;
    statusText: string;
    headers: Record<string, string>;
    data: unknown;
    config: RequestConfigInfo;
}

/**
 * Type-safe base API client for Conduit Admin using native fetch
 * Provides all functionality without HTTP complexity
 */
declare abstract class FetchBaseApiClient {
    protected readonly logger?: Logger;
    protected readonly cache?: CacheProvider;
    protected readonly retryConfig: RetryConfig;
    protected readonly retryDelays?: number[];
    protected readonly onError?: (error: Error) => void;
    protected readonly onRequest?: (config: RequestConfigInfo) => void | Promise<void>;
    protected readonly onResponse?: (response: ResponseInfo) => void | Promise<void>;
    protected readonly baseUrl: string;
    protected readonly masterKey: string;
    protected readonly timeout: number;
    protected readonly defaultHeaders: Record<string, string>;
    constructor(config: ApiClientConfig);
    private normalizeRetryConfig;
    /**
     * Type-safe request method with proper request/response typing
     */
    protected request<TResponse = unknown, TRequest = unknown>(url: string, options?: RequestOptions<TRequest> & {
        method?: HttpMethod;
    }): Promise<TResponse>;
    /**
     * Type-safe GET request
     */
    protected get<TResponse = unknown>(url: string, optionsOrParams?: {
        headers?: Record<string, string>;
        signal?: AbortSignal;
        timeout?: number;
        responseType?: 'json' | 'text' | 'blob' | 'arraybuffer';
    } | Record<string, unknown>, extraOptions?: {
        headers?: Record<string, string>;
        signal?: AbortSignal;
        timeout?: number;
        responseType?: 'json' | 'text' | 'blob' | 'arraybuffer';
    }): Promise<TResponse>;
    /**
     * Type-safe POST request
     */
    protected post<TResponse = unknown, TRequest = unknown>(url: string, data?: TRequest, options?: {
        headers?: Record<string, string>;
        signal?: AbortSignal;
        timeout?: number;
    }): Promise<TResponse>;
    /**
     * Type-safe PUT request
     */
    protected put<TResponse = unknown, TRequest = unknown>(url: string, data?: TRequest, options?: {
        headers?: Record<string, string>;
        signal?: AbortSignal;
        timeout?: number;
    }): Promise<TResponse>;
    /**
     * Type-safe PATCH request
     */
    protected patch<TResponse = unknown, TRequest = unknown>(url: string, data?: TRequest, options?: {
        headers?: Record<string, string>;
        signal?: AbortSignal;
        timeout?: number;
    }): Promise<TResponse>;
    /**
     * Type-safe DELETE request
     */
    protected delete<TResponse = unknown>(url: string, options?: {
        headers?: Record<string, string>;
        signal?: AbortSignal;
        timeout?: number;
    }): Promise<TResponse>;
    private buildUrl;
    private buildHeaders;
    private executeWithRetry;
    private parseErrorResponse;
    private calculateRetryDelay;
    private sleep;
    protected log(level: 'debug' | 'info' | 'warn' | 'error', message: string, ...args: unknown[]): void;
    protected getCacheKey(methodOrResource: string, urlOrId?: unknown, paramsOrId2?: Record<string, unknown> | string): string;
    protected getFromCache<T>(key: string): Promise<T | null>;
    protected setCache(key: string, value: unknown, ttl?: number): Promise<void>;
    /**
     * Execute a function with caching
     */
    protected withCache<T>(cacheKey: string, fn: () => Promise<T>, ttl?: number): Promise<T>;
    private buildUrlWithParams;
}

/**
 * This file is auto-generated based on the Conduit Admin API controllers
 * DO NOT MODIFY DIRECTLY
 *
 * Generated from Admin API endpoints:
 * - VirtualKeysController
 * - DashboardController
 * - ModelProviderMappingController
 * - ProviderCredentialsController
 * - GlobalSettingsController
 */
interface operations {
    VirtualKeys_GetAll: {
        parameters: {
            query?: {
                page?: number;
                pageSize?: number;
            };
        };
        responses: {
            200: {
                content: {
                    "application/json": components["schemas"]["VirtualKeyListResponseDto"];
                };
            };
            401: {
                content: {
                    "application/json": components["schemas"]["ErrorResponse"];
                };
            };
        };
    };
    Dashboard_Metrics: {
        parameters: {};
        responses: {
            200: {
                content: {
                    "application/json": {
                        totalRequests?: number;
                        totalCost?: number;
                        activeVirtualKeys?: number;
                        errorRate?: number;
                        avgResponseTime?: number;
                        topModels?: Array<{
                            model?: string;
                            requests?: number;
                            cost?: number;
                        }>;
                        recentActivity?: Array<{
                            timestamp?: string;
                            action?: string;
                            details?: string;
                        }>;
                    };
                };
            };
            401: {
                content: {
                    "application/json": components["schemas"]["ErrorResponse"];
                };
            };
        };
    };
    Dashboard_GetTimeSeriesData: {
        parameters: {
            query?: {
                interval?: "day" | "week" | "month";
                days?: number;
            };
        };
        responses: {
            200: {
                content: {
                    "application/json": {
                        data?: Array<{
                            date?: string;
                            requests?: number;
                            cost?: number;
                            errors?: number;
                        }>;
                    };
                };
            };
            401: {
                content: {
                    "application/json": components["schemas"]["ErrorResponse"];
                };
            };
        };
    };
    Dashboard_GetProviderMetrics: {
        parameters: {
            query?: {
                days?: number;
            };
        };
        responses: {
            200: {
                content: {
                    "application/json": Array<{
                        provider?: string;
                        requests?: number;
                        totalCost?: number;
                        avgResponseTime?: number;
                        errorRate?: number;
                    }>;
                };
            };
            401: {
                content: {
                    "application/json": components["schemas"]["ErrorResponse"];
                };
            };
        };
    };
}
interface paths {
    "/api/virtualkeys": {
        get: {
            responses: {
                200: {
                    content: {
                        "application/json": components["schemas"]["VirtualKeyDto"][];
                    };
                };
                500: {
                    content: {
                        "text/plain": string;
                    };
                };
            };
        };
        post: {
            requestBody: {
                content: {
                    "application/json": components["schemas"]["CreateVirtualKeyRequestDto"];
                };
            };
            responses: {
                201: {
                    content: {
                        "application/json": components["schemas"]["CreateVirtualKeyResponseDto"];
                    };
                };
                400: {
                    content: {
                        "application/json": any;
                    };
                };
                401: {
                    content: {
                        "text/plain": string;
                    };
                };
                403: {
                    content: {
                        "text/plain": string;
                    };
                };
                500: {
                    content: {
                        "application/json": {
                            message: string;
                        };
                    };
                };
            };
        };
    };
    "/api/virtualkeys/{id}": {
        get: {
            parameters: {
                path: {
                    id: number;
                };
            };
            responses: {
                200: {
                    content: {
                        "application/json": components["schemas"]["VirtualKeyDto"];
                    };
                };
                404: {
                    content: {
                        "text/plain": string;
                    };
                };
                500: {
                    content: {
                        "text/plain": string;
                    };
                };
            };
        };
        put: {
            parameters: {
                path: {
                    id: number;
                };
            };
            requestBody: {
                content: {
                    "application/json": components["schemas"]["UpdateVirtualKeyRequestDto"];
                };
            };
            responses: {
                204: {
                    content: never;
                };
                400: {
                    content: {
                        "application/json": any;
                    };
                };
                401: {
                    content: {
                        "text/plain": string;
                    };
                };
                403: {
                    content: {
                        "text/plain": string;
                    };
                };
                404: {
                    content: {
                        "text/plain": string;
                    };
                };
                500: {
                    content: {
                        "text/plain": string;
                    };
                };
            };
        };
        delete: {
            parameters: {
                path: {
                    id: number;
                };
            };
            responses: {
                204: {
                    content: never;
                };
                401: {
                    content: {
                        "text/plain": string;
                    };
                };
                403: {
                    content: {
                        "text/plain": string;
                    };
                };
                404: {
                    content: {
                        "text/plain": string;
                    };
                };
                500: {
                    content: {
                        "text/plain": string;
                    };
                };
            };
        };
    };
    "/api/virtualkeys/{id}/reset-spend": {
        post: {
            parameters: {
                path: {
                    id: number;
                };
            };
            responses: {
                204: {
                    content: never;
                };
                401: {
                    content: {
                        "text/plain": string;
                    };
                };
                403: {
                    content: {
                        "text/plain": string;
                    };
                };
                404: {
                    content: {
                        "text/plain": string;
                    };
                };
                500: {
                    content: {
                        "text/plain": string;
                    };
                };
            };
        };
    };
    "/api/virtualkeys/validate": {
        post: {
            requestBody: {
                content: {
                    "application/json": components["schemas"]["ValidateVirtualKeyRequest"];
                };
            };
            responses: {
                200: {
                    content: {
                        "application/json": components["schemas"]["VirtualKeyValidationResult"];
                    };
                };
                400: {
                    content: {
                        "application/json": any;
                    };
                };
                500: {
                    content: {
                        "text/plain": string;
                    };
                };
            };
        };
    };
    "/api/virtualkeys/{id}/spend": {
        post: {
            parameters: {
                path: {
                    id: number;
                };
            };
            requestBody: {
                content: {
                    "application/json": components["schemas"]["UpdateSpendRequest"];
                };
            };
            responses: {
                204: {
                    content: never;
                };
                400: {
                    content: {
                        "application/json": any;
                    };
                };
                404: {
                    content: {
                        "text/plain": string;
                    };
                };
                500: {
                    content: {
                        "text/plain": string;
                    };
                };
            };
        };
    };
    "/api/virtualkeys/{id}/check-budget": {
        post: {
            parameters: {
                path: {
                    id: number;
                };
            };
            responses: {
                200: {
                    content: {
                        "application/json": components["schemas"]["BudgetCheckResult"];
                    };
                };
                404: {
                    content: {
                        "text/plain": string;
                    };
                };
                500: {
                    content: {
                        "text/plain": string;
                    };
                };
            };
        };
    };
    "/api/virtualkeys/{id}/validation-info": {
        get: {
            parameters: {
                path: {
                    id: number;
                };
            };
            responses: {
                200: {
                    content: {
                        "application/json": components["schemas"]["VirtualKeyValidationInfoDto"];
                    };
                };
                404: {
                    content: {
                        "text/plain": string;
                    };
                };
                500: {
                    content: {
                        "text/plain": string;
                    };
                };
            };
        };
    };
    "/api/virtualkeys/maintenance": {
        post: {
            responses: {
                204: {
                    content: never;
                };
                401: {
                    content: {
                        "text/plain": string;
                    };
                };
                403: {
                    content: {
                        "text/plain": string;
                    };
                };
                500: {
                    content: {
                        "text/plain": string;
                    };
                };
            };
        };
    };
    "/api/dashboard/metrics/realtime": {
        get: {
            responses: {
                200: {
                    content: {
                        "application/json": {
                            timestamp: string;
                            system: {
                                totalRequestsHour: number;
                                totalRequestsDay: number;
                                avgLatencyHour: number;
                                errorRateHour: number;
                                activeProviders: number;
                                activeKeys: number;
                            };
                            modelMetrics: Array<{
                                model: string;
                                requestCount: number;
                                avgLatency: number;
                                totalTokens: number;
                                totalCost: number;
                                errorRate: number;
                            }>;
                            providerStatus: Array<{
                                providerName: string;
                                isEnabled: boolean;
                                lastHealthCheck?: {
                                    isHealthy: boolean;
                                    checkedAt: string;
                                    responseTime: number;
                                };
                            }>;
                            topKeys: Array<{
                                id: number;
                                name: string;
                                requestsToday: number;
                                costToday: number;
                                budgetUtilization: number;
                            }>;
                            refreshIntervalSeconds: number;
                        };
                    };
                };
                500: {
                    content: {
                        "application/json": {
                            error: string;
                            message: string;
                        };
                    };
                };
            };
        };
    };
    "/api/dashboard/metrics/timeseries": {
        get: {
            parameters: {
                query?: {
                    period?: string;
                };
            };
            responses: {
                200: {
                    content: {
                        "application/json": {
                            period: string;
                            startTime: string;
                            endTime: string;
                            intervalMinutes: number;
                            series: Array<{
                                timestamp: string;
                                requests: number;
                                avgLatency: number;
                                errors: number;
                                totalCost: number;
                                totalTokens: number;
                            }>;
                        };
                    };
                };
                400: {
                    content: {
                        "application/json": {
                            error: string;
                        };
                    };
                };
                500: {
                    content: {
                        "application/json": {
                            error: string;
                            message: string;
                        };
                    };
                };
            };
        };
    };
    "/api/dashboard/metrics/providers": {
        get: {
            responses: {
                200: {
                    content: {
                        "application/json": {
                            timestamp: string;
                            modelMetrics: Array<{
                                model: string;
                                metrics: {
                                    totalRequests: number;
                                    successfulRequests: number;
                                    failedRequests: number;
                                    avgLatency: number;
                                    p95Latency: number;
                                    totalCost: number;
                                    totalTokens: number;
                                };
                            }>;
                            healthHistory: Array<{
                                provider: string;
                                healthChecks: number;
                                successRate: number;
                                avgResponseTime: number;
                                lastCheck: string;
                            }>;
                        };
                    };
                };
                500: {
                    content: {
                        "application/json": {
                            error: string;
                            message: string;
                        };
                    };
                };
            };
        };
    };
    "/api/modelprovidermapping": {
        get: {
            responses: {
                200: {
                    content: {
                        "application/json": components["schemas"]["ModelProviderMappingDto"][];
                    };
                };
                500: {
                    content: {
                        "text/plain": string;
                    };
                };
            };
        };
        post: {
            requestBody: {
                content: {
                    "application/json": components["schemas"]["ModelProviderMappingDto"];
                };
            };
            responses: {
                201: {
                    content: {
                        "application/json": components["schemas"]["ModelProviderMappingDto"];
                    };
                };
                400: {
                    content: {
                        "text/plain": string;
                    };
                };
                401: {
                    content: {
                        "text/plain": string;
                    };
                };
                403: {
                    content: {
                        "text/plain": string;
                    };
                };
                500: {
                    content: {
                        "text/plain": string;
                    };
                };
            };
        };
    };
    "/api/modelprovidermapping/{id}": {
        get: {
            parameters: {
                path: {
                    id: number;
                };
            };
            responses: {
                200: {
                    content: {
                        "application/json": components["schemas"]["ModelProviderMappingDto"];
                    };
                };
                404: {
                    content: {
                        "text/plain": string;
                    };
                };
                500: {
                    content: {
                        "text/plain": string;
                    };
                };
            };
        };
        put: {
            parameters: {
                path: {
                    id: number;
                };
            };
            requestBody: {
                content: {
                    "application/json": components["schemas"]["ModelProviderMappingDto"];
                };
            };
            responses: {
                204: {
                    content: never;
                };
                400: {
                    content: {
                        "text/plain": string;
                    };
                };
                401: {
                    content: {
                        "text/plain": string;
                    };
                };
                403: {
                    content: {
                        "text/plain": string;
                    };
                };
                404: {
                    content: {
                        "text/plain": string;
                    };
                };
                500: {
                    content: {
                        "text/plain": string;
                    };
                };
            };
        };
        delete: {
            parameters: {
                path: {
                    id: number;
                };
            };
            responses: {
                204: {
                    content: never;
                };
                401: {
                    content: {
                        "text/plain": string;
                    };
                };
                403: {
                    content: {
                        "text/plain": string;
                    };
                };
                404: {
                    content: {
                        "text/plain": string;
                    };
                };
                500: {
                    content: {
                        "text/plain": string;
                    };
                };
            };
        };
    };
    "/api/modelprovidermapping/by-model/{modelId}": {
        get: {
            parameters: {
                path: {
                    modelId: string;
                };
            };
            responses: {
                200: {
                    content: {
                        "application/json": components["schemas"]["ModelProviderMappingDto"];
                    };
                };
                404: {
                    content: {
                        "text/plain": string;
                    };
                };
                500: {
                    content: {
                        "text/plain": string;
                    };
                };
            };
        };
    };
    "/api/modelprovidermapping/providers": {
        get: {
            responses: {
                200: {
                    content: {
                        "application/json": components["schemas"]["ProviderDataDto"][];
                    };
                };
                500: {
                    content: {
                        "text/plain": string;
                    };
                };
            };
        };
    };
    "/api/modelprovidermapping/bulk": {
        post: {
            requestBody: {
                content: {
                    "application/json": components["schemas"]["BulkModelMappingRequest"];
                };
            };
            responses: {
                200: {
                    content: {
                        "application/json": components["schemas"]["BulkModelMappingResponse"];
                    };
                };
                400: {
                    content: {
                        "text/plain": string;
                    };
                };
                401: {
                    content: {
                        "text/plain": string;
                    };
                };
                403: {
                    content: {
                        "text/plain": string;
                    };
                };
                500: {
                    content: {
                        "application/json": components["schemas"]["BulkModelMappingResponse"];
                    };
                };
            };
        };
    };
    "/api/modelprovidermapping/discover/provider/{providerName}": {
        get: {
            parameters: {
                path: {
                    providerName: string;
                };
            };
            responses: {
                200: {
                    content: {
                        "application/json": components["schemas"]["DiscoveredModel"][];
                    };
                };
                400: {
                    content: {
                        "text/plain": string;
                    };
                };
                500: {
                    content: {
                        "text/plain": string;
                    };
                };
            };
        };
    };
    "/api/modelprovidermapping/discover/model/{providerName}/{modelId}": {
        get: {
            parameters: {
                path: {
                    providerName: string;
                    modelId: string;
                };
            };
            responses: {
                200: {
                    content: {
                        "application/json": components["schemas"]["DiscoveredModel"];
                    };
                };
                400: {
                    content: {
                        "text/plain": string;
                    };
                };
                404: {
                    content: {
                        "text/plain": string;
                    };
                };
                500: {
                    content: {
                        "text/plain": string;
                    };
                };
            };
        };
    };
    "/api/modelprovidermapping/discover/capability/{modelAlias}/{capability}": {
        get: {
            parameters: {
                path: {
                    modelAlias: string;
                    capability: string;
                };
            };
            responses: {
                200: {
                    content: {
                        "application/json": boolean;
                    };
                };
                400: {
                    content: {
                        "text/plain": string;
                    };
                };
                500: {
                    content: {
                        "text/plain": string;
                    };
                };
            };
        };
    };
    "/api/providercredentials": {
        get: {
            responses: {
                200: {
                    content: {
                        "application/json": components["schemas"]["ProviderCredentialDto"][];
                    };
                };
                500: {
                    content: {
                        "text/plain": string;
                    };
                };
            };
        };
        post: {
            requestBody: {
                content: {
                    "application/json": components["schemas"]["CreateProviderCredentialDto"];
                };
            };
            responses: {
                201: {
                    content: {
                        "application/json": components["schemas"]["ProviderCredentialDto"];
                    };
                };
                400: {
                    content: {
                        "application/json": any;
                    };
                };
                500: {
                    content: {
                        "text/plain": string;
                    };
                };
            };
        };
    };
    "/api/providercredentials/{id}": {
        get: {
            parameters: {
                path: {
                    id: number;
                };
            };
            responses: {
                200: {
                    content: {
                        "application/json": components["schemas"]["ProviderCredentialDto"];
                    };
                };
                404: {
                    content: {
                        "application/json": {
                            error: string;
                        };
                    };
                };
                500: {
                    content: {
                        "text/plain": string;
                    };
                };
            };
        };
        put: {
            parameters: {
                path: {
                    id: number;
                };
            };
            requestBody: {
                content: {
                    "application/json": components["schemas"]["UpdateProviderCredentialDto"];
                };
            };
            responses: {
                204: {
                    content: never;
                };
                400: {
                    content: {
                        "application/json": any;
                    };
                };
                404: {
                    content: {
                        "application/json": {
                            error: string;
                        };
                    };
                };
                500: {
                    content: {
                        "text/plain": string;
                    };
                };
            };
        };
        delete: {
            parameters: {
                path: {
                    id: number;
                };
            };
            responses: {
                204: {
                    content: never;
                };
                404: {
                    content: {
                        "application/json": {
                            error: string;
                        };
                    };
                };
                500: {
                    content: {
                        "text/plain": string;
                    };
                };
            };
        };
    };
    "/api/providercredentials/name/{providerName}": {
        get: {
            parameters: {
                path: {
                    providerName: string;
                };
            };
            responses: {
                200: {
                    content: {
                        "application/json": components["schemas"]["ProviderCredentialDto"];
                    };
                };
                404: {
                    content: {
                        "application/json": {
                            error: string;
                        };
                    };
                };
                500: {
                    content: {
                        "text/plain": string;
                    };
                };
            };
        };
    };
    "/api/providercredentials/names": {
        get: {
            responses: {
                200: {
                    content: {
                        "application/json": components["schemas"]["ProviderDataDto"][];
                    };
                };
                500: {
                    content: {
                        "text/plain": string;
                    };
                };
            };
        };
    };
    "/api/providercredentials/test/{id}": {
        post: {
            parameters: {
                path: {
                    id: number;
                };
            };
            responses: {
                200: {
                    content: {
                        "application/json": components["schemas"]["ProviderConnectionTestResultDto"];
                    };
                };
                404: {
                    content: {
                        "text/plain": string;
                    };
                };
                500: {
                    content: {
                        "text/plain": string;
                    };
                };
            };
        };
    };
    "/api/providercredentials/test": {
        post: {
            requestBody: {
                content: {
                    "application/json": components["schemas"]["ProviderCredentialDto"];
                };
            };
            responses: {
                200: {
                    content: {
                        "application/json": components["schemas"]["ProviderConnectionTestResultDto"];
                    };
                };
                400: {
                    content: {
                        "application/json": any;
                    };
                };
                500: {
                    content: {
                        "text/plain": string;
                    };
                };
            };
        };
    };
    "/api/globalsettings": {
        get: {
            responses: {
                200: {
                    content: {
                        "application/json": components["schemas"]["GlobalSettingDto"][];
                    };
                };
                500: {
                    content: {
                        "text/plain": string;
                    };
                };
            };
        };
        post: {
            requestBody: {
                content: {
                    "application/json": components["schemas"]["CreateGlobalSettingDto"];
                };
            };
            responses: {
                201: {
                    content: {
                        "application/json": components["schemas"]["GlobalSettingDto"];
                    };
                };
                400: {
                    content: {
                        "application/json": any;
                    };
                };
                500: {
                    content: {
                        "text/plain": string;
                    };
                };
            };
        };
    };
    "/api/globalsettings/{id}": {
        get: {
            parameters: {
                path: {
                    id: number;
                };
            };
            responses: {
                200: {
                    content: {
                        "application/json": components["schemas"]["GlobalSettingDto"];
                    };
                };
                404: {
                    content: {
                        "text/plain": string;
                    };
                };
                500: {
                    content: {
                        "text/plain": string;
                    };
                };
            };
        };
        put: {
            parameters: {
                path: {
                    id: number;
                };
            };
            requestBody: {
                content: {
                    "application/json": components["schemas"]["UpdateGlobalSettingDto"];
                };
            };
            responses: {
                204: {
                    content: never;
                };
                400: {
                    content: {
                        "application/json": any;
                    };
                };
                404: {
                    content: {
                        "text/plain": string;
                    };
                };
                500: {
                    content: {
                        "text/plain": string;
                    };
                };
            };
        };
        delete: {
            parameters: {
                path: {
                    id: number;
                };
            };
            responses: {
                204: {
                    content: never;
                };
                404: {
                    content: {
                        "text/plain": string;
                    };
                };
                500: {
                    content: {
                        "text/plain": string;
                    };
                };
            };
        };
    };
    "/api/globalsettings/by-key/{key}": {
        get: {
            parameters: {
                path: {
                    key: string;
                };
            };
            responses: {
                200: {
                    content: {
                        "application/json": components["schemas"]["GlobalSettingDto"];
                    };
                };
                404: {
                    content: {
                        "text/plain": string;
                    };
                };
                500: {
                    content: {
                        "text/plain": string;
                    };
                };
            };
        };
        delete: {
            parameters: {
                path: {
                    key: string;
                };
            };
            responses: {
                204: {
                    content: never;
                };
                404: {
                    content: {
                        "text/plain": string;
                    };
                };
                500: {
                    content: {
                        "text/plain": string;
                    };
                };
            };
        };
    };
    "/api/globalsettings/by-key": {
        put: {
            requestBody: {
                content: {
                    "application/json": components["schemas"]["UpdateGlobalSettingByKeyDto"];
                };
            };
            responses: {
                204: {
                    content: never;
                };
                400: {
                    content: {
                        "application/json": any;
                    };
                };
                500: {
                    content: {
                        "text/plain": string;
                    };
                };
            };
        };
    };
}
interface components {
    schemas: {
        ErrorResponse: {
            error?: string;
            message?: string;
            statusCode?: number;
            timestamp?: string;
        };
        VirtualKeyListResponseDto: {
            items: components["schemas"]["VirtualKeyDto"][];
            totalCount: number;
            page: number;
            pageSize: number;
            totalPages: number;
        };
        VirtualKeyDto: {
            id: number;
            keyName: string;
            keyPrefix?: string;
            allowedModels?: string;
            maxBudget?: number;
            currentSpend: number;
            budgetDuration?: string;
            budgetStartDate?: string;
            isEnabled: boolean;
            expiresAt?: string;
            createdAt: string;
            updatedAt: string;
            metadata?: string;
            rateLimitRpm?: number;
            rateLimitRpd?: number;
            description?: string;
            name: string;
            isActive: boolean;
            usageLimit?: number;
            rateLimit?: number;
        };
        CreateVirtualKeyRequestDto: {
            keyName: string;
            allowedModels?: string;
            maxBudget?: number;
            budgetDuration?: string;
            expiresAt?: string;
            metadata?: string;
            rateLimitRpm?: number;
            rateLimitRpd?: number;
        };
        CreateVirtualKeyResponseDto: {
            virtualKey: string;
            keyInfo: components["schemas"]["VirtualKeyDto"];
        };
        UpdateVirtualKeyRequestDto: {
            keyName?: string;
            allowedModels?: string;
            maxBudget?: number;
            budgetDuration?: string;
            isEnabled?: boolean;
            expiresAt?: string;
            metadata?: string;
            rateLimitRpm?: number;
            rateLimitRpd?: number;
        };
        ValidateVirtualKeyRequest: {
            key: string;
            requestedModel?: string;
        };
        VirtualKeyValidationResult: {
            isValid: boolean;
            virtualKeyId?: number;
            keyName?: string;
            allowedModels?: string;
            maxBudget?: number;
            currentSpend: number;
            errorMessage?: string;
        };
        UpdateSpendRequest: {
            cost: number;
        };
        BudgetCheckResult: {
            wasReset: boolean;
            newBudgetStartDate?: string;
        };
        VirtualKeyValidationInfoDto: {
            id: number;
            keyName: string;
            allowedModels?: string;
            maxBudget?: number;
            currentSpend: number;
            budgetDuration?: string;
            budgetStartDate?: string;
            isEnabled: boolean;
            expiresAt?: string;
            rateLimitRpm?: number;
            rateLimitRpd?: number;
        };
        ModelProviderMappingDto: {
            id: number;
            modelId: string;
            providerModelId: string;
            providerId: string;
            providerName?: string;
            priority: number;
            isEnabled: boolean;
            capabilities?: string;
            maxContextLength?: number;
            supportsVision: boolean;
            supportsAudioTranscription: boolean;
            supportsTextToSpeech: boolean;
            supportsRealtimeAudio: boolean;
            supportsImageGeneration: boolean;
            supportsVideoGeneration: boolean;
            supportsEmbeddings: boolean;
            tokenizerType?: string;
            supportedVoices?: string;
            supportedLanguages?: string;
            supportedFormats?: string;
            isDefault: boolean;
            defaultCapabilityType?: string;
            createdAt: string;
            updatedAt: string;
            notes?: string;
        };
        BulkModelMappingRequest: {
            mappings: components["schemas"]["CreateModelProviderMappingDto"][];
            replaceExisting: boolean;
            validateProviderModels: boolean;
        };
        CreateModelProviderMappingDto: {
            modelId: string;
            providerModelId: string;
            providerId: string;
            priority: number;
            isEnabled: boolean;
            capabilities?: string;
            maxContextLength?: number;
            supportsVision: boolean;
            supportsAudioTranscription: boolean;
            supportsTextToSpeech: boolean;
            supportsRealtimeAudio: boolean;
            supportsImageGeneration: boolean;
            supportsVideoGeneration: boolean;
            tokenizerType?: string;
            supportedVoices?: string;
            supportedLanguages?: string;
            supportedFormats?: string;
            isDefault: boolean;
            defaultCapabilityType?: string;
            notes?: string;
        };
        BulkModelMappingResponse: {
            created: components["schemas"]["ModelProviderMappingDto"][];
            updated: components["schemas"]["ModelProviderMappingDto"][];
            failed: components["schemas"]["BulkMappingError"][];
            totalProcessed: number;
            successCount: number;
            failureCount: number;
            isSuccess: boolean;
        };
        BulkMappingError: {
            index: number;
            mapping: components["schemas"]["CreateModelProviderMappingDto"];
            errorMessage: string;
            details?: string;
            errorType: "Validation" | "Duplicate" | "ProviderModelNotFound" | "SystemError" | "ProviderNotFound";
        };
        DiscoveredModel: {
            modelId: string;
            provider: string;
            displayName?: string;
            capabilities: components["schemas"]["ModelCapabilities"];
            metadata?: Record<string, any>;
            lastVerified: string;
        };
        ModelCapabilities: {
            chat: boolean;
            chatStream: boolean;
            embeddings: boolean;
            imageGeneration: boolean;
            vision: boolean;
            videoGeneration: boolean;
            videoUnderstanding: boolean;
            functionCalling: boolean;
            toolUse: boolean;
            jsonMode: boolean;
            maxTokens?: number;
            maxOutputTokens?: number;
            supportedImageSizes?: string[];
            supportedVideoResolutions?: string[];
            maxVideoDurationSeconds?: number;
        };
        ProviderCredentialDto: {
            id: number;
            providerName: string;
            apiBase: string;
            apiKey: string;
            isEnabled: boolean;
            organization?: string;
            modelEndpoint?: string;
            additionalConfig?: string;
            orgId?: string;
            projectId?: string;
            region?: string;
            endpointUrl?: string;
            deploymentName?: string;
            createdAt: string;
            updatedAt: string;
        };
        CreateProviderCredentialDto: {
            providerName: string;
            apiBase?: string;
            apiKey?: string;
            isEnabled: boolean;
            organization?: string;
            modelEndpoint?: string;
            additionalConfig?: string;
        };
        UpdateProviderCredentialDto: {
            id: number;
            apiBase?: string;
            apiKey?: string;
            isEnabled: boolean;
            organization?: string;
            modelEndpoint?: string;
            additionalConfig?: string;
        };
        ProviderDataDto: {
            id: number;
            providerName: string;
        };
        ProviderConnectionTestResultDto: {
            success: boolean;
            message: string;
            errorDetails?: string;
            providerName: string;
            timestamp: string;
        };
        GlobalSettingDto: {
            id: number;
            key: string;
            value: string;
            description?: string;
            createdAt: string;
            updatedAt: string;
        };
        CreateGlobalSettingDto: {
            key: string;
            value: string;
            description?: string;
        };
        UpdateGlobalSettingDto: {
            id: number;
            value: string;
            description?: string;
        };
        UpdateGlobalSettingByKeyDto: {
            key: string;
            value: string;
            description?: string;
        };
    };
}

type VirtualKeyDto = components['schemas']['VirtualKeyDto'];
type CreateVirtualKeyRequestDto = components['schemas']['CreateVirtualKeyRequestDto'];
type CreateVirtualKeyResponseDto = components['schemas']['CreateVirtualKeyResponseDto'];
type UpdateVirtualKeyRequestDto = components['schemas']['UpdateVirtualKeyRequestDto'];
type VirtualKeyValidationResponseDto = components['schemas']['VirtualKeyValidationResult'];
interface VirtualKeyListResponseDto {
    items: VirtualKeyDto[];
    totalCount: number;
    page: number;
    pageSize: number;
    totalPages: number;
}
interface VirtualKeySpendDto {
    id: number;
    virtualKeyId: number;
    timestamp: string;
    modelUsed: string;
    inputTokens: number;
    outputTokens: number;
    totalTokens: number;
    cost: number;
    requestId?: string;
    metadata?: string;
}
interface VirtualKeyDiscoveryPreviewDto {
    data: DiscoveredModelDto[];
    count: number;
}
interface DiscoveredModelDto {
    id: string;
    provider?: string;
    displayName: string;
    capabilities: Record<string, unknown>;
}
/**
 * Type-safe Virtual Key service using native fetch
 */
declare class FetchVirtualKeyService {
    private readonly client;
    constructor(client: FetchBaseApiClient);
    /**
     * Get all virtual keys with optional pagination
     */
    list(page?: number, pageSize?: number, config?: RequestConfig): Promise<VirtualKeyListResponseDto>;
    /**
     * Get a virtual key by ID
     */
    get(id: string, config?: RequestConfig): Promise<VirtualKeyDto>;
    /**
     * Get a virtual key by the key value
     */
    getByKey(key: string, config?: RequestConfig): Promise<VirtualKeyDto>;
    /**
     * Create a new virtual key
     */
    create(data: CreateVirtualKeyRequestDto, config?: RequestConfig): Promise<CreateVirtualKeyResponseDto>;
    /**
     * Update an existing virtual key
     */
    update(id: string, data: UpdateVirtualKeyRequestDto, config?: RequestConfig): Promise<VirtualKeyDto>;
    /**
     * Delete a virtual key
     */
    delete(id: string, config?: RequestConfig): Promise<void>;
    /**
     * Regenerate a virtual key's key value
     */
    regenerateKey(id: string, config?: RequestConfig): Promise<VirtualKeyDto>;
    /**
     * Validate a virtual key
     */
    validate(key: string, config?: RequestConfig): Promise<VirtualKeyValidationResponseDto>;
    /**
     * Get spend history for a virtual key
     */
    getSpend(id: string, page?: number, pageSize?: number, startDate?: string, endDate?: string, config?: RequestConfig): Promise<VirtualKeySpendDto[]>;
    /**
     * Reset spend for a virtual key
     */
    resetSpend(id: string, config?: RequestConfig): Promise<void>;
    /**
     * Run maintenance tasks for virtual keys
     */
    maintenance(config?: RequestConfig): Promise<{
        message: string;
    }>;
    /**
     * Preview what models and capabilities a virtual key would see when calling the discovery endpoint
     */
    previewDiscovery(id: string, capability?: string, config?: RequestConfig): Promise<VirtualKeyDiscoveryPreviewDto>;
    /**
     * Helper method to check if a key is active and within budget
     */
    isKeyValid(key: VirtualKeyDto): boolean;
    /**
     * Helper method to calculate remaining budget
     */
    getRemainingBudget(key: VirtualKeyDto): number | null;
    /**
     * Helper method to format budget duration
     */
    formatBudgetDuration(duration: VirtualKeyDto['budgetDuration']): string;
}

type MetricsResponse$1 = operations['Dashboard_Metrics']['responses']['200']['content']['application/json'];
type TimeSeriesData$1 = operations['Dashboard_GetTimeSeriesData']['responses']['200']['content']['application/json'];
type ProviderMetrics = operations['Dashboard_GetProviderMetrics']['responses']['200']['content']['application/json'];
/**
 * Type-safe Dashboard service using native fetch
 */
declare class FetchDashboardService {
    private readonly client;
    constructor(client: FetchBaseApiClient);
    /**
     * Get real-time dashboard metrics
     */
    getMetrics(config?: RequestConfig): Promise<MetricsResponse$1>;
    /**
     * Get time series data for charts
     */
    getTimeSeriesData(interval?: 'day' | 'week' | 'month', days?: number, config?: RequestConfig): Promise<TimeSeriesData$1>;
    /**
     * Get provider-specific metrics
     */
    getProviderMetrics(days?: number, config?: RequestConfig): Promise<ProviderMetrics>;
    /**
     * Helper method to calculate average requests per day
     */
    calculateAverageRequestsPerDay(timeSeriesData: TimeSeriesData$1): number;
    /**
     * Helper method to calculate total cost from time series data
     */
    calculateTotalCost(timeSeriesData: TimeSeriesData$1): number;
    /**
     * Helper method to find peak usage time
     */
    findPeakUsageTime(timeSeriesData: TimeSeriesData$1): {
        date: string;
        requests: number;
    } | null;
    /**
     * Helper method to calculate provider cost distribution
     */
    calculateProviderCostDistribution(providerMetrics: ProviderMetrics): Array<{
        provider: string;
        percentage: number;
    }>;
    /**
     * Helper method to format metrics for display
     */
    formatMetrics(metrics: MetricsResponse$1): {
        totalRequests: string;
        totalCost: string;
        activeKeys: string;
        errorRate: string;
        avgResponseTime: string;
    };
    private formatNumber;
    private formatCurrency;
    private formatPercentage;
    private formatMilliseconds;
}

/**
 * Common type definitions to replace Record<string, any> usage
 * These types provide proper structure for various SDK operations
 */
/**
 * Feature flag evaluation context
 */
interface FeatureFlagContext {
    /** User ID for user-specific flags */
    userId?: string;
    /** Virtual key ID for key-specific flags */
    keyId?: string;
    /** Environment (dev, staging, prod) */
    environment?: string;
    /** Custom attributes for evaluation */
    attributes?: {
        [key: string]: string | number | boolean | undefined;
    };
}
/**
 * Provider-specific settings for different providers
 */
interface ProviderSettings {
    /** OpenAI specific settings */
    openai?: {
        organization?: string;
        apiVersion?: string;
        maxRetries?: number;
        timeout?: number;
    };
    /** Anthropic specific settings */
    anthropic?: {
        anthropicVersion?: string;
        maxTokensToSample?: number;
    };
    /** Azure OpenAI specific settings */
    azure?: {
        deploymentName?: string;
        apiVersion?: string;
        resourceName?: string;
    };
    /** Generic provider settings */
    [provider: string]: {
        [key: string]: string | number | boolean | undefined;
    } | undefined;
}
/**
 * Audio provider settings
 */
interface AudioProviderSettings {
    /** Provider name */
    provider: string;
    /** Voice selection */
    defaultVoice?: string;
    /** Language preference */
    defaultLanguage?: string;
    /** Audio format settings */
    outputFormat?: 'mp3' | 'wav' | 'ogg' | 'flac';
    /** Sample rate */
    sampleRate?: number;
    /** Additional provider-specific settings */
    providerSpecific?: {
        [key: string]: string | number | boolean;
    };
}
/**
 * Query parameters for provider models
 */
interface ModelQueryParams {
    /** Page number for pagination */
    page?: number;
    /** Items per page */
    pageSize?: number;
    /** Filter by provider */
    provider?: string;
    /** Filter by model type */
    modelType?: 'chat' | 'completion' | 'embedding' | 'audio' | 'image';
    /** Filter by active status */
    isActive?: boolean;
    /** Sort field */
    sortBy?: 'name' | 'provider' | 'created' | 'updated';
    /** Sort direction */
    sortDirection?: 'asc' | 'desc';
}
/**
 * Analytics query options
 */
interface AnalyticsOptions {
    /** Include detailed breakdowns */
    includeDetails?: boolean;
    /** Aggregation level */
    aggregation?: 'hour' | 'day' | 'week' | 'month';
    /** Time zone for aggregation */
    timezone?: string;
    /** Include zero values */
    includeZeros?: boolean;
}
/**
 * System diagnostic check result
 */
interface DiagnosticResult {
    /** Check status */
    status: 'healthy' | 'degraded' | 'unhealthy' | 'unknown';
    /** Response time in milliseconds */
    responseTime?: number;
    /** Error message if unhealthy */
    error?: string;
    /** Additional details */
    details?: {
        [key: string]: string | number | boolean;
    };
}
/**
 * System diagnostic checks
 */
interface DiagnosticChecks {
    /** Database health */
    database: DiagnosticResult;
    /** Cache health */
    cache: DiagnosticResult;
    /** Queue health */
    queue: DiagnosticResult;
    /** Storage health */
    storage: DiagnosticResult;
    /** Provider health checks */
    providers?: {
        [provider: string]: DiagnosticResult;
    };
}
/**
 * Session metadata for authentication
 */
interface SessionMetadata {
    /** Login timestamp */
    loginTime?: string;
    /** Last activity timestamp */
    lastActivity?: string;
    /** User agent string */
    userAgent?: string;
    /** IP address */
    ipAddress?: string;
    /** Session source */
    source?: 'web' | 'api' | 'cli';
    /** Additional session attributes */
    attributes?: {
        [key: string]: string | number | boolean;
    };
}
/**
 * Monitoring field definitions
 */
interface MonitoringFields {
    /** Request ID */
    requestId?: string;
    /** Correlation ID */
    correlationId?: string;
    /** User ID */
    userId?: string;
    /** Virtual key ID */
    keyId?: string;
    /** Provider name */
    provider?: string;
    /** Model name */
    model?: string;
    /** Response time */
    responseTime?: number;
    /** Token usage */
    tokens?: {
        prompt?: number;
        completion?: number;
        total?: number;
    };
    /** Custom fields */
    custom?: {
        [key: string]: string | number | boolean;
    };
}
/**
 * Export destination configuration
 */
interface ExportDestinationConfig {
    /** S3 configuration */
    s3?: {
        bucket: string;
        region: string;
        prefix?: string;
        accessKeyId?: string;
        secretAccessKey?: string;
        serverSideEncryption?: boolean;
    };
    /** Email configuration */
    email?: {
        recipients: string[];
        subject?: string;
        body?: string;
        attachmentFormat?: 'csv' | 'json' | 'zip';
    };
    /** Webhook configuration */
    webhook?: {
        url: string;
        headers?: {
            [key: string]: string;
        };
        method?: 'POST' | 'PUT';
        retryCount?: number;
        timeoutSeconds?: number;
    };
}
/**
 * Provider health details
 */
interface HealthCheckDetails {
    /** Last successful check timestamp */
    lastSuccessAt?: string;
    /** Last failure timestamp */
    lastFailureAt?: string;
    /** Consecutive failure count */
    consecutiveFailures?: number;
    /** Average response time */
    avgResponseTime?: number;
    /** Error messages */
    recentErrors?: string[];
    /** Additional metrics */
    metrics?: {
        [key: string]: number;
    };
}
/**
 * Security event details for different event types
 */
interface SecurityEventDetails {
    /** Authentication failure details */
    authFailure?: {
        attemptedUsername?: string;
        reason: string;
        attemptCount?: number;
        sourceIp?: string;
    };
    /** Rate limit violation details */
    rateLimit?: {
        limit: number;
        windowSeconds: number;
        currentUsage: number;
        resetAt?: string;
    };
    /** Access violation details */
    accessViolation?: {
        resource: string;
        action: string;
        reason: string;
    };
    /** Generic details */
    [eventType: string]: {
        [key: string]: string | number | boolean | undefined;
    } | undefined;
}
/**
 * Security change record
 */
interface SecurityChangeRecord {
    /** Changed field name */
    field: string;
    /** Previous value */
    oldValue?: string | number | boolean | null;
    /** New value */
    newValue?: string | number | boolean | null;
    /** Change timestamp */
    changedAt: string;
    /** User who made the change */
    changedBy: string;
    /** Change reason */
    reason?: string;
}
/**
 * System parameters for various operations
 */
interface SystemParameters {
    /** Cache parameters */
    cache?: {
        ttl?: number;
        maxSize?: number;
        evictionPolicy?: string;
    };
    /** Queue parameters */
    queue?: {
        maxRetries?: number;
        retryDelay?: number;
        priority?: number;
    };
    /** Rate limit parameters */
    rateLimit?: {
        requests?: number;
        windowSeconds?: number;
        burstSize?: number;
    };
    /** Generic parameters */
    [category: string]: {
        [key: string]: string | number | boolean | undefined;
    } | undefined;
}
/**
 * Generic metadata type
 */
type Metadata = Record<string, string | number | boolean | null | undefined>;
/**
 * Extended metadata that can include nested objects
 */
type ExtendedMetadata = Record<string, unknown>;
/**
 * Custom settings for providers and configurations
 */
type CustomSettings = Record<string, string | number | boolean | string[] | Record<string, unknown>>;
/**
 * Validation function type
 */
type ValidationFunction<T = unknown> = (value: T) => boolean;
/**
 * Event data payload
 */
type EventData = Record<string, unknown>;
/**
 * Generic configuration value
 */
type ConfigValue = string | number | boolean | string[] | Record<string, unknown>;
/**
 * Router action parameters
 */
type RouterActionParameters = {
    targetProvider?: string;
    targetModel?: string;
    fallbackProvider?: string;
    fallbackModel?: string;
    [key: string]: string | number | boolean | undefined;
};
/**
 * Maintenance task configuration
 */
type MaintenanceTaskConfig = {
    schedule?: string;
    retention?: number;
    batchSize?: number;
    [key: string]: string | number | boolean | undefined;
};
/**
 * Maintenance task result
 */
type MaintenanceTaskResultData = {
    processed?: number;
    errors?: number;
    duration?: number;
    details?: Record<string, unknown>;
};
/**
 * Custom metric dimensions
 */
type MetricDimensions = Record<string, string | number | boolean>;
/**
 * Additional provider info
 */
type AdditionalProviderInfo = {
    version?: string;
    region?: string;
    endpoint?: string;
    features?: string[];
    limits?: Record<string, number>;
    [key: string]: unknown;
};

/**
 * Type-safe metadata interfaces to replace Record<string, any>
 * These interfaces provide proper typing for metadata fields used throughout the SDK
 */
/**
 * Base metadata interface for common fields
 */
interface BaseMetadata {
    /** User or system that created/owns this resource */
    createdBy?: string;
    /** Purpose or description of the resource */
    purpose?: string;
    /** Department, team, or project */
    department?: string;
    /** Environment (dev, staging, prod) */
    environment?: string;
    /** Custom tags for categorization */
    tags?: string[];
}
/**
 * Virtual key metadata
 */
interface VirtualKeyMetadata extends BaseMetadata {
    /** Customer or client ID */
    customerId?: string;
    /** Project or application name */
    projectName?: string;
    /** Cost center for billing */
    costCenter?: string;
    /** Contact email for notifications */
    contactEmail?: string;
    /** Additional notes */
    notes?: string;
    /** Token consumption limit */
    tokenLimit?: number;
    /** Token limit period (hour, day, month) */
    tokenPeriod?: 'hour' | 'day' | 'month';
}
/**
 * Provider configuration metadata
 */
interface ProviderConfigMetadata {
    /** Region or location */
    region?: string;
    /** API version */
    apiVersion?: string;
    /** Custom endpoint URL */
    endpoint?: string;
    /** Additional provider-specific settings */
    settings?: {
        [key: string]: string | number | boolean;
    };
}
/**
 * Analytics and monitoring metadata
 */
interface AnalyticsMetadata extends BaseMetadata {
    /** Source system or service */
    source?: string;
    /** Correlation ID for tracking */
    correlationId?: string;
    /** Session or request ID */
    sessionId?: string;
    /** User agent or client info */
    userAgent?: string;
    /** IP address or location */
    ipAddress?: string;
    /** Custom metrics */
    metrics?: {
        [key: string]: number;
    };
}
/**
 * Alert configuration metadata
 */
interface AlertMetadata {
    /** Severity level */
    severity?: 'low' | 'medium' | 'high' | 'critical';
    /** Alert category */
    category?: string;
    /** Runbook URL */
    runbookUrl?: string;
    /** Escalation policy */
    escalationPolicy?: string;
    /** Notification channels */
    notificationChannels?: string[];
}
/**
 * Security event metadata
 */
interface SecurityEventMetadata {
    /** Event type */
    eventType?: string;
    /** Actor or user */
    actor?: string;
    /** Resource affected */
    resource?: string;
    /** Action performed */
    action?: string;
    /** Result or outcome */
    result?: 'success' | 'failure' | 'blocked';
    /** Risk score */
    riskScore?: number;
    /** Additional context */
    context?: {
        [key: string]: string | number | boolean;
    };
}
/**
 * Export configuration metadata
 */
interface ExportConfigMetadata {
    /** Export format */
    format?: 'csv' | 'json' | 'xml' | 'parquet';
    /** Compression type */
    compression?: 'none' | 'gzip' | 'zip';
    /** Encryption settings */
    encryption?: {
        enabled: boolean;
        algorithm?: string;
    };
    /** Destination details */
    destination?: {
        type: 's3' | 'email' | 'webhook' | 'ftp';
        url?: string;
        bucket?: string;
        path?: string;
    };
}
/**
 * Model configuration metadata
 */
interface ModelConfigMetadata {
    /** Model description */
    description?: string;
    /** Model version */
    version?: string;
    /** Model family or type */
    family?: string;
    /** Supported features */
    features?: string[];
    /** Performance tier */
    tier?: 'basic' | 'standard' | 'premium';
    /** Custom parameters */
    parameters?: {
        [key: string]: string | number | boolean;
    };
}
/**
 * Audio configuration metadata
 */
interface AudioConfigMetadata {
    /** Audio format */
    format?: 'mp3' | 'wav' | 'ogg' | 'flac';
    /** Sample rate in Hz */
    sampleRate?: number;
    /** Bit rate in kbps */
    bitRate?: number;
    /** Number of channels */
    channels?: 1 | 2;
    /** Language code */
    language?: string;
    /** Voice ID or name */
    voice?: string;
}
/**
 * Video generation metadata
 */
interface VideoGenerationMetadata extends BaseMetadata {
    /** Video resolution */
    resolution?: string;
    /** Frame rate */
    fps?: number;
    /** Duration in seconds */
    duration?: number;
    /** Style or theme */
    style?: string;
    /** Webhook URL for completion */
    webhookUrl?: string;
    /** Callback metadata */
    callbackMetadata?: {
        [key: string]: string | number | boolean;
    };
}
/**
 * Type representing valid JSON values
 */
type JsonValue = string | number | boolean | null | JsonObject | JsonArray;
type JsonObject = {
    [key: string]: JsonValue;
};
type JsonArray = JsonValue[];
/**
 * Type guard to check if a value is a valid metadata object
 */
declare function isValidMetadata(value: unknown): value is Record<string, JsonValue>;
/**
 * Safely parse JSON metadata from string
 */
declare function parseMetadata<T extends Record<string, JsonValue>>(metadataString: string | null | undefined): T | undefined;
/**
 * Safely stringify metadata to JSON
 */
declare function stringifyMetadata<T extends Record<string, JsonValue>>(metadata: T | null | undefined): string | undefined;

interface ProviderCredentialDto {
    id: number;
    providerName: string;
    apiKey?: string;
    apiBase?: string;
    organization?: string;
    isEnabled: boolean;
    createdAt: string;
    updatedAt: string;
}
interface CreateProviderCredentialDto {
    providerName: string;
    apiKey?: string;
    apiBase?: string;
    organization?: string;
    isEnabled?: boolean;
}
interface UpdateProviderCredentialDto {
    apiKey?: string;
    apiBase?: string;
    organization?: string;
    isEnabled?: boolean;
}
interface ProviderConnectionTestRequest {
    providerName: string;
    apiKey?: string;
    apiBase?: string;
    organization?: string;
}
interface ProviderConnectionTestResultDto {
    success: boolean;
    message: string;
    errorDetails?: string;
    providerName: string;
    modelsAvailable?: string[];
    responseTimeMs?: number;
    timestamp?: string;
}
interface ProviderDataDto {
    name: string;
    displayName: string;
    supportedModels: string[];
    requiresApiKey: boolean;
    requiresEndpoint: boolean;
    requiresOrganizationId: boolean;
    configSchema?: ProviderConfigMetadata;
}
interface ProviderHealthConfigurationDto {
    providerName: string;
    isEnabled: boolean;
    checkIntervalSeconds: number;
    timeoutSeconds: number;
    unhealthyThreshold: number;
    healthyThreshold: number;
    testModel?: string;
    lastCheckTime?: string;
    isHealthy?: boolean;
    consecutiveFailures?: number;
    consecutiveSuccesses?: number;
}
interface UpdateProviderHealthConfigurationDto {
    isEnabled?: boolean;
    checkIntervalSeconds?: number;
    timeoutSeconds?: number;
    unhealthyThreshold?: number;
    healthyThreshold?: number;
    testModel?: string;
}
interface ProviderHealthRecordDto {
    id: number;
    providerName: string;
    checkTime: string;
    isHealthy: boolean;
    responseTimeMs?: number;
    errorMessage?: string;
    statusCode?: number;
    modelsChecked?: string[];
}
interface ProviderHealthStatusDto {
    providerName: string;
    isHealthy: boolean;
    lastCheckTime?: string;
    lastSuccessTime?: string;
    lastFailureTime?: string;
    consecutiveFailures: number;
    consecutiveSuccesses: number;
    averageResponseTimeMs?: number;
    uptime?: number;
    errorRate?: number;
}
interface ProviderHealthSummaryDto {
    totalProviders: number;
    healthyProviders: number;
    unhealthyProviders: number;
    unconfiguredProviders: number;
    providers: ProviderHealthStatusDto[];
}
interface CreateProviderHealthConfigurationDto {
    providerName: string;
    monitoringEnabled?: boolean;
    checkIntervalMinutes?: number;
    timeoutSeconds?: number;
    consecutiveFailuresThreshold?: number;
    notificationsEnabled?: boolean;
    customEndpointUrl?: string;
}
interface ProviderHealthStatisticsDto {
    totalProviders: number;
    onlineProviders: number;
    offlineProviders: number;
    unknownProviders: number;
    averageResponseTimeMs: number;
    totalErrors: number;
    errorCategoryDistribution: Record<string, number>;
    timePeriodHours: number;
}
declare enum StatusType {
    Online = 0,
    Offline = 1,
    Unknown = 2
}
interface ProviderStatus {
    status: StatusType;
    statusMessage?: string;
    responseTimeMs: number;
    lastCheckedUtc: Date;
    errorCategory?: string;
}
interface ProviderFilters extends FilterOptions {
    isEnabled?: boolean;
    providerName?: string;
    hasApiKey?: boolean;
    isHealthy?: boolean;
}
interface ProviderHealthFilters extends FilterOptions {
    providerName?: string;
    isHealthy?: boolean;
    startDate?: string;
    endDate?: string;
    minResponseTime?: number;
    maxResponseTime?: number;
}
interface ProviderUsageStatistics {
    providerName: string;
    totalRequests: number;
    successfulRequests: number;
    failedRequests: number;
    averageResponseTime: number;
    totalCost: number;
    modelsUsed: Record<string, number>;
    errorTypes: Record<string, number>;
    timeRange: {
        start: string;
        end: string;
    };
}

interface HealthSummaryDto {
    overall: 'healthy' | 'degraded' | 'unhealthy';
    providers: ProviderHealthSummary[];
    lastUpdated: string;
    alerts: number;
    degradedCount: number;
    unhealthyCount: number;
}
interface ProviderHealthSummary {
    providerId: string;
    providerName: string;
    status: 'healthy' | 'degraded' | 'unhealthy' | 'unknown';
    uptime: number;
    avgLatency: number;
    errorRate: number;
    lastChecked: string;
}
interface ProviderHealthDto {
    providerId: string;
    providerName: string;
    status: 'healthy' | 'degraded' | 'unhealthy' | 'unknown';
    details: {
        connectivity: HealthCheck;
        performance: HealthCheck;
        errorRate: HealthCheck;
        quotaUsage: HealthCheck;
    };
    metrics: {
        uptime: UptimeMetric;
        latency: LatencyMetric;
        throughput: ThroughputMetric;
        errors: ErrorMetric;
    };
    lastIncident?: Incident;
    maintenanceWindows?: MaintenanceWindow[];
}
interface HealthCheck {
    status: 'ok' | 'warning' | 'critical';
    message: string;
    lastChecked: string;
    details?: HealthCheckDetails;
}
interface UptimeMetric {
    percentage: number;
    totalUptime: number;
    totalDowntime: number;
    since: string;
}
interface LatencyMetric {
    current: number;
    avg: number;
    min: number;
    max: number;
    p50: number;
    p95: number;
    p99: number;
}
interface ThroughputMetric {
    requestsPerMinute: number;
    tokensPerMinute: number;
    bytesPerMinute: number;
}
interface ErrorMetric {
    rate: number;
    count: number;
    types: Record<string, number>;
}
interface HistoryParams {
    startDate?: string;
    endDate?: string;
    resolution?: 'minute' | 'hour' | 'day';
    includeIncidents?: boolean;
}
interface HealthHistory {
    providerId: string;
    dataPoints: HealthDataPoint[];
    incidents: Incident[];
    summary: {
        avgUptime: number;
        totalIncidents: number;
        avgRecoveryTime: number;
    };
}
interface HealthDataPoint {
    timestamp: string;
    status: 'healthy' | 'degraded' | 'unhealthy';
    uptime: number;
    latency: number;
    errorRate: number;
    throughput: number;
}
interface AlertParams extends FilterOptions {
    severity?: ('info' | 'warning' | 'critical')[];
    type?: ('connectivity' | 'performance' | 'quota' | 'error_rate')[];
    providerId?: string;
    acknowledged?: boolean;
    resolved?: boolean;
    startDate?: string;
    endDate?: string;
}
interface HealthAlert {
    id: string;
    providerId: string;
    providerName: string;
    severity: 'info' | 'warning' | 'critical';
    type: 'connectivity' | 'performance' | 'quota' | 'error_rate';
    message: string;
    createdAt: string;
    acknowledgedAt?: string;
    resolvedAt?: string;
}
interface ConnectionTestResult {
    success: boolean;
    latency: number;
    statusCode?: number;
    error?: string;
    details: {
        dnsResolution: number;
        tcpConnection: number;
        tlsHandshake: number;
        httpResponse: number;
    };
}
interface PerformanceParams {
    startDate?: string;
    endDate?: string;
    resolution?: 'minute' | 'hour' | 'day';
}
interface PerformanceMetrics$1 {
    latency: {
        p50: number;
        p95: number;
        p99: number;
        avg: number;
    };
    throughput: {
        requestsPerMinute: number;
        tokensPerMinute: number;
    };
    availability: {
        uptime: number;
        downtime: number;
        mtbf: number;
        mttr: number;
    };
    errors: {
        rate: number;
        types: ErrorTypeCount[];
    };
}
interface ErrorTypeCount {
    type: string;
    count: number;
    percentage: number;
}
interface Incident {
    id: string;
    startTime: string;
    endTime?: string;
    severity: 'minor' | 'major' | 'critical';
    type: string;
    description: string;
    impact: string;
    resolution?: string;
}
interface MaintenanceWindow {
    id: string;
    startTime: string;
    endTime: string;
    description: string;
    impact: 'none' | 'degraded' | 'outage';
}
interface HealthAlertListResponseDto {
    items: HealthAlert[];
    totalCount: number;
    page: number;
    pageSize: number;
    totalPages: number;
}
interface ProviderHealthListResponseDto {
    items: ProviderHealthStatusDto[];
    totalCount: number;
    page: number;
    pageSize: number;
    totalPages: number;
}
interface ProviderHealthStatusResponse {
    providers: ProviderHealthItem[];
    _warning?: string;
}
interface ProviderHealthItem {
    id: string;
    name: string;
    status: 'healthy' | 'degraded' | 'unhealthy' | 'unknown';
    lastChecked: string;
    responseTime: number;
    uptime: number;
    errorRate: number;
    details?: {
        lastError?: string;
        consecutiveFailures?: number;
        lastSuccessfulCheck?: string;
    };
}
interface ProviderWithHealthDto {
    id: string;
    name: string;
    isEnabled: boolean;
    providerName: string;
    apiKey?: string;
    health: {
        status: 'healthy' | 'degraded' | 'unhealthy' | 'unknown';
        responseTime: number;
        uptime: number;
        errorRate: number;
    };
}
interface ProviderHealthMetricsDto {
    providerId: string;
    providerName: string;
    metrics: {
        totalRequests: number;
        failedRequests: number;
        avgResponseTime: number;
        p95ResponseTime: number;
        p99ResponseTime: number;
        availability: number;
        endpoints: ProviderEndpointHealth[];
        models: ProviderModelHealth[];
        rateLimit: {
            requests: {
                used: number;
                limit: number;
                reset: string;
            };
            tokens: {
                used: number;
                limit: number;
                reset: string;
            };
        };
    };
    incidents: ProviderHealthIncident[];
}
interface ProviderEndpointHealth {
    name: string;
    status: 'healthy' | 'degraded' | 'down';
    responseTime: number;
    lastCheck: string;
}
interface ProviderModelHealth {
    name: string;
    available: boolean;
    responseTime: number;
    tokenCapacity: {
        used: number;
        total: number;
    };
}
interface ProviderHealthIncident {
    id: string;
    timestamp: string;
    type: 'outage' | 'degradation' | 'rate_limit';
    duration: number;
    message: string;
    resolved: boolean;
}
interface ProviderHealthHistoryOptions {
    startDate: string;
    endDate: string;
    resolution: 'minute' | 'hour' | 'day';
    includeIncidents: boolean;
}
interface ProviderHealthHistoryResponse {
    dataPoints: ProviderHealthDataPoint[];
    incidents: ProviderHealthIncident[];
}
interface ProviderHealthDataPoint {
    timestamp: string;
    responseTime: number;
    errorRate: number;
    availability: number;
}

type ProviderDto = components['schemas']['ProviderCredentialDto'];
type CreateProviderDto = components['schemas']['CreateProviderCredentialDto'];
type UpdateProviderDto = components['schemas']['UpdateProviderCredentialDto'];
type TestConnectionResult = components['schemas']['ProviderConnectionTestResultDto'];
interface ProviderListResponseDto {
    items: ProviderDto[];
    totalCount: number;
    page: number;
    pageSize: number;
    totalPages: number;
}
interface ProviderConfig {
    providerName: string;
    apiKey: string;
    baseUrl?: string;
    organizationId?: string;
    additionalConfig?: ProviderSettings;
}
interface HealthStatusParams {
    includeHistory?: boolean;
    historyDays?: number;
}
interface ExportParams$3 {
    format: 'json' | 'csv' | 'excel';
    startDate?: string;
    endDate?: string;
    providers?: string[];
}
interface ExportResult$3 {
    fileUrl: string;
    fileName: string;
    expiresAt: string;
    size: number;
}
interface ProviderHealthStatus {
    providerId: string;
    providerName: string;
    status: 'healthy' | 'degraded' | 'unhealthy' | 'unknown';
    lastCheck?: string;
    responseTime?: number;
    details?: HealthCheckDetails;
}
/**
 * Type-safe Providers service using native fetch
 */
declare class FetchProvidersService {
    private readonly client;
    constructor(client: FetchBaseApiClient);
    /**
     * Get all providers with optional pagination
     */
    list(page?: number, pageSize?: number, config?: RequestConfig): Promise<ProviderListResponseDto>;
    /**
     * Get a specific provider by ID
     */
    getById(id: number, config?: RequestConfig): Promise<ProviderDto>;
    /**
     * Create a new provider
     */
    create(data: CreateProviderDto, config?: RequestConfig): Promise<ProviderDto>;
    /**
     * Update an existing provider
     */
    update(id: number, data: UpdateProviderDto, config?: RequestConfig): Promise<ProviderDto>;
    /**
     * Delete a provider
     */
    deleteById(id: number, config?: RequestConfig): Promise<void>;
    /**
     * Test connection for a specific provider
     */
    testConnectionById(id: number, config?: RequestConfig): Promise<TestConnectionResult>;
    /**
     * Test a provider configuration without creating it
     */
    testConfig(providerConfig: ProviderConfig, config?: RequestConfig): Promise<TestConnectionResult>;
    /**
     * Get health status for all providers
     */
    getHealthStatus(params?: HealthStatusParams, config?: RequestConfig): Promise<ProviderHealthStatus[]>;
    /**
     * Export provider health data
     */
    exportHealthData(params: ExportParams$3, config?: RequestConfig): Promise<ExportResult$3>;
    /**
     * Helper method to check if provider is enabled
     */
    isProviderEnabled(provider: ProviderDto): boolean;
    /**
     * Helper method to check if provider has API key configured
     */
    hasApiKey(provider: ProviderDto): boolean;
    /**
     * Helper method to format provider display name
     */
    formatProviderName(provider: ProviderDto): string;
    /**
     * Helper method to get provider status
     */
    getProviderStatus(provider: ProviderDto): 'active' | 'inactive' | 'unconfigured';
    /**
     * Get health status for providers.
     * Retrieves health information for a specific provider or all providers,
     * including status, response times, uptime, and error rates.
     *
     * @param providerId - Optional provider ID to get health for specific provider
     * @param config - Optional request configuration for timeout, signal, headers
     * @returns Promise<ProviderHealthStatusResponse> - Provider health status including:
     *   - providers: Array of provider health information
     *   - status: Overall health status (healthy, degraded, unhealthy, unknown)
     *   - responseTime: Average response time in milliseconds
     *   - uptime: Uptime percentage
     *   - errorRate: Error rate percentage
     * @throws {Error} When provider health data cannot be retrieved
     * @since Issue #430 - Provider Health SDK Methods
     */
    getHealth(providerId?: string, config?: RequestConfig): Promise<ProviderHealthStatusResponse>;
    /**
     * Get all providers with their health status.
     * Retrieves the complete list of providers enriched with current health
     * information including status, response times, and availability metrics.
     *
     * @param config - Optional request configuration for timeout, signal, headers
     * @returns Promise<ProviderWithHealthDto[]> - Array of providers with health data
     * @throws {Error} When provider data with health cannot be retrieved
     * @since Issue #430 - Provider Health SDK Methods
     */
    listWithHealth(config?: RequestConfig): Promise<ProviderWithHealthDto[]>;
    /**
     * Get detailed health metrics for a specific provider.
     * Retrieves comprehensive health metrics including request statistics,
     * response time percentiles, endpoint health, model availability,
     * rate limiting information, and recent incidents.
     *
     * @param providerId - Provider ID to get detailed metrics for
     * @param timeRange - Optional time range for metrics (e.g., '1h', '24h', '7d')
     * @param config - Optional request configuration for timeout, signal, headers
     * @returns Promise<ProviderHealthMetricsDto> - Detailed provider health metrics
     * @throws {Error} When provider health metrics cannot be retrieved
     * @since Issue #430 - Provider Health SDK Methods
     */
    getHealthMetrics(providerId: string, timeRange?: string, config?: RequestConfig): Promise<ProviderHealthMetricsDto>;
}

interface SystemInfoDto {
    version: string;
    buildDate: string;
    environment: string;
    uptime: number;
    systemTime: string;
    features: {
        ipFiltering: boolean;
        providerHealth: boolean;
        costTracking: boolean;
        audioSupport: boolean;
    };
    runtime: {
        dotnetVersion: string;
        os: string;
        architecture: string;
        memoryUsage: number;
        cpuUsage?: number;
    };
    database: {
        provider: string;
        connectionString?: string;
        isConnected: boolean;
        pendingMigrations?: string[];
    };
}
interface HealthStatusDto {
    status: 'healthy' | 'degraded' | 'unhealthy';
    timestamp: string;
    checks: {
        [key: string]: {
            status: 'healthy' | 'degraded' | 'unhealthy';
            description?: string;
            duration?: number;
            error?: string;
        };
    };
    totalDuration: number;
}
interface BackupDto {
    id: string;
    filename: string;
    createdAt: string;
    size: number;
    type: 'manual' | 'scheduled';
    status: 'completed' | 'in_progress' | 'failed';
    error?: string;
    downloadUrl?: string;
    expiresAt?: string;
}
interface CreateBackupRequest {
    description?: string;
    includeKeys?: boolean;
    includeProviders?: boolean;
    includeSettings?: boolean;
    includeLogs?: boolean;
    encryptionPassword?: string;
}
interface RestoreBackupRequest {
    backupId: string;
    decryptionPassword?: string;
    overwriteExisting?: boolean;
    selectedItems?: {
        keys?: boolean;
        providers?: boolean;
        settings?: boolean;
        logs?: boolean;
    };
}
interface BackupRestoreResult {
    success: boolean;
    restoredItems: {
        keys?: number;
        providers?: number;
        settings?: number;
        logs?: number;
    };
    errors?: string[];
    warnings?: string[];
}
interface NotificationDto {
    id: number;
    virtualKeyId?: number;
    virtualKeyName?: string;
    type: NotificationType;
    severity: NotificationSeverity;
    message: string;
    isRead: boolean;
    createdAt: Date;
}
declare enum NotificationType {
    BudgetWarning = 0,
    ExpirationWarning = 1,
    System = 2
}
declare enum NotificationSeverity {
    Info = 0,
    Warning = 1,
    Error = 2
}
interface CreateNotificationDto {
    virtualKeyId?: number;
    type: NotificationType;
    severity: NotificationSeverity;
    message: string;
}
interface UpdateNotificationDto {
    message?: string;
    isRead?: boolean;
}
interface NotificationFilters {
    page?: number;
    pageSize?: number;
    sortBy?: string;
    sortDirection?: 'asc' | 'desc';
    type?: NotificationType;
    severity?: NotificationSeverity;
    isRead?: boolean;
    virtualKeyId?: number;
    startDate?: Date;
    endDate?: Date;
}
interface NotificationSummary {
    totalNotifications: number;
    unreadNotifications: number;
    readNotifications: number;
    notificationsByType: Record<NotificationType, number>;
    notificationsBySeverity: Record<NotificationSeverity, number>;
    mostRecentNotification?: NotificationDto;
    oldestUnreadNotification?: NotificationDto;
}
interface NotificationBulkResponse {
    successCount: number;
    totalCount: number;
    failedIds?: number[];
    errors?: string[];
}
interface NotificationStatistics {
    total: number;
    unread: number;
    read: number;
    byType: Record<string, number>;
    bySeverity: Record<string, number>;
    recent: {
        lastHour: number;
        last24Hours: number;
        lastWeek: number;
    };
}
interface MaintenanceTaskDto {
    name: string;
    description: string;
    lastRun?: string;
    nextRun?: string;
    status: 'idle' | 'running' | 'failed';
    canRunManually: boolean;
    schedule?: string;
}
interface RunMaintenanceTaskRequest {
    taskName: string;
    parameters?: MaintenanceTaskConfig;
}
interface MaintenanceTaskResult {
    taskName: string;
    startTime: string;
    endTime: string;
    success: boolean;
    itemsProcessed?: number;
    errors?: string[];
    logs?: string[];
}
interface AuditLogDto {
    id: string;
    timestamp: string;
    action: string;
    category: 'auth' | 'config' | 'data' | 'system';
    userId?: string;
    ipAddress?: string;
    userAgent?: string;
    resourceType?: string;
    resourceId?: string;
    oldValue?: ConfigValue;
    newValue?: ConfigValue;
    result: 'success' | 'failure';
    errorMessage?: string;
}
interface AuditLogFilters extends FilterOptions {
    startDate?: string;
    endDate?: string;
    action?: string;
    category?: string;
    userId?: string;
    ipAddress?: string;
    resourceType?: string;
    result?: 'success' | 'failure';
}
interface FeatureAvailability {
    features: Record<string, {
        available: boolean;
        status: 'available' | 'coming_soon' | 'in_development' | 'not_planned';
        message?: string;
        version?: string;
        releaseDate?: string;
    }>;
    timestamp: string;
}
interface SystemHealthDto {
    overall: 'healthy' | 'degraded' | 'unhealthy';
    components: {
        api: {
            status: 'healthy' | 'degraded' | 'unhealthy';
            message?: string;
            lastChecked: string;
        };
        database: {
            status: 'healthy' | 'degraded' | 'unhealthy';
            message?: string;
            lastChecked: string;
        };
        cache: {
            status: 'healthy' | 'degraded' | 'unhealthy';
            message?: string;
            lastChecked: string;
        };
        queue: {
            status: 'healthy' | 'degraded' | 'unhealthy';
            message?: string;
            lastChecked: string;
        };
    };
    metrics: {
        cpu: number;
        memory: number;
        disk: number;
        activeConnections: number;
    };
}
interface SystemMetricsDto {
    cpuUsage: number;
    memoryUsage: number;
    diskUsage: number;
    activeConnections: number;
    uptime: number;
}
interface ServiceStatusDto {
    coreApi: {
        status: 'healthy' | 'degraded' | 'unhealthy';
        latency: number;
        endpoint: string;
    };
    adminApi: {
        status: 'healthy' | 'degraded' | 'unhealthy';
        latency: number;
        endpoint: string;
    };
    database: {
        status: 'healthy' | 'degraded' | 'unhealthy';
        latency: number;
        connections: number;
    };
    cache: {
        status: 'healthy' | 'degraded' | 'unhealthy';
        latency: number;
        hitRate: number;
    };
}
interface HealthEventDto {
    id: string;
    timestamp: string;
    type: 'provider_down' | 'provider_up' | 'system_issue' | 'system_recovered';
    message: string;
    severity: 'info' | 'warning' | 'error';
    source?: string;
    metadata?: {
        providerId?: string;
        componentName?: string;
        errorDetails?: string;
        duration?: number;
    };
}
interface HealthEventsResponseDto {
    events: HealthEventDto[];
}
interface HealthEventSubscriptionOptions {
    severityFilter?: ('info' | 'warning' | 'error')[];
    typeFilter?: ('provider_down' | 'provider_up' | 'system_issue' | 'system_recovered')[];
    sourceFilter?: string[];
}
interface HealthEventSubscription {
    unsubscribe(): void;
    isConnected(): boolean;
    onEvent(callback: (event: HealthEventDto) => void): void;
    onConnectionStateChanged(callback: (connected: boolean) => void): void;
}

interface MetricsParams {
    period?: 'hour' | 'day' | 'week' | 'month';
    includeDetails?: boolean;
}
interface PerformanceMetrics {
    cpu: {
        usage: number;
        cores: number;
    };
    memory: {
        used: number;
        total: number;
        percentage: number;
    };
    requests: {
        total: number;
        perMinute: number;
        averageLatency: number;
    };
    timestamp: string;
}
interface ExportParams$2 {
    format: 'json' | 'csv' | 'excel';
    startDate?: string;
    endDate?: string;
    metrics?: string[];
}
interface ExportResult$2 {
    fileUrl: string;
    fileName: string;
    expiresAt: string;
    size: number;
}
/**
 * Type-safe System service using native fetch
 */
declare class FetchSystemService {
    private readonly client;
    constructor(client: FetchBaseApiClient);
    /**
     * Get system information
     */
    getSystemInfo(config?: RequestConfig): Promise<SystemInfoDto>;
    /**
     * Get system health status
     */
    getHealth(config?: RequestConfig): Promise<HealthStatusDto>;
    /**
     * Get WebUI virtual key for authentication
     * CRITICAL: This is required for WebUI authentication
     */
    getWebUIVirtualKey(config?: RequestConfig): Promise<string>;
    /**
     * Get performance metrics (optional)
     */
    getPerformanceMetrics(params?: MetricsParams, config?: RequestConfig): Promise<PerformanceMetrics>;
    /**
     * Export performance data (optional)
     */
    exportPerformanceData(params: ExportParams$2, config?: RequestConfig): Promise<ExportResult$2>;
    /**
     * Get comprehensive system health status and metrics.
     * This method aggregates health data from multiple endpoints to provide
     * a complete picture of system health including individual component status
     * and overall system metrics.
     *
     * @param config - Optional request configuration for timeout, signal, headers
     * @returns Promise<SystemHealthDto> - Complete system health information including:
     *   - overall: Overall system health status
     *   - components: Individual service component health (API, database, cache, queue)
     *   - metrics: Resource utilization metrics (CPU, memory, disk, active connections)
     * @throws {Error} When system health data cannot be retrieved
     * @since Issue #427 - System Health SDK Methods
     */
    getSystemHealth(config?: RequestConfig): Promise<SystemHealthDto>;
    /**
     * Get detailed system resource metrics.
     * Retrieves current system resource utilization including CPU, memory, disk usage,
     * active connections, and system uptime. Attempts to use dedicated metrics endpoint
     * with fallback to constructed metrics from system info.
     *
     * @param config - Optional request configuration for timeout, signal, headers
     * @returns Promise<SystemMetricsDto> - System resource metrics including:
     *   - cpuUsage: CPU utilization percentage (0-100)
     *   - memoryUsage: Memory utilization percentage (0-100)
     *   - diskUsage: Disk utilization percentage (0-100)
     *   - activeConnections: Number of active connections
     *   - uptime: System uptime in seconds
     * @throws {Error} When metrics data cannot be retrieved
     * @since Issue #427 - System Health SDK Methods
     */
    getSystemMetrics(config?: RequestConfig): Promise<SystemMetricsDto>;
    /**
     * Get health status of individual services.
     * Retrieves detailed health information for each service component including
     * Core API, Admin API, database, and cache services with latency and status details.
     * Uses dedicated services endpoint with fallback to health checks.
     *
     * @param config - Optional request configuration for timeout, signal, headers
     * @returns Promise<ServiceStatusDto> - Individual service health status including:
     *   - coreApi: Core API service health, latency, and endpoint
     *   - adminApi: Admin API service health, latency, and endpoint
     *   - database: Database health, latency, and connection count
     *   - cache: Cache service health, latency, and hit rate
     * @throws {Error} When service status data cannot be retrieved
     * @since Issue #427 - System Health SDK Methods
     */
    getServiceStatus(config?: RequestConfig): Promise<ServiceStatusDto>;
    /**
     * Get system uptime in seconds.
     * Retrieves the current system uptime by calling the system info endpoint
     * and extracting the uptime value.
     *
     * @param config - Optional request configuration for timeout, signal, headers
     * @returns Promise<number> - System uptime in seconds since last restart
     * @throws {Error} When system uptime cannot be retrieved
     * @since Issue #427 - System Health SDK Methods
     */
    getUptime(config?: RequestConfig): Promise<number>;
    /**
     * Get the number of active connections to the system.
     * Attempts to retrieve active connection count from metrics endpoint with
     * intelligent fallback using system metrics and heuristics when direct
     * connection data is unavailable.
     *
     * @param config - Optional request configuration for timeout, signal, headers
     * @returns Promise<number> - Number of currently active connections to the system
     * @throws {Error} When connection count cannot be determined
     * @since Issue #427 - System Health SDK Methods
     */
    getActiveConnections(config?: RequestConfig): Promise<number>;
    /**
     * Get recent health events for the system.
     * Retrieves historical health events including provider outages, system issues,
     * and recovery events with detailed metadata and timestamps.
     *
     * @param limit - Optional limit on number of events to return (default: 50)
     * @param config - Optional request configuration for timeout, signal, headers
     * @returns Promise<HealthEventsResponseDto> - Array of health events with:
     *   - id: Unique event identifier
     *   - timestamp: ISO timestamp of event occurrence
     *   - type: Event type (provider_down, provider_up, system_issue, system_recovered)
     *   - message: Human-readable event description
     *   - severity: Event severity level (info, warning, error)
     *   - source: Event source (provider name, component name)
     *   - metadata: Additional context and details
     * @throws {Error} When health events cannot be retrieved
     * @since Issue #428 - Health Events SDK Methods
     */
    getHealthEvents(limit?: number, config?: RequestConfig): Promise<HealthEventsResponseDto>;
    /**
     * Subscribe to real-time health event updates.
     * Creates a persistent connection to receive live health events as they occur,
     * supporting filtering by severity, type, and source with automatic reconnection.
     *
     * @param options - Optional subscription configuration:
     *   - severityFilter: Array of severity levels to include
     *   - typeFilter: Array of event types to include
     *   - sourceFilter: Array of sources to include
     * @param config - Optional request configuration for timeout, signal, headers
     * @returns Promise<HealthEventSubscription> - Subscription handle with:
     *   - unsubscribe(): Disconnect from events
     *   - isConnected(): Check connection status
     *   - onEvent(): Register event callback
     *   - onConnectionStateChanged(): Register connection callback
     * @throws {Error} When subscription cannot be established
     * @since Issue #428 - Health Events SDK Methods
     */
    subscribeToHealthEvents(options?: HealthEventSubscriptionOptions, config?: RequestConfig): Promise<HealthEventSubscription>;
    /**
     * Helper method to check if system is healthy
     */
    isSystemHealthy(health: HealthStatusDto): boolean;
    /**
     * Helper method to get unhealthy services
     */
    getUnhealthyServices(health: HealthStatusDto): string[];
    /**
     * Helper method to format uptime
     */
    formatUptime(uptimeSeconds: number): string;
    /**
     * Helper method to check if a feature is enabled
     */
    isFeatureEnabled(systemInfo: SystemInfoDto, feature: keyof SystemInfoDto['features']): boolean;
}

interface ModelProviderMappingDto {
    id: number;
    modelId: string;
    providerId: string;
    providerModelId: string;
    isEnabled: boolean;
    priority: number;
    createdAt: string;
    updatedAt: string;
    metadata?: string;
    /** Whether this model supports vision/image input capabilities */
    supportsVision: boolean;
    /** Whether this model supports image generation capabilities */
    supportsImageGeneration: boolean;
    /** Whether this model supports audio transcription capabilities */
    supportsAudioTranscription: boolean;
    /** Whether this model supports text-to-speech capabilities */
    supportsTextToSpeech: boolean;
    /** Whether this model supports real-time audio streaming capabilities */
    supportsRealtimeAudio: boolean;
    /** Whether this model supports function calling */
    supportsFunctionCalling: boolean;
    /** Whether this model supports streaming responses */
    supportsStreaming: boolean;
    /** Whether this model supports video generation capabilities */
    supportsVideoGeneration: boolean;
    /** Whether this model supports embeddings generation */
    supportsEmbeddings: boolean;
    /** Optional model capabilities (e.g., vision, function-calling) */
    capabilities?: string;
    /** Optional maximum context length */
    maxContextLength?: number;
    /** The maximum output tokens for this model */
    maxOutputTokens?: number;
    /** Supported languages for transcription/TTS (comma-separated) */
    supportedLanguages?: string;
    /** Supported voices for TTS (comma-separated) */
    supportedVoices?: string;
    /** Supported input formats (comma-separated) */
    supportedFormats?: string;
    /** The tokenizer type used by this model */
    tokenizerType?: string;
    /** Whether this mapping is the default for its capability type */
    isDefault: boolean;
    /** The capability type this mapping is default for (e.g., 'chat', 'image-generation') */
    defaultCapabilityType?: string;
}
interface CreateModelProviderMappingDto {
    modelId: string;
    providerId: string;
    providerModelId: string;
    isEnabled?: boolean;
    priority?: number;
    metadata?: string;
    supportsVision?: boolean;
    supportsImageGeneration?: boolean;
    supportsAudioTranscription?: boolean;
    supportsTextToSpeech?: boolean;
    supportsRealtimeAudio?: boolean;
    supportsFunctionCalling?: boolean;
    supportsStreaming?: boolean;
    supportsVideoGeneration?: boolean;
    supportsEmbeddings?: boolean;
    capabilities?: string;
    maxContextLength?: number;
    maxOutputTokens?: number;
    supportedLanguages?: string;
    supportedVoices?: string;
    supportedFormats?: string;
    tokenizerType?: string;
    isDefault?: boolean;
    defaultCapabilityType?: string;
}
interface UpdateModelProviderMappingDto {
    /**
     * The ID of the model mapping.
     * Required by backend for validation - must match the ID in the route.
     */
    id?: number;
    /**
     * The model ID/alias.
     * Required by backend even for updates (not just creates).
     */
    modelId?: string;
    providerId?: string;
    providerModelId?: string;
    isEnabled?: boolean;
    priority?: number;
    metadata?: string;
    supportsVision?: boolean;
    supportsImageGeneration?: boolean;
    supportsAudioTranscription?: boolean;
    supportsTextToSpeech?: boolean;
    supportsRealtimeAudio?: boolean;
    supportsFunctionCalling?: boolean;
    supportsStreaming?: boolean;
    supportsVideoGeneration?: boolean;
    supportsEmbeddings?: boolean;
    /**
     * @deprecated Legacy field - backend should derive this from individual capability flags
     */
    capabilities?: string;
    maxContextLength?: number;
    maxOutputTokens?: number;
    supportedLanguages?: string;
    supportedVoices?: string;
    supportedFormats?: string;
    tokenizerType?: string;
    isDefault?: boolean;
    defaultCapabilityType?: string;
}
interface ModelMappingFilters extends FilterOptions {
    modelId?: string;
    providerId?: string;
    isEnabled?: boolean;
    minPriority?: number;
    maxPriority?: number;
    supportsVision?: boolean;
    supportsImageGeneration?: boolean;
    supportsAudioTranscription?: boolean;
    supportsTextToSpeech?: boolean;
    supportsRealtimeAudio?: boolean;
    supportsFunctionCalling?: boolean;
    supportsStreaming?: boolean;
    isDefault?: boolean;
    defaultCapabilityType?: string;
}
interface ModelProviderInfo {
    providerId: string;
    providerName: string;
    providerModelId: string;
    isAvailable: boolean;
    isEnabled: boolean;
    priority: number;
    estimatedCost?: {
        inputTokenCost: number;
        outputTokenCost: number;
        currency: string;
    };
}
interface ModelRoutingInfo {
    modelId: string;
    primaryProvider?: ModelProviderInfo;
    fallbackProviders: ModelProviderInfo[];
    loadBalancingEnabled: boolean;
    routingStrategy: 'priority' | 'round-robin' | 'least-cost' | 'fastest';
}
interface BulkMappingRequest {
    mappings: CreateModelProviderMappingDto[];
    replaceExisting?: boolean;
}
interface BulkMappingResponse {
    created: ModelProviderMappingDto[];
    updated: ModelProviderMappingDto[];
    failed: {
        index: number;
        error: string;
        mapping: CreateModelProviderMappingDto;
    }[];
}
interface ModelMappingSuggestion {
    modelId: string;
    suggestedProviders: {
        providerId: string;
        providerName: string;
        providerModelId: string;
        confidence: number;
        reasoning: string;
        estimatedPerformance?: {
            latency: number;
            reliability: number;
            costEfficiency: number;
        };
    }[];
}
/** Represents a discovered model from a provider */
interface DiscoveredModel {
    /** The model ID */
    modelId: string;
    /** The provider name */
    provider: string;
    /** The model display name */
    displayName: string;
    /** The discovered capabilities */
    capabilities: {
        chat?: boolean;
        chatStream?: boolean;
        embeddings?: boolean;
        imageGeneration?: boolean;
        vision?: boolean;
        videoGeneration?: boolean;
        videoUnderstanding?: boolean;
        functionCalling?: boolean;
        toolUse?: boolean;
        jsonMode?: boolean;
        maxTokens?: number;
        maxOutputTokens?: number;
        supportedImageSizes?: string[] | null;
        supportedVideoResolutions?: string[] | null;
        maxVideoDurationSeconds?: number | null;
    };
    /** Model metadata */
    metadata?: {
        original_model_id?: string;
        inferred?: boolean;
        [key: string]: unknown;
    };
    /** When the model was last verified */
    lastVerified?: string;
}
/** Represents model capabilities discovered during model discovery */
interface ModelCapabilities$1 {
    /** Whether the model supports chat completions */
    supportsChat: boolean;
    /** Whether the model supports vision/image input */
    supportsVision: boolean;
    /** Whether the model supports image generation */
    supportsImageGeneration: boolean;
    /** Whether the model supports audio transcription */
    supportsAudioTranscription: boolean;
    /** Whether the model supports text-to-speech */
    supportsTextToSpeech: boolean;
    /** Whether the model supports real-time audio streaming */
    supportsRealtimeAudio: boolean;
    /** Whether the model supports function calling */
    supportsFunctionCalling: boolean;
    /** Whether the model supports streaming responses */
    supportsStreaming: boolean;
    /** The maximum context length */
    maxContextLength?: number;
    /** The maximum output tokens */
    maxOutputTokens?: number;
    /** Supported languages (comma-separated) */
    supportedLanguages?: string;
    /** Supported voices for TTS (comma-separated) */
    supportedVoices?: string;
    /** Supported input formats (comma-separated) */
    supportedFormats?: string;
    /** The tokenizer type */
    tokenizerType?: string;
}
/** Represents the result of a capability test for a specific model */
interface CapabilityTestResult {
    /** The model alias that was tested */
    modelAlias: string;
    /** The capability that was tested */
    capability: string;
    /** Whether the capability test was successful */
    isSupported: boolean;
    /** The confidence score of the test result (0-1) */
    confidence: number;
    /** Additional details about the test result */
    details?: string;
    /** Any error that occurred during testing */
    error?: string;
    /** The test duration in milliseconds */
    testDurationMs: number;
    /** When the test was performed */
    testedAt: string;
}

/**
 * Type-safe Model Mappings service using native fetch
 */
declare class FetchModelMappingsService {
    private readonly client;
    constructor(client: FetchBaseApiClient);
    /**
     * Get all model mappings
     * Note: The backend currently returns a plain array, not a paginated response
     */
    list(config?: RequestConfig): Promise<ModelProviderMappingDto[]>;
    /**
     * Get a specific model mapping by ID
     */
    getById(id: number, config?: RequestConfig): Promise<ModelProviderMappingDto>;
    /**
     * Create a new model mapping
     */
    create(data: CreateModelProviderMappingDto, config?: RequestConfig): Promise<ModelProviderMappingDto>;
    /**
     * Update an existing model mapping
     */
    update(id: number, data: UpdateModelProviderMappingDto, config?: RequestConfig): Promise<void>;
    /**
     * Delete a model mapping
     */
    deleteById(id: number, config?: RequestConfig): Promise<void>;
    /**
     * Discover all available models from all providers
     */
    discoverModels(config?: RequestConfig): Promise<DiscoveredModel[]>;
    /**
     * Discover models from a specific provider
     */
    discoverProviderModels(providerName: string, config?: RequestConfig): Promise<DiscoveredModel[]>;
    /**
     * Test a specific capability for a model mapping
     */
    testCapability(id: number, capability: string, testParams?: Record<string, unknown>, config?: RequestConfig): Promise<CapabilityTestResult>;
    /**
     * Get routing information for a model
     */
    getRouting(modelId: string, config?: RequestConfig): Promise<ModelRoutingInfo>;
    /**
     * Get model mapping suggestions
     */
    getSuggestions(config?: RequestConfig): Promise<ModelMappingSuggestion[]>;
    /**
     * Bulk create model mappings
     */
    bulkCreate(request: BulkMappingRequest, config?: RequestConfig): Promise<BulkMappingResponse>;
    /**
     * Bulk update model mappings
     */
    bulkUpdate(updates: {
        id: number;
        data: UpdateModelProviderMappingDto;
    }[], config?: RequestConfig): Promise<void>;
    /**
     * Helper method to check if a mapping is enabled
     */
    isMappingEnabled(mapping: ModelProviderMappingDto): boolean;
    /**
     * Helper method to get mapping capabilities
     */
    getMappingCapabilities(mapping: ModelProviderMappingDto): string[];
    /**
     * Helper method to format mapping display name
     */
    formatMappingName(mapping: ModelProviderMappingDto): string;
    /**
     * Helper method to check if a model supports a specific capability
     */
    supportsCapability(mapping: ModelProviderMappingDto, capability: string): boolean;
}

interface ModelDto {
    id: string;
    name: string;
    displayName: string;
    provider: string;
    description?: string;
    contextWindow: number;
    maxTokens: number;
    inputCost: number;
    outputCost: number;
    capabilities: ModelCapabilities;
    status: 'active' | 'deprecated' | 'beta' | 'preview';
    releaseDate?: string;
    deprecationDate?: string;
}
interface ModelDetailsDto extends ModelDto {
    version: string;
    trainingData?: string;
    benchmarks?: Record<string, number>;
    limitations?: string[];
    bestPractices?: string[];
    examples?: ModelExample[];
}
interface ModelExample {
    title: string;
    description: string;
    input: string;
    output: string;
}
interface ModelSearchFilters {
    providers?: string[];
    capabilities?: Partial<ModelCapabilities>;
    status?: ('active' | 'deprecated' | 'beta' | 'preview')[];
    minContextWindow?: number;
    maxCost?: number;
}
interface ModelSearchResult {
    models: ModelDto[];
    totalCount: number;
    facets: {
        providers: Record<string, number>;
        capabilities: Record<keyof ModelCapabilities, number>;
        status: Record<string, number>;
    };
}
interface ModelCapabilities {
    chat: boolean;
    completion: boolean;
    embedding: boolean;
    vision: boolean;
    functionCalling: boolean;
    streaming: boolean;
    fineTuning: boolean;
    plugins: boolean;
}
interface ModelListResponseDto {
    items: ModelDto[];
    totalCount: number;
    page: number;
    pageSize: number;
    totalPages: number;
}
interface RefreshModelsRequest {
    providerName: string;
    forceRefresh?: boolean;
}
interface RefreshModelsResponse {
    provider: string;
    providerName?: string;
    modelsCount: number;
    modelsUpdated?: number;
    modelsAdded?: number;
    modelsRemoved?: number;
    refreshedAt?: string;
    success: boolean;
    message: string;
}

/**
 * Type-safe Provider Models service using native fetch
 */
declare class FetchProviderModelsService {
    private readonly client;
    constructor(client: FetchBaseApiClient);
    /**
     * Get models for a specific provider
     */
    getProviderModels(providerName: string, config?: RequestConfig): Promise<ModelDto[]>;
    /**
     * Get cached models for a specific provider (faster, may be stale)
     * @deprecated This endpoint doesn't exist in Admin API. Use getProviderModels instead.
     */
    getCachedProviderModels(providerName: string, config?: RequestConfig): Promise<ModelDto[]>;
    /**
     * Refresh model list from provider
     * @deprecated This endpoint doesn't exist in Admin API. Model discovery happens in real-time.
     */
    refreshProviderModels(providerName: string, config?: RequestConfig): Promise<RefreshModelsResponse>;
    /**
     * Get detailed model information
     */
    getModelDetails(providerName: string, modelId: string, config?: RequestConfig): Promise<ModelDetailsDto>;
    /**
     * Get model capabilities
     */
    getModelCapabilities(providerName: string, modelId: string, config?: RequestConfig): Promise<ModelCapabilities>;
    /**
     * Helper method to check if a model supports a specific capability
     */
    modelSupportsCapability(model: ModelDto, capability: keyof ModelCapabilities): boolean;
    /**
     * Helper method to filter models by capabilities
     */
    filterModelsByCapabilities(models: ModelDto[], requiredCapabilities: Partial<ModelCapabilities>): ModelDto[];
    /**
     * Helper method to get active models only
     */
    getActiveModels(models: ModelDto[]): ModelDto[];
    /**
     * Helper method to group models by provider
     */
    groupModelsByProvider(models: ModelDto[]): Record<string, ModelDto[]>;
    /**
     * Helper method to calculate total cost for tokens
     */
    calculateCost(model: ModelDto, inputTokens: number, outputTokens: number): number;
    /**
     * Helper method to find cheapest model with specific capabilities
     */
    findCheapestModel(models: ModelDto[], requiredCapabilities: Partial<ModelCapabilities>): ModelDto | undefined;
    /**
     * Helper method to sort models by context window size
     */
    sortByContextWindow(models: ModelDto[], descending?: boolean): ModelDto[];
    /**
     * Helper method to format model display name with provider
     */
    formatModelName(model: ModelDto): string;
    /**
     * Helper method to check if model is deprecated or will be soon
     */
    isModelDeprecated(model: ModelDto): boolean;
    /**
     * Helper method to get model status label
     */
    getModelStatusLabel(model: ModelDto): string;
}

interface GlobalSettingDto {
    key: string;
    value: string;
    description?: string;
    dataType: 'string' | 'number' | 'boolean' | 'json';
    category?: string;
    isSecret?: boolean;
    createdAt: string;
    updatedAt: string;
}
interface CreateGlobalSettingDto {
    key: string;
    value: string;
    description?: string;
    dataType?: 'string' | 'number' | 'boolean' | 'json';
    category?: string;
    isSecret?: boolean;
}
interface UpdateGlobalSettingDto {
    value: string;
    description?: string;
    category?: string;
}
interface SettingCategory {
    name: string;
    description: string;
    settings: GlobalSettingDto[];
}
interface AudioConfigurationDto {
    provider: string;
    isEnabled: boolean;
    apiKey?: string;
    apiEndpoint?: string;
    defaultVoice?: string;
    defaultModel?: string;
    maxDuration?: number;
    allowedVoices?: string[];
    customSettings?: CustomSettings;
    createdAt: string;
    updatedAt: string;
}
interface CreateAudioConfigurationDto {
    provider: string;
    isEnabled?: boolean;
    apiKey?: string;
    apiEndpoint?: string;
    defaultVoice?: string;
    defaultModel?: string;
    maxDuration?: number;
    allowedVoices?: string[];
    customSettings?: CustomSettings;
}
interface UpdateAudioConfigurationDto {
    isEnabled?: boolean;
    apiKey?: string;
    apiEndpoint?: string;
    defaultVoice?: string;
    defaultModel?: string;
    maxDuration?: number;
    allowedVoices?: string[];
    customSettings?: CustomSettings;
}
interface RouterConfigurationDto {
    routingStrategy: 'priority' | 'round-robin' | 'least-cost' | 'fastest' | 'random';
    fallbackEnabled: boolean;
    maxRetries: number;
    retryDelay: number;
    loadBalancingEnabled: boolean;
    healthCheckEnabled: boolean;
    healthCheckInterval: number;
    circuitBreakerEnabled: boolean;
    circuitBreakerThreshold: number;
    circuitBreakerDuration: number;
    customRules?: RouterRule[];
    createdAt: string;
    updatedAt: string;
}
interface RouterRule {
    id?: number;
    name: string;
    condition: RouterCondition;
    action: RouterAction;
    priority: number;
    isEnabled: boolean;
}
interface RouterCondition {
    type: 'model' | 'key' | 'metadata' | 'time' | 'cost';
    operator: 'equals' | 'contains' | 'greater_than' | 'less_than' | 'between';
    value: ConfigValue;
}
interface RouterAction {
    type: 'route_to_provider' | 'block' | 'rate_limit' | 'add_metadata';
    value: ConfigValue;
}
interface UpdateRouterConfigurationDto {
    routingStrategy?: 'priority' | 'round-robin' | 'least-cost' | 'fastest' | 'random';
    fallbackEnabled?: boolean;
    maxRetries?: number;
    retryDelay?: number;
    loadBalancingEnabled?: boolean;
    healthCheckEnabled?: boolean;
    healthCheckInterval?: number;
    circuitBreakerEnabled?: boolean;
    circuitBreakerThreshold?: number;
    circuitBreakerDuration?: number;
    customRules?: RouterRule[];
}
interface SystemConfiguration {
    general: GlobalSettingDto[];
    audio: AudioConfigurationDto[];
    router: RouterConfigurationDto;
    categories: SettingCategory[];
}
interface SettingFilters extends FilterOptions {
    category?: string;
    dataType?: string;
    isSecret?: boolean;
    searchKey?: string;
}

interface SettingUpdate {
    key: string;
    value: unknown;
}
interface SettingsListResponseDto {
    items: GlobalSettingDto[];
    totalCount: number;
    page: number;
    pageSize: number;
    totalPages: number;
}
interface SettingsDto {
    settings: GlobalSettingDto[];
    categories: string[];
    lastModified: string;
}
/**
 * Type-safe Settings service using native fetch
 */
declare class FetchSettingsService {
    private readonly client;
    constructor(client: FetchBaseApiClient);
    /**
     * Get all global settings
     */
    getGlobalSettings(config?: RequestConfig): Promise<SettingsDto>;
    /**
     * Get all global settings with pagination
     */
    listGlobalSettings(page?: number, pageSize?: number, config?: RequestConfig): Promise<SettingsListResponseDto>;
    /**
     * Get a specific setting by key
     */
    getGlobalSetting(key: string, config?: RequestConfig): Promise<GlobalSettingDto>;
    /**
     * Create a new global setting
     */
    createGlobalSetting(data: CreateGlobalSettingDto, config?: RequestConfig): Promise<GlobalSettingDto>;
    /**
     * Update a specific setting
     */
    updateGlobalSetting(key: string, data: UpdateGlobalSettingDto, config?: RequestConfig): Promise<void>;
    /**
     * Delete a global setting
     */
    deleteGlobalSetting(key: string, config?: RequestConfig): Promise<void>;
    /**
     * Batch update multiple settings
     */
    batchUpdateSettings(settings: SettingUpdate[], config?: RequestConfig): Promise<void>;
    /**
     * Get settings grouped by category
     */
    getSettingsByCategory(config?: RequestConfig): Promise<SettingCategory[]>;
    /**
     * Helper method to check if a setting exists
     */
    settingExists(key: string, config?: RequestConfig): Promise<boolean>;
    /**
     * Helper method to get typed setting value
     */
    getTypedSettingValue<T = unknown>(key: string, config?: RequestConfig): Promise<T>;
    /**
     * Helper method to update setting with type conversion
     */
    updateTypedSetting<T>(key: string, value: T, description?: string, config?: RequestConfig): Promise<void>;
    /**
     * Helper method to get all secret settings (with values hidden)
     */
    getSecretSettings(config?: RequestConfig): Promise<GlobalSettingDto[]>;
    /**
     * Helper method to validate setting value based on data type
     */
    validateSettingValue(value: string, dataType: string): boolean;
    /**
     * Helper method to format setting value for display
     */
    formatSettingValue(setting: GlobalSettingDto): string;
}

interface RequestLogParams {
    page?: number;
    pageSize?: number;
    startDate?: string;
    endDate?: string;
    virtualKeyId?: string;
    provider?: string;
    model?: string;
    statusCode?: number;
    minLatency?: number;
    maxLatency?: number;
    sortBy?: 'timestamp' | 'latency' | 'cost' | 'tokens';
    sortOrder?: 'asc' | 'desc';
}
interface RequestLogPage {
    items: RequestLogDto[];
    totalCount: number;
    page: number;
    pageSize: number;
    totalPages: number;
}
interface UsageParams {
    startDate?: string;
    endDate?: string;
    groupBy?: 'hour' | 'day' | 'week' | 'month';
    virtualKeyIds?: string[];
    providers?: string[];
    models?: string[];
}
interface UsageAnalytics {
    summary: {
        totalRequests: number;
        totalTokens: number;
        totalCost: number;
        averageLatency: number;
        successRate: number;
    };
    byProvider: Record<string, ProviderUsage>;
    byVirtualKey: Record<string, VirtualKeyUsage>;
    byModel: Record<string, ModelUsage>;
    timeSeries: TimeSeriesData[];
    timeRange: {
        start: string;
        end: string;
    };
}
interface ProviderUsage {
    provider: string;
    requests: number;
    tokens: number;
    cost: number;
    averageLatency: number;
    successRate: number;
}
interface VirtualKeyUsage {
    keyId: string;
    keyName: string;
    requests: number;
    tokens: number;
    cost: number;
    averageLatency: number;
}
interface ModelUsage {
    model: string;
    provider: string;
    requests: number;
    tokens: number;
    cost: number;
    averageLatency: number;
}
interface TimeSeriesData {
    timestamp: string;
    requests: number;
    tokens: number;
    cost: number;
    averageLatency: number;
    successRate: number;
}
interface VirtualKeyParams {
    startDate?: string;
    endDate?: string;
    virtualKeyIds?: string[];
    groupBy?: 'hour' | 'day' | 'week' | 'month';
}
interface VirtualKeyAnalytics {
    virtualKeys: VirtualKeyUsageSummary[];
    topUsers: {
        byRequests: VirtualKeyRanking[];
        byCost: VirtualKeyRanking[];
        byTokens: VirtualKeyRanking[];
    };
    trends: {
        daily: TrendData[];
        weekly: TrendData[];
        monthly: TrendData[];
    };
}
interface VirtualKeyUsageSummary {
    keyId: string;
    keyName: string;
    totalRequests: number;
    totalTokens: number;
    totalCost: number;
    averageRequestsPerDay: number;
    budgetUsed: number;
    budgetRemaining: number;
    lastUsed: string;
}
interface VirtualKeyRanking {
    keyId: string;
    keyName: string;
    value: number;
    percentage: number;
}
interface TrendData {
    period: string;
    value: number;
    change: number;
    changePercentage: number;
}
interface CapabilityUsage {
    capability: string;
    requests: number;
    percentage: number;
    models: string[];
}
interface ModelPerformanceMetrics {
    model: string;
    provider: string;
    averageLatency: number;
    p50Latency: number;
    p95Latency: number;
    p99Latency: number;
    successRate: number;
    errorRate: number;
    timeoutRate: number;
}
interface ExportParams$1 {
    format: 'csv' | 'json' | 'excel';
    startDate?: string;
    endDate?: string;
    filters?: {
        providers?: string[];
        models?: string[];
        virtualKeyIds?: string[];
        status?: string[];
        [key: string]: string[] | undefined;
    };
}
interface ExportResult$1 {
    url: string;
    expiresAt: string;
    size: number;
    recordCount: number;
}
interface RequestLogDto {
    id: string;
    timestamp: string;
    virtualKeyId?: number;
    virtualKeyName?: string;
    model: string;
    provider: string;
    inputTokens: number;
    outputTokens: number;
    cost: number;
    currency: string;
    duration: number;
    status: 'success' | 'error' | 'timeout';
    errorMessage?: string;
    ipAddress?: string;
    userAgent?: string;
    requestHeaders?: Record<string, string>;
    responseHeaders?: Record<string, string>;
    metadata?: AnalyticsMetadata;
}
interface RequestLogFilters extends FilterOptions {
    startDate?: string;
    endDate?: string;
    virtualKeyId?: number;
    model?: string;
    provider?: string;
    status?: 'success' | 'error' | 'timeout';
    minCost?: number;
    maxCost?: number;
    minDuration?: number;
    maxDuration?: number;
    ipAddress?: string;
}
interface UsageMetricsDto {
    period: DateRange;
    totalRequests: number;
    successfulRequests: number;
    failedRequests: number;
    averageLatency: number;
    p95Latency: number;
    p99Latency: number;
    requestsPerMinute: number;
    peakRequestsPerMinute: number;
    uniqueKeys: number;
    uniqueModels: number;
    errorRate: number;
}
interface ModelUsageDto {
    modelId: string;
    totalRequests: number;
    totalTokens: number;
    totalCost: number;
    averageTokensPerRequest: number;
    averageCostPerRequest: number;
    successRate: number;
    averageLatency: number;
    popularKeys: {
        keyId: number;
        keyName: string;
        requestCount: number;
    }[];
}
interface KeyUsageDto {
    keyId: number;
    keyName: string;
    totalRequests: number;
    totalCost: number;
    budgetUsed: number;
    budgetRemaining: number;
    averageCostPerRequest: number;
    requestsPerDay: number;
    popularModels: {
        modelId: string;
        requestCount: number;
        totalCost: number;
    }[];
    lastUsed: string;
}
interface AnalyticsFilters {
    startDate: string;
    endDate: string;
    virtualKeyIds?: number[];
    models?: string[];
    providers?: string[];
    groupBy?: 'hour' | 'day' | 'week' | 'month';
    includeMetadata?: boolean;
}
interface CostForecastDto {
    forecastPeriod: DateRange;
    predictedCost: number;
    confidence: number;
    basedOnPeriod: DateRange;
    factors: {
        name: string;
        impact: number;
        description: string;
    }[];
    recommendations: string[];
}
interface AnomalyDto {
    id: string;
    detectedAt: string;
    type: 'cost_spike' | 'usage_spike' | 'error_rate' | 'latency';
    severity: 'low' | 'medium' | 'high';
    description: string;
    affectedResources: {
        type: 'key' | 'model' | 'provider';
        id: string;
        name: string;
    }[];
    metrics: {
        cost?: number;
        tokens?: number;
        latency?: number;
        errorRate?: number;
        [key: string]: number | undefined;
    };
    resolved: boolean;
}
interface TimeSeriesDataPoint {
    timestamp: string;
    requests: number;
    cost: number;
    tokens: number;
}
interface ProviderUsageBreakdown {
    provider: string;
    requests: number;
    cost: number;
    tokens: number;
    percentage: number;
}
interface ModelUsageBreakdown {
    model: string;
    provider: string;
    requests: number;
    cost: number;
    tokens: number;
}
interface VirtualKeyUsageBreakdown {
    keyName: string;
    requests: number;
    cost: number;
    tokens: number;
    lastUsed: string;
}
interface EndpointUsageBreakdown {
    endpoint: string;
    requests: number;
    avgDuration: number;
    errorRate: number;
}
interface RequestLogStatisticsParams {
    startDate?: string;
    endDate?: string;
    virtualKeyId?: string;
    provider?: string;
    model?: string;
}
interface ServiceHealthMetrics {
    name: string;
    status: 'healthy' | 'degraded' | 'unhealthy';
    uptime: number;
    responseTime: number;
    errorRate: number;
    lastChecked: string;
}
interface QueueMetrics {
    name: string;
    size: number;
    processing: number;
    failed: number;
    throughput: number;
}
interface DatabaseMetrics {
    connections: {
        active: number;
        idle: number;
        total: number;
    };
    queryPerformance: {
        averageTime: number;
        slowQueries: number;
    };
    size: number;
}
interface SystemAlert {
    id: string;
    severity: 'info' | 'warning' | 'error' | 'critical';
    message: string;
    timestamp: string;
    service?: string;
}
interface ProviderHealthDetails {
    provider: string;
    status: 'healthy' | 'degraded' | 'unhealthy';
    uptime: number;
    averageLatency: number;
    errorRate: number;
    lastChecked: string;
    endpoints: EndpointHealth[];
    history?: HealthHistoryPoint[];
}
interface EndpointHealth {
    endpoint: string;
    status: 'healthy' | 'degraded' | 'unhealthy';
    responseTime: number;
    successRate: number;
    lastError?: string;
}
interface HealthHistoryPoint {
    timestamp: string;
    status: 'healthy' | 'degraded' | 'unhealthy';
    uptime: number;
    errorRate: number;
}
interface ProviderIncident {
    id: string;
    provider: string;
    severity: 'low' | 'medium' | 'high' | 'critical';
    status: 'active' | 'resolved';
    startTime: string;
    endTime?: string;
    description: string;
    impact: string;
}
interface VirtualKeyDetail {
    keyId: string;
    keyName: string;
    status: 'active' | 'expired' | 'disabled';
    usage: {
        requests: number;
        requestsChange: number;
        tokens: number;
        tokensChange: number;
        cost: number;
        costChange: number;
        lastUsed: string;
        errorRate: number;
    };
    quota: {
        limit: number;
        used: number;
        remaining: number;
        percentage: number;
        resetDate?: string;
    };
    performance: {
        averageLatency: number;
        errorRate: number;
        successRate: number;
    };
    trends: {
        dailyChange: number;
        weeklyChange: number;
    };
    endpointBreakdown: {
        path: string;
        requests: number;
        avgDuration: number;
        errorRate: number;
    }[];
    timeSeries?: {
        timestamp: string;
        requests: number;
        tokens: number;
        cost: number;
        errorRate: number;
    }[];
    tokenLimit?: number;
    tokenPeriod?: string;
}
interface QuotaAlert {
    keyId: string;
    keyName: string;
    type: 'approaching_limit' | 'exceeded_limit' | 'unusual_activity';
    severity: 'info' | 'warning' | 'critical';
    message: string;
    threshold?: number;
    currentUsage?: number;
}

/**
 * Type-safe Analytics service using native fetch
 */
declare class FetchAnalyticsService {
    private readonly client;
    constructor(client: FetchBaseApiClient);
    /**
     * Get paginated request logs
     */
    getRequestLogs(params?: RequestLogParams, config?: RequestConfig): Promise<RequestLogPage>;
    /**
     * Get a specific request log by ID
     */
    getRequestLogById(id: string, config?: RequestConfig): Promise<RequestLogDto>;
    /**
     * Export request logs
     */
    exportRequestLogs(params: ExportParams$1, config?: RequestConfig): Promise<ExportResult$1>;
    /**
     * Helper method to get export status
     */
    getExportStatus(exportId: string, config?: RequestConfig): Promise<ExportResult$1>;
    /**
     * Helper method to download export
     */
    downloadExport(exportId: string, config?: RequestConfig): Promise<Blob>;
    /**
     * Helper method to format date range
     */
    formatDateRange(days: number): {
        startDate: string;
        endDate: string;
    };
    /**
     * Helper method to calculate growth rate
     */
    calculateGrowthRate(current: number, previous: number): number;
    /**
     * Helper method to get top items from analytics
     */
    getTopItems<T extends {
        value: number;
    }>(items: T[], limit?: number): T[];
    /**
     * Helper method to aggregate time series data
     */
    aggregateTimeSeries(data: Array<{
        timestamp: string;
        value: number;
    }>, groupBy: 'hour' | 'day' | 'week' | 'month'): Array<{
        period: string;
        value: number;
    }>;
    /**
     * Helper method to validate date range
     */
    validateDateRange(startDate?: string, endDate?: string): boolean;
}

/**
 * Health configuration data structure
 */
interface HealthConfigurationData {
    id: string;
    providerId: string;
    checkInterval: number;
    timeout: number;
    retryAttempts: number;
    thresholds: {
        responseTime: number;
        errorRate: number;
        uptime: number;
    };
    enabled: boolean;
    createdAt: string;
    updatedAt: string;
}
/**
 * Health history data structure
 */
interface HealthHistoryData {
    providerId: string;
    timestamp: string;
    status: string;
    responseTime: number;
    errorRate: number;
    availability: number;
}

/**
 * Type-safe Provider Health service using native fetch
 */
declare class FetchProviderHealthService {
    private readonly client;
    constructor(client: FetchBaseApiClient);
    /**
     * Get current health summary for all providers
     */
    getHealthSummary(config?: RequestConfig): Promise<HealthSummaryDto>;
    /**
     * Get legacy health summary (using existing endpoint)
     */
    getLegacyHealthSummary(config?: RequestConfig): Promise<HealthSummaryDto>;
    /**
     * Get detailed health status for a specific provider
     */
    getProviderHealth(providerId: string, config?: RequestConfig): Promise<ProviderHealthDto>;
    /**
     * Get legacy provider health status
     */
    getLegacyProviderStatus(providerId: string, config?: RequestConfig): Promise<ProviderHealthStatusDto>;
    /**
     * Get health history for a provider
     */
    getHealthHistory(providerId: string, params?: HistoryParams, config?: RequestConfig): Promise<HealthHistory>;
    /**
     * Get all health history records
     */
    getAllHealthHistory(startDate?: string, endDate?: string, config?: RequestConfig): Promise<HealthHistoryData[]>;
    /**
     * Get health alerts
     */
    getHealthAlerts(params?: AlertParams, config?: RequestConfig): Promise<HealthAlert[]>;
    /**
     * Test provider connectivity
     */
    testProviderConnection(providerId: string, config?: RequestConfig): Promise<ConnectionTestResult>;
    /**
     * Get provider performance metrics
     */
    getProviderPerformance(providerId: string, params?: PerformanceParams, config?: RequestConfig): Promise<PerformanceMetrics$1>;
    /**
     * Get provider health configurations
     */
    getHealthConfigurations(config?: RequestConfig): Promise<HealthConfigurationData[]>;
    /**
     * Get health configuration for a specific provider
     */
    getProviderHealthConfiguration(providerId: string, config?: RequestConfig): Promise<HealthConfigurationData>;
    /**
     * Create health configuration for a provider
     */
    createHealthConfiguration(data: Partial<HealthConfigurationData>, config?: RequestConfig): Promise<HealthConfigurationData>;
    /**
     * Update health configuration for a provider
     */
    updateHealthConfiguration(providerId: string, data: Partial<HealthConfigurationData>, config?: RequestConfig): Promise<void>;
    /**
     * Acknowledge a health alert
     */
    acknowledgeAlert(alertId: string, config?: RequestConfig): Promise<void>;
    /**
     * Resolve a health alert
     */
    resolveAlert(alertId: string, resolution?: string, config?: RequestConfig): Promise<void>;
    /**
     * Get historical health data for a provider.
     * Retrieves time-series health data for a specific provider including
     * response times, error rates, availability metrics, and related incidents
     * over the specified time period with configurable resolution.
     *
     * @param providerId - Provider ID to get historical data for
     * @param options - Configuration options:
     *   - startDate: Start date for the history range (ISO string)
     *   - endDate: End date for the history range (ISO string)
     *   - resolution: Data point resolution (minute, hour, day)
     *   - includeIncidents: Whether to include incident data
     * @param config - Optional request configuration for timeout, signal, headers
     * @returns Promise<ProviderHealthHistoryResponse> - Historical health data with:
     *   - dataPoints: Time-series data points with metrics
     *   - incidents: Related incidents if requested
     * @throws {Error} When provider health history cannot be retrieved
     * @since Issue #430 - Provider Health SDK Methods
     */
    getProviderHealthHistory(providerId: string, options: ProviderHealthHistoryOptions, config?: RequestConfig): Promise<ProviderHealthHistoryResponse>;
    /**
     * Helper method to calculate health score
     */
    calculateHealthScore(metrics: {
        uptime: number;
        errorRate: number;
        avgLatency: number;
        expectedLatency: number;
    }): number;
    /**
     * Helper method to determine health status from score
     */
    getHealthStatus(score: number): 'healthy' | 'degraded' | 'unhealthy';
    /**
     * Helper method to format uptime percentage
     */
    formatUptime(uptime: number): string;
    /**
     * Helper method to get severity color
     */
    getSeverityColor(severity: 'info' | 'warning' | 'critical'): string;
    /**
     * Helper method to check if provider needs attention
     */
    needsAttention(provider: ProviderHealthSummary): boolean;
    /**
     * Helper method to group alerts by provider
     */
    groupAlertsByProvider(alerts: HealthAlert[]): Record<string, HealthAlert[]>;
    /**
     * Helper method to calculate MTBF (Mean Time Between Failures)
     */
    calculateMTBF(incidents: Array<{
        startTime: string;
        endTime?: string;
    }>, timeRangeHours: number): number;
    /**
     * Helper method to calculate MTTR (Mean Time To Recover)
     */
    calculateMTTR(incidents: Array<{
        startTime: string;
        endTime?: string;
    }>): number;
}

interface IpWhitelistDto {
    enabled: boolean;
    ips: IpEntry[];
    lastModified: string;
    totalBlocked: number;
}
interface IpEntry {
    ip: string;
    cidr?: string;
    description?: string;
    addedBy: string;
    addedAt: string;
    lastSeen?: string;
}
interface SecurityEventParams extends FilterOptions {
    startDate?: string;
    endDate?: string;
    severity?: 'low' | 'medium' | 'high' | 'critical';
    type?: SecurityEventType;
    status?: 'active' | 'acknowledged' | 'resolved';
}
type SecurityEventType = 'suspicious_activity' | 'rate_limit_exceeded' | 'invalid_key_attempt' | 'ip_blocked' | 'unusual_usage_pattern' | 'potential_breach' | 'policy_violation';
interface SecurityEventExtended {
    id: string;
    type: SecurityEventType;
    severity: 'low' | 'medium' | 'high' | 'critical';
    title: string;
    description: string;
    source: {
        ip?: string;
        virtualKeyId?: string;
        userId?: string;
    };
    timestamp: string;
    status: 'active' | 'acknowledged' | 'resolved';
    metadata?: ExtendedMetadata;
}
interface SecurityEventPage {
    items: SecurityEventExtended[];
    totalCount: number;
    page: number;
    pageSize: number;
    totalPages: number;
}
interface ThreatSummaryDto {
    threatLevel: 'low' | 'medium' | 'high' | 'critical';
    activeThreats: number;
    blockedAttempts24h: number;
    suspiciousActivities24h: number;
    topThreats: ThreatCategory[];
}
interface ThreatCategory {
    category: string;
    count: number;
    severity: 'low' | 'medium' | 'high' | 'critical';
    trend: 'increasing' | 'stable' | 'decreasing';
}
interface ActiveThreat {
    id: string;
    type: string;
    severity: 'low' | 'medium' | 'high' | 'critical';
    source: string;
    firstDetected: string;
    lastActivity: string;
    attemptCount: number;
    status: 'monitoring' | 'blocking' | 'mitigated';
    recommendedAction?: string;
}
interface AccessPolicy {
    id: string;
    name: string;
    description?: string;
    type: 'ip_based' | 'key_based' | 'rate_limit' | 'custom';
    rules: PolicyRule[];
    enabled: boolean;
    priority: number;
    createdAt: string;
    updatedAt: string;
}
interface PolicyRule {
    condition: {
        field: string;
        operator: 'equals' | 'contains' | 'gt' | 'lt' | 'regex';
        value: ConfigValue;
    };
    action: 'allow' | 'deny' | 'limit' | 'log';
    metadata?: ExtendedMetadata;
}
interface CreateAccessPolicyDto {
    name: string;
    description?: string;
    type: 'ip_based' | 'key_based' | 'rate_limit' | 'custom';
    rules: PolicyRule[];
    enabled?: boolean;
    priority?: number;
}
interface UpdateAccessPolicyDto {
    name?: string;
    description?: string;
    rules?: PolicyRule[];
    enabled?: boolean;
    priority?: number;
}
interface AuditLogParams extends FilterOptions {
    startDate?: string;
    endDate?: string;
    action?: string;
    userId?: string;
    resourceType?: string;
    resourceId?: string;
}
interface AuditLog {
    id: string;
    timestamp: string;
    userId: string;
    action: string;
    resourceType: string;
    resourceId?: string;
    changes?: SecurityChangeRecord[];
    ipAddress?: string;
    userAgent?: string;
    result: 'success' | 'failure';
    errorMessage?: string;
}
interface AuditLogPage {
    items: AuditLog[];
    totalCount: number;
    page: number;
    pageSize: number;
    totalPages: number;
}
interface ExportParams {
    format: 'json' | 'csv' | 'pdf';
    startDate?: string;
    endDate?: string;
    includeMetadata?: boolean;
}
interface ExportResult {
    exportId: string;
    status: 'pending' | 'processing' | 'completed' | 'failed';
    downloadUrl?: string;
    expiresAt?: string;
    error?: string;
}

/**
 * Security-related models for the Admin SDK
 */

/**
 * Represents a security event in the system
 */
interface SecurityEvent {
    /** Unique identifier for the security event */
    id: string;
    /** Timestamp when the event occurred */
    timestamp: string;
    /** Type of security event */
    type: 'authentication_failure' | 'rate_limit_exceeded' | 'suspicious_activity' | 'invalid_api_key';
    /** Severity level of the event */
    severity: 'low' | 'medium' | 'high' | 'critical';
    /** Source of the security event */
    source: string;
    /** Associated virtual key ID, if applicable */
    virtualKeyId?: string;
    /** IP address associated with the event */
    ipAddress?: string;
    /** Additional event details */
    details: SecurityEventDetails;
    /** HTTP status code, if applicable */
    statusCode?: number;
}
/**
 * Data transfer object for creating a security event
 */
interface CreateSecurityEventDto {
    /** Type of security event */
    type: 'authentication_failure' | 'rate_limit_exceeded' | 'suspicious_activity' | 'invalid_api_key';
    /** Severity level of the event */
    severity: 'low' | 'medium' | 'high' | 'critical';
    /** Source of the security event */
    source: string;
    /** Associated virtual key ID, if applicable */
    virtualKeyId?: string;
    /** IP address associated with the event */
    ipAddress?: string;
    /** Additional event details */
    details: SecurityEventDetails;
    /** HTTP status code, if applicable */
    statusCode?: number;
}
/**
 * Filters for querying security events
 */
interface SecurityEventFilters {
    /** Number of hours to look back */
    hours?: number;
    /** Start date for the query range */
    startDate?: string;
    /** End date for the query range */
    endDate?: string;
    /** Filter by severity level */
    severity?: 'low' | 'medium' | 'high' | 'critical';
    /** Filter by event type */
    type?: 'authentication_failure' | 'rate_limit_exceeded' | 'suspicious_activity' | 'invalid_api_key';
    /** Page number for pagination */
    page?: number;
    /** Number of items per page */
    pageSize?: number;
}
/**
 * Represents a detected threat in the system
 */
interface ThreatDetection {
    /** Unique identifier for the threat */
    id: string;
    /** Title of the threat */
    title: string;
    /** Type of threat */
    type: string;
    /** Severity level of the threat */
    severity: 'minor' | 'major' | 'critical';
    /** Current status of the threat */
    status: 'active' | 'acknowledged' | 'resolved';
    /** Timestamp when the threat was detected */
    detectedAt: string;
    /** Source of the threat detection */
    source: string;
    /** Resources affected by the threat */
    affectedResources: string[];
    /** Detailed description of the threat */
    description: string;
    /** Recommended actions to address the threat */
    recommendations: string[];
}
/**
 * Filters for querying threats
 */
interface ThreatFilters {
    /** Filter by threat status */
    status?: 'active' | 'acknowledged' | 'resolved';
    /** Filter by severity level */
    severity?: 'minor' | 'major' | 'critical';
    /** Page number for pagination */
    page?: number;
    /** Number of items per page */
    pageSize?: number;
}
/**
 * Analytics data for threat detection
 */
interface ThreatAnalytics {
    /** Overall threat level */
    threatLevel: 'low' | 'medium' | 'high' | 'critical';
    /** Threat-related metrics */
    metrics: {
        /** Number of blocked requests */
        blockedRequests: number;
        /** Number of suspicious activities detected */
        suspiciousActivity: number;
        /** Number of rate limit hits */
        rateLimitHits: number;
        /** Number of failed authentication attempts */
        failedAuthentications: number;
        /** Number of currently active threats */
        activeThreats: number;
    };
    /** Top threats by type */
    topThreats: Array<{
        /** Type of threat */
        type: string;
        /** Number of occurrences */
        count: number;
    }>;
    /** Threat trend over time */
    threatTrend: Array<{
        /** Date of the data point */
        date: string;
        /** Number of threats on that date */
        count: number;
    }>;
}
/**
 * Compliance metrics for the system
 */
interface ComplianceMetrics {
    /** Overall compliance score (0-100) */
    overallScore: number;
    /** Compliance scores by category */
    categories: {
        /** Data protection compliance score */
        dataProtection: number;
        /** Access control compliance score */
        accessControl: number;
        /** Audit logging compliance score */
        auditLogging: number;
        /** Incident response compliance score */
        incidentResponse: number;
        /** Monitoring compliance score */
        monitoring: number;
    };
    /** Timestamp of the last compliance assessment */
    lastAssessment: string;
    /** List of compliance issues */
    issues: Array<{
        /** Category of the issue */
        category: string;
        /** Severity of the issue */
        severity: string;
        /** Description of the issue */
        description: string;
    }>;
}
/**
 * Paged result for security-related queries
 */
interface PagedResult<T> {
    /** Array of items in the current page */
    items: T[];
    /** Total number of items across all pages */
    totalCount: number;
    /** Current page number */
    page: number;
    /** Number of items per page */
    pageSize: number;
    /** Total number of pages */
    totalPages: number;
}
/**
 * Actions that can be taken on a threat
 */
type ThreatAction = 'acknowledge' | 'resolve' | 'ignore';
/**
 * Export formats supported by the security service
 */
type ExportFormat = 'json' | 'csv' | 'pdf';

interface ComplianceReport {
    startDate: string;
    endDate: string;
    overallScore: number;
    categories: {
        dataProtection: {
            score: number;
            issues: string[];
        };
        accessControl: {
            score: number;
            issues: string[];
        };
        auditCompliance: {
            score: number;
            issues: string[];
        };
        threatResponse: {
            score: number;
            issues: string[];
        };
    };
    recommendations: string[];
    generatedAt: string;
}
/**
 * Type-safe Security service using native fetch
 */
declare class FetchSecurityService {
    private readonly client;
    constructor(client: FetchBaseApiClient);
    /**
     * Get IP whitelist configuration
     */
    getIpWhitelist(config?: RequestConfig): Promise<IpWhitelistDto>;
    /**
     * Add IPs to whitelist
     */
    addToIpWhitelist(ips: string[], config?: RequestConfig): Promise<void>;
    /**
     * Remove IPs from whitelist
     */
    removeFromIpWhitelist(ips: string[], config?: RequestConfig): Promise<void>;
    /**
     * Get security events with filtering
     */
    getSecurityEvents(params?: SecurityEventParams, config?: RequestConfig): Promise<SecurityEventPage>;
    /**
     * Get security events using existing endpoint and types
     */
    getEvents(filters?: SecurityEventFilters, config?: RequestConfig): Promise<PagedResult<SecurityEvent>>;
    /**
     * Get a specific security event by ID
     */
    getSecurityEventById(id: string, config?: RequestConfig): Promise<SecurityEventExtended>;
    /**
     * Acknowledge a security event
     */
    acknowledgeSecurityEvent(id: string, config?: RequestConfig): Promise<void>;
    /**
     * Report a new security event
     */
    reportEvent(event: CreateSecurityEventDto, config?: RequestConfig): Promise<SecurityEvent>;
    /**
     * Export security events
     */
    exportEvents(params: ExportParams, config?: RequestConfig): Promise<ExportResult>;
    /**
     * Get threat summary
     */
    getThreatSummary(config?: RequestConfig): Promise<ThreatSummaryDto>;
    /**
     * Get active threats
     */
    getActiveThreats(config?: RequestConfig): Promise<ActiveThreat[]>;
    /**
     * Get threats using existing endpoint
     */
    getThreats(filters?: ThreatFilters, config?: RequestConfig): Promise<PagedResult<ThreatDetection>>;
    /**
     * Update threat status
     */
    updateThreatStatus(id: string, action: 'acknowledge' | 'resolve' | 'ignore', config?: RequestConfig): Promise<void>;
    /**
     * Get threat analytics
     */
    getThreatAnalytics(config?: RequestConfig): Promise<ThreatAnalytics>;
    /**
     * Get access policies
     */
    getAccessPolicies(config?: RequestConfig): Promise<AccessPolicy[]>;
    /**
     * Create access policy
     */
    createAccessPolicy(policy: CreateAccessPolicyDto, config?: RequestConfig): Promise<AccessPolicy>;
    /**
     * Update access policy
     */
    updateAccessPolicy(id: string, policy: UpdateAccessPolicyDto, config?: RequestConfig): Promise<AccessPolicy>;
    /**
     * Delete access policy
     */
    deleteAccessPolicy(id: string, config?: RequestConfig): Promise<void>;
    /**
     * Get audit logs
     */
    getAuditLogs(params?: AuditLogParams, config?: RequestConfig): Promise<AuditLogPage>;
    /**
     * Export audit logs
     */
    exportAuditLogs(params: ExportParams, config?: RequestConfig): Promise<ExportResult>;
    /**
     * Get compliance metrics
     */
    getComplianceMetrics(config?: RequestConfig): Promise<ComplianceMetrics>;
    /**
     * Get compliance report
     */
    getComplianceReport(startDate: string, endDate: string, config?: RequestConfig): Promise<ComplianceReport>;
    /**
     * Validate IP address or CIDR notation
     */
    validateIpAddress(ip: string): boolean;
    /**
     * Calculate security score based on metrics
     */
    calculateSecurityScore(metrics: {
        blockedAttempts: number;
        suspiciousActivities: number;
        activeThreats: number;
        failedAuthentications: number;
    }): number;
    /**
     * Group security events by type
     */
    groupEventsByType(events: SecurityEventExtended[]): Record<string, SecurityEventExtended[]>;
    /**
     * Get severity color for UI display
     */
    getSeverityColor(severity: 'low' | 'medium' | 'high' | 'critical'): string;
    /**
     * Format threat level for display
     */
    formatThreatLevel(level: 'low' | 'medium' | 'high' | 'critical'): string;
    /**
     * Check if an IP is in a CIDR range
     */
    isIpInRange(ip: string, cidr: string): boolean;
    /**
     * Generate policy recommendation based on current threats
     */
    generatePolicyRecommendation(threats: ActiveThreat[]): PolicyRule[];
}

interface RoutingConfigDto {
    defaultStrategy: 'round_robin' | 'least_latency' | 'cost_optimized' | 'priority';
    fallbackEnabled: boolean;
    retryPolicy: RetryPolicy;
    timeoutMs: number;
    maxConcurrentRequests: number;
}
interface RetryPolicy {
    maxAttempts: number;
    initialDelayMs: number;
    maxDelayMs: number;
    backoffMultiplier: number;
    retryableStatuses: number[];
}
interface UpdateRoutingConfigDto$1 {
    defaultStrategy?: 'round_robin' | 'least_latency' | 'cost_optimized' | 'priority';
    fallbackEnabled?: boolean;
    retryPolicy?: Partial<RetryPolicy>;
    timeoutMs?: number;
    maxConcurrentRequests?: number;
}
interface RoutingRule$1 {
    id: string;
    name: string;
    priority: number;
    conditions: RuleCondition[];
    actions: RuleAction[];
    enabled: boolean;
    stats?: {
        matchCount: number;
        lastMatched?: string;
    };
}
interface RuleCondition {
    type: 'model' | 'header' | 'body' | 'time' | 'load';
    field?: string;
    operator: 'equals' | 'contains' | 'regex' | 'gt' | 'lt' | 'between';
    value: ConfigValue;
}
interface RuleAction {
    type: 'route' | 'transform' | 'cache' | 'rate_limit' | 'log';
    target?: string;
    parameters?: RouterActionParameters;
}
interface CreateRoutingRuleDto {
    name: string;
    priority?: number;
    conditions: RuleCondition[];
    actions: RuleAction[];
    enabled?: boolean;
}
interface UpdateRoutingRuleDto {
    name?: string;
    priority?: number;
    conditions?: RuleCondition[];
    actions?: RuleAction[];
    enabled?: boolean;
}
interface CacheConfigDto {
    enabled: boolean;
    strategy: 'lru' | 'lfu' | 'ttl' | 'adaptive';
    maxSizeBytes: number;
    defaultTtlSeconds: number;
    rules: CacheRule[];
    redis?: {
        enabled: boolean;
        endpoint: string;
        cluster: boolean;
    };
}
interface UpdateCacheConfigDto {
    enabled?: boolean;
    strategy?: 'lru' | 'lfu' | 'ttl' | 'adaptive';
    maxSizeBytes?: number;
    defaultTtlSeconds?: number;
    rules?: CacheRule[];
    redis?: {
        enabled?: boolean;
        endpoint?: string;
        cluster?: boolean;
    };
}
interface CacheRule {
    id: string;
    pattern: string;
    ttlSeconds: number;
    maxSizeBytes?: number;
    conditions?: CacheCondition[];
}
interface CacheCondition {
    type: 'header' | 'query' | 'body' | 'time';
    field: string;
    operator: 'equals' | 'contains' | 'regex' | 'exists';
    value?: ConfigValue;
}
interface CacheClearParams {
    pattern?: string;
    region?: string;
    type?: 'all' | 'expired' | 'pattern';
    force?: boolean;
}
interface CacheClearResult {
    success: boolean;
    clearedCount: number;
    clearedSizeBytes: number;
    errors?: string[];
}
interface CacheStatsDto {
    hitRate: number;
    missRate: number;
    evictionRate: number;
    totalRequests: number;
    totalHits: number;
    totalMisses: number;
    currentSizeBytes: number;
    maxSizeBytes: number;
    itemCount: number;
    topKeys: CacheKeyStats[];
}
interface CacheKeyStats {
    key: string;
    hits: number;
    misses: number;
    sizeBytes: number;
    ttlSeconds: number;
    lastAccessed: string;
}
interface LoadBalancerConfigDto {
    algorithm: 'round_robin' | 'weighted_round_robin' | 'least_connections' | 'ip_hash' | 'random';
    healthCheck: {
        enabled: boolean;
        intervalSeconds: number;
        timeoutSeconds: number;
        unhealthyThreshold: number;
        healthyThreshold: number;
    };
    weights?: Record<string, number>;
    stickySession?: {
        enabled: boolean;
        cookieName: string;
        ttlSeconds: number;
    };
}
interface UpdateLoadBalancerConfigDto {
    algorithm?: 'round_robin' | 'weighted_round_robin' | 'least_connections' | 'ip_hash' | 'random';
    healthCheck?: Partial<LoadBalancerConfigDto['healthCheck']>;
    weights?: Record<string, number>;
    stickySession?: Partial<LoadBalancerConfigDto['stickySession']>;
}
interface LoadBalancerHealthDto {
    status: 'healthy' | 'degraded' | 'unhealthy';
    nodes: LoadBalancerNode[];
    lastCheck: string;
    distribution: Record<string, number>;
}
interface LoadBalancerNode {
    id: string;
    endpoint: string;
    status: 'healthy' | 'unhealthy' | 'draining';
    weight: number;
    activeConnections: number;
    totalRequests: number;
    avgResponseTime: number;
    lastHealthCheck: string;
}
interface PerformanceConfigDto {
    connectionPool: {
        minSize: number;
        maxSize: number;
        acquireTimeoutMs: number;
        idleTimeoutMs: number;
    };
    requestQueue: {
        maxSize: number;
        timeout: number;
        priorityLevels: number;
    };
    circuitBreaker: {
        enabled: boolean;
        failureThreshold: number;
        resetTimeoutMs: number;
        halfOpenRequests: number;
    };
    rateLimiter: {
        enabled: boolean;
        requestsPerSecond: number;
        burstSize: number;
    };
}
interface UpdatePerformanceConfigDto {
    connectionPool?: Partial<PerformanceConfigDto['connectionPool']>;
    requestQueue?: Partial<PerformanceConfigDto['requestQueue']>;
    circuitBreaker?: Partial<PerformanceConfigDto['circuitBreaker']>;
    rateLimiter?: Partial<PerformanceConfigDto['rateLimiter']>;
}
interface PerformanceTestParams {
    duration: number;
    concurrentUsers: number;
    requestsPerSecond: number;
    models: string[];
    payloadSize: 'small' | 'medium' | 'large';
}
interface PerformanceTestResult {
    summary: {
        totalRequests: number;
        successfulRequests: number;
        failedRequests: number;
        avgLatency: number;
        p50Latency: number;
        p95Latency: number;
        p99Latency: number;
        throughput: number;
    };
    timeline: PerformanceDataPoint[];
    errors: ErrorSummary[];
    recommendations: string[];
}
interface PerformanceDataPoint {
    timestamp: string;
    requestsPerSecond: number;
    avgLatency: number;
    errorRate: number;
    activeConnections: number;
}
interface ErrorSummary {
    type: string;
    count: number;
    message: string;
    firstOccurred: string;
    lastOccurred: string;
}
interface FeatureFlag {
    key: string;
    name: string;
    description?: string;
    enabled: boolean;
    rolloutPercentage?: number;
    conditions?: FeatureFlagCondition[];
    metadata?: ExtendedMetadata;
    lastModified: string;
}
interface FeatureFlagCondition {
    type: 'user' | 'key' | 'environment' | 'custom';
    field: string;
    operator: 'in' | 'not_in' | 'equals' | 'regex';
    values: ConfigValue[];
}
interface UpdateFeatureFlagDto {
    name?: string;
    description?: string;
    enabled?: boolean;
    rolloutPercentage?: number;
    conditions?: FeatureFlagCondition[];
    metadata?: ExtendedMetadata;
}
/**
 * Routing health status information
 */
interface RoutingHealthStatus {
    /** Overall routing system health */
    status: 'healthy' | 'degraded' | 'unhealthy' | 'unknown';
    /** Last health check timestamp */
    lastChecked: string;
    /** Total number of active routes */
    totalRoutes: number;
    /** Number of healthy routes */
    healthyRoutes: number;
    /** Number of degraded routes */
    degradedRoutes: number;
    /** Number of failed routes */
    failedRoutes: number;
    /** Load balancer status */
    loadBalancer: {
        status: 'healthy' | 'degraded' | 'unhealthy';
        activeNodes: number;
        totalNodes: number;
        avgResponseTime: number;
    };
    /** Circuit breaker status */
    circuitBreakers: {
        totalBreakers: number;
        openBreakers: number;
        halfOpenBreakers: number;
        closedBreakers: number;
    };
    /** Overall performance metrics */
    performance: {
        avgLatency: number;
        p95Latency: number;
        requestsPerSecond: number;
        errorRate: number;
        successRate: number;
    };
}
/**
 * Individual route health information
 */
interface RouteHealthDetails {
    /** Route identifier */
    routeId: string;
    /** Route name or description */
    routeName: string;
    /** Route pattern or path */
    pattern: string;
    /** Current health status */
    status: 'healthy' | 'degraded' | 'unhealthy' | 'disabled';
    /** Target provider or endpoint */
    target: string;
    /** Health check results */
    healthCheck: {
        status: 'passing' | 'failing' | 'warning';
        lastCheck: string;
        responseTime: number;
        statusCode?: number;
        errorMessage?: string;
    };
    /** Performance metrics */
    metrics: {
        requestCount: number;
        successCount: number;
        errorCount: number;
        avgResponseTime: number;
        p95ResponseTime: number;
        throughput: number;
    };
    /** Circuit breaker status */
    circuitBreaker: {
        state: 'closed' | 'open' | 'half-open';
        failureCount: number;
        successCount: number;
        lastStateChange: string;
        nextRetryAttempt?: string;
    };
    /** Configuration details */
    configuration: {
        enabled: boolean;
        weight: number;
        timeout: number;
        retryPolicy: {
            maxRetries: number;
            backoffMultiplier: number;
            maxBackoffMs: number;
        };
    };
}
/**
 * Routing health history data point
 */
interface RoutingHealthDataPoint {
    timestamp: string;
    overallStatus: 'healthy' | 'degraded' | 'unhealthy';
    healthyRoutes: number;
    totalRoutes: number;
    avgLatency: number;
    requestsPerSecond: number;
    errorRate: number;
    activeCircuitBreakers: number;
}
/**
 * Routing health history response
 */
interface RoutingHealthHistory {
    /** Time-series health data */
    dataPoints: RoutingHealthDataPoint[];
    /** Summary statistics */
    summary: {
        timeRange: string;
        avgHealthyPercentage: number;
        maxLatency: number;
        minLatency: number;
        avgLatency: number;
        totalRequests: number;
        totalErrors: number;
        uptimePercentage: number;
    };
    /** Significant incidents during the period */
    incidents: Array<{
        id: string;
        timestamp: string;
        type: 'outage' | 'degradation' | 'circuit_breaker' | 'configuration';
        affectedRoutes: string[];
        duration: number;
        resolved: boolean;
        description: string;
    }>;
}
/**
 * Options for routing health monitoring
 */
interface RoutingHealthOptions {
    /** Include detailed route information */
    includeRouteDetails?: boolean;
    /** Include historical data */
    includeHistory?: boolean;
    /** Historical data time range */
    historyTimeRange?: '1h' | '24h' | '7d' | '30d';
    /** Data resolution for history */
    historyResolution?: 'minute' | 'hour' | 'day';
    /** Include performance metrics */
    includePerformanceMetrics?: boolean;
    /** Include circuit breaker status */
    includeCircuitBreakers?: boolean;
}
/**
 * Comprehensive routing health response
 */
interface RoutingHealthResponse {
    /** Overall health status */
    health: RoutingHealthStatus;
    /** Individual route details */
    routes: RouteHealthDetails[];
    /** Historical health data */
    history?: RoutingHealthHistory;
    /** Real-time subscription information */
    subscription?: {
        endpoint: string;
        connectionId: string;
        events: string[];
    };
}
/**
 * Route performance test parameters
 */
interface RoutePerformanceTestParams {
    /** Routes to test (empty for all) */
    routeIds?: string[];
    /** Test duration in seconds */
    duration: number;
    /** Concurrent requests per route */
    concurrency: number;
    /** Request rate per second */
    requestRate: number;
    /** Test payload configuration */
    payload?: {
        size: number;
        complexity: 'simple' | 'medium' | 'complex';
        customData?: ExtendedMetadata;
    };
    /** Performance thresholds */
    thresholds?: {
        maxLatency: number;
        maxErrorRate: number;
        minThroughput: number;
    };
}
/**
 * Route performance test results
 */
interface RoutePerformanceTestResult {
    /** Test execution details */
    testInfo: {
        testId: string;
        startTime: string;
        endTime: string;
        duration: number;
        params: RoutePerformanceTestParams;
    };
    /** Overall test results */
    summary: {
        totalRequests: number;
        successfulRequests: number;
        failedRequests: number;
        avgLatency: number;
        p50Latency: number;
        p95Latency: number;
        p99Latency: number;
        maxLatency: number;
        minLatency: number;
        throughput: number;
        errorRate: number;
        thresholdsPassed: boolean;
    };
    /** Per-route results */
    routeResults: Array<{
        routeId: string;
        routeName: string;
        requests: number;
        successes: number;
        failures: number;
        avgLatency: number;
        p95Latency: number;
        throughput: number;
        errorRate: number;
        thresholdsPassed: boolean;
        errors: Array<{
            type: string;
            count: number;
            percentage: number;
            lastOccurrence: string;
        }>;
    }>;
    /** Timeline data */
    timeline: Array<{
        timestamp: string;
        requestsPerSecond: number;
        avgLatency: number;
        errorRate: number;
        activeRoutes: number;
    }>;
    /** Recommendations */
    recommendations: string[];
}
/**
 * Circuit breaker configuration
 */
interface CircuitBreakerConfig {
    /** Circuit breaker identifier */
    id: string;
    /** Associated route ID */
    routeId: string;
    /** Failure threshold to open circuit */
    failureThreshold: number;
    /** Success threshold to close circuit */
    successThreshold: number;
    /** Timeout before attempting half-open */
    timeout: number;
    /** Sliding window size for failure tracking */
    slidingWindowSize: number;
    /** Minimum number of calls before evaluation */
    minimumNumberOfCalls: number;
    /** Slow call duration threshold */
    slowCallDurationThreshold: number;
    /** Slow call rate threshold */
    slowCallRateThreshold: number;
    /** Whether circuit breaker is enabled */
    enabled: boolean;
}
/**
 * Circuit breaker status
 */
interface CircuitBreakerStatus {
    /** Circuit breaker configuration */
    config: CircuitBreakerConfig;
    /** Current state */
    state: 'closed' | 'open' | 'half-open' | 'disabled';
    /** Failure metrics */
    metrics: {
        failureRate: number;
        slowCallRate: number;
        numberOfCalls: number;
        numberOfFailedCalls: number;
        numberOfSlowCalls: number;
        numberOfSuccessfulCalls: number;
    };
    /** State transitions */
    stateTransitions: Array<{
        timestamp: string;
        fromState: string;
        toState: string;
        reason: string;
    }>;
    /** Last state change */
    lastStateChange: string;
    /** Next retry attempt (for open state) */
    nextRetryAttempt?: string;
}

/**
 * Configuration-related models for routing and caching
 */

/**
 * Routing configuration for multi-provider setups
 */
interface RoutingConfiguration {
    /** Enable automatic failover to backup providers */
    enableFailover: boolean;
    /** Enable load balancing across providers */
    enableLoadBalancing: boolean;
    /** Request timeout in seconds */
    requestTimeoutSeconds: number;
    /** Number of retry attempts on failure */
    retryAttempts: number;
    /** Delay between retries in milliseconds */
    retryDelayMs: number;
    /** Circuit breaker threshold (failures before circuit opens) */
    circuitBreakerThreshold: number;
    /** Health check interval in seconds */
    healthCheckIntervalSeconds: number;
    /** Load balancing strategy */
    loadBalancingStrategy: 'round-robin' | 'least-connections' | 'weighted' | 'random';
    /** Routing rules for conditional routing */
    routingRules: RoutingRule[];
    /** Provider priorities for failover */
    providerPriorities: ProviderPriority[];
}
/**
 * DTO for updating routing configuration
 */
interface UpdateRoutingConfigDto {
    enableFailover?: boolean;
    enableLoadBalancing?: boolean;
    requestTimeoutSeconds?: number;
    retryAttempts?: number;
    retryDelayMs?: number;
    circuitBreakerThreshold?: number;
    healthCheckIntervalSeconds?: number;
    loadBalancingStrategy?: 'round-robin' | 'least-connections' | 'weighted' | 'random';
    routingRules?: RoutingRule[];
    providerPriorities?: ProviderPriority[];
}
/**
 * Routing rule for conditional provider selection
 */
interface RoutingRule {
    /** Unique identifier for the rule */
    id: string;
    /** Name of the routing rule */
    name: string;
    /** Condition expression (e.g., "model.startsWith('gpt-4')") */
    condition: string;
    /** Target provider when condition matches */
    targetProvider: string;
    /** Priority order for rule evaluation */
    priority: number;
    /** Whether the rule is active */
    enabled: boolean;
}
/**
 * Provider priority for failover scenarios
 */
interface ProviderPriority {
    /** Provider name */
    provider: string;
    /** Priority (lower number = higher priority) */
    priority: number;
    /** Weight for weighted load balancing */
    weight?: number;
}
/**
 * Test result for routing configuration
 */
interface TestResult {
    /** Whether the test passed */
    success: boolean;
    /** Test execution time in milliseconds */
    executionTimeMs: number;
    /** Detailed test results */
    results: Array<{
        test: string;
        passed: boolean;
        message: string;
        details?: ExtendedMetadata;
    }>;
    /** Any errors encountered */
    errors: string[];
}
/**
 * Load balancer health status
 */
interface LoadBalancerHealth {
    /** Provider name */
    provider: string;
    /** Health status */
    status: 'healthy' | 'degraded' | 'unhealthy';
    /** Last health check timestamp */
    lastCheckTime: string;
    /** Response time in milliseconds */
    responseTimeMs: number;
    /** Success rate percentage */
    successRate: number;
    /** Active connections */
    activeConnections: number;
    /** Error count in last interval */
    errorCount: number;
}
/**
 * Caching configuration
 */
interface CachingConfiguration {
    /** Default TTL in seconds */
    defaultTTLSeconds: number;
    /** Maximum memory size in MB */
    maxMemorySizeMB: number;
    /** Cache eviction policy */
    evictionPolicy: 'lru' | 'lfu' | 'fifo';
    /** Whether compression is enabled */
    compressionEnabled: boolean;
    /** Whether distributed caching is enabled */
    distributedCacheEnabled: boolean;
    /** Redis connection string for distributed cache */
    redisConnectionString?: string;
    /** List of cacheable endpoints */
    cacheableEndpoints: string[];
    /** Patterns to exclude from caching */
    excludePatterns: string[];
}
/**
 * DTO for updating caching configuration
 */
interface UpdateCachingConfigDto {
    defaultTTLSeconds?: number;
    maxMemorySizeMB?: number;
    evictionPolicy?: 'lru' | 'lfu' | 'fifo';
    compressionEnabled?: boolean;
    distributedCacheEnabled?: boolean;
    redisConnectionString?: string;
    cacheableEndpoints?: string[];
    excludePatterns?: string[];
}
/**
 * Cache policy for fine-grained control
 */
interface CachePolicy {
    /** Unique identifier */
    id: string;
    /** Policy name */
    name: string;
    /** Policy type */
    type: 'endpoint' | 'model' | 'global';
    /** Pattern to match (regex or glob) */
    pattern: string;
    /** TTL in seconds */
    ttlSeconds: number;
    /** Maximum size in MB */
    maxSizeMB?: number;
    /** Caching strategy */
    strategy: 'memory' | 'redis' | 'hybrid';
    /** Whether the policy is enabled */
    enabled: boolean;
    /** Additional metadata */
    metadata?: BaseMetadata;
}
/**
 * DTO for creating a cache policy
 */
interface CreateCachePolicyDto {
    name: string;
    type: 'endpoint' | 'model' | 'global';
    pattern: string;
    ttlSeconds: number;
    maxSizeMB?: number;
    strategy: 'memory' | 'redis' | 'hybrid';
    enabled?: boolean;
    metadata?: BaseMetadata;
}
/**
 * DTO for updating a cache policy
 */
interface UpdateCachePolicyDto {
    name?: string;
    pattern?: string;
    ttlSeconds?: number;
    maxSizeMB?: number;
    strategy?: 'memory' | 'redis' | 'hybrid';
    enabled?: boolean;
    metadata?: BaseMetadata;
}
/**
 * Cache region information
 */
interface CacheRegion {
    /** Region identifier */
    id: string;
    /** Region name */
    name: string;
    /** Region type */
    type: 'memory' | 'redis' | 'distributed';
    /** Region health status */
    status: 'healthy' | 'degraded' | 'offline';
    /** Nodes in this region */
    nodes: CacheNode[];
    /** Region statistics */
    statistics: {
        hitRate: number;
        missRate: number;
        evictionRate: number;
        memoryUsageMB: number;
        itemCount: number;
    };
}
/**
 * Cache node information
 */
interface CacheNode {
    /** Node identifier */
    id: string;
    /** Node hostname */
    hostname: string;
    /** Node status */
    status: 'online' | 'offline' | 'maintenance';
    /** Memory usage in MB */
    memoryUsageMB: number;
    /** Number of cached items */
    itemCount: number;
}
/**
 * Result of cache clearing operation
 */
interface ClearCacheResult {
    /** Whether the operation was successful */
    success: boolean;
    /** Number of items cleared */
    itemsCleared: number;
    /** Memory freed in MB */
    memoryFreedMB: number;
    /** Time taken in milliseconds */
    executionTimeMs: number;
    /** Any errors encountered */
    errors?: string[];
}
/**
 * Cache statistics
 */
interface CacheStatistics {
    /** Global cache statistics */
    global: {
        totalHits: number;
        totalMisses: number;
        hitRate: number;
        memoryUsageMB: number;
        itemCount: number;
    };
    /** Statistics by region */
    byRegion: Record<string, RegionStatistics>;
    /** Statistics by endpoint */
    byEndpoint: Record<string, EndpointStatistics>;
    /** Trend data */
    trends: {
        hourly: StatisticPoint[];
        daily: StatisticPoint[];
    };
}
/**
 * Region-specific statistics
 */
interface RegionStatistics {
    hits: number;
    misses: number;
    hitRate: number;
    memoryUsageMB: number;
    itemCount: number;
    evictions: number;
}
/**
 * Endpoint-specific statistics
 */
interface EndpointStatistics {
    hits: number;
    misses: number;
    hitRate: number;
    averageResponseTimeMs: number;
    cachedResponseSizeKB: number;
}
/**
 * Point in time statistic
 */
interface StatisticPoint {
    timestamp: string;
    hitRate: number;
    requestCount: number;
    memoryUsageMB: number;
}

/**
 * Type-safe Configuration service using native fetch
 */
declare class FetchConfigurationService {
    private readonly client;
    constructor(client: FetchBaseApiClient);
    /**
     * Get routing configuration
     */
    getRoutingConfig(config?: RequestConfig): Promise<RoutingConfigDto>;
    /**
     * Get routing configuration (using existing endpoint)
     */
    getRoutingConfiguration(config?: RequestConfig): Promise<RoutingConfiguration>;
    /**
     * Update routing configuration
     */
    updateRoutingConfig(data: UpdateRoutingConfigDto$1, config?: RequestConfig): Promise<RoutingConfigDto>;
    /**
     * Update routing configuration (using existing endpoint)
     */
    updateRoutingConfiguration(data: UpdateRoutingConfigDto, config?: RequestConfig): Promise<RoutingConfiguration>;
    /**
     * Test routing configuration
     */
    testRoutingConfig(config?: RequestConfig): Promise<TestResult>;
    /**
     * Get routing rules
     */
    getRoutingRules(config?: RequestConfig): Promise<RoutingRule$1[]>;
    /**
     * Create routing rule
     */
    createRoutingRule(rule: CreateRoutingRuleDto, config?: RequestConfig): Promise<RoutingRule$1>;
    /**
     * Update routing rule
     */
    updateRoutingRule(id: string, rule: UpdateRoutingRuleDto, config?: RequestConfig): Promise<RoutingRule$1>;
    /**
     * Delete routing rule
     */
    deleteRoutingRule(id: string, config?: RequestConfig): Promise<void>;
    /**
     * Get cache configuration
     */
    getCacheConfig(config?: RequestConfig): Promise<CacheConfigDto>;
    /**
     * Get caching configuration (using existing endpoint)
     */
    getCachingConfiguration(config?: RequestConfig): Promise<CachingConfiguration>;
    /**
     * Update cache configuration
     */
    updateCacheConfig(data: UpdateCacheConfigDto, config?: RequestConfig): Promise<CacheConfigDto>;
    /**
     * Update caching configuration (using existing endpoint)
     */
    updateCachingConfiguration(data: UpdateCacheConfigDto, config?: RequestConfig): Promise<CachingConfiguration>;
    /**
     * Clear cache
     */
    clearCache(params?: CacheClearParams, config?: RequestConfig): Promise<CacheClearResult>;
    /**
     * Clear cache by region (using existing endpoint)
     */
    clearCacheByRegion(regionId: string, config?: RequestConfig): Promise<ClearCacheResult>;
    /**
     * Get cache statistics
     */
    getCacheStats(config?: RequestConfig): Promise<CacheStatsDto>;
    /**
     * Get cache statistics (using existing endpoint)
     */
    getCacheStatistics(config?: RequestConfig): Promise<CacheStatistics>;
    /**
     * Get cache policies
     */
    getCachePolicies(config?: RequestConfig): Promise<CachePolicy[]>;
    /**
     * Create cache policy
     */
    createCachePolicy(policy: CreateCachePolicyDto, config?: RequestConfig): Promise<CachePolicy>;
    /**
     * Update cache policy
     */
    updateCachePolicy(id: string, policy: UpdateCachePolicyDto, config?: RequestConfig): Promise<CachePolicy>;
    /**
     * Delete cache policy
     */
    deleteCachePolicy(id: string, config?: RequestConfig): Promise<void>;
    /**
     * Get load balancer configuration
     */
    getLoadBalancerConfig(config?: RequestConfig): Promise<LoadBalancerConfigDto>;
    /**
     * Update load balancer configuration
     */
    updateLoadBalancerConfig(data: UpdateLoadBalancerConfigDto, config?: RequestConfig): Promise<LoadBalancerConfigDto>;
    /**
     * Get load balancer health
     */
    getLoadBalancerHealth(config?: RequestConfig): Promise<LoadBalancerHealthDto>;
    /**
     * Get load balancer health (using existing endpoint)
     */
    getLoadBalancerHealthStatus(config?: RequestConfig): Promise<LoadBalancerHealth[]>;
    /**
     * Get performance configuration
     */
    getPerformanceConfig(config?: RequestConfig): Promise<PerformanceConfigDto>;
    /**
     * Update performance configuration
     */
    updatePerformanceConfig(data: UpdatePerformanceConfigDto, config?: RequestConfig): Promise<PerformanceConfigDto>;
    /**
     * Run performance test
     */
    runPerformanceTest(params: PerformanceTestParams, config?: RequestConfig): Promise<PerformanceTestResult>;
    /**
     * Get feature flags
     */
    getFeatureFlags(config?: RequestConfig): Promise<FeatureFlag[]>;
    /**
     * Update feature flag
     */
    updateFeatureFlag(key: string, data: UpdateFeatureFlagDto, config?: RequestConfig): Promise<FeatureFlag>;
    /**
     * Get comprehensive routing health status.
     * Retrieves overall routing system health including route status, load balancer
     * health, circuit breaker status, and performance metrics with optional
     * detailed information and historical data.
     *
     * @param options - Routing health monitoring options:
     *   - includeRouteDetails: Include individual route health information
     *   - includeHistory: Include historical health data
     *   - historyTimeRange: Time range for historical data
     *   - historyResolution: Data resolution for history
     *   - includePerformanceMetrics: Include performance metrics
     *   - includeCircuitBreakers: Include circuit breaker status
     * @param config - Optional request configuration for timeout, signal, headers
     * @returns Promise<RoutingHealthResponse> - Comprehensive routing health data
     * @throws {Error} When routing health data cannot be retrieved
     * @since Issue #437 - Routing Health and Configuration SDK Methods
     *
     * @example
     * ```typescript
     * // Get basic routing health status
     * const health = await adminClient.configuration.getRoutingHealthStatus();
     * console.warn(`Overall status: ${health.health.status}`);
     * console.warn(`Healthy routes: ${health.health.healthyRoutes}/${health.health.totalRoutes}`);
     *
     * // Get detailed health information with history
     * const detailedHealth = await adminClient.configuration.getRoutingHealthStatus({
     *   includeRouteDetails: true,
     *   includeHistory: true,
     *   historyTimeRange: '24h',
     *   includeCircuitBreakers: true
     * });
     *
     * detailedHealth.routes.forEach(route => {
     *   console.warn(`Route ${route.routeName}: ${route.status}`);
     *   console.warn(`  Circuit breaker: ${route.circuitBreaker.state}`);
     *   console.warn(`  Avg response time: ${route.metrics.avgResponseTime}ms`);
     * });
     * ```
     */
    getRoutingHealthStatus(options?: RoutingHealthOptions, config?: RequestConfig): Promise<RoutingHealthResponse>;
    /**
     * Get health status for a specific route.
     * Retrieves detailed health information for a single route including
     * health checks, performance metrics, circuit breaker status, and
     * configuration details.
     *
     * @param routeId - Route identifier to get health information for
     * @param config - Optional request configuration for timeout, signal, headers
     * @returns Promise<RouteHealthDetails> - Detailed route health information
     * @throws {Error} When route health data cannot be retrieved
     * @since Issue #437 - Routing Health and Configuration SDK Methods
     *
     * @example
     * ```typescript
     * // Get health status for a specific route
     * const routeHealth = await adminClient.configuration.getRouteHealthStatus('route-openai-gpt4');
     *
     * console.warn(`Route: ${routeHealth.routeName}`);
     * console.warn(`Status: ${routeHealth.status}`);
     * console.warn(`Health check: ${routeHealth.healthCheck.status}`);
     * console.warn(`Response time: ${routeHealth.healthCheck.responseTime}ms`);
     * console.warn(`Circuit breaker: ${routeHealth.circuitBreaker.state}`);
     * console.warn(`Success rate: ${(routeHealth.metrics.successCount / routeHealth.metrics.requestCount * 100).toFixed(2)}%`);
     * ```
     */
    getRouteHealthStatus(routeId: string, config?: RequestConfig): Promise<RouteHealthDetails>;
    /**
     * Get routing health history data.
     * Retrieves historical routing health data with time-series information,
     * summary statistics, and incident tracking for the specified time period.
     *
     * @param timeRange - Time range for historical data (e.g., '1h', '24h', '7d', '30d')
     * @param resolution - Data resolution ('minute', 'hour', 'day')
     * @param config - Optional request configuration for timeout, signal, headers
     * @returns Promise<RoutingHealthHistory> - Historical routing health data
     * @throws {Error} When routing health history cannot be retrieved
     * @since Issue #437 - Routing Health and Configuration SDK Methods
     *
     * @example
     * ```typescript
     * // Get 24-hour routing health history with hourly resolution
     * const history = await adminClient.configuration.getRoutingHealthHistory('24h', 'hour');
     *
     * console.warn(`Time range: ${history.summary.timeRange}`);
     * console.warn(`Average healthy percentage: ${history.summary.avgHealthyPercentage}%`);
     * console.warn(`Uptime: ${history.summary.uptimePercentage}%`);
     *
     * // Review historical data points
     * history.dataPoints.forEach(point => {
     *   console.warn(`${point.timestamp}: ${point.healthyRoutes}/${point.totalRoutes} routes healthy`);
     * });
     *
     * // Check for incidents
     * history.incidents.forEach(incident => {
     *   console.warn(`Incident: ${incident.type} affecting ${incident.affectedRoutes.length} routes`);
     * });
     * ```
     */
    getRoutingHealthHistory(timeRange?: '1h' | '24h' | '7d' | '30d', resolution?: 'minute' | 'hour' | 'day', config?: RequestConfig): Promise<RoutingHealthHistory>;
    /**
     * Run performance test on routing system.
     * Executes a comprehensive performance test on the routing system or specific
     * routes with configurable parameters including load, duration, and thresholds.
     *
     * @param params - Performance test parameters:
     *   - routeIds: Specific routes to test (empty for all)
     *   - duration: Test duration in seconds
     *   - concurrency: Concurrent requests per route
     *   - requestRate: Request rate per second
     *   - payload: Test payload configuration
     *   - thresholds: Performance thresholds for pass/fail
     * @param config - Optional request configuration for timeout, signal, headers
     * @returns Promise<RoutePerformanceTestResult> - Comprehensive test results
     * @throws {Error} When performance test cannot be executed
     * @since Issue #437 - Routing Health and Configuration SDK Methods
     *
     * @example
     * ```typescript
     * // Run comprehensive routing performance test
     * const testResult = await adminClient.configuration.runRoutePerformanceTest({
     *   duration: 300, // 5 minutes
     *   concurrency: 50,
     *   requestRate: 100,
     *   thresholds: {
     *     maxLatency: 2000,
     *     maxErrorRate: 5,
     *     minThroughput: 80
     *   }
     * });
     *
     * console.warn(`Test completed: ${testResult.summary.thresholdsPassed ? 'PASSED' : 'FAILED'}`);
     * console.warn(`Total requests: ${testResult.summary.totalRequests}`);
     * console.warn(`Success rate: ${((testResult.summary.successfulRequests / testResult.summary.totalRequests) * 100).toFixed(2)}%`);
     * console.warn(`Average latency: ${testResult.summary.avgLatency}ms`);
     * console.warn(`P95 latency: ${testResult.summary.p95Latency}ms`);
     *
     * // Review per-route results
     * testResult.routeResults.forEach(route => {
     *   console.warn(`Route ${route.routeName}: ${route.thresholdsPassed ? 'PASSED' : 'FAILED'}`);
     * });
     *
     * // Get recommendations
     * testResult.recommendations.forEach(rec => console.warn(`💡 ${rec}`));
     * ```
     */
    runRoutePerformanceTest(params: RoutePerformanceTestParams, config?: RequestConfig): Promise<RoutePerformanceTestResult>;
    /**
     * Get circuit breaker configurations and status.
     * Retrieves all circuit breaker configurations and their current status
     * including state, metrics, and recent state transitions.
     *
     * @param config - Optional request configuration for timeout, signal, headers
     * @returns Promise<CircuitBreakerStatus[]> - Circuit breaker status array
     * @throws {Error} When circuit breaker data cannot be retrieved
     * @since Issue #437 - Routing Health and Configuration SDK Methods
     *
     * @example
     * ```typescript
     * // Get all circuit breaker status
     * const circuitBreakers = await adminClient.configuration.getCircuitBreakerStatus();
     *
     * circuitBreakers.forEach(breaker => {
     *   console.warn(`Circuit breaker ${breaker.config.id}:`);
     *   console.warn(`  Route: ${breaker.config.routeId}`);
     *   console.warn(`  State: ${breaker.state}`);
     *   console.warn(`  Failure rate: ${breaker.metrics.failureRate}%`);
     *   console.warn(`  Calls: ${breaker.metrics.numberOfCalls}`);
     *
     *   if (breaker.state === 'open') {
     *     console.warn(`  Next retry: ${breaker.nextRetryAttempt}`);
     *   }
     * });
     * ```
     */
    getCircuitBreakerStatus(config?: RequestConfig): Promise<CircuitBreakerStatus[]>;
    /**
     * Update circuit breaker configuration.
     * Updates the configuration for a specific circuit breaker including
     * thresholds, timeouts, and other circuit breaker parameters.
     *
     * @param breakerId - Circuit breaker identifier
     * @param config - Circuit breaker configuration updates
     * @param requestConfig - Optional request configuration for timeout, signal, headers
     * @returns Promise<CircuitBreakerStatus> - Updated circuit breaker status
     * @throws {Error} When circuit breaker configuration cannot be updated
     * @since Issue #437 - Routing Health and Configuration SDK Methods
     *
     * @example
     * ```typescript
     * // Update circuit breaker configuration
     * const updatedBreaker = await adminClient.configuration.updateCircuitBreakerConfig(
     *   'breaker-openai-gpt4',
     *   {
     *     failureThreshold: 10,
     *     timeout: 30000,
     *     enabled: true
     *   }
     * );
     *
     * console.warn(`Circuit breaker updated: ${updatedBreaker.config.id}`);
     * console.warn(`New failure threshold: ${updatedBreaker.config.failureThreshold}`);
     * ```
     */
    updateCircuitBreakerConfig(breakerId: string, config: Partial<CircuitBreakerConfig>, requestConfig?: RequestConfig): Promise<CircuitBreakerStatus>;
    /**
     * Subscribe to real-time routing health events.
     * Establishes a real-time connection to receive routing health events
     * including route health changes, circuit breaker state changes, and
     * performance alerts.
     *
     * @param eventTypes - Types of events to subscribe to
     * @param config - Optional request configuration for timeout, signal, headers
     * @returns Promise<{ connectionId: string; unsubscribe: () => void }> - Subscription info
     * @throws {Error} When subscription cannot be established
     * @since Issue #437 - Routing Health and Configuration SDK Methods
     *
     * @example
     * ```typescript
     * // Subscribe to routing health events
     * const subscription = await adminClient.configuration.subscribeToRoutingHealthEvents([
     *   'route_health_change',
     *   'circuit_breaker_state_change',
     *   'performance_alert'
     * ]);
     *
     * console.warn(`Subscribed with connection ID: ${subscription.connectionId}`);
     *
     * // Handle events (this would typically use SignalR or WebSocket)
     * // subscription.onEvent((event: RoutingHealthEvent) => {
     * //   console.warn(`Event: ${event.type} - ${event.details.message}`);
     * // });
     *
     * // Unsubscribe when done
     * // subscription.unsubscribe();
     * ```
     */
    subscribeToRoutingHealthEvents(eventTypes?: string[], config?: RequestConfig): Promise<{
        connectionId: string;
        unsubscribe: () => void;
    }>;
    private transformRoutingHealthResponse;
    private transformRouteHealthDetails;
    private transformRoutingHealthHistory;
    private transformRoutePerformanceTestResult;
    private transformCircuitBreakerStatus;
    private generateMockRoutingHealthResponse;
    private generateMockRoutingHealthStatus;
    private generateMockRouteHealthDetails;
    private generateMockRoutingHealthHistory;
    private generateMockRoutePerformanceTestResult;
    private generateMockCircuitBreakerStatus;
    /**
     * Validate routing rule conditions
     */
    validateRoutingRule(rule: CreateRoutingRuleDto): string[];
    /**
     * Calculate optimal cache size based on usage patterns
     */
    calculateOptimalCacheSize(stats: CacheStatsDto): number;
    /**
     * Get load balancer algorithm recommendation
     */
    recommendLoadBalancerAlgorithm(nodes: LoadBalancerHealthDto['nodes']): 'round_robin' | 'weighted_round_robin' | 'least_connections';
    /**
     * Calculate circuit breaker settings based on performance metrics
     */
    calculateCircuitBreakerSettings(metrics: PerformanceTestResult): {
        failureThreshold: number;
        resetTimeoutMs: number;
        halfOpenRequests: number;
    };
    /**
     * Check if feature flag should be enabled for a given context
     */
    evaluateFeatureFlag(flag: FeatureFlag, context: Record<string, unknown>): boolean;
    /**
     * Transform circuit breaker update response to CircuitBreakerStatus
     */
    private transformCircuitBreakerUpdateResponse;
    /**
     * Simple string hash function for consistent bucketing
     */
    private hashString;
    /**
     * Format cache size for display
     */
    formatCacheSize(bytes: number): string;
    /**
     * Generate performance test recommendations
     */
    generatePerformanceRecommendations(result: PerformanceTestResult): string[];
}

/**
 * Real-time monitoring metric
 */
interface MetricDataPoint {
    timestamp: string;
    value: number;
    unit: string;
    tags?: Record<string, string>;
}
/**
 * Metric time series data
 */
interface MetricTimeSeries {
    name: string;
    displayName: string;
    unit: string;
    aggregation: 'avg' | 'sum' | 'max' | 'min' | 'count';
    dataPoints: MetricDataPoint[];
    metadata?: AlertMetadata;
}
/**
 * Real-time metrics parameters
 */
interface MetricsQueryParams {
    metrics: string[];
    startTime?: string;
    endTime?: string;
    interval?: string;
    aggregation?: 'avg' | 'sum' | 'max' | 'min' | 'count';
    groupBy?: string[];
    filters?: Record<string, string>;
}
/**
 * Real-time metrics response
 */
interface MetricsResponse {
    series: MetricTimeSeries[];
    query: MetricsQueryParams;
    executionTimeMs: number;
}
/**
 * Alert severity levels
 */
type AlertSeverity = 'critical' | 'error' | 'warning' | 'info';
/**
 * Alert status
 */
type AlertStatus = 'active' | 'acknowledged' | 'resolved' | 'suppressed';
/**
 * Alert trigger type
 */
type AlertTriggerType = 'threshold' | 'anomaly' | 'pattern' | 'availability';
/**
 * Alert definition
 */
interface AlertDto {
    id: string;
    name: string;
    description?: string;
    severity: AlertSeverity;
    status: AlertStatus;
    metric: string;
    condition: AlertCondition;
    actions: AlertAction[];
    metadata?: AlertMetadata;
    createdAt: string;
    updatedAt: string;
    lastTriggered?: string;
    triggeredCount: number;
    enabled: boolean;
}
/**
 * Alert condition definition
 */
interface AlertCondition {
    type: AlertTriggerType;
    operator: 'gt' | 'gte' | 'lt' | 'lte' | 'eq' | 'neq' | 'contains' | 'not_contains';
    threshold?: number;
    duration?: string;
    evaluationWindow?: string;
    anomalyConfidence?: number;
    pattern?: string;
}
/**
 * Alert action definition
 */
interface AlertAction {
    type: 'email' | 'webhook' | 'slack' | 'teams' | 'pagerduty' | 'log';
    config: {
        recipients?: string[];
        url?: string;
        channel?: string;
        apiKey?: string;
        priority?: string;
        template?: string;
        [key: string]: string | string[] | undefined;
    };
    cooldownMinutes?: number;
}
/**
 * Create alert request
 */
interface CreateAlertDto {
    name: string;
    description?: string;
    severity: AlertSeverity;
    metric: string;
    condition: AlertCondition;
    actions: AlertAction[];
    metadata?: AlertMetadata;
    enabled?: boolean;
}
/**
 * Update alert request
 */
interface UpdateAlertDto {
    name?: string;
    description?: string;
    severity?: AlertSeverity;
    condition?: AlertCondition;
    actions?: AlertAction[];
    metadata?: AlertMetadata;
    enabled?: boolean;
}
/**
 * Alert history entry
 */
interface AlertHistoryEntry {
    alertId: string;
    timestamp: string;
    status: AlertStatus;
    value: number;
    message: string;
    actionsTaken: string[];
    acknowledgedBy?: string;
    resolvedBy?: string;
    notes?: string;
}
/**
 * Dashboard definition
 */
interface DashboardDto {
    id: string;
    name: string;
    description?: string;
    layout: DashboardLayout;
    widgets: DashboardWidget[];
    refreshInterval?: number;
    metadata?: AlertMetadata;
    isPublic: boolean;
    createdBy: string;
    createdAt: string;
    updatedAt: string;
}
/**
 * Dashboard layout configuration
 */
interface DashboardLayout {
    type: 'grid' | 'flex' | 'fixed';
    columns?: number;
    rows?: number;
    breakpoints?: Record<string, number>;
}
/**
 * Dashboard widget definition
 */
interface DashboardWidget {
    id: string;
    type: 'metric' | 'chart' | 'table' | 'gauge' | 'heatmap' | 'logs' | 'alerts';
    title: string;
    position: WidgetPosition;
    config: WidgetConfig;
    dataSource: WidgetDataSource;
}
/**
 * Widget position in dashboard
 */
interface WidgetPosition {
    x: number;
    y: number;
    width: number;
    height: number;
}
/**
 * Widget configuration
 */
interface WidgetConfig {
    chartType?: 'line' | 'bar' | 'area' | 'pie' | 'scatter';
    colors?: string[];
    showLegend?: boolean;
    showGrid?: boolean;
    yAxisRange?: [number, number];
    thresholds?: Array<{
        value: number;
        color: string;
        label?: string;
    }>;
    displayFormat?: string;
    [key: string]: string | number | boolean | string[] | [number, number] | Array<{
        value: number;
        color: string;
        label?: string;
    }> | undefined;
}
/**
 * Widget data source configuration
 */
interface WidgetDataSource {
    metrics?: string[];
    query?: string;
    interval?: string;
    aggregation?: 'avg' | 'sum' | 'max' | 'min' | 'count';
    filters?: Record<string, string>;
}
/**
 * Create dashboard request
 */
interface CreateDashboardDto {
    name: string;
    description?: string;
    layout: DashboardLayout;
    widgets: Omit<DashboardWidget, 'id'>[];
    refreshInterval?: number;
    metadata?: AlertMetadata;
    isPublic?: boolean;
}
/**
 * Update dashboard request
 */
interface UpdateDashboardDto {
    name?: string;
    description?: string;
    layout?: DashboardLayout;
    widgets?: DashboardWidget[];
    refreshInterval?: number;
    metadata?: AlertMetadata;
    isPublic?: boolean;
}
/**
 * System resource metrics
 */
interface SystemResourceMetrics {
    cpu: MonitoringCpuMetrics;
    memory: MonitoringMemoryMetrics;
    disk: DiskMetrics;
    network: NetworkMetrics;
    processes: ProcessMetrics[];
    timestamp: string;
}
/**
 * Extended CPU metrics
 */
interface MonitoringCpuMetrics {
    usage: number;
    userTime: number;
    systemTime: number;
    idleTime: number;
    cores: CpuCoreMetrics[];
}
/**
 * CPU core metrics
 */
interface CpuCoreMetrics {
    coreId: number;
    usage: number;
    frequency: number;
    temperature?: number;
}
/**
 * Extended memory metrics
 */
interface MonitoringMemoryMetrics {
    total: number;
    used: number;
    free: number;
    available: number;
    cached: number;
    buffers: number;
    swapTotal: number;
    swapUsed: number;
    swapFree: number;
}
/**
 * Disk metrics
 */
interface DiskMetrics {
    devices: DiskDeviceMetrics[];
    totalReadBytes: number;
    totalWriteBytes: number;
    readOpsPerSecond: number;
    writeOpsPerSecond: number;
}
/**
 * Disk device metrics
 */
interface DiskDeviceMetrics {
    device: string;
    mountPoint: string;
    totalSpace: number;
    usedSpace: number;
    freeSpace: number;
    usagePercent: number;
    readBytes: number;
    writeBytes: number;
    ioBusy: number;
}
/**
 * Network metrics
 */
interface NetworkMetrics {
    interfaces: NetworkInterfaceMetrics[];
    totalBytesReceived: number;
    totalBytesSent: number;
    packetsReceived: number;
    packetsSent: number;
    errors: number;
    dropped: number;
}
/**
 * Network interface metrics
 */
interface NetworkInterfaceMetrics {
    name: string;
    bytesReceived: number;
    bytesSent: number;
    packetsReceived: number;
    packetsSent: number;
    errors: number;
    dropped: number;
    status: 'up' | 'down';
}
/**
 * Process metrics
 */
interface ProcessMetrics {
    pid: number;
    name: string;
    cpuUsage: number;
    memoryUsage: number;
    threads: number;
    handles: number;
    startTime: string;
}
/**
 * Distributed trace
 */
interface TraceDto {
    traceId: string;
    spans: SpanDto[];
    startTime: string;
    endTime: string;
    duration: number;
    serviceName: string;
    status: 'ok' | 'error' | 'timeout';
    tags: Record<string, string>;
}
/**
 * Trace span
 */
interface SpanDto {
    spanId: string;
    parentSpanId?: string;
    operationName: string;
    serviceName: string;
    startTime: string;
    endTime: string;
    duration: number;
    status: 'ok' | 'error' | 'timeout';
    tags: Record<string, string>;
    logs: SpanLog[];
}
/**
 * Span log entry
 */
interface SpanLog {
    timestamp: string;
    level: 'debug' | 'info' | 'warn' | 'error';
    message: string;
    fields?: {
        [key: string]: string | number | boolean | null;
    };
}
/**
 * Trace query parameters
 */
interface TraceQueryParams {
    service?: string;
    operation?: string;
    minDuration?: number;
    maxDuration?: number;
    status?: 'ok' | 'error' | 'timeout';
    startTime?: string;
    endTime?: string;
    tags?: Record<string, string>;
    limit?: number;
}
/**
 * Log entry
 */
interface LogEntry {
    id: string;
    timestamp: string;
    level: 'debug' | 'info' | 'warn' | 'error' | 'fatal';
    message: string;
    service: string;
    traceId?: string;
    spanId?: string;
    fields: EventData;
    stackTrace?: string;
}
/**
 * Log query parameters
 */
interface LogQueryParams {
    query?: string;
    level?: 'debug' | 'info' | 'warn' | 'error' | 'fatal';
    service?: string;
    startTime?: string;
    endTime?: string;
    traceId?: string;
    fields?: Record<string, string>;
    limit?: number;
    offset?: number;
}
/**
 * Log stream options
 */
interface LogStreamOptions {
    query?: string;
    level?: 'debug' | 'info' | 'warn' | 'error' | 'fatal';
    service?: string;
    follow?: boolean;
    tail?: number;
}
/**
 * Monitoring health status
 */
interface MonitoringHealthStatus {
    healthy: boolean;
    services: ServiceHealthStatus[];
    lastCheck: string;
    message?: string;
}
/**
 * Service health status
 */
interface ServiceHealthStatus {
    name: string;
    status: 'healthy' | 'degraded' | 'unhealthy' | 'unknown';
    lastCheck: string;
    message?: string;
    metrics?: Record<string, number>;
}
/**
 * Metric export parameters
 */
interface MetricExportParams {
    metrics: string[];
    startTime: string;
    endTime: string;
    format: 'csv' | 'json' | 'prometheus';
    aggregation?: 'raw' | 'avg' | 'sum' | 'max' | 'min';
    interval?: string;
}
/**
 * Metric export result
 */
interface MetricExportResult {
    exportId: string;
    status: 'pending' | 'processing' | 'completed' | 'failed';
    format: 'csv' | 'json' | 'prometheus';
    sizeBytes?: number;
    recordCount?: number;
    downloadUrl?: string;
    error?: string;
    createdAt: string;
    completedAt?: string;
}

/**
 * Type-safe Monitoring service using native fetch
 */
declare class FetchMonitoringService {
    private readonly client;
    constructor(client: FetchBaseApiClient);
    /**
     * Query real-time metrics
     */
    queryMetrics(params: MetricsQueryParams, config?: RequestConfig): Promise<MetricsResponse>;
    /**
     * Stream real-time metrics
     */
    streamMetrics(params: MetricsQueryParams, config?: RequestConfig): AsyncGenerator<MetricsResponse, void, unknown>;
    /**
     * Export metrics data
     */
    exportMetrics(params: MetricExportParams, config?: RequestConfig): Promise<MetricExportResult>;
    /**
     * Get metric export status
     */
    getExportStatus(exportId: string, config?: RequestConfig): Promise<MetricExportResult>;
    /**
     * List alerts
     */
    listAlerts(filters?: FilterOptions & {
        severity?: AlertSeverity;
        status?: AlertStatus;
        metric?: string;
    }, config?: RequestConfig): Promise<PagedResponse<AlertDto>>;
    /**
     * Get alert by ID
     */
    getAlert(alertId: string, config?: RequestConfig): Promise<AlertDto>;
    /**
     * Create alert
     */
    createAlert(alert: CreateAlertDto, config?: RequestConfig): Promise<AlertDto>;
    /**
     * Update alert
     */
    updateAlert(alertId: string, alert: UpdateAlertDto, config?: RequestConfig): Promise<AlertDto>;
    /**
     * Delete alert
     */
    deleteAlert(alertId: string, config?: RequestConfig): Promise<void>;
    /**
     * Acknowledge alert
     */
    acknowledgeAlert(alertId: string, notes?: string, config?: RequestConfig): Promise<AlertDto>;
    /**
     * Resolve alert
     */
    resolveAlert(alertId: string, notes?: string, config?: RequestConfig): Promise<AlertDto>;
    /**
     * Get alert history
     */
    getAlertHistory(alertId: string, filters?: FilterOptions, config?: RequestConfig): Promise<PagedResponse<AlertHistoryEntry>>;
    /**
     * List dashboards
     */
    listDashboards(filters?: FilterOptions, config?: RequestConfig): Promise<PagedResponse<DashboardDto>>;
    /**
     * Get dashboard by ID
     */
    getDashboard(dashboardId: string, config?: RequestConfig): Promise<DashboardDto>;
    /**
     * Create dashboard
     */
    createDashboard(dashboard: CreateDashboardDto, config?: RequestConfig): Promise<DashboardDto>;
    /**
     * Update dashboard
     */
    updateDashboard(dashboardId: string, dashboard: UpdateDashboardDto, config?: RequestConfig): Promise<DashboardDto>;
    /**
     * Delete dashboard
     */
    deleteDashboard(dashboardId: string, config?: RequestConfig): Promise<void>;
    /**
     * Clone dashboard
     */
    cloneDashboard(dashboardId: string, name: string, config?: RequestConfig): Promise<DashboardDto>;
    /**
     * Get system resource metrics
     */
    getSystemMetrics(config?: RequestConfig): Promise<SystemResourceMetrics>;
    /**
     * Stream system resource metrics
     */
    streamSystemMetrics(config?: RequestConfig): AsyncGenerator<SystemResourceMetrics, void, unknown>;
    /**
     * Search traces
     */
    searchTraces(params: TraceQueryParams, config?: RequestConfig): Promise<PagedResponse<TraceDto>>;
    /**
     * Get trace by ID
     */
    getTrace(traceId: string, config?: RequestConfig): Promise<TraceDto>;
    /**
     * Search logs
     */
    searchLogs(params: LogQueryParams, config?: RequestConfig): Promise<PagedResponse<LogEntry>>;
    /**
     * Stream logs
     */
    streamLogs(options: LogStreamOptions, config?: RequestConfig): AsyncGenerator<LogEntry, void, unknown>;
    /**
     * Get monitoring health status
     */
    getHealthStatus(config?: RequestConfig): Promise<MonitoringHealthStatus>;
    /**
     * Calculate metric statistics
     */
    calculateMetricStats(series: MetricsResponse['series'][0]): {
        min: number;
        max: number;
        avg: number;
        sum: number;
        count: number;
        stdDev: number;
    };
    /**
     * Format metric value with unit
     */
    formatMetricValue(value: number, unit: string): string;
    /**
     * Format bytes to human readable format
     */
    private formatBytes;
    /**
     * Parse log query into structured format
     */
    parseLogQuery(query: string): LogQueryParams;
    /**
     * Generate alert summary message
     */
    generateAlertSummary(alerts: AlertDto[]): string;
    /**
     * Calculate system health score
     */
    calculateSystemHealthScore(metrics: SystemResourceMetrics): number;
    /**
     * Get recommended alert actions based on severity
     */
    getRecommendedAlertActions(severity: AlertSeverity): AlertAction[];
}

/**
 * Audio configuration models and types for the Conduit Admin API
 */

/**
 * Request for creating or updating an audio provider configuration
 */
interface AudioProviderConfigRequest {
    /** The name of the audio provider */
    name: string;
    /** The base URL for the audio provider API */
    baseUrl: string;
    /** The API key for authentication */
    apiKey: string;
    /** Whether this provider is enabled */
    isEnabled?: boolean;
    /** The supported operation types */
    supportedOperations?: string[];
    /** Additional configuration settings */
    settings?: AudioConfigMetadata;
    /** The priority/weight of this provider */
    priority?: number;
    /** The timeout in seconds for requests to this provider */
    timeoutSeconds?: number;
}
/**
 * Audio provider configuration
 */
interface AudioProviderConfigDto extends AudioProviderConfigRequest {
    /** The unique identifier for the provider configuration */
    id: string;
    /** When the configuration was created */
    createdAt: string;
    /** When the configuration was last updated */
    updatedAt: string;
    /** The last time the provider was tested */
    lastTestedAt?: string;
    /** Whether the last test was successful */
    lastTestSuccessful?: boolean;
    /** The result message from the last test */
    lastTestMessage?: string;
}
/**
 * Request for creating or updating audio cost configuration
 */
interface AudioCostConfigRequest {
    /** The audio provider identifier */
    providerId: string;
    /** The operation type (e.g., "speech-to-text", "text-to-speech") */
    operationType: string;
    /** The model name */
    modelName?: string;
    /** The cost per unit */
    costPerUnit: number;
    /** The unit type (e.g., "minute", "character", "request") */
    unitType: string;
    /** The currency code */
    currency?: string;
    /** Whether this cost configuration is active */
    isActive?: boolean;
    /** When this cost configuration becomes effective */
    effectiveFrom?: string;
    /** When this cost configuration expires */
    effectiveTo?: string;
}
/**
 * Audio cost configuration
 */
interface AudioCostConfigDto extends AudioCostConfigRequest {
    /** The unique identifier for the cost configuration */
    id: string;
    /** When the configuration was created */
    createdAt: string;
    /** When the configuration was last updated */
    updatedAt: string;
}
/**
 * Audio usage information
 */
interface AudioUsageDto {
    /** The unique identifier for the usage entry */
    id: string;
    /** The virtual key that was used */
    virtualKey: string;
    /** The audio provider that was used */
    provider: string;
    /** The operation type */
    operationType: string;
    /** The model that was used */
    model?: string;
    /** The number of units consumed */
    unitsConsumed: number;
    /** The unit type */
    unitType: string;
    /** The cost incurred */
    cost: number;
    /** The currency */
    currency: string;
    /** When the usage occurred */
    timestamp: string;
    /** The duration of the audio processing in seconds */
    durationSeconds?: number;
    /** The size of the audio file in bytes */
    fileSizeBytes?: number;
    /** Additional metadata about the usage */
    metadata?: AudioConfigMetadata;
}
/**
 * Audio usage summary information
 */
interface AudioUsageSummaryDto {
    /** The start date of the summary period */
    startDate: string;
    /** The end date of the summary period */
    endDate: string;
    /** The total number of requests */
    totalRequests: number;
    /** The total cost */
    totalCost: number;
    /** The currency */
    currency: string;
    /** The total duration processed in seconds */
    totalDurationSeconds: number;
    /** The total file size processed in bytes */
    totalFileSizeBytes: number;
    /** Usage breakdown by virtual key */
    usageByKey: AudioKeyUsageDto[];
    /** Usage breakdown by provider */
    usageByProvider: AudioProviderUsageDto[];
    /** Usage breakdown by operation type */
    usageByOperation: AudioOperationUsageDto[];
}
/**
 * Audio usage breakdown by virtual key
 */
interface AudioKeyUsageDto {
    /** The virtual key */
    virtualKey: string;
    /** The number of requests */
    requestCount: number;
    /** The total cost */
    totalCost: number;
    /** The total duration in seconds */
    totalDurationSeconds: number;
}
/**
 * Audio usage breakdown by provider
 */
interface AudioProviderUsageDto {
    /** The provider name */
    provider: string;
    /** The number of requests */
    requestCount: number;
    /** The total cost */
    totalCost: number;
    /** The total duration in seconds */
    totalDurationSeconds: number;
}
/**
 * Audio usage breakdown by operation type
 */
interface AudioOperationUsageDto {
    /** The operation type */
    operationType: string;
    /** The number of requests */
    requestCount: number;
    /** The total cost */
    totalCost: number;
    /** The total duration in seconds */
    totalDurationSeconds: number;
}
/**
 * Real-time audio session
 */
interface RealtimeSessionDto {
    /** The unique session identifier */
    sessionId: string;
    /** The virtual key being used */
    virtualKey: string;
    /** The provider being used */
    provider: string;
    /** The operation type */
    operationType: string;
    /** The model being used */
    model?: string;
    /** When the session started */
    startedAt: string;
    /** The current status of the session */
    status: string;
    /** The current metrics for the session */
    metrics?: RealtimeSessionMetricsDto;
}
/**
 * Real-time session metrics
 */
interface RealtimeSessionMetricsDto {
    /** The duration of the session in seconds */
    durationSeconds: number;
    /** The number of requests processed */
    requestsProcessed: number;
    /** The total cost so far */
    totalCost: number;
    /** The average response time in milliseconds */
    averageResponseTimeMs: number;
    /** The current throughput in requests per minute */
    throughputRpm: number;
}
/**
 * Result of testing an audio provider
 */
interface AudioProviderTestResult {
    /** Whether the test was successful */
    success: boolean;
    /** The test result message */
    message: string;
    /** The response time in milliseconds */
    responseTimeMs?: number;
    /** When the test was performed */
    testedAt: string;
    /** Additional test details */
    details?: {
        capabilities?: string[];
        models?: string[];
        features?: string[];
        [key: string]: string[] | string | undefined;
    };
}
/**
 * Parameters for filtering audio usage data
 */
interface AudioUsageFilters {
    /** Optional start date filter */
    startDate?: string;
    /** Optional end date filter */
    endDate?: string;
    /** Optional virtual key filter */
    virtualKey?: string;
    /** Optional provider filter */
    provider?: string;
    /** Optional operation type filter */
    operationType?: string;
    /** Page number for pagination (1-based) */
    page?: number;
    /** Number of items per page */
    pageSize?: number;
}
/**
 * Parameters for filtering audio usage summary
 */
interface AudioUsageSummaryFilters {
    /** Start date for the summary */
    startDate: string;
    /** End date for the summary */
    endDate: string;
    /** Optional virtual key filter */
    virtualKey?: string;
    /** Optional provider filter */
    provider?: string;
    /** Optional operation type filter */
    operationType?: string;
}
/**
 * Common audio operation types
 */
declare const AudioOperationTypes: {
    /** Speech-to-text operation */
    readonly SPEECH_TO_TEXT: "speech-to-text";
    /** Text-to-speech operation */
    readonly TEXT_TO_SPEECH: "text-to-speech";
    /** Audio transcription operation */
    readonly TRANSCRIPTION: "transcription";
    /** Audio translation operation */
    readonly TRANSLATION: "translation";
};
type AudioOperationType = typeof AudioOperationTypes[keyof typeof AudioOperationTypes];
/**
 * Common audio unit types
 */
declare const AudioUnitTypes: {
    /** Cost per minute of audio */
    readonly MINUTE: "minute";
    /** Cost per second of audio */
    readonly SECOND: "second";
    /** Cost per character processed */
    readonly CHARACTER: "character";
    /** Cost per request */
    readonly REQUEST: "request";
    /** Cost per byte processed */
    readonly BYTE: "byte";
};
type AudioUnitType = typeof AudioUnitTypes[keyof typeof AudioUnitTypes];
/**
 * Common currencies
 */
declare const AudioCurrencies: {
    /** US Dollar */
    readonly USD: "USD";
    /** Euro */
    readonly EUR: "EUR";
    /** British Pound */
    readonly GBP: "GBP";
    /** Japanese Yen */
    readonly JPY: "JPY";
};
type AudioCurrency = typeof AudioCurrencies[keyof typeof AudioCurrencies];
/**
 * Validates an audio provider configuration request
 */
declare function validateAudioProviderRequest(request: AudioProviderConfigRequest): void;
/**
 * Validates an audio cost configuration request
 */
declare function validateAudioCostConfigRequest(request: AudioCostConfigRequest): void;
/**
 * Validates audio usage filters
 */
declare function validateAudioUsageFilters(filters: AudioUsageFilters): void;

/**
 * Service for managing audio provider configurations, cost settings, and usage analytics
 */
declare class AudioConfigurationService {
    private readonly client;
    private static readonly PROVIDERS_ENDPOINT;
    private static readonly COSTS_ENDPOINT;
    private static readonly USAGE_ENDPOINT;
    private static readonly SESSIONS_ENDPOINT;
    constructor(client: FetchBaseApiClient);
    /**
     * Creates a new audio provider configuration
     */
    createProvider(request: AudioProviderConfigRequest): Promise<AudioProviderConfigDto>;
    /**
     * Gets all audio provider configurations
     */
    getProviders(): Promise<AudioProviderConfigDto[]>;
    /**
     * Gets enabled audio providers for a specific operation type
     */
    getEnabledProviders(operationType: string): Promise<AudioProviderConfigDto[]>;
    /**
     * Gets a specific audio provider configuration by ID
     */
    getProvider(providerId: string): Promise<AudioProviderConfigDto>;
    /**
     * Updates an existing audio provider configuration
     */
    updateProvider(providerId: string, request: AudioProviderConfigRequest): Promise<AudioProviderConfigDto>;
    /**
     * Deletes an audio provider configuration
     */
    deleteProvider(providerId: string): Promise<void>;
    /**
     * Tests the connectivity and configuration of an audio provider
     */
    testProvider(providerId: string): Promise<AudioProviderTestResult>;
    /**
     * Creates a new audio cost configuration
     */
    createCostConfig(request: AudioCostConfigRequest): Promise<AudioCostConfigDto>;
    /**
     * Gets all audio cost configurations
     */
    getCostConfigs(): Promise<AudioCostConfigDto[]>;
    /**
     * Gets a specific audio cost configuration by ID
     */
    getCostConfig(configId: string): Promise<AudioCostConfigDto>;
    /**
     * Updates an existing audio cost configuration
     */
    updateCostConfig(configId: string, request: AudioCostConfigRequest): Promise<AudioCostConfigDto>;
    /**
     * Deletes an audio cost configuration
     */
    deleteCostConfig(configId: string): Promise<void>;
    /**
     * Gets audio usage data with optional filtering
     */
    getUsage(filters?: AudioUsageFilters): Promise<PagedResponse<AudioUsageDto>>;
    /**
     * Gets audio usage summary for a date range
     */
    getUsageSummary(filters: AudioUsageSummaryFilters): Promise<AudioUsageSummaryDto>;
    /**
     * Gets all active real-time audio sessions
     */
    getActiveSessions(): Promise<RealtimeSessionDto[]>;
    /**
     * Gets a specific real-time session by ID
     */
    getSession(sessionId: string): Promise<RealtimeSessionDto>;
    /**
     * Terminates an active real-time audio session
     */
    terminateSession(sessionId: string): Promise<{
        success: boolean;
        sessionId: string;
        message?: string;
    }>;
}

type FilterType = 'whitelist' | 'blacklist';
type FilterMode = 'permissive' | 'restrictive';
interface IpFilterDto {
    id: number;
    name: string;
    ipAddressOrCidr: string;
    filterType: FilterType;
    isEnabled: boolean;
    description?: string;
    createdAt: string;
    updatedAt: string;
    lastMatchedAt?: string;
    matchCount?: number;
    expiresAt?: string;
    createdBy?: string;
    lastModifiedBy?: string;
    blockedCount?: number;
}
interface CreateIpFilterDto {
    name: string;
    ipAddressOrCidr: string;
    filterType: FilterType;
    isEnabled?: boolean;
    description?: string;
}
interface UpdateIpFilterDto {
    id: number;
    name?: string;
    ipAddressOrCidr?: string;
    filterType?: FilterType;
    isEnabled?: boolean;
    description?: string;
}
interface IpFilterSettingsDto {
    isEnabled: boolean;
    defaultAllow: boolean;
    bypassForAdminUi: boolean;
    excludedEndpoints: string[];
    filterMode: FilterMode;
    whitelistFilters: IpFilterDto[];
    blacklistFilters: IpFilterDto[];
    maxFiltersPerType?: number;
    ipv6Enabled?: boolean;
}
interface UpdateIpFilterSettingsDto {
    isEnabled?: boolean;
    defaultAllow?: boolean;
    bypassForAdminUi?: boolean;
    excludedEndpoints?: string[];
    filterMode?: FilterMode;
    ipv6Enabled?: boolean;
}
interface IpCheckRequest {
    ipAddress: string;
    endpoint?: string;
}
interface IpCheckResult {
    isAllowed: boolean;
    deniedReason?: string;
    matchedFilter?: string;
    matchedFilterId?: number;
    filterType?: FilterType;
    isDefaultAction?: boolean;
}
interface IpFilterFilters extends FilterOptions {
    filterType?: FilterType;
    isEnabled?: boolean;
    nameContains?: string;
    ipAddressOrCidrContains?: string;
    lastMatchedAfter?: string;
    lastMatchedBefore?: string;
    minMatchCount?: number;
}
interface IpFilterStatistics {
    totalFilters: number;
    enabledFilters: number;
    allowFilters: number;
    denyFilters: number;
    totalMatches: number;
    recentMatches: {
        timestamp: string;
        ipAddress: string;
        filterName: string;
        action: 'allowed' | 'denied';
    }[];
    topMatchedFilters: {
        filterId: number;
        filterName: string;
        matchCount: number;
    }[];
}
interface BulkIpFilterRequest {
    filters: CreateIpFilterDto[];
    replaceExisting?: boolean;
    filterType?: FilterType;
}
interface BulkIpFilterResponse {
    created: IpFilterDto[];
    updated: IpFilterDto[];
    failed: {
        index: number;
        error: string;
        filter: CreateIpFilterDto;
    }[];
}
interface IpFilterValidationResult {
    isValid: boolean;
    errors: string[];
    warnings: string[];
    suggestedCidr?: string;
    overlappingFilters?: {
        id: number;
        name: string;
        ipAddressOrCidr: string;
    }[];
}
interface CreateTemporaryIpFilterDto extends CreateIpFilterDto {
    expiresAt: string;
    reason?: string;
}
interface BulkOperationResult {
    success: number;
    failed: number;
    errors: Array<{
        id: string;
        error: string;
    }>;
}
interface IpFilterImport {
    ipAddress?: string;
    ipRange?: string;
    rule: 'allow' | 'deny';
    description?: string;
    expiresAt?: string;
}
interface IpFilterImportResult {
    imported: number;
    skipped: number;
    failed: number;
    errors: Array<{
        row: number;
        error: string;
    }>;
}
interface BlockedRequestStats {
    totalBlocked: number;
    uniqueIps: number;
    topBlockedIps: Array<{
        ipAddress: string;
        count: number;
        country?: string;
    }>;
    blocksByRule: Array<{
        ruleId: string;
        ruleName: string;
        count: number;
    }>;
    timeline: Array<{
        timestamp: string;
        count: number;
    }>;
}

declare class FetchIpFilterService {
    private readonly client;
    constructor(client: FetchBaseApiClient);
    create(request: CreateIpFilterDto): Promise<IpFilterDto>;
    list(filters?: IpFilterFilters): Promise<IpFilterDto[]>;
    getById(id: number): Promise<IpFilterDto>;
    getEnabled(): Promise<IpFilterDto[]>;
    update(id: number, request: UpdateIpFilterDto): Promise<void>;
    deleteById(id: number): Promise<void>;
    getSettings(): Promise<IpFilterSettingsDto>;
    updateSettings(request: UpdateIpFilterSettingsDto): Promise<void>;
    checkIp(ipAddress: string): Promise<IpCheckResult>;
    search(query: string): Promise<IpFilterDto[]>;
    enableFilter(id: number): Promise<void>;
    disableFilter(id: number): Promise<void>;
    createAllowFilter(name: string, ipAddressOrCidr: string, description?: string): Promise<IpFilterDto>;
    createDenyFilter(name: string, ipAddressOrCidr: string, description?: string): Promise<IpFilterDto>;
    getFiltersByType(filterType: FilterType): Promise<IpFilterDto[]>;
    bulkCreate(rules: CreateIpFilterDto[]): Promise<BulkOperationResult>;
    bulkUpdate(operation: 'enable' | 'disable', ruleIds: string[]): Promise<IpFilterDto[]>;
    bulkDelete(ruleIds: string[]): Promise<BulkOperationResult>;
    createTemporary(rule: CreateTemporaryIpFilterDto): Promise<IpFilterDto>;
    getExpiring(withinHours: number): Promise<IpFilterDto[]>;
    import(rules: IpFilterImport[]): Promise<IpFilterImportResult>;
    export(format: 'json' | 'csv'): Promise<Blob>;
    getBlockedRequestStats(params: {
        startDate?: string;
        endDate?: string;
        groupBy?: 'rule' | 'country' | 'hour';
    }): Promise<BlockedRequestStats>;
    getStatistics(): Promise<IpFilterStatistics>;
    importFilters(_file: File | Blob, _format: 'csv' | 'json'): Promise<BulkIpFilterResponse>;
    exportFilters(_format: 'csv' | 'json', _filterType?: FilterType): Promise<Blob>;
    validateCidr(_cidrRange: string): Promise<IpFilterValidationResult>;
    testRules(_ipAddress: string, _proposedRules?: CreateIpFilterDto[]): Promise<{
        currentResult: IpCheckResult;
        proposedResult?: IpCheckResult;
        changes?: string[];
    }>;
    private invalidateCache;
}

interface ErrorQueueInfo {
    queueName: string;
    originalQueue: string;
    messageCount: number;
    messageBytes: number;
    consumerCount: number;
    oldestMessageTimestamp?: string;
    newestMessageTimestamp?: string;
    messageRate: number;
    status: 'ok' | 'warning' | 'critical';
}
interface ErrorQueueSummary {
    totalQueues: number;
    totalMessages: number;
    totalBytes: number;
    criticalQueues: string[];
    warningQueues: string[];
}
interface ErrorQueueListResponse {
    queues: ErrorQueueInfo[];
    summary: ErrorQueueSummary;
    timestamp: string;
}
interface ErrorMessage {
    messageId: string;
    correlationId: string;
    timestamp: string;
    messageType: string;
    headers: Record<string, unknown>;
    body?: unknown;
    error: ErrorDetails;
    retryCount: number;
}
interface ErrorMessageDetail extends ErrorMessage {
    context: Record<string, unknown>;
    fullException?: string;
}
interface ErrorDetails {
    exceptionType: string;
    message: string;
    stackTrace?: string;
    failedAt: string;
}
interface ErrorMessageListResponse {
    queueName: string;
    messages: ErrorMessage[];
    page: number;
    pageSize: number;
    totalMessages: number;
    totalPages: number;
}
interface ErrorRateTrend {
    period: string;
    errorCount: number;
    errorsPerMinute: number;
}
interface FailingMessageType {
    messageType: string;
    failureCount: number;
    percentage: number;
    mostCommonError: string;
}
interface QueueGrowthPattern {
    queueName: string;
    growthRate: number;
    trend: 'increasing' | 'decreasing' | 'stable';
    currentCount: number;
}
interface ErrorQueueStatistics {
    since: string;
    until: string;
    groupBy: string;
    errorRateTrends: ErrorRateTrend[];
    topFailingMessageTypes: FailingMessageType[];
    queueGrowthPatterns: QueueGrowthPattern[];
    averageMessageAgeHours: number;
    totalErrors: number;
}
interface HealthStatusCounts {
    healthy: number;
    warning: number;
    critical: number;
}
interface HealthIssue {
    severity: 'warning' | 'critical';
    queueName: string;
    description: string;
    suggestedAction?: string;
}
interface ErrorQueueHealth {
    status: 'healthy' | 'degraded' | 'unhealthy';
    timestamp: string;
    statusCounts: HealthStatusCounts;
    issues: HealthIssue[];
    healthScore: number;
}
/**
 * Response from clearing all messages in a queue
 */
interface QueueClearResponse {
    success: boolean;
    message: string;
    deletedCount: number;
}
/**
 * Response from replaying messages
 */
interface MessageReplayResponse {
    success: boolean;
    message: string;
    successCount: number;
    failedCount: number;
}
/**
 * Response from deleting a specific message
 */
interface MessageDeleteResponse {
    success: boolean;
    message: string;
    deletedCount: number;
}
/**
 * Type-safe Error Queue service using native fetch
 */
declare class FetchErrorQueueService {
    private readonly client;
    constructor(client: FetchBaseApiClient);
    /**
     * Get all error queues with optional filters
     */
    getErrorQueues(options?: {
        includeEmpty?: boolean;
        minMessages?: number;
        queueNameFilter?: string;
    }, config?: RequestConfig): Promise<ErrorQueueListResponse>;
    /**
     * Get messages from a specific error queue
     */
    getErrorMessages(queueName: string, options?: {
        page?: number;
        pageSize?: number;
        includeHeaders?: boolean;
        includeBody?: boolean;
    }, config?: RequestConfig): Promise<ErrorMessageListResponse>;
    /**
     * Get details of a specific error message
     */
    getErrorMessage(queueName: string, messageId: string, config?: RequestConfig): Promise<ErrorMessageDetail>;
    /**
     * Get aggregated statistics and trends for error queues
     */
    getStatistics(options?: {
        since?: Date;
        groupBy?: 'hour' | 'day' | 'week';
    }, config?: RequestConfig): Promise<ErrorQueueStatistics>;
    /**
     * Get health status of error queues for monitoring systems
     */
    getHealth(config?: RequestConfig): Promise<ErrorQueueHealth>;
    /**
     * Clear all messages from an error queue
     * @param queueName - Name of the error queue to clear
     * @param config - Optional request configuration
     * @returns Response with the number of deleted messages
     */
    clearQueue(queueName: string, config?: RequestConfig): Promise<QueueClearResponse>;
    /**
     * Replay a specific failed message
     * @param queueName - Name of the error queue
     * @param messageId - ID of the message to replay
     * @param config - Optional request configuration
     * @returns Response with replay operation results
     */
    replayMessage(queueName: string, messageId: string, config?: RequestConfig): Promise<MessageReplayResponse>;
    /**
     * Replay all messages in a queue or specific messages if IDs provided
     * @param queueName - Name of the error queue
     * @param messageIds - Optional array of message IDs to replay. If not provided, all messages are replayed
     * @param config - Optional request configuration
     * @returns Response with replay operation results
     */
    replayAllMessages(queueName: string, messageIds?: string[], config?: RequestConfig): Promise<MessageReplayResponse>;
    /**
     * Delete a specific message from an error queue
     * @param queueName - Name of the error queue
     * @param messageId - ID of the message to delete
     * @param config - Optional request configuration
     * @returns Response with deletion results
     */
    deleteMessage(queueName: string, messageId: string, config?: RequestConfig): Promise<MessageDeleteResponse>;
}

interface CostDashboardDto {
    timeFrame: string;
    startDate: string;
    endDate: string;
    last24HoursCost: number;
    last7DaysCost: number;
    last30DaysCost: number;
    totalCost: number;
    topModelsBySpend: DetailedCostDataDto[];
    topProvidersBySpend: DetailedCostDataDto[];
    topVirtualKeysBySpend: DetailedCostDataDto[];
    costChangePercentage?: number;
}
interface DetailedCostDataDto {
    name: string;
    cost: number;
    percentage: number;
}
interface ModelCostDto$1 {
    model: string;
    provider: string;
    cost: number;
    requestCount: number;
    tokenCount: number;
}
interface ProviderCostDto {
    provider: string;
    cost: number;
    requestCount: number;
    tokenCount: number;
}
interface DailyCostDto {
    date: string;
    cost: number;
    requestCount: number;
    tokenCount: number;
}
interface CostTrendDto {
    period: string;
    startDate: string;
    endDate: string;
    data: CostTrendDataDto[];
}
interface CostTrendDataDto {
    date: string;
    cost: number;
}
interface ModelCostDataDto {
    model: string;
    cost: number;
    totalTokens: number;
    requestCount: number;
    costPerToken: number;
    averageCostPerRequest: number;
}
interface VirtualKeyCostDataDto {
    virtualKeyId: number;
    keyName: string;
    cost: number;
    requestCount: number;
    averageCostPerRequest: number;
    budgetUsed?: number;
    budgetRemaining?: number;
}
/**
 * Type-safe Cost Dashboard service using native fetch
 * Provides access to actual /api/costs endpoints
 */
declare class FetchCostDashboardService {
    private readonly client;
    constructor(client: FetchBaseApiClient);
    /**
     * Get cost dashboard summary data
     * @param timeframe - The timeframe for the summary (daily, weekly, monthly)
     * @param startDate - Optional start date
     * @param endDate - Optional end date
     */
    getCostSummary(timeframe?: 'daily' | 'weekly' | 'monthly', startDate?: string, endDate?: string, config?: RequestConfig): Promise<CostDashboardDto>;
    /**
     * Get cost trend data
     * @param period - The period for the trend (daily, weekly, monthly)
     * @param startDate - Optional start date
     * @param endDate - Optional end date
     */
    getCostTrends(period?: 'daily' | 'weekly' | 'monthly', startDate?: string, endDate?: string, config?: RequestConfig): Promise<CostTrendDto>;
    /**
     * Get model costs data
     * @param startDate - Optional start date
     * @param endDate - Optional end date
     */
    getModelCosts(startDate?: string, endDate?: string, config?: RequestConfig): Promise<ModelCostDataDto[]>;
    /**
     * Get virtual key costs data
     * @param startDate - Optional start date
     * @param endDate - Optional end date
     */
    getVirtualKeyCosts(startDate?: string, endDate?: string, config?: RequestConfig): Promise<VirtualKeyCostDataDto[]>;
    /**
     * Helper method to format date range
     */
    formatDateRange(days: number): {
        startDate: string;
        endDate: string;
    };
    /**
     * Helper method to calculate growth rate
     */
    calculateGrowthRate(current: number, previous: number): number;
}

interface ModelCost {
    id: number;
    modelIdPattern: string;
    providerName: string;
    modelType: 'chat' | 'embedding' | 'image' | 'audio' | 'video';
    inputCostPerMillionTokens?: number;
    outputCostPerMillionTokens?: number;
    costPerRequest?: number;
    costPerSecond?: number;
    costPerImage?: number;
    isActive: boolean;
    priority: number;
    effectiveDate: string;
    expiryDate?: string;
    metadata?: ModelConfigMetadata;
    createdAt: string;
    updatedAt: string;
    batchProcessingMultiplier?: number;
    supportsBatchProcessing: boolean;
    imageQualityMultipliers?: string;
    cachedInputTokenCost?: number;
    cachedInputWriteCost?: number;
    costPerSearchUnit?: number;
    costPerInferenceStep?: number;
    defaultInferenceSteps?: number;
}
interface ModelCostDto {
    id: number;
    modelId: string;
    inputTokenCost: number;
    outputTokenCost: number;
    currency: string;
    effectiveDate: string;
    expiryDate?: string;
    providerId?: string;
    description?: string;
    isActive: boolean;
    createdAt: string;
    updatedAt: string;
    batchProcessingMultiplier?: number;
    supportsBatchProcessing: boolean;
    imageQualityMultipliers?: string;
    cachedInputTokenCost?: number;
    cachedInputWriteCost?: number;
    costPerSearchUnit?: number;
    costPerInferenceStep?: number;
    defaultInferenceSteps?: number;
}
interface CreateModelCostDto {
    modelId: string;
    inputTokenCost: number;
    outputTokenCost: number;
    currency?: string;
    effectiveDate?: string;
    expiryDate?: string;
    providerId?: string;
    description?: string;
    isActive?: boolean;
    batchProcessingMultiplier?: number;
    supportsBatchProcessing?: boolean;
    imageQualityMultipliers?: string;
    cachedInputTokenCost?: number;
    cachedInputWriteCost?: number;
    costPerSearchUnit?: number;
    costPerInferenceStep?: number;
    defaultInferenceSteps?: number;
}
interface UpdateModelCostDto {
    inputTokenCost?: number;
    outputTokenCost?: number;
    currency?: string;
    effectiveDate?: string;
    expiryDate?: string;
    providerId?: string;
    description?: string;
    isActive?: boolean;
    batchProcessingMultiplier?: number;
    supportsBatchProcessing?: boolean;
    imageQualityMultipliers?: string;
    cachedInputTokenCost?: number;
    cachedInputWriteCost?: number;
    costPerSearchUnit?: number;
    costPerInferenceStep?: number;
    defaultInferenceSteps?: number;
}
interface ModelCostFilters extends FilterOptions {
    modelId?: string;
    providerId?: string;
    currency?: string;
    isActive?: boolean;
    effectiveAfter?: string;
    effectiveBefore?: string;
    minInputCost?: number;
    maxInputCost?: number;
    minOutputCost?: number;
    maxOutputCost?: number;
}
interface ModelCostCalculation {
    modelId: string;
    inputTokens: number;
    outputTokens: number;
    inputCost: number;
    outputCost: number;
    totalCost: number;
    currency: string;
    costPerThousandInputTokens: number;
    costPerThousandOutputTokens: number;
}
interface BulkModelCostUpdate {
    modelIds: string[];
    adjustment: {
        type: 'percentage' | 'fixed';
        value: number;
        applyTo: 'input' | 'output' | 'both';
    };
    effectiveDate?: string;
    reason?: string;
}
interface ModelCostHistory {
    modelId: string;
    history: {
        id: number;
        inputTokenCost: number;
        outputTokenCost: number;
        effectiveDate: string;
        expiryDate?: string;
        changeReason?: string;
    }[];
}
interface CostEstimate {
    scenarios: {
        name: string;
        inputTokens: number;
        outputTokens: number;
    }[];
    models: string[];
    results: {
        scenario: string;
        costs: {
            modelId: string;
            totalCost: number;
            inputCost: number;
            outputCost: number;
            currency: string;
        }[];
    }[];
    recommendations?: {
        mostCostEffective: string;
        bestValueForMoney: string;
        notes: string[];
    };
}
interface ModelCostComparison {
    baseModel: string;
    comparisonModels: string[];
    inputTokens: number;
    outputTokens: number;
    results: {
        modelId: string;
        totalCost: number;
        costDifference: number;
        percentageDifference: number;
        currency: string;
    }[];
}
interface ModelCostOverview {
    modelName: string;
    providerName: string;
    modelType: string;
    totalRequests: number;
    totalTokens: number;
    totalCost: number;
    averageCostPerRequest: number;
    costTrend: 'increasing' | 'decreasing' | 'stable';
    trendPercentage: number;
}
interface CostTrend {
    date: string;
    cost: number;
    requests: number;
    tokens: number;
}
interface ImportResult {
    success: number;
    failed: number;
    errors: Array<{
        row: number;
        error: string;
    }>;
}

interface ModelCostListParams {
    page?: number;
    pageSize?: number;
    provider?: string;
    isActive?: boolean;
}
interface ModelCostOverviewParams {
    startDate?: string;
    endDate?: string;
    groupBy?: 'provider' | 'model';
}
interface BulkUpdateRequest {
    updates: Array<{
        id: number;
        changes: Partial<UpdateModelCostDto>;
    }>;
}
interface CreateModelCostDtoBackend {
    modelIdPattern: string;
    inputTokenCost: number;
    outputTokenCost: number;
    embeddingTokenCost?: number;
    imageCostPerImage?: number;
    audioCostPerMinute?: number;
    audioCostPerKCharacters?: number;
    audioInputCostPerMinute?: number;
    audioOutputCostPerMinute?: number;
    videoCostPerSecond?: number;
    videoResolutionMultipliers?: string;
    description?: string;
    priority?: number;
}
/**
 * Type-safe Model Cost service using native fetch
 */
declare class FetchModelCostService {
    private readonly client;
    constructor(client: FetchBaseApiClient);
    /**
     * Get all model costs with optional pagination and filtering
     */
    list(params?: ModelCostListParams, config?: RequestConfig): Promise<PagedResult<ModelCost>>;
    /**
     * Get a specific model cost by ID
     */
    getById(id: number, config?: RequestConfig): Promise<ModelCost>;
    /**
     * Get model costs by provider name
     */
    getByProvider(providerName: string, config?: RequestConfig): Promise<ModelCost[]>;
    /**
     * Get model cost by pattern
     */
    getByPattern(pattern: string, config?: RequestConfig): Promise<ModelCost | null>;
    /**
     * Create a new model cost configuration
     */
    create(data: CreateModelCostDto | CreateModelCostDtoBackend, config?: RequestConfig): Promise<ModelCost>;
    /**
     * Update an existing model cost configuration
     */
    update(id: number, data: UpdateModelCostDto, config?: RequestConfig): Promise<ModelCost>;
    /**
     * Delete a model cost configuration
     */
    deleteById(id: number, config?: RequestConfig): Promise<void>;
    /**
     * Import multiple model costs at once
     */
    import(modelCosts: (CreateModelCostDto | CreateModelCostDtoBackend)[], config?: RequestConfig): Promise<ImportResult>;
    /**
     * Bulk update multiple model costs
     */
    bulkUpdate(updates: BulkUpdateRequest['updates'], config?: RequestConfig): Promise<ModelCost[]>;
    /**
     * Get model cost overview with aggregation
     */
    getOverview(params?: ModelCostOverviewParams, config?: RequestConfig): Promise<ModelCostOverview[]>;
    /**
     * Helper method to check if a model matches a pattern
     */
    doesModelMatchPattern(modelId: string, pattern: string): boolean;
    /**
     * Helper method to find the best matching cost for a model
     */
    findBestMatch(modelId: string, costs: ModelCost[]): Promise<ModelCost | null>;
    /**
     * Helper method to calculate cost for given token usage
     */
    calculateTokenCost(cost: ModelCost, inputTokens: number, outputTokens: number): {
        inputCost: number;
        outputCost: number;
        totalCost: number;
    };
    /**
     * Helper method to get cost type from model ID
     */
    getCostType(modelId: string): 'text' | 'embedding' | 'image' | 'audio' | 'video';
}

/**
 * Type-safe Conduit Admin Client using native fetch
 *
 * Provides full type safety for all admin operations without HTTP complexity
 *
 * @example
 * ```typescript
 * const client = new FetchConduitAdminClient({
 *   baseUrl: 'https://admin.conduit.ai',
 *   masterKey: 'your-master-key'
 * });
 *
 * // All operations are fully typed
 * const keys = await client.virtualKeys.list();
 * const metrics = await client.dashboard.getMetrics();
 * ```
 */
declare class FetchConduitAdminClient extends FetchBaseApiClient {
    readonly virtualKeys: FetchVirtualKeyService;
    readonly dashboard: FetchDashboardService;
    readonly providers: FetchProvidersService;
    readonly system: FetchSystemService;
    readonly modelMappings: FetchModelMappingsService;
    readonly providerModels: FetchProviderModelsService;
    readonly settings: FetchSettingsService;
    readonly analytics: FetchAnalyticsService;
    readonly providerHealth: FetchProviderHealthService;
    readonly security: FetchSecurityService;
    readonly configuration: FetchConfigurationService;
    readonly monitoring: FetchMonitoringService;
    readonly audio: AudioConfigurationService;
    readonly ipFilters: FetchIpFilterService;
    readonly errorQueues: FetchErrorQueueService;
    readonly costDashboard: FetchCostDashboardService;
    readonly modelCosts: FetchModelCostService;
    constructor(config: ApiClientConfig);
    /**
     * Type guard for checking if an error is a ConduitError
     */
    isConduitError(error: unknown): error is ConduitError;
    /**
     * Type guard for checking if an error is an authentication error
     */
    isAuthError(error: unknown): error is ConduitError;
    /**
     * Type guard for checking if an error is a rate limit error
     */
    isRateLimitError(error: unknown): error is ConduitError;
    /**
     * Type guard for checking if an error is a validation error
     */
    isValidationError(error: unknown): error is ConduitError;
    /**
     * Type guard for checking if an error is a not found error
     */
    isNotFoundError(error: unknown): error is ConduitError;
    /**
     * Type guard for checking if an error is a server error
     */
    isServerError(error: unknown): error is ConduitError;
}

export { type IpFilterStatistics as $, type AudioConfigurationDto as A, type BulkMappingRequest as B, type ConfigValue as C, type DiscoveredModel as D, type ExportDestinationConfig as E, FetchBaseApiClient as F, type GlobalSettingDto as G, type SystemConfiguration as H, type CreateIpFilterDto as I, type IpFilterDto as J, type IpFilterFilters as K, type UpdateIpFilterDto as L, type ModelProviderMappingDto as M, type IpFilterSettingsDto as N, type UpdateIpFilterSettingsDto as O, type ProviderCredentialDto as P, type IpCheckResult as Q, type RouterConfigurationDto as R, type SettingFilters as S, type FilterType as T, type UpdateProviderCredentialDto as U, type VirtualKeyMetadata as V, type BulkOperationResult as W, type CreateTemporaryIpFilterDto as X, type IpFilterImport as Y, type IpFilterImportResult as Z, type BlockedRequestStats as _, type ExtendedMetadata as a, type CachingConfiguration as a$, type BulkIpFilterResponse as a0, type IpFilterValidationResult as a1, type CreateModelCostDto as a2, type ModelCost as a3, type PagedResult as a4, type ModelCostFilters as a5, type ModelCostDto as a6, type UpdateModelCostDto as a7, type ModelCostCalculation as a8, type ImportResult as a9, type AuditLogFilters as aA, type AuditLogDto as aB, type DiagnosticChecks as aC, type FeatureAvailability as aD, type ApiClientConfig as aE, type CreateProviderHealthConfigurationDto as aF, type ProviderHealthStatisticsDto as aG, type ProviderStatus as aH, type UpdateNotificationDto as aI, NotificationType as aJ, NotificationSeverity as aK, type NotificationStatistics as aL, type NotificationBulkResponse as aM, type NotificationFilters as aN, type NotificationSummary as aO, type SecurityEventFilters as aP, type SecurityEvent as aQ, type CreateSecurityEventDto as aR, type ThreatFilters as aS, type ThreatDetection as aT, type ThreatAction as aU, type ThreatAnalytics as aV, type ComplianceMetrics as aW, type RoutingConfiguration as aX, type UpdateRoutingConfigDto as aY, type TestResult as aZ, type LoadBalancerHealth as a_, type ModelCostOverview as aa, type CostTrend as ab, type BulkModelCostUpdate as ac, type ModelCostHistory as ad, type CostEstimate as ae, type ModelCostComparison as af, type RequestLogFilters as ag, type RequestLogDto as ah, type UsageMetricsDto as ai, type ModelUsageDto as aj, type KeyUsageDto as ak, type AnalyticsFilters as al, type CostForecastDto as am, type AnomalyDto as an, type AnalyticsOptions as ao, type SystemInfoDto as ap, type HealthStatusDto as aq, type BackupDto as ar, type CreateBackupRequest as as, type RestoreBackupRequest as at, type BackupRestoreResult as au, type NotificationDto as av, type CreateNotificationDto as aw, type MaintenanceTaskDto as ax, type RunMaintenanceTaskRequest as ay, type MaintenanceTaskResult as az, type CreateProviderCredentialDto as b, type SettingsListResponseDto as b$, type UpdateCachingConfigDto as b0, type CachePolicy as b1, type CreateCachePolicyDto as b2, type UpdateCachePolicyDto as b3, type CacheRegion as b4, type ClearCacheResult as b5, type CacheStatistics as b6, type RoutingConfigDto as b7, type UpdateRoutingConfigDto$1 as b8, type RoutingRule$1 as b9, type CacheCondition as bA, type CacheClearParams as bB, type CacheClearResult as bC, type CacheStatsDto as bD, type CacheKeyStats as bE, type LoadBalancerConfigDto as bF, type UpdateLoadBalancerConfigDto as bG, type LoadBalancerNode as bH, type PerformanceConfigDto as bI, type UpdatePerformanceConfigDto as bJ, type PerformanceTestParams as bK, type PerformanceTestResult as bL, type PerformanceDataPoint as bM, type ErrorSummary as bN, type FeatureFlag as bO, type FeatureFlagCondition as bP, type UpdateFeatureFlagDto as bQ, type ModelProviderInfo as bR, type ModelCapabilities$1 as bS, FetchVirtualKeyService as bT, FetchProvidersService as bU, FetchSystemService as bV, FetchModelMappingsService as bW, FetchProviderModelsService as bX, FetchSettingsService as bY, type SettingUpdate as bZ, type SettingsDto as b_, type CreateRoutingRuleDto as ba, type UpdateRoutingRuleDto as bb, type LoadBalancerHealthDto as bc, FetchConduitAdminClient as bd, type IpWhitelistDto as be, type IpEntry as bf, type SecurityEventParams as bg, type SecurityEventType as bh, type SecurityEventExtended as bi, type SecurityEventPage as bj, type ThreatSummaryDto as bk, type ThreatCategory as bl, type ActiveThreat as bm, type AccessPolicy as bn, type PolicyRule as bo, type CreateAccessPolicyDto as bp, type UpdateAccessPolicyDto as bq, type AuditLogParams as br, type AuditLog as bs, type AuditLogPage as bt, type RetryPolicy as bu, type RuleCondition as bv, type RuleAction as bw, type CacheConfigDto as bx, type UpdateCacheConfigDto as by, type CacheRule as bz, type ProviderFilters as c, type HistoryParams as c$, FetchAnalyticsService as c0, FetchProviderHealthService as c1, FetchSecurityService as c2, FetchConfigurationService as c3, FetchMonitoringService as c4, FetchIpFilterService as c5, FetchErrorQueueService as c6, FetchCostDashboardService as c7, FetchModelCostService as c8, type CostDashboardDto as c9, type paths as cA, type Logger as cB, type CacheProvider as cC, HttpError as cD, type SignalRConfig as cE, type RequestConfigInfo as cF, type RetryConfig as cG, type ConduitConfig as cH, type RequestConfig as cI, type ResponseInfo as cJ, StatusType as cK, type ModelDto as cL, type ModelDetailsDto as cM, type ModelExample as cN, type ModelSearchFilters as cO, type ModelSearchResult as cP, type ModelListResponseDto as cQ, type RefreshModelsRequest as cR, type RefreshModelsResponse as cS, type HealthSummaryDto as cT, type ProviderHealthSummary as cU, type ProviderHealthDto as cV, type HealthCheck as cW, type UptimeMetric as cX, type LatencyMetric as cY, type ThroughputMetric as cZ, type ErrorMetric as c_, type ModelCostDto$1 as ca, type ProviderCostDto as cb, type DailyCostDto as cc, type CostTrendDto as cd, type ModelCostDataDto as ce, type VirtualKeyCostDataDto as cf, type ErrorQueueInfo as cg, type ErrorQueueSummary as ch, type ErrorQueueListResponse as ci, type ErrorMessage as cj, type ErrorMessageDetail as ck, type ErrorDetails as cl, type ErrorMessageListResponse as cm, type ErrorRateTrend as cn, type FailingMessageType as co, type QueueGrowthPattern as cp, type ErrorQueueStatistics as cq, type HealthStatusCounts as cr, type HealthIssue as cs, type ErrorQueueHealth as ct, type QueueClearResponse as cu, type MessageReplayResponse as cv, type MessageDeleteResponse as cw, AudioConfigurationService as cx, type components as cy, type operations as cz, type ProviderConnectionTestResultDto as d, type SystemMetricsDto as d$, type HealthHistory as d0, type HealthDataPoint as d1, type AlertParams as d2, type HealthAlert as d3, type ConnectionTestResult as d4, type PerformanceParams as d5, type PerformanceMetrics$1 as d6, type ErrorTypeCount as d7, type Incident as d8, type MaintenanceWindow as d9, type TimeSeriesData as dA, type VirtualKeyParams as dB, type VirtualKeyAnalytics as dC, type VirtualKeyUsageSummary as dD, type VirtualKeyRanking as dE, type TrendData as dF, type CapabilityUsage as dG, type ModelPerformanceMetrics as dH, type ExportParams$1 as dI, type ExportResult$1 as dJ, type TimeSeriesDataPoint as dK, type ProviderUsageBreakdown as dL, type ModelUsageBreakdown as dM, type VirtualKeyUsageBreakdown as dN, type EndpointUsageBreakdown as dO, type RequestLogStatisticsParams as dP, type ServiceHealthMetrics as dQ, type QueueMetrics as dR, type DatabaseMetrics as dS, type SystemAlert as dT, type ProviderHealthDetails as dU, type EndpointHealth as dV, type HealthHistoryPoint as dW, type ProviderIncident as dX, type VirtualKeyDetail as dY, type QuotaAlert as dZ, type SystemHealthDto as d_, type HealthAlertListResponseDto as da, type ProviderHealthListResponseDto as db, type ProviderHealthStatusResponse as dc, type ProviderHealthItem as dd, type ProviderWithHealthDto as de, type ProviderHealthMetricsDto as df, type ProviderEndpointHealth as dg, type ProviderModelHealth as dh, type ProviderHealthIncident as di, type ProviderHealthHistoryOptions as dj, type ProviderHealthHistoryResponse as dk, type ProviderHealthDataPoint as dl, type SettingCategory as dm, type RouterCondition as dn, type RouterAction as dp, type FilterMode as dq, type IpCheckRequest as dr, type BulkIpFilterRequest as ds, type RequestLogParams as dt, type RequestLogPage as du, type UsageParams as dv, type UsageAnalytics as dw, type ProviderUsage as dx, type VirtualKeyUsage as dy, type ModelUsage as dz, type ProviderConnectionTestRequest as e, type LogQueryParams as e$, type ServiceStatusDto as e0, type HealthEventDto as e1, type HealthEventsResponseDto as e2, type HealthEventSubscriptionOptions as e3, type HealthEventSubscription as e4, type AudioProviderConfigRequest as e5, type AudioProviderConfigDto as e6, type AudioCostConfigRequest as e7, type AudioCostConfigDto as e8, type AudioUsageDto as e9, type AlertCondition as eA, type AlertAction as eB, type CreateAlertDto as eC, type UpdateAlertDto as eD, type AlertHistoryEntry as eE, type DashboardDto as eF, type DashboardLayout as eG, type DashboardWidget as eH, type WidgetPosition as eI, type WidgetConfig as eJ, type WidgetDataSource as eK, type CreateDashboardDto as eL, type UpdateDashboardDto as eM, type SystemResourceMetrics as eN, type MonitoringCpuMetrics as eO, type CpuCoreMetrics as eP, type MonitoringMemoryMetrics as eQ, type DiskMetrics as eR, type DiskDeviceMetrics as eS, type NetworkMetrics as eT, type NetworkInterfaceMetrics as eU, type ProcessMetrics as eV, type TraceDto as eW, type SpanDto as eX, type SpanLog as eY, type TraceQueryParams as eZ, type LogEntry as e_, type AudioUsageSummaryDto as ea, type AudioKeyUsageDto as eb, type AudioProviderUsageDto as ec, type AudioOperationUsageDto as ed, type RealtimeSessionDto as ee, type RealtimeSessionMetricsDto as ef, type AudioProviderTestResult as eg, type AudioUsageFilters as eh, type AudioUsageSummaryFilters as ei, AudioOperationTypes as ej, type AudioOperationType as ek, AudioUnitTypes as el, type AudioUnitType as em, AudioCurrencies as en, type AudioCurrency as eo, validateAudioProviderRequest as ep, validateAudioCostConfigRequest as eq, validateAudioUsageFilters as er, type MetricDataPoint as es, type MetricTimeSeries as et, type MetricsQueryParams as eu, type MetricsResponse as ev, type AlertSeverity as ew, type AlertStatus as ex, type AlertTriggerType as ey, type AlertDto as ez, type ProviderHealthConfigurationDto as f, type LogStreamOptions as f0, type MonitoringHealthStatus as f1, type ServiceHealthStatus as f2, type MetricExportParams as f3, type MetricExportResult as f4, type ExportFormat as f5, type RoutingRule as f6, type ProviderPriority as f7, type CacheNode as f8, type RegionStatistics as f9, type CustomSettings as fA, type ValidationFunction as fB, type EventData as fC, type RouterActionParameters as fD, type MaintenanceTaskConfig as fE, type MaintenanceTaskResultData as fF, type MetricDimensions as fG, type AdditionalProviderInfo as fH, type EndpointStatistics as fa, type StatisticPoint as fb, type BaseMetadata as fc, type ProviderConfigMetadata as fd, type AnalyticsMetadata as fe, type AlertMetadata as ff, type SecurityEventMetadata as fg, type ExportConfigMetadata as fh, type ModelConfigMetadata as fi, type AudioConfigMetadata as fj, type VideoGenerationMetadata as fk, isValidMetadata as fl, parseMetadata as fm, stringifyMetadata as fn, type FeatureFlagContext as fo, type ProviderSettings as fp, type AudioProviderSettings as fq, type ModelQueryParams as fr, type DiagnosticResult as fs, type SessionMetadata as ft, type MonitoringFields as fu, type HealthCheckDetails as fv, type SecurityEventDetails as fw, type SecurityChangeRecord as fx, type SystemParameters as fy, type Metadata as fz, type UpdateProviderHealthConfigurationDto as g, type ProviderHealthSummaryDto as h, type ProviderHealthStatusDto as i, type ProviderHealthFilters as j, type ProviderHealthRecordDto as k, type ProviderUsageStatistics as l, type ProviderDataDto as m, type CreateModelProviderMappingDto as n, type ModelMappingFilters as o, type UpdateModelProviderMappingDto as p, type BulkMappingResponse as q, type CapabilityTestResult as r, type ModelRoutingInfo as s, type ModelMappingSuggestion as t, type CreateGlobalSettingDto as u, type UpdateGlobalSettingDto as v, type CreateAudioConfigurationDto as w, type UpdateAudioConfigurationDto as x, type UpdateRouterConfigurationDto as y, type RouterRule as z };
