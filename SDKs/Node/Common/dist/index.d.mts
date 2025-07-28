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
 * Strongly-typed enumeration of supported LLM providers.
 * These numeric values must match the C# ProviderType enum exactly.
 * @see https://github.com/knnlabs/Conduit/blob/main/ConduitLLM.Core/Enums/ProviderType.cs
 */
declare enum ProviderType {
    /** OpenAI provider (GPT models) */
    OpenAI = 1,
    /** Anthropic provider (Claude models) */
    Anthropic = 2,
    /** Azure OpenAI Service */
    AzureOpenAI = 3,
    /** Google Gemini */
    Gemini = 4,
    /** Google Vertex AI */
    VertexAI = 5,
    /** Cohere */
    Cohere = 6,
    /** Mistral AI */
    Mistral = 7,
    /** Groq */
    Groq = 8,
    /** Ollama (local models) */
    Ollama = 9,
    /** Replicate */
    Replicate = 10,
    /** Fireworks AI */
    Fireworks = 11,
    /** AWS Bedrock */
    Bedrock = 12,
    /** Hugging Face */
    HuggingFace = 13,
    /** AWS SageMaker */
    SageMaker = 14,
    /** OpenRouter */
    OpenRouter = 15,
    /** OpenAI-compatible generic provider */
    OpenAICompatible = 16,
    /** MiniMax */
    MiniMax = 17,
    /** Ultravox */
    Ultravox = 18,
    /** ElevenLabs (audio) */
    ElevenLabs = 19,
    /** Google Cloud (audio) */
    GoogleCloud = 20,
    /** Cerebras (high-performance inference) */
    Cerebras = 21
}
/**
 * Type guard to check if a value is a valid ProviderType
 */
declare function isProviderType(value: unknown): value is ProviderType;
/**
 * Get the display name for a provider type
 */
declare function getProviderDisplayName(provider: ProviderType): string;

/**
 * Base model interface used by both Core and Admin SDKs
 */
interface BaseModel {
    /** Unique identifier for the model */
    id: string;
    /** Human-readable name of the model */
    name: string;
    /** Provider that owns this model */
    providerId: string;
    /** Type of provider */
    providerType: ProviderType;
}
/**
 * Model feature support interface
 */
interface ModelFeatureSupport {
    /** Whether the model supports vision/image inputs */
    supportsVision: boolean;
    /** Whether the model supports image generation */
    supportsImageGeneration: boolean;
    /** Whether the model supports audio transcription */
    supportsAudioTranscription: boolean;
    /** Whether the model supports text-to-speech */
    supportsTextToSpeech: boolean;
    /** Whether the model supports realtime audio */
    supportsRealtimeAudio: boolean;
    /** Whether the model supports function calling */
    supportsFunctionCalling: boolean;
    /** Maximum tokens the model supports */
    maxTokens?: number;
    /** Context window size */
    contextWindow?: number;
}
/**
 * Extended model information with capabilities
 */
interface ModelWithCapabilities extends BaseModel {
    /** Model capabilities */
    capabilities: ModelFeatureSupport;
    /** Whether the model is enabled */
    isEnabled: boolean;
    /** Creation timestamp */
    createdAt: string;
    /** Last update timestamp */
    updatedAt?: string;
}
/**
 * Model usage statistics
 */
interface ModelUsageStats {
    /** Model ID */
    modelId: string;
    /** Total requests made */
    totalRequests: number;
    /** Total tokens consumed */
    totalTokens: number;
    /** Total cost */
    totalCost: number;
    /** Average response time in ms */
    averageResponseTime: number;
    /** Success rate (0-1) */
    successRate: number;
    /** Time period for these stats */
    period: {
        start: string;
        end: string;
    };
}
/**
 * Model pricing information
 */
interface ModelPricing {
    /** Model ID */
    modelId: string;
    /** Provider type */
    providerType: ProviderType;
    /** Input token cost (per 1K tokens) */
    inputCostPer1K: number;
    /** Output token cost (per 1K tokens) */
    outputCostPer1K: number;
    /** Currency (USD, EUR, etc.) */
    currency: string;
    /** Effective date for this pricing */
    effectiveDate: string;
}
/**
 * Model health/availability status
 */
interface ModelHealthStatus {
    /** Model ID */
    modelId: string;
    /** Whether the model is currently available */
    isAvailable: boolean;
    /** Last successful check timestamp */
    lastChecked: string;
    /** Average response time in last check */
    responseTime?: number;
    /** Error message if not available */
    errorMessage?: string;
    /** Number of consecutive failures */
    consecutiveFailures: number;
}
/**
 * Type guard to check if object has model capabilities
 */
declare function hasModelFeatureSupport(obj: unknown): obj is {
    capabilities: ModelFeatureSupport;
};
/**
 * Type guard to check if object is a BaseModel
 */
declare function isBaseModel(obj: unknown): obj is BaseModel;
/**
 * Discovered model from provider
 */
interface DiscoveredModel {
    /** Model ID */
    id: string;
    /** Provider type */
    provider: ProviderType;
    /** Display name */
    display_name?: string;
    /** Model description */
    description?: string;
    /** Model capabilities */
    capabilities?: Record<string, boolean | number | string[]>;
    /** Additional metadata */
    metadata?: Record<string, unknown>;
}
/**
 * Model mapping between Conduit and provider models
 */
interface ModelMapping {
    /** Unique identifier */
    id: number;
    /** Conduit model ID */
    modelId: string;
    /** Provider ID */
    providerId: string;
    /** Provider type */
    providerType: ProviderType;
    /** Provider's model ID */
    providerModelId: string;
    /** Whether mapping is enabled */
    isEnabled: boolean;
    /** Priority for routing */
    priority: number;
    /** Feature support */
    features: ModelFeatureSupport;
    /** Creation timestamp */
    createdAt: string;
    /** Update timestamp */
    updatedAt?: string;
    /** Additional metadata */
    metadata?: Record<string, unknown>;
}
/**
 * Model cost information
 */
interface ModelCostInfo {
    /** Model pattern (exact match or prefix with *) */
    modelIdPattern: string;
    /** Cost per million input tokens */
    inputCostPerMillionTokens: number;
    /** Cost per million output tokens */
    outputCostPerMillionTokens: number;
    /** Cost per embedding token (if applicable) */
    embeddingTokenCost?: number;
    /** Cost per generated image */
    imageCostPerImage?: number;
    /** Cost per minute of audio */
    audioCostPerMinute?: number;
    /** Cost per thousand characters of audio */
    audioCostPerKCharacters?: number;
    /** Cost per minute of audio input */
    audioInputCostPerMinute?: number;
    /** Cost per minute of audio output */
    audioOutputCostPerMinute?: number;
    /** Cost per second of video */
    videoCostPerSecond?: number;
    /** Resolution multipliers for video */
    videoResolutionMultipliers?: Record<string, number>;
    /** Description */
    description?: string;
    /** Priority for pattern matching */
    priority?: number;
}

/**
 * Common API request and response types shared between Core and Admin SDKs
 */
/**
 * Paginated request parameters
 */
interface PaginatedRequest {
    /** Page number (1-based) */
    page?: number;
    /** Number of items per page */
    pageSize?: number;
    /** Field to sort by */
    sortBy?: string;
    /** Sort direction */
    sortOrder?: 'asc' | 'desc';
}
/**
 * Extended paginated response wrapper with navigation helpers
 */
interface ExtendedPaginatedResponse<T> {
    /** Array of items for current page */
    items: T[];
    /** Total number of items */
    totalCount: number;
    /** Current page number (1-based) */
    page: number;
    /** Number of items per page */
    pageSize: number;
    /** Total number of pages */
    totalPages: number;
    /** Whether there's a next page */
    hasNextPage?: boolean;
    /** Whether there's a previous page */
    hasPreviousPage?: boolean;
}
/**
 * Standard API error response
 */
interface ApiError {
    /** Error code for programmatic handling */
    code: string;
    /** Human-readable error message */
    message: string;
    /** Additional error details */
    details?: Record<string, unknown>;
    /** Field-specific errors for validation */
    fieldErrors?: Record<string, string[]>;
    /** Request ID for debugging */
    requestId?: string;
    /** Timestamp when error occurred */
    timestamp?: string;
}
/**
 * Standard API success response wrapper
 */
interface ApiSuccessResponse<T> {
    /** Response data */
    data: T;
    /** Success status */
    success: boolean;
    /** Optional message */
    message?: string;
    /** Response metadata */
    meta?: ResponseMetadata;
}
/**
 * Response metadata
 */
interface ResponseMetadata {
    /** Request ID for tracing */
    requestId: string;
    /** Response timestamp */
    timestamp: string;
    /** API version */
    version: string;
    /** Response time in ms */
    responseTime?: number;
}
/**
 * Batch operation request
 */
interface BatchRequest<T> {
    /** Array of items to process */
    items: T[];
    /** Whether to continue on error */
    continueOnError?: boolean;
    /** Maximum items to process in parallel */
    parallelism?: number;
}
/**
 * Batch operation response
 */
interface BatchResponse<T> {
    /** Successfully processed items */
    succeeded: BatchResult<T>[];
    /** Failed items */
    failed: BatchError[];
    /** Summary statistics */
    summary: {
        total: number;
        succeeded: number;
        failed: number;
        duration: number;
    };
}
/**
 * Individual batch result
 */
interface BatchResult<T> {
    /** Index in original request */
    index: number;
    /** Processed result */
    result: T;
}
/**
 * Individual batch error
 */
interface BatchError {
    /** Index in original request */
    index: number;
    /** Error details */
    error: ApiError;
}
/**
 * Date range filter
 */
interface DateRangeFilter {
    /** Start date (inclusive) */
    startDate?: string;
    /** End date (inclusive) */
    endDate?: string;
}
/**
 * Numeric range filter
 */
interface NumericRangeFilter {
    /** Minimum value (inclusive) */
    min?: number;
    /** Maximum value (inclusive) */
    max?: number;
}
/**
 * Alternative paginated result interface used in some endpoints
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
 * Sort configuration
 */
interface SortConfig {
    /** Field to sort by */
    field: string;
    /** Sort direction */
    direction: 'asc' | 'desc';
}
/**
 * Filter operators
 */
declare enum FilterOperator {
    EQUALS = "eq",
    NOT_EQUALS = "ne",
    GREATER_THAN = "gt",
    GREATER_THAN_OR_EQUAL = "gte",
    LESS_THAN = "lt",
    LESS_THAN_OR_EQUAL = "lte",
    IN = "in",
    NOT_IN = "nin",
    CONTAINS = "contains",
    STARTS_WITH = "startsWith",
    ENDS_WITH = "endsWith"
}
/**
 * Generic filter
 */
interface Filter {
    /** Field to filter on */
    field: string;
    /** Filter operator */
    operator: FilterOperator;
    /** Filter value */
    value: unknown;
}
/**
 * Health check response
 */
interface HealthCheckResponse {
    /** Overall health status */
    status: 'healthy' | 'degraded' | 'unhealthy';
    /** Service version */
    version: string;
    /** Uptime in seconds */
    uptime: number;
    /** Individual component health */
    components: Record<string, ComponentHealth>;
}
/**
 * Base interface for create DTOs
 */
interface CreateDto<T> {
    /** The data to create */
    data: Partial<T>;
}
/**
 * Base interface for update DTOs
 */
interface UpdateDto<T> {
    /** The fields to update */
    data: Partial<T>;
    /** Optional version for optimistic concurrency */
    version?: string;
}
/**
 * Base interface for delete operations
 */
interface DeleteDto {
    /** Optional reason for deletion */
    reason?: string;
    /** Whether to force delete (bypass soft delete) */
    force?: boolean;
}
/**
 * Bulk operation request
 */
interface BulkOperationRequest<T> {
    /** Items to process */
    items: T[];
    /** Whether to continue on error */
    continueOnError?: boolean;
    /** Maximum parallel operations */
    parallelism?: number;
}
/**
 * Bulk operation response
 */
interface BulkOperationResponse<T> {
    /** Successfully processed items */
    succeeded: T[];
    /** Failed items with errors */
    failed: Array<{
        item: T;
        error: ApiError;
    }>;
    /** Summary statistics */
    summary: {
        total: number;
        succeeded: number;
        failed: number;
    };
}
/**
 * Component health status
 */
interface ComponentHealth {
    /** Component status */
    status: 'healthy' | 'degraded' | 'unhealthy';
    /** Optional message */
    message?: string;
    /** Last check timestamp */
    lastCheck: string;
    /** Response time in ms */
    responseTime?: number;
}

/**
 * Common constants shared across all Conduit SDKs
 */
/**
 * API version constants
 */
declare const API_VERSION = "v1";
declare const API_PREFIX = "/api";
/**
 * Default pagination settings
 */
declare const PAGINATION: {
    readonly DEFAULT_PAGE_SIZE: 20;
    readonly MAX_PAGE_SIZE: 100;
    readonly DEFAULT_PAGE: 1;
};
/**
 * Cache TTL values in seconds
 */
declare const CACHE_TTL: {
    readonly SHORT: 60;
    readonly MEDIUM: 300;
    readonly LONG: 3600;
    readonly VERY_LONG: 86400;
};
/**
 * Task status constants
 */
declare const TASK_STATUS: {
    readonly PENDING: "pending";
    readonly PROCESSING: "processing";
    readonly COMPLETED: "completed";
    readonly FAILED: "failed";
    readonly CANCELLED: "cancelled";
    readonly TIMEOUT: "timeout";
};
type TaskStatus = typeof TASK_STATUS[keyof typeof TASK_STATUS];
/**
 * Task polling configuration
 */
declare const POLLING_CONFIG: {
    readonly DEFAULT_INTERVAL: 1000;
    readonly MAX_INTERVAL: 30000;
    readonly DEFAULT_TIMEOUT: 300000;
    readonly BACKOFF_FACTOR: 1.5;
};
/**
 * Budget duration types
 */
declare const BUDGET_DURATION: {
    readonly TOTAL: "Total";
    readonly DAILY: "Daily";
    readonly WEEKLY: "Weekly";
    readonly MONTHLY: "Monthly";
};
type BudgetDuration = typeof BUDGET_DURATION[keyof typeof BUDGET_DURATION];
/**
 * Filter types for IP filtering
 */
declare const FILTER_TYPE: {
    readonly ALLOW: "whitelist";
    readonly DENY: "blacklist";
};
type FilterType = typeof FILTER_TYPE[keyof typeof FILTER_TYPE];
/**
 * Filter modes
 */
declare const FILTER_MODE: {
    readonly PERMISSIVE: "permissive";
    readonly RESTRICTIVE: "restrictive";
};
type FilterMode = typeof FILTER_MODE[keyof typeof FILTER_MODE];
/**
 * Chat message roles
 */
declare const CHAT_ROLES: {
    readonly SYSTEM: "system";
    readonly USER: "user";
    readonly ASSISTANT: "assistant";
    readonly FUNCTION: "function";
    readonly TOOL: "tool";
};
type ChatRole = typeof CHAT_ROLES[keyof typeof CHAT_ROLES];
/**
 * Image response formats
 */
declare const IMAGE_RESPONSE_FORMATS: {
    readonly URL: "url";
    readonly B64_JSON: "b64_json";
};
type ImageResponseFormat = typeof IMAGE_RESPONSE_FORMATS[keyof typeof IMAGE_RESPONSE_FORMATS];
/**
 * Video response formats
 */
declare const VIDEO_RESPONSE_FORMATS: {
    readonly URL: "url";
    readonly B64_JSON: "b64_json";
};
type VideoResponseFormat = typeof VIDEO_RESPONSE_FORMATS[keyof typeof VIDEO_RESPONSE_FORMATS];
/**
 * Common date formats
 */
declare const DATE_FORMATS: {
    readonly API_DATETIME: "YYYY-MM-DDTHH:mm:ss[Z]";
    readonly API_DATE: "YYYY-MM-DD";
    readonly DISPLAY_DATETIME: "MMM D, YYYY [at] h:mm A";
    readonly DISPLAY_DATE: "MMM D, YYYY";
};
/**
 * Streaming constants
 */
declare const STREAM_CONSTANTS: {
    readonly DEFAULT_BUFFER_SIZE: number;
    readonly DEFAULT_TIMEOUT: 60000;
    readonly CHUNK_DELIMITER: "\n\n";
    readonly DATA_PREFIX: "data: ";
    readonly EVENT_PREFIX: "event: ";
    readonly DONE_MESSAGE: "[DONE]";
};
/**
 * Client identification
 */
declare const CLIENT_INFO: {
    readonly CORE_NAME: "@conduit/core";
    readonly ADMIN_NAME: "@conduit/admin";
    readonly VERSION: "0.2.0";
};
/**
 * Health status values
 */
declare const HEALTH_STATUS: {
    readonly HEALTHY: "healthy";
    readonly DEGRADED: "degraded";
    readonly UNHEALTHY: "unhealthy";
};
type HealthStatus = typeof HEALTH_STATUS[keyof typeof HEALTH_STATUS];
/**
 * Common regex patterns
 */
declare const PATTERNS: {
    readonly API_KEY: RegExp;
    readonly EMAIL: RegExp;
    readonly URL: RegExp;
    readonly ISO_DATE: RegExp;
};

/**
 * Common validation utilities shared across Conduit SDKs
 */
/**
 * Validates email format
 */
declare function isValidEmail(email: string): boolean;
/**
 * Validates URL format
 */
declare function isValidUrl(url: string): boolean;
/**
 * Validates API key format
 */
declare function isValidApiKey(apiKey: string): boolean;
/**
 * Validates ISO date string
 */
declare function isValidIsoDate(date: string): boolean;
/**
 * Validates UUID format
 */
declare function isValidUuid(uuid: string): boolean;
/**
 * Validates that a value is not null or undefined
 */
declare function assertDefined<T>(value: T | null | undefined, name: string): T;
/**
 * Validates that a string is not empty
 */
declare function assertNotEmpty(value: string | null | undefined, name: string): string;
/**
 * Validates that a number is within a range
 */
declare function assertInRange(value: number, min: number, max: number, name: string): number;
/**
 * Validates that a value is one of allowed values
 */
declare function assertOneOf<T>(value: T, allowed: readonly T[], name: string): T;
/**
 * Validates array length
 */
declare function assertArrayLength<T>(array: T[], min: number, max: number, name: string): T[];
/**
 * Validates that an object has required properties
 */
declare function assertHasProperties<T extends Record<string, unknown>>(obj: T, required: (keyof T)[], name: string): T;
/**
 * Sanitizes a string by removing potentially dangerous characters
 */
declare function sanitizeString(str: string, maxLength?: number): string;
/**
 * Type guard to check if value is a non-empty string
 */
declare function isNonEmptyString(value: unknown): value is string;
/**
 * Type guard to check if value is a positive number
 */
declare function isPositiveNumber(value: unknown): value is number;
/**
 * Type guard to check if value is a valid enum value
 */
declare function isEnumValue<T extends Record<string, string | number>>(value: unknown, enumObject: T): value is T[keyof T];
/**
 * Validates JSON string
 */
declare function isValidJson(str: string): boolean;
/**
 * Validates base64 string
 */
declare function isValidBase64(str: string): boolean;
/**
 * Creates a validation function that checks multiple conditions
 */
declare function createValidator<T>(validators: Array<(value: T) => boolean | string>): (value: T) => void;

/**
 * Date and time utility functions shared across Conduit SDKs
 */
/**
 * Formats a date to ISO string with UTC timezone
 */
declare function toIsoString(date: Date | string | number): string;
/**
 * Parses an ISO date string to Date object
 */
declare function parseIsoDate(dateStr: string): Date;
/**
 * Gets current timestamp in ISO format
 */
declare function getCurrentTimestamp(): string;
/**
 * Calculates time difference in milliseconds
 */
declare function getTimeDifference(start: Date | string, end?: Date | string): number;
/**
 * Formats duration in milliseconds to human-readable string
 */
declare function formatDuration(ms: number): string;
/**
 * Adds time to a date
 */
declare function addTime(date: Date | string, amount: number, unit: 'seconds' | 'minutes' | 'hours' | 'days'): Date;
/**
 * Checks if a date is within a range
 */
declare function isDateInRange(date: Date | string, start: Date | string, end: Date | string): boolean;
/**
 * Gets the start of a time period
 */
declare function getStartOf(date: Date | string, period: 'day' | 'week' | 'month' | 'year'): Date;
/**
 * Gets the end of a time period
 */
declare function getEndOf(date: Date | string, period: 'day' | 'week' | 'month' | 'year'): Date;
/**
 * Formats a date for API requests (YYYY-MM-DD)
 */
declare function formatApiDate(date: Date | string): string;
/**
 * Parses a Unix timestamp to Date
 */
declare function fromUnixTimestamp(timestamp: number): Date;
/**
 * Converts Date to Unix timestamp (seconds)
 */
declare function toUnixTimestamp(date: Date | string): number;

/**
 * Formatting utilities shared across Conduit SDKs
 */
/**
 * Formats a number as currency
 */
declare function formatCurrency(amount: number, currency?: string, locale?: string): string;
/**
 * Formats a number with commas
 */
declare function formatNumber(value: number, decimals?: number, locale?: string): string;
/**
 * Formats bytes to human-readable size
 */
declare function formatBytes(bytes: number, decimals?: number): string;
/**
 * Formats a percentage
 */
declare function formatPercentage(value: number, decimals?: number): string;
/**
 * Truncates a string with ellipsis
 */
declare function truncateString(str: string, maxLength: number, suffix?: string): string;
/**
 * Capitalizes first letter of a string
 */
declare function capitalize(str: string): string;
/**
 * Converts string to title case
 */
declare function toTitleCase(str: string): string;
/**
 * Converts string to kebab-case
 */
declare function toKebabCase(str: string): string;
/**
 * Converts string to snake_case
 */
declare function toSnakeCase(str: string): string;
/**
 * Converts string to camelCase
 */
declare function toCamelCase(str: string): string;
/**
 * Pads a string or number with zeros
 */
declare function padZero(value: string | number, length: number): string;
/**
 * Formats a duration in seconds to HH:MM:SS
 */
declare function formatDurationHMS(seconds: number): string;
/**
 * Pluralizes a word based on count
 */
declare function pluralize(count: number, singular: string, plural?: string): string;
/**
 * Formats a list of items with proper grammar
 */
declare function formatList(items: string[], conjunction?: string): string;
/**
 * Masks sensitive data
 */
declare function maskSensitive(value: string, showFirst?: number, showLast?: number, maskChar?: string): string;
/**
 * Formats a file path to be more readable
 */
declare function formatFilePath(path: string, maxLength?: number): string;

/**
 * Common utilities for Conduit SDKs
 */

/**
 * Delays execution for specified milliseconds
 */
declare function delay(ms: number): Promise<void>;
/**
 * Retries a function with exponential backoff
 */
declare function retry<T>(fn: () => Promise<T>, options?: {
    maxRetries?: number;
    initialDelay?: number;
    maxDelay?: number;
    backoffFactor?: number;
    shouldRetry?: (error: Error, attempt: number) => boolean;
}): Promise<T>;
/**
 * Creates a debounced version of a function
 */
declare function debounce<T extends (...args: any[]) => any>(fn: T, wait: number): (...args: Parameters<T>) => void;
/**
 * Creates a throttled version of a function
 */
declare function throttle<T extends (...args: any[]) => any>(fn: T, limit: number): (...args: Parameters<T>) => void;
/**
 * Deep clones an object
 */
declare function deepClone<T>(obj: T): T;
/**
 * Deep merges objects
 */
declare function deepMerge<T = any>(target: any, ...sources: any[]): T;
/**
 * Checks if a value is a plain object
 */
declare function isObject(value: unknown): value is Record<string, unknown>;
/**
 * Groups an array by a key function
 */
declare function groupBy<T, K extends string | number | symbol>(array: T[], keyFn: (item: T) => K): Record<K, T[]>;
/**
 * Chunks an array into smaller arrays
 */
declare function chunk<T>(array: T[], size: number): T[][];
/**
 * Picks specified properties from an object
 */
declare function pick<T extends Record<string, any>, K extends keyof T>(obj: T, keys: K[]): Pick<T, K>;
/**
 * Omits specified properties from an object
 */
declare function omit<T extends Record<string, any>, K extends keyof T>(obj: T, keys: K[]): Omit<T, K>;
/**
 * Creates a promise that resolves after a timeout
 */
declare function withTimeout<T>(promise: Promise<T>, timeoutMs: number, timeoutError?: Error): Promise<T>;
/**
 * Memoizes a function
 */
declare function memoize<T extends (...args: any[]) => any>(fn: T, keyFn?: (...args: Parameters<T>) => string): T;

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

interface BaseClientConfig extends BaseClientOptions {
    baseURL: string;
}
interface BaseRequestOptions {
    headers?: Record<string, string>;
    signal?: AbortSignal;
    timeout?: number;
    responseType?: 'json' | 'text' | 'blob' | 'arraybuffer';
}
/**
 * Abstract base API client for Conduit SDKs
 * Provides common HTTP functionality with authentication handled by subclasses
 */
declare abstract class BaseApiClient {
    protected readonly config: Required<Omit<BaseClientConfig, 'logger' | 'cache' | 'onError' | 'onRequest' | 'onResponse'>> & Pick<BaseClientConfig, 'logger' | 'cache' | 'onError' | 'onRequest' | 'onResponse'>;
    protected readonly retryConfig: RetryConfig;
    protected readonly logger?: Logger;
    protected readonly cache?: CacheProvider;
    constructor(config: BaseClientConfig);
    /**
     * Abstract method for SDK-specific authentication headers
     * Must be implemented by Core and Admin SDK clients
     */
    protected abstract getAuthHeaders(): Record<string, string>;
    /**
     * Get base URL for services that need direct access
     */
    getBaseURL(): string;
    /**
     * Get timeout for services that need direct access
     */
    getTimeout(): number;
    /**
     * Type-safe request method with proper request/response typing
     */
    protected request<TResponse = unknown, TRequest = unknown>(url: string, options?: BaseRequestOptions & {
        method?: HttpMethod;
        body?: TRequest;
    }): Promise<TResponse>;
    /**
     * Type-safe GET request with support for query parameters
     */
    protected get<TResponse = unknown>(url: string, paramsOrOptions?: Record<string, unknown> | BaseRequestOptions, options?: BaseRequestOptions): Promise<TResponse>;
    /**
     * Type-safe POST request
     */
    protected post<TResponse = unknown, TRequest = unknown>(url: string, data?: TRequest, options?: BaseRequestOptions): Promise<TResponse>;
    /**
     * Type-safe PUT request
     */
    protected put<TResponse = unknown, TRequest = unknown>(url: string, data?: TRequest, options?: BaseRequestOptions): Promise<TResponse>;
    /**
     * Type-safe PATCH request
     */
    protected patch<TResponse = unknown, TRequest = unknown>(url: string, data?: TRequest, options?: BaseRequestOptions): Promise<TResponse>;
    /**
     * Type-safe DELETE request
     */
    protected delete<TResponse = unknown>(url: string, options?: BaseRequestOptions): Promise<TResponse>;
    /**
     * Build full URL from path
     */
    private buildUrl;
    /**
     * Build headers with authentication and defaults
     */
    private buildHeaders;
    /**
     * Execute request with retry logic
     */
    private executeWithRetry;
    /**
     * Parse response based on content type
     */
    private parseResponse;
    /**
     * Handle error responses
     */
    protected abstract handleErrorResponse(response: Response): Promise<Error>;
    /**
     * Determine if error should trigger retry
     */
    protected shouldRetry(error: unknown): boolean;
    /**
     * Calculate retry delay
     */
    private calculateDelay;
    /**
     * Sleep for specified milliseconds
     */
    private sleep;
    /**
     * Handle and transform errors
     */
    protected handleError(error: unknown): Error;
    /**
     * Normalize retry configuration
     */
    private normalizeRetryConfig;
    /**
     * Log message using logger if available
     */
    protected log(level: 'debug' | 'info' | 'warn' | 'error', message: string, ...args: unknown[]): void;
    /**
     * Build URL with query parameters
     */
    protected buildUrlWithParams(url: string, params: Record<string, unknown>): string;
    /**
     * Get cache key for a request
     */
    protected getCacheKey(resource: string, id?: unknown, params?: Record<string, unknown>): string;
    /**
     * Get from cache
     */
    protected getFromCache<T>(key: string): Promise<T | null>;
    /**
     * Set cache value
     */
    protected setCache(key: string, value: unknown, ttl?: number): Promise<void>;
    /**
     * Execute function with caching
     */
    protected withCache<T>(cacheKey: string, fn: () => Promise<T>, ttl?: number): Promise<T>;
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

export { API_PREFIX, API_VERSION, type ApiError, type ApiResponse, type ApiSuccessResponse, AuthError, AuthenticationError, AuthorizationError, BUDGET_DURATION, BaseApiClient, type BaseClientConfig, type BaseClientOptions, type BaseModel, type BaseRequestOptions, type BaseSignalRConfig, BaseSignalRConnection, type BatchError, type BatchOperationParams, type BatchRequest, type BatchResponse, type BatchResult, type BudgetDuration, type BulkOperationRequest, type BulkOperationResponse, CACHE_TTL, CHAT_ROLES, CLIENT_INFO, CONTENT_TYPES, type CacheProvider, type ChatRole, type ClientLifecycleCallbacks, type ComponentHealth, ConduitError, ConflictError, type ContentType, type CreateDto, DATE_FORMATS, type DateRange, type DateRangeFilter, DefaultTransports, type DeleteDto, type DiscoveredModel, ERROR_CODES, type ErrorCode, type ErrorResponse, type ErrorResponseFormat, type ExtendedPaginatedResponse, type ExtendedRequestInit, FILTER_MODE, FILTER_TYPE, type Filter, type FilterMode, FilterOperator, type FilterOptions, type FilterType, HEALTH_STATUS, HTTP_HEADERS, HTTP_STATUS, type HealthCheckResponse, type HealthStatus, HttpError, type HttpHeader, HttpMethod, type HttpStatusCode, HttpTransportType, HubConnectionState, IMAGE_RESPONSE_FORMATS, type ImageResponseFormat, type Logger, type ModelCapabilities, ModelCapability, type ModelCapabilityInfo, type ModelConstraints, type ModelCostInfo, type ModelFeatureSupport, type ModelHealthStatus, type ModelMapping, type ModelPricing, type ModelUsageStats, type ModelWithCapabilities, NetworkError, NotFoundError, NotImplementedError, type NumericRangeFilter, PAGINATION, PATTERNS, POLLING_CONFIG, type PagedResponse, type PagedResult, type PaginatedRequest, type PaginatedResponse, type PaginationParams, type PerformanceMetrics, ProviderType, RETRY_CONFIG, RateLimitError, type RequestConfigInfo, type RequestOptions, type ResponseInfo, type ResponseMetadata, ResponseParser, type RetryConfig, type RetryConfigValue, STREAM_CONSTANTS, type SearchParams, ServerError, type SignalRArgs, type SignalRAuthConfig, type SignalRConfig, type SignalRConnectionOptions, SignalRLogLevel, type SignalRValue, type SortConfig, type SortDirection, type SortOptions, StreamError, TASK_STATUS, TIMEOUTS, type TaskStatus, type TimeRangeParams, TimeoutError, type TimeoutValue, type UpdateDto, type Usage, VIDEO_RESPONSE_FORMATS, ValidationError, type VideoResponseFormat, addTime, assertArrayLength, assertDefined, assertHasProperties, assertInRange, assertNotEmpty, assertOneOf, capitalize, chunk, createErrorFromResponse, createValidator, debounce, deepClone, deepMerge, delay, deserializeError, formatApiDate, formatBytes, formatCurrency, formatDuration, formatDurationHMS, formatFilePath, formatList, formatNumber, formatPercentage, fromUnixTimestamp, getCapabilityCategory, getCapabilityDisplayName, getCurrentTimestamp, getEndOf, getErrorMessage, getErrorStatusCode, getProviderDisplayName, getStartOf, getTimeDifference, groupBy, handleApiError, hasModelFeatureSupport, isAuthError, isAuthorizationError, isBaseModel, isConduitError, isConflictError, isDateInRange, isEnumValue, isErrorLike, isHttpError, isHttpMethod, isHttpNetworkError, isNetworkError, isNonEmptyString, isNotFoundError, isObject, isPositiveNumber, isProviderType, isRateLimitError, isSerializedConduitError, isStreamError, isTimeoutError, isValidApiKey, isValidBase64, isValidEmail, isValidIsoDate, isValidJson, isValidUrl, isValidUuid, isValidationError, maskSensitive, memoize, omit, padZero, parseIsoDate, pick, pluralize, retry, sanitizeString, serializeError, throttle, toCamelCase, toIsoString, toKebabCase, toSnakeCase, toTitleCase, toUnixTimestamp, truncateString, withTimeout };
