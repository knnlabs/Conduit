import { SignalRConfig, RequestConfigInfo, ResponseInfo as ResponseInfo$1, RetryConfig as RetryConfig$1, HttpMethod, Usage, PerformanceMetrics, ConduitError, BaseSignalRConnection as BaseSignalRConnection$1, HubConnectionState, ModelCapability as ModelCapability$1 } from '@knn_labs/conduit-common';
export { ApiResponse, AuthenticationError, ConduitError, RequestOptions as CoreRequestOptions, DefaultTransports, HttpMethod, HttpTransportType, HubConnectionState, NetworkError, PerformanceMetrics, RateLimitError, SignalRConfig, SignalRConnectionOptions, SignalRLogLevel, StreamError, Usage } from '@knn_labs/conduit-common';
import * as signalR from '@microsoft/signalr';

interface ClientConfig {
    apiKey: string;
    baseURL?: string;
    timeout?: number;
    maxRetries?: number;
    headers?: Record<string, string>;
    debug?: boolean;
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
    onRequest?: (config: RequestConfig) => void | Promise<void>;
    /**
     * Callback invoked after each response
     */
    onResponse?: (response: ResponseInfo) => void | Promise<void>;
}
interface RequestOptions {
    signal?: AbortSignal;
    headers?: Record<string, string>;
    timeout?: number;
    correlationId?: string;
    responseType?: 'json' | 'text' | 'arraybuffer' | 'blob';
}
interface RetryConfig extends RetryConfig$1 {
    maxRetries: number;
    initialDelay: number;
    maxDelay: number;
    factor: number;
}
interface RequestConfig extends RequestConfigInfo {
    method: string;
    url: string;
    headers: Record<string, string>;
    data?: unknown;
}
interface ResponseInfo extends ResponseInfo$1 {
    status: number;
    statusText: string;
    headers: Record<string, string>;
    data: unknown;
    config: RequestConfig;
}

/**
 * Type-safe base client using native fetch API
 * Provides all the functionality of HTTP with better type safety
 */
declare abstract class FetchBasedClient {
    protected readonly config: Required<Omit<ClientConfig, 'onError' | 'onRequest' | 'onResponse'>> & Pick<ClientConfig, 'onError' | 'onRequest' | 'onResponse'>;
    protected readonly retryConfig: RetryConfig;
    protected readonly retryDelays: number[];
    constructor(config: ClientConfig);
    /**
     * Type-safe request method with proper request/response typing
     */
    protected request<TResponse = unknown, TRequest = unknown>(url: string, options?: RequestOptions & {
        method?: HttpMethod;
        body?: TRequest;
    }): Promise<TResponse>;
    /**
     * Type-safe GET request
     */
    protected get<TResponse = unknown>(url: string, options?: RequestOptions): Promise<TResponse>;
    /**
     * Type-safe POST request
     */
    protected post<TResponse = unknown, TRequest = unknown>(url: string, data?: TRequest, options?: RequestOptions): Promise<TResponse>;
    /**
     * Type-safe PUT request
     */
    protected put<TResponse = unknown, TRequest = unknown>(url: string, data?: TRequest, options?: RequestOptions): Promise<TResponse>;
    /**
     * Type-safe PATCH request
     */
    protected patch<TResponse = unknown, TRequest = unknown>(url: string, data?: TRequest, options?: RequestOptions): Promise<TResponse>;
    /**
     * Type-safe DELETE request
     */
    protected delete<TResponse = unknown>(url: string, options?: RequestOptions): Promise<TResponse>;
    private buildUrl;
    private buildHeaders;
    private executeWithRetry;
    private handleErrorResponse;
    private shouldRetry;
    private calculateDelay;
    private sleep;
    private handleError;
}

interface components {
    schemas: {
        ChatCompletionRequest: {
            /**
             * @description ID of the model to use
             * @example gpt-4
             */
            model: string;
            /** @description List of messages comprising the conversation so far */
            messages: components["schemas"]["Message"][];
            /**
             * Format: double
             * @description Sampling temperature between 0 and 2
             * @default 1
             */
            temperature: number;
            /**
             * @description Maximum number of tokens to generate
             * @example 1024
             */
            max_tokens?: number;
            /**
             * Format: double
             * @description Nucleus sampling parameter
             * @default 1
             */
            top_p: number;
            /**
             * @description Number of chat completion choices to generate
             * @default 1
             */
            n: number;
            /**
             * @description Whether to stream back partial progress
             * @default false
             */
            stream: boolean;
            /** @description Sequences where the API will stop generating further tokens */
            stop?: string | string[];
            /**
             * Format: double
             * @description Penalize new tokens based on whether they appear in the text so far
             * @default 0
             */
            presence_penalty: number;
            /**
             * Format: double
             * @description Penalize new tokens based on their existing frequency in the text
             * @default 0
             */
            frequency_penalty: number;
            /** @description Modify the likelihood of specified tokens appearing in the completion */
            logit_bias?: {
                [key: string]: number;
            };
            /** @description Unique identifier representing your end-user */
            user?: string;
            /** @description List of tools the model may call */
            tools?: components["schemas"]["Tool"][];
            /** @description Controls which tool is called by the model */
            tool_choice?: ("none" | "auto") | components["schemas"]["ToolChoice"];
            response_format?: components["schemas"]["ResponseFormat"];
            /** @description Random seed for deterministic outputs */
            seed?: number;
        };
        Message: {
            /**
             * @description Role of the message author
             * @enum {string}
             */
            role: "system" | "user" | "assistant" | "function" | "tool";
            /** @description Content of the message */
            content: string | components["schemas"]["ContentPart"][];
            /** @description Name of the function (when role is function) */
            name?: string;
            /** @description Function call made by the assistant */
            function_call?: components["schemas"]["FunctionCall"];
            /** @description Tool calls made by the assistant */
            tool_calls?: components["schemas"]["ToolCall"][];
        };
        ContentPart: {
            /** @enum {string} */
            type: "text";
            text: string;
        } | {
            /** @enum {string} */
            type: "image_url";
            image_url: {
                /** Format: uri */
                url: string;
                /**
                 * @default auto
                 * @enum {string}
                 */
                detail: "auto" | "low" | "high";
            };
        };
        Tool: {
            /** @enum {string} */
            type: "function";
            function: components["schemas"]["FunctionDefinition"];
        };
        FunctionDefinition: {
            /** @description Name of the function */
            name: string;
            /** @description Description of what the function does */
            description?: string;
            /** @description Parameters the function accepts (JSON Schema) */
            parameters?: Record<string, never>;
        };
        ToolChoice: {
            /** @enum {string} */
            type: "function";
            function: {
                name: string;
            };
        };
        ResponseFormat: {
            /** @enum {string} */
            type: "text" | "json_object";
            /** @description JSON Schema for structured outputs */
            schema?: Record<string, never>;
        };
        ChatCompletionResponse: {
            /** @description Unique identifier for the chat completion */
            id: string;
            /** @enum {string} */
            object: "chat.completion";
            /**
             * Format: int64
             * @description Unix timestamp of when the completion was created
             */
            created: number;
            /** @description Model used for the completion */
            model: string;
            /** @description System fingerprint for the model configuration */
            system_fingerprint?: string;
            choices: components["schemas"]["Choice"][];
            usage?: components["schemas"]["Usage"];
        };
        Choice: {
            /** @description Index of the choice in the list */
            index: number;
            message: components["schemas"]["Message"];
            /**
             * @description Reason the model stopped generating tokens
             * @enum {string}
             */
            finish_reason: "stop" | "length" | "tool_calls" | "content_filter" | "function_call";
            /** @description Log probabilities for the output tokens */
            logprobs?: Record<string, never> | null;
        };
        ChatCompletionChunk: {
            id: string;
            /** @enum {string} */
            object: "chat.completion.chunk";
            /** Format: int64 */
            created: number;
            model: string;
            system_fingerprint?: string;
            choices: components["schemas"]["StreamingChoice"][];
        };
        StreamingChoice: {
            index: number;
            delta: components["schemas"]["DeltaContent"];
            /** @enum {string|null} */
            finish_reason?: "stop" | "length" | "tool_calls" | "content_filter" | "function_call" | null;
        };
        DeltaContent: {
            /** @enum {string} */
            role?: "system" | "user" | "assistant" | "function" | "tool";
            content?: string | null;
            function_call?: components["schemas"]["FunctionCall"];
            tool_calls?: components["schemas"]["ToolCall"][];
        };
        FunctionCall: {
            name: string;
            /** @description JSON-encoded arguments */
            arguments: string;
        };
        ToolCall: {
            id: string;
            /** @enum {string} */
            type: "function";
            function: components["schemas"]["FunctionCall"];
        };
        Usage: {
            /** @description Number of tokens in the prompt */
            prompt_tokens: number;
            /** @description Number of tokens in the generated completion */
            completion_tokens: number;
            /** @description Total number of tokens used */
            total_tokens: number;
        };
        Model: {
            /** @description Model identifier */
            id: string;
            /** @enum {string} */
            object: "model";
            /**
             * Format: int64
             * @description Unix timestamp of model creation
             */
            created?: number;
            /** @description Organization that owns the model */
            owned_by?: string;
        };
        EmbeddingRequest: {
            /** @description ID of the model to use */
            model: string;
            /** @description Input text to embed */
            input: string | string[];
            /**
             * @description Format to return embeddings in
             * @default float
             * @enum {string}
             */
            encoding_format: "float" | "base64";
            /** @description Number of dimensions for the output embeddings */
            dimensions?: number;
            /** @description Unique identifier representing your end-user */
            user?: string;
        };
        EmbeddingResponse: {
            /** @enum {string} */
            object: "list";
            data: {
                /** @enum {string} */
                object: "embedding";
                embedding: number[];
                index: number;
            }[];
            model: string;
            usage: {
                prompt_tokens: number;
                total_tokens: number;
            };
        };
        ImageGenerationRequest: {
            /** @description ID of the model to use */
            model: string;
            /** @description Text description of the desired image(s) */
            prompt: string;
            /**
             * @description Number of images to generate
             * @default 1
             */
            n: number;
            /**
             * @description Quality of the image
             * @default standard
             * @enum {string}
             */
            quality: "standard" | "hd";
            /**
             * @description Format in which generated images are returned
             * @default url
             * @enum {string}
             */
            response_format: "url" | "b64_json";
            /**
             * @description Size of the generated images
             * @default 1024x1024
             * @enum {string}
             */
            size: "256x256" | "512x512" | "1024x1024" | "1792x1024" | "1024x1792";
            /**
             * @description Style of the generated images
             * @default vivid
             * @enum {string}
             */
            style: "vivid" | "natural";
            /** @description Unique identifier representing your end-user */
            user?: string;
        };
        ImageGenerationResponse: {
            /**
             * Format: int64
             * @description Unix timestamp of when the images were created
             */
            created: number;
            data: {
                /**
                 * Format: uri
                 * @description URL of the generated image
                 */
                url?: string;
                /** @description Base64-encoded JSON of the generated image */
                b64_json?: string;
                /** @description The prompt that was used to generate the image */
                revised_prompt?: string;
            }[];
        };
        Error: {
            error: string | {
                /** @description Human-readable error message */
                message: string;
                /** @description Error type */
                type: string;
                /** @description Error code */
                code?: string;
                /** @description Parameter related to the error */
                param?: string | null;
            };
        };
    };
    responses: {
        /** @description Bad request */
        BadRequest: {
            headers: {
                [name: string]: unknown;
            };
            content: {
                "application/json": components["schemas"]["Error"];
            };
        };
        /** @description Authentication required */
        Unauthorized: {
            headers: {
                [name: string]: unknown;
            };
            content: {
                "application/json": components["schemas"]["Error"];
            };
        };
        /** @description Rate limit exceeded */
        RateLimitExceeded: {
            headers: {
                [name: string]: unknown;
            };
            content: {
                "application/json": components["schemas"]["Error"];
            };
        };
        /** @description Internal server error */
        InternalServerError: {
            headers: {
                [name: string]: unknown;
            };
            content: {
                "application/json": components["schemas"]["Error"];
            };
        };
    };
    parameters: never;
    requestBodies: never;
    headers: never;
    pathItems: never;
}

interface ResponseFormat {
    type: 'text' | 'json_object';
}
interface FunctionCall {
    name: string;
    arguments: string;
}
interface ToolCall {
    id: string;
    type: 'function';
    function: FunctionCall;
}
interface FunctionDefinition {
    name: string;
    description?: string;
    parameters?: Record<string, unknown>;
}
interface Tool {
    type: 'function';
    function: FunctionDefinition;
}
type FinishReason = 'stop' | 'length' | 'tool_calls' | 'content_filter' | null;
interface ErrorResponse {
    error: {
        message: string;
        type: string;
        param?: string | null;
        code?: string | null;
    };
}

/**
 * Type-safe metadata interfaces for Core SDK
 */
/**
 * Chat completion metadata
 */
interface ChatMetadata {
    /** Conversation or session ID */
    conversationId?: string;
    /** User ID making the request */
    userId?: string;
    /** Application or client name */
    application?: string;
    /** Request purpose or context */
    context?: string;
    /** Custom tracking ID */
    trackingId?: string;
    /** Additional properties */
    custom?: {
        [key: string]: string | number | boolean;
    };
}
/**
 * Video generation webhook metadata
 */
interface VideoWebhookMetadata {
    /** Job or task ID */
    jobId?: string;
    /** User or customer ID */
    userId?: string;
    /** Callback URL for status updates */
    callbackUrl?: string;
    /** Custom reference ID */
    referenceId?: string;
    /** Priority level */
    priority?: 'low' | 'normal' | 'high';
    /** Additional callback data */
    callbackData?: {
        [key: string]: string | number | boolean;
    };
}
/**
 * Tool/Function call parameters
 */
interface ToolParameters {
    [key: string]: string | number | boolean | null | ToolParameters | ToolParameters[];
}
/**
 * Notification metadata
 */
interface NotificationMetadata {
    /** Notification type */
    type?: string;
    /** Source system */
    source?: string;
    /** Target user or group */
    target?: string;
    /** Priority level */
    priority?: 'low' | 'normal' | 'high' | 'urgent';
    /** Expiration time */
    expiresAt?: string;
    /** Action URL */
    actionUrl?: string;
    /** Custom data */
    data?: {
        [key: string]: string | number | boolean;
    };
}
/**
 * Type guard to check if a value is valid metadata
 */
declare function isValidMetadata(value: unknown): value is Record<string, unknown>;
/**
 * Safely parse metadata from various sources
 */
declare function parseMetadata<T extends Record<string, unknown>>(metadata: string | Record<string, unknown> | null | undefined): T | undefined;
/**
 * Convert metadata to string if needed
 */
declare function stringifyMetadata<T extends Record<string, unknown>>(metadata: T | null | undefined): string | undefined;

/**
 * Text content part for multi-modal messages
 */
interface TextContent {
    type: 'text';
    text: string;
}
/**
 * Image content part for multi-modal messages
 */
interface ImageContent {
    type: 'image_url';
    image_url: {
        /**
         * URL of the image or base64 encoded image data
         * For base64, use format: "data:image/jpeg;base64,{base64_data}"
         */
        url: string;
        /**
         * Level of detail for image processing
         * - 'low': Faster processing, lower token usage
         * - 'high': More detailed analysis, higher token usage
         * - 'auto': Let the model decide (default)
         */
        detail?: 'low' | 'high' | 'auto';
    };
}
/**
 * Content can be a simple string, null, or an array of content parts for multi-modal messages
 */
type MessageContent = string | null | Array<TextContent | ImageContent>;
interface ChatCompletionMessage {
    role: 'system' | 'user' | 'assistant' | 'tool';
    content: MessageContent;
    name?: string;
    tool_calls?: ToolCall[];
    tool_call_id?: string;
}
interface ChatCompletionRequest$1 {
    model: string;
    messages: ChatCompletionMessage[];
    frequency_penalty?: number;
    logit_bias?: Record<string, number>;
    logprobs?: boolean;
    top_logprobs?: number;
    max_tokens?: number;
    n?: number;
    presence_penalty?: number;
    response_format?: ResponseFormat;
    seed?: number;
    stop?: string | string[];
    stream?: boolean;
    temperature?: number;
    top_p?: number;
    /**
     * Limits the number of tokens to consider for each step of generation.
     * Only the top K most likely tokens are considered for sampling.
     * @minimum 1
     * @maximum 100
     */
    top_k?: number;
    tools?: Tool[];
    tool_choice?: 'none' | 'auto' | {
        type: 'function';
        function: {
            name: string;
        };
    };
    user?: string;
    /**
     * @deprecated Use 'tools' instead. Functions are converted to tools internally.
     */
    functions?: Array<{
        name: string;
        description?: string;
        parameters?: ToolParameters;
    }>;
    /**
     * @deprecated Use 'tool_choice' instead.
     */
    function_call?: 'none' | 'auto' | {
        name: string;
    };
}
interface ChatCompletionChoice {
    index: number;
    message: ChatCompletionMessage;
    logprobs?: unknown;
    finish_reason: FinishReason;
}
interface ChatCompletionResponse$1 {
    id: string;
    object: 'chat.completion';
    created: number;
    model: string;
    system_fingerprint?: string;
    choices: ChatCompletionChoice[];
    usage: Usage;
    performance?: PerformanceMetrics;
}
interface ChatCompletionChunkChoice {
    index: number;
    delta: Partial<ChatCompletionMessage>;
    logprobs?: unknown;
    finish_reason: FinishReason;
}
interface ChatCompletionChunk$1 {
    id: string;
    object: 'chat.completion.chunk';
    created: number;
    model: string;
    system_fingerprint?: string;
    choices: ChatCompletionChunkChoice[];
    usage?: Usage;
    performance?: PerformanceMetrics;
}
/**
 * Helper functions for working with multi-modal content
 */
declare const ContentHelpers: {
    /**
     * Creates a text content part
     */
    text(text: string): TextContent;
    /**
     * Creates an image content part from a URL
     */
    imageUrl(url: string, detail?: "low" | "high" | "auto"): ImageContent;
    /**
     * Creates an image content part from base64 data
     */
    imageBase64(base64Data: string, mimeType?: string, detail?: "low" | "high" | "auto"): ImageContent;
    /**
     * Checks if content contains images
     */
    hasImages(content: MessageContent): boolean;
    /**
     * Extracts text from multi-modal content
     */
    extractText(content: MessageContent): string;
    /**
     * Extracts images from multi-modal content
     */
    extractImages(content: MessageContent): ImageContent[];
};

/**
 * Image generation models and interfaces for OpenAI-compatible API
 */
interface ImageGenerationRequest {
    /**
     * A text description of the desired image(s). The maximum length is 1000 characters for dall-e-2 and 4000 characters for dall-e-3.
     */
    prompt: string;
    /**
     * The model to use for image generation.
     */
    model?: string;
    /**
     * The number of images to generate. Must be between 1 and 10. For dall-e-3, only n=1 is supported.
     */
    n?: number;
    /**
     * The quality of the image that will be generated. hd creates images with finer details and greater consistency across the image. This param is only supported for dall-e-3.
     */
    quality?: 'standard' | 'hd';
    /**
     * The format in which the generated images are returned. Must be one of url or b64_json.
     */
    response_format?: 'url' | 'b64_json';
    /**
     * The size of the generated images. Must be one of 256x256, 512x512, or 1024x1024 for dall-e-2. Must be one of 1024x1024, 1792x1024, or 1024x1792 for dall-e-3 models.
     */
    size?: '256x256' | '512x512' | '1024x1024' | '1792x1024' | '1024x1792';
    /**
     * The style of the generated images. Must be one of vivid or natural. Vivid causes the model to lean towards generating hyper-real and dramatic images. Natural causes the model to produce more natural, less hyper-real looking images. This param is only supported for dall-e-3.
     */
    style?: 'vivid' | 'natural';
    /**
     * A unique identifier representing your end-user, which can help OpenAI to monitor and detect abuse. Learn more.
     */
    user?: string;
}
interface ImageData {
    /**
     * The base64-encoded JSON of the generated image, if response_format is b64_json.
     */
    b64_json?: string;
    /**
     * The URL of the generated image, if response_format is url (default).
     */
    url?: string;
    /**
     * The prompt that was used to generate the image, if there was any revision to the prompt.
     */
    revised_prompt?: string;
}
interface ImageGenerationResponse {
    /**
     * The Unix timestamp (in seconds) when the image was created.
     */
    created: number;
    /**
     * The list of generated images.
     */
    data: ImageData[];
}
interface ImageEditRequest {
    /**
     * The image to edit. Must be a valid PNG file, less than 4MB, and square. If mask is not provided, image must have transparency, which will be used as the mask.
     */
    image: File | Blob;
    /**
     * A text description of the desired image(s). The maximum length is 1000 characters.
     */
    prompt: string;
    /**
     * An additional image whose fully transparent areas (e.g. where alpha is zero) indicate where image should be edited. Must be a valid PNG file, less than 4MB, and have the same dimensions as image.
     */
    mask?: File | Blob;
    /**
     * The model to use for image editing. Only dall-e-2 is supported at this time.
     */
    model?: string;
    /**
     * The number of images to generate. Must be between 1 and 10.
     */
    n?: number;
    /**
     * The format in which the generated images are returned. Must be one of url or b64_json.
     */
    response_format?: 'url' | 'b64_json';
    /**
     * The size of the generated images. Must be one of 256x256, 512x512, or 1024x1024.
     */
    size?: '256x256' | '512x512' | '1024x1024';
    /**
     * A unique identifier representing your end-user, which can help OpenAI to monitor and detect abuse.
     */
    user?: string;
}
interface ImageVariationRequest {
    /**
     * The image to use as the basis for the variation(s). Must be a valid PNG file, less than 4MB, and square.
     */
    image: File | Blob;
    /**
     * The model to use for image variation. Only dall-e-2 is supported at this time.
     */
    model?: string;
    /**
     * The number of images to generate. Must be between 1 and 10.
     */
    n?: number;
    /**
     * The format in which the generated images are returned. Must be one of url or b64_json.
     */
    response_format?: 'url' | 'b64_json';
    /**
     * The size of the generated images. Must be one of 256x256, 512x512, or 1024x1024.
     */
    size?: '256x256' | '512x512' | '1024x1024';
    /**
     * A unique identifier representing your end-user, which can help OpenAI to monitor and detect abuse.
     */
    user?: string;
}
type ImageEditResponse = ImageGenerationResponse;
type ImageVariationResponse = ImageGenerationResponse;
/**
 * Supported image generation models
 */
declare const IMAGE_MODELS: {
    readonly DALL_E_2: "dall-e-2";
    readonly DALL_E_3: "dall-e-3";
    readonly MINIMAX_IMAGE: "minimax-image";
};
type ImageModel = typeof IMAGE_MODELS[keyof typeof IMAGE_MODELS];
/**
 * Model-specific capabilities and constraints
 */
declare const IMAGE_MODEL_CAPABILITIES: {
    readonly "dall-e-2": {
        readonly maxPromptLength: 1000;
        readonly supportedSizes: readonly ["256x256", "512x512", "1024x1024"];
        readonly supportedQualities: readonly ["standard"];
        readonly supportedStyles: readonly [];
        readonly maxImages: 10;
        readonly supportsEdit: true;
        readonly supportsVariation: true;
    };
    readonly "dall-e-3": {
        readonly maxPromptLength: 4000;
        readonly supportedSizes: readonly ["1024x1024", "1792x1024", "1024x1792"];
        readonly supportedQualities: readonly ["standard", "hd"];
        readonly supportedStyles: readonly ["vivid", "natural"];
        readonly maxImages: 1;
        readonly supportsEdit: false;
        readonly supportsVariation: false;
    };
    readonly "minimax-image": {
        readonly maxPromptLength: 2000;
        readonly supportedSizes: readonly ["1024x1024", "1792x1024", "1024x1792"];
        readonly supportedQualities: readonly ["standard", "hd"];
        readonly supportedStyles: readonly ["vivid", "natural"];
        readonly maxImages: 4;
        readonly supportsEdit: false;
        readonly supportsVariation: false;
    };
};
/**
 * Default values for image generation requests
 */
declare const IMAGE_DEFAULTS: {
    readonly model: "dall-e-3";
    readonly n: 1;
    readonly quality: "standard";
    readonly response_format: "url";
    readonly size: "1024x1024";
    readonly style: "vivid";
};
/**
 * Async image generation request extending the basic generation request
 */
interface AsyncImageGenerationRequest extends ImageGenerationRequest {
    /**
     * The webhook URL to receive the result when generation is complete
     */
    webhook_url?: string;
    /**
     * Additional metadata to include with the webhook callback
     */
    webhook_metadata?: Record<string, unknown>;
    /**
     * The timeout for the generation task in seconds (1-3600)
     */
    timeout_seconds?: number;
}
/**
 * Task status for async operations
 */
type TaskStatus = 'pending' | 'running' | 'completed' | 'failed' | 'cancelled' | 'timedout';
/**
 * Response from an async image generation request
 */
interface AsyncImageGenerationResponse {
    /**
     * The unique task identifier
     */
    task_id: string;
    /**
     * The current status of the task
     */
    status: TaskStatus;
    /**
     * The progress percentage (0-100)
     */
    progress: number;
    /**
     * An optional progress message
     */
    message?: string;
    /**
     * The estimated time to completion in seconds
     */
    estimated_time_to_completion?: number;
    /**
     * When the task was created
     */
    created_at: string;
    /**
     * When the task was last updated
     */
    updated_at: string;
    /**
     * The generation result, available when status is 'completed'
     */
    result?: ImageGenerationResponse;
    /**
     * Error information if the task failed
     */
    error?: string;
}
/**
 * Options for polling task status
 */
interface TaskPollingOptions$1 {
    /**
     * The polling interval in milliseconds (default: 1000)
     */
    intervalMs?: number;
    /**
     * The maximum polling timeout in milliseconds (default: 300000 - 5 minutes)
     */
    timeoutMs?: number;
    /**
     * Whether to use exponential backoff for polling intervals (default: true)
     */
    useExponentialBackoff?: boolean;
    /**
     * The maximum interval between polls in milliseconds when using exponential backoff (default: 10000)
     */
    maxIntervalMs?: number;
}

/**
 * Audio API models for Conduit Core client library
 * Supports speech-to-text, text-to-speech, and audio translation capabilities
 */

type AudioFormat = 'mp3' | 'wav' | 'flac' | 'ogg' | 'aac' | 'opus' | 'pcm' | 'm4a' | 'webm';
type TranscriptionFormat = 'json' | 'text' | 'srt' | 'vtt' | 'verbose_json';
type TimestampGranularity = 'segment' | 'word';
type TextToSpeechModel = 'tts-1' | 'tts-1-hd' | 'elevenlabs-tts' | 'azure-tts' | 'openai-tts';
type Voice = 'alloy' | 'echo' | 'fable' | 'onyx' | 'nova' | 'shimmer' | 'rachel' | 'adam' | 'antoni' | 'arnold' | 'josh' | 'sam';
type TranscriptionModel = 'whisper-1' | 'whisper-large' | 'deepgram-nova' | 'azure-stt' | 'openai-whisper';
interface AudioFile {
    /** The audio file data as Buffer, Blob, or base64 string */
    data: Buffer | Blob | string;
    /** The filename of the audio file */
    filename: string;
    /** The MIME type of the audio file */
    contentType?: string;
}
interface VoiceSettings {
    /** Voice stability (0.0 to 1.0) */
    stability?: number;
    /** Voice similarity boost (0.0 to 1.0) */
    similarity_boost?: number;
    /** Voice style exaggeration (0.0 to 1.0) */
    style?: number;
    /** Use speaker boost for enhanced clarity */
    use_speaker_boost?: boolean;
}
interface AudioTranscriptionRequest {
    /** The audio file to transcribe */
    file: AudioFile;
    /** The model to use for transcription */
    model: TranscriptionModel;
    /** The language of the input audio (ISO-639-1 format, e.g., 'en', 'es') */
    language?: string;
    /** An optional text to guide the model's style or continue a previous audio segment */
    prompt?: string;
    /** The format of the transcript output */
    response_format?: TranscriptionFormat;
    /** The sampling temperature (0 to 1) */
    temperature?: number;
    /** The timestamp granularities to populate for this transcription */
    timestamp_granularities?: TimestampGranularity[];
}
interface AudioTranscriptionResponse {
    /** The transcribed text */
    text: string;
    /** The task performed (e.g., 'transcribe') */
    task?: string;
    /** The language of the input audio */
    language?: string;
    /** The duration of the input audio in seconds */
    duration?: number;
    /** Array of transcription segments with timestamps */
    segments?: TranscriptionSegment[];
    /** Array of words with timestamps (if word-level timestamps requested) */
    words?: TranscriptionWord[];
    /** Token usage information */
    usage?: Usage;
}
interface TranscriptionSegment {
    /** Unique identifier of the segment */
    id: number;
    /** Seek offset of the segment */
    seek: number;
    /** Start time of the segment in seconds */
    start: number;
    /** End time of the segment in seconds */
    end: number;
    /** Text content of the segment */
    text: string;
    /** Array of token IDs for the text content */
    tokens?: number[];
    /** Temperature parameter used for generation */
    temperature?: number;
    /** Average logprob of the segment */
    avg_logprob?: number;
    /** Compression ratio of the segment */
    compression_ratio?: number;
    /** Probability of no speech */
    no_speech_prob?: number;
}
interface TranscriptionWord {
    /** The text content of the word */
    word: string;
    /** Start time of the word in seconds */
    start: number;
    /** End time of the word in seconds */
    end: number;
}
interface AudioTranslationRequest {
    /** The audio file to translate */
    file: AudioFile;
    /** The model to use for translation */
    model: TranscriptionModel;
    /** An optional text to guide the model's style or continue a previous audio segment */
    prompt?: string;
    /** The format of the transcript output */
    response_format?: TranscriptionFormat;
    /** The sampling temperature (0 to 1) */
    temperature?: number;
}
interface AudioTranslationResponse {
    /** The translated text (always in English) */
    text: string;
    /** The task performed (e.g., 'translate') */
    task?: string;
    /** The language of the input audio */
    language?: string;
    /** The duration of the input audio in seconds */
    duration?: number;
    /** Array of translation segments with timestamps */
    segments?: TranscriptionSegment[];
    /** Array of words with timestamps (if word-level timestamps requested) */
    words?: TranscriptionWord[];
    /** Token usage information */
    usage?: Usage;
}
interface TextToSpeechRequest {
    /** The model to use for speech generation */
    model: TextToSpeechModel;
    /** The text to convert to speech (max 4096 characters) */
    input: string;
    /** The voice to use for speech generation */
    voice: Voice;
    /** The format to audio in */
    response_format?: AudioFormat;
    /** The speed of the generated audio (0.25 to 4.0) */
    speed?: number;
    /** Advanced voice settings (for compatible providers) */
    voice_settings?: VoiceSettings;
}
interface TextToSpeechResponse {
    /** The generated audio data as Buffer */
    audio: Buffer;
    /** The format of the returned audio */
    format: AudioFormat;
    /** Additional metadata about the generation */
    metadata?: {
        /** Duration of the generated audio in seconds */
        duration?: number;
        /** Size of the audio data in bytes */
        size?: number;
        /** Sample rate of the audio */
        sample_rate?: number;
        /** Number of audio channels */
        channels?: number;
    };
    /** Token usage information (if applicable) */
    usage?: Usage;
}
interface HybridAudioRequest {
    /** The input audio file for processing */
    file: AudioFile;
    /** The model configuration for each stage */
    models: {
        /** Speech-to-text model */
        transcription: TranscriptionModel;
        /** Chat completion model for LLM processing */
        chat: string;
        /** Text-to-speech model */
        speech: TextToSpeechModel;
    };
    /** Voice configuration for TTS output */
    voice: Voice;
    /** System prompt for the LLM stage */
    system_prompt?: string;
    /** Additional context for the conversation */
    context?: string;
    /** Language settings */
    language?: string;
    /** Temperature settings for each stage */
    temperature?: {
        transcription?: number;
        chat?: number;
    };
    /** Voice settings for TTS */
    voice_settings?: VoiceSettings;
    /** Session ID for conversation continuity */
    session_id?: string;
}
interface HybridAudioResponse {
    /** The generated audio response */
    audio: Buffer;
    /** The transcribed input text */
    transcription: string;
    /** The LLM's text response */
    llm_response: string;
    /** Metadata for each processing stage */
    stages: {
        transcription: {
            duration: number;
            confidence?: number;
            language?: string;
        };
        llm: {
            duration: number;
            tokens_used: number;
            model_used: string;
        };
        speech: {
            duration: number;
            audio_duration: number;
            format: AudioFormat;
        };
    };
    /** Combined usage statistics */
    usage: {
        transcription_tokens?: number;
        llm_tokens: Usage;
        total_processing_time_ms: number;
    };
    /** Session information */
    session_id?: string;
}
interface RealtimeConnectionRequest {
    /** The model to use for real-time processing */
    model: string;
    /** Voice configuration for real-time TTS */
    voice?: Voice;
    /** Audio format for input/output */
    audio_format?: AudioFormat;
    /** Sample rate for audio processing */
    sample_rate?: number;
    /** Whether to enable voice activity detection */
    enable_vad?: boolean;
    /** Session configuration */
    session_config?: RealtimeSessionConfig;
}
interface RealtimeSessionConfig {
    /** Instructions for the assistant */
    instructions?: string;
    /** Audio input/output configuration */
    input_audio_format?: AudioFormat;
    output_audio_format?: AudioFormat;
    /** Voice configuration */
    voice?: Voice;
    /** Model configuration */
    model?: string;
    /** Temperature for responses */
    temperature?: number;
    /** Maximum response tokens */
    max_response_output_tokens?: number;
    /** Tools available to the assistant */
    tools?: Array<{
        type: 'function';
        name: string;
        description?: string;
        parameters?: Record<string, unknown>;
    }>;
    /** Turn detection configuration */
    turn_detection?: {
        type: 'server_vad' | 'none';
        threshold?: number;
        prefix_padding_ms?: number;
        silence_duration_ms?: number;
    };
}
interface RealtimeMessage {
    /** The type of real-time message */
    type: 'session.created' | 'session.updated' | 'input_audio_buffer.append' | 'input_audio_buffer.commit' | 'input_audio_buffer.clear' | 'conversation.item.create' | 'response.create' | 'response.cancel' | 'error';
    /** The message data */
    data?: Record<string, unknown>;
    /** Message ID for tracking */
    event_id?: string;
}
interface RealtimeSession {
    /** Unique session identifier */
    id: string;
    /** Current session status */
    status: 'active' | 'idle' | 'ended' | 'error';
    /** Session configuration */
    config: RealtimeSessionConfig;
    /** Connection metadata */
    connection: {
        created_at: string;
        last_activity: string;
        duration_seconds: number;
    };
    /** Usage statistics for the session */
    usage: {
        total_audio_minutes: number;
        total_tokens: number;
        input_tokens: number;
        output_tokens: number;
    };
}
interface AudioMetadata {
    /** Duration in seconds */
    duration: number;
    /** File size in bytes */
    size: number;
    /** Audio format */
    format: AudioFormat;
    /** Sample rate in Hz */
    sample_rate: number;
    /** Number of channels */
    channels: number;
    /** Bit depth */
    bit_depth?: number;
    /** Bitrate in kbps */
    bitrate?: number;
}
interface AudioProcessingOptions {
    /** Maximum file size in bytes (default: 25MB) */
    max_file_size?: number;
    /** Supported audio formats */
    supported_formats?: AudioFormat[];
    /** Quality settings */
    quality?: 'low' | 'medium' | 'high' | 'ultra';
    /** Whether to normalize audio */
    normalize?: boolean;
    /** Whether to remove noise */
    denoise?: boolean;
}
interface AudioError {
    code: 'invalid_audio_format' | 'file_too_large' | 'audio_too_long' | 'unsupported_language' | 'transcription_failed' | 'synthesis_failed' | 'realtime_connection_failed';
    message: string;
    details?: Record<string, unknown>;
}
interface AudioValidation {
    /** Validate audio file format and size */
    validateAudioFile(file: AudioFile, options?: AudioProcessingOptions): Promise<boolean>;
    /** Get audio metadata */
    getAudioMetadata(file: AudioFile): Promise<AudioMetadata>;
    /** Convert audio format */
    convertAudioFormat(file: AudioFile, targetFormat: AudioFormat): Promise<AudioFile>;
}

/**
 * Request for creating embeddings
 */
interface EmbeddingRequest {
    /**
     * Input text(s) to generate embeddings for
     */
    input: string | string[];
    /**
     * ID of the model to use
     */
    model: string;
    /**
     * The format to return the embeddings in
     * Can be either "float" or "base64"
     * Default: "float"
     */
    encoding_format?: 'float' | 'base64';
    /**
     * The number of dimensions the resulting output embeddings should have
     * Only supported in text-embedding-3 and later models
     */
    dimensions?: number;
    /**
     * A unique identifier representing your end-user
     */
    user?: string;
}
/**
 * Response from creating embeddings
 */
interface EmbeddingResponse {
    /**
     * The list of embeddings generated
     */
    data: EmbeddingData[];
    /**
     * The model used for embedding generation
     */
    model: string;
    /**
     * The object type, always "embedding"
     */
    object: 'list';
    /**
     * Usage statistics for the request
     */
    usage: EmbeddingUsage;
}
/**
 * Individual embedding data
 */
interface EmbeddingData {
    /**
     * The embedding vector represented as an array of floats or base64 encoded string
     */
    embedding: number[] | string;
    /**
     * The index of the embedding in the list of embeddings
     */
    index: number;
    /**
     * The object type, always "embedding"
     */
    object: 'embedding';
}
/**
 * Usage statistics for embeddings
 */
interface EmbeddingUsage {
    /**
     * The number of tokens used by the prompt
     */
    prompt_tokens: number;
    /**
     * The total number of tokens used by the request
     */
    total_tokens: number;
}
/**
 * Available embedding models
 */
declare const EmbeddingModels: {
    /**
     * OpenAI text-embedding-ada-002
     * Dimensions: 1536
     */
    readonly ADA_002: "text-embedding-ada-002";
    /**
     * OpenAI text-embedding-3-small
     * Dimensions: 1536 (can be reduced)
     */
    readonly EMBEDDING_3_SMALL: "text-embedding-3-small";
    /**
     * OpenAI text-embedding-3-large
     * Dimensions: 3072 (can be reduced)
     */
    readonly EMBEDDING_3_LARGE: "text-embedding-3-large";
    /**
     * Default embedding model
     */
    readonly DEFAULT: "text-embedding-3-small";
};
/**
 * Encoding format options
 */
declare const EmbeddingEncodingFormats: {
    /**
     * Return embeddings as array of floats (default)
     */
    readonly FLOAT: "float";
    /**
     * Return embeddings as base64-encoded string
     */
    readonly BASE64: "base64";
};
/**
 * Validates an embedding request
 */
declare function validateEmbeddingRequest(request: EmbeddingRequest): void;
/**
 * Converts embedding response to float array
 */
declare function convertEmbeddingToFloatArray(embedding: number[] | string): number[];
/**
 * Calculates cosine similarity between two embedding vectors
 */
declare function calculateCosineSimilarity(embedding1: number[], embedding2: number[]): number;

/**
 * Base interface for all streaming chunks
 */
interface BaseStreamChunk {
    id: string;
    object: string;
    created: number;
}
/**
 * Generic streaming response type
 */
interface StreamingResponse<T extends BaseStreamChunk> {
    /**
     * Async iterator for consuming stream chunks
     */
    [Symbol.asyncIterator](): AsyncIterator<T>;
    /**
     * Collects all chunks and returns the complete response
     */
    toArray(): Promise<T[]>;
    /**
     * Transforms the stream with a custom function
     */
    map<U>(fn: (chunk: T) => U | Promise<U>): AsyncGenerator<U, void, unknown>;
    /**
     * Filters stream chunks based on a predicate
     */
    filter(predicate: (chunk: T) => boolean | Promise<boolean>): AsyncGenerator<T, void, unknown>;
    /**
     * Takes only the first n chunks from the stream
     */
    take(n: number): AsyncGenerator<T, void, unknown>;
    /**
     * Skips the first n chunks from the stream
     */
    skip(n: number): AsyncGenerator<T, void, unknown>;
    /**
     * Cancels the stream
     */
    cancel(): void;
}

/**
 * Enhanced SSE (Server-Sent Events) event types supported by Conduit.
 * These event types allow for richer streaming responses that include
 * performance metrics and other metadata alongside content.
 *
 * @enum {string}
 * @since 0.3.0
 */
declare enum EnhancedSSEEventType {
    /** Regular content event containing chat completion chunks */
    Content = "content",
    /** Live performance metrics during streaming */
    Metrics = "metrics",
    /** Final performance metrics at stream completion */
    MetricsFinal = "metrics-final",
    /** Error events during streaming */
    Error = "error",
    /** Stream completion marker */
    Done = "done"
}
/**
 * Performance metrics sent during streaming (matches Core API format).
 * These metrics provide real-time insights into the streaming performance.
 *
 * @interface StreamingMetrics
 * @since 0.3.0
 *
 * @example
 * ```typescript
 * {
 *   request_id: 'req-123',
 *   elapsed_ms: 1500,
 *   tokens_generated: 25,
 *   current_tokens_per_second: 16.67,
 *   time_to_first_token_ms: 120,
 *   avg_inter_token_latency_ms: 60
 * }
 * ```
 */
interface StreamingMetrics {
    /** Unique identifier for the streaming request */
    request_id?: string;
    /** Total elapsed time in milliseconds since stream start */
    elapsed_ms?: number;
    /** Number of tokens generated so far */
    tokens_generated?: number;
    /** Current token generation rate (tokens per second) */
    current_tokens_per_second?: number;
    /** Time to first token in milliseconds */
    time_to_first_token_ms?: number;
    /** Average latency between tokens in milliseconds */
    avg_inter_token_latency_ms?: number;
}
/**
 * Final performance metrics sent at the end of a streaming response.
 * Provides comprehensive performance statistics for the entire request.
 *
 * @interface FinalMetrics
 * @since 0.3.0
 *
 * @example
 * ```typescript
 * {
 *   total_latency_ms: 2500,
 *   time_to_first_token_ms: 150,
 *   tokens_per_second: 42.0,
 *   prompt_tokens_per_second: 200,
 *   completion_tokens_per_second: 42.0,
 *   provider: 'openai',
 *   model: 'gpt-4',
 *   streaming: true,
 *   avg_inter_token_latency_ms: 59.5,
 *   prompt_tokens: 50,
 *   completion_tokens: 105,
 *   total_tokens: 155
 * }
 * ```
 */
interface FinalMetrics {
    /** Total end-to-end latency in milliseconds */
    total_latency_ms?: number;
    /** Time to first token in milliseconds */
    time_to_first_token_ms?: number;
    /** Overall tokens per second for the completion */
    tokens_per_second?: number;
    /** Processing speed for prompt tokens (tokens/second) */
    prompt_tokens_per_second?: number;
    /** Generation speed for completion tokens (tokens/second) */
    completion_tokens_per_second?: number;
    /** LLM provider name (e.g., 'openai', 'anthropic') */
    provider?: string;
    /** Model identifier (e.g., 'gpt-4', 'claude-3') */
    model?: string;
    /** Whether streaming was used for this request */
    streaming?: boolean;
    /** Average latency between consecutive tokens in milliseconds */
    avg_inter_token_latency_ms?: number;
    /** Number of tokens in the prompt */
    prompt_tokens?: number;
    /** Number of tokens in the completion */
    completion_tokens?: number;
    /** Total token count (prompt + completion) */
    total_tokens?: number;
}
/**
 * Enhanced streaming event that preserves SSE event types.
 * Wraps different types of data (content, metrics, errors) with their event type.
 * Does not extend BaseStreamChunk as it represents wrapped events.
 *
 * @interface EnhancedStreamEvent
 * @since 0.3.0
 *
 * @example
 * ```typescript
 * // Content event
 * {
 *   type: 'content',
 *   data: { id: 'chatcmpl-123', object: 'chat.completion.chunk', ... }
 * }
 *
 * // Metrics event
 * {
 *   type: 'metrics',
 *   data: { current_tokens_per_second: 42.5, tokens_generated: 30 }
 * }
 * ```
 */
interface EnhancedStreamEvent {
    /** The type of SSE event */
    type: EnhancedSSEEventType;
    /** The event data, type depends on the event type */
    data: ChatCompletionChunk$1 | StreamingMetrics | FinalMetrics | string;
}
/**
 * Type guard to check if data is a ChatCompletionChunk.
 *
 * @param {unknown} data - The data to check
 * @returns {boolean} True if data is a ChatCompletionChunk
 * @since 0.3.0
 *
 * @example
 * ```typescript
 * if (isChatCompletionChunk(event.data)) {
 *   // TypeScript now knows event.data is ChatCompletionChunk
 *   console.log(event.data.choices[0].delta.content);
 * }
 * ```
 */
declare function isChatCompletionChunk(data: unknown): data is ChatCompletionChunk$1;
/**
 * Type guard to check if data is StreamingMetrics.
 *
 * @param {unknown} data - The data to check
 * @returns {boolean} True if data is StreamingMetrics
 * @since 0.3.0
 *
 * @example
 * ```typescript
 * if (isStreamingMetrics(event.data)) {
 *   // TypeScript now knows event.data is StreamingMetrics
 *   console.log(`Speed: ${event.data.current_tokens_per_second} tokens/sec`);
 * }
 * ```
 */
declare function isStreamingMetrics(data: unknown): data is StreamingMetrics;
/**
 * Type guard to check if data is FinalMetrics.
 *
 * @param {unknown} data - The data to check
 * @returns {boolean} True if data is FinalMetrics
 * @since 0.3.0
 *
 * @example
 * ```typescript
 * if (isFinalMetrics(event.data)) {
 *   // TypeScript now knows event.data is FinalMetrics
 *   console.log(`Total tokens: ${event.data.total_tokens}`);
 *   console.log(`Average speed: ${event.data.tokens_per_second} tokens/sec`);
 * }
 * ```
 */
declare function isFinalMetrics(data: unknown): data is FinalMetrics;

/**
 * Enhanced streaming response interface for handling heterogeneous SSE event types.
 * Unlike the standard StreamingResponse, this doesn't require events to extend BaseStreamChunk,
 * allowing for different event types (content, metrics, errors) in the same stream.
 *
 * @interface EnhancedStreamingResponse
 * @template T The type of events in the stream (typically EnhancedStreamEvent)
 * @since 0.3.0
 *
 * @example
 * ```typescript
 * const stream = await client.chat.createEnhancedStream({
 *   model: 'gpt-4',
 *   messages: [{ role: 'user', content: 'Hello!' }],
 *   stream: true
 * });
 *
 * // Iterate over events
 * for await (const event of stream) {
 *   switch (event.type) {
 *     case 'content':
 *       console.log('Content:', event.data);
 *       break;
 *     case 'metrics':
 *       console.log('Metrics:', event.data);
 *       break;
 *   }
 * }
 *
 * // Or collect all events
 * const allEvents = await stream.toArray();
 *
 * // Cancel the stream early
 * stream.cancel();
 * ```
 */
interface EnhancedStreamingResponse<T> {
    /**
     * Async iterator for consuming stream events.
     * Allows using for-await-of loops to process events as they arrive.
     *
     * @returns {AsyncIterator<T>} An async iterator that yields events
     */
    [Symbol.asyncIterator](): AsyncIterator<T>;
    /**
     * Collects all remaining events in the stream and returns them as an array.
     * This will consume the entire stream, so it should not be used with large streams.
     *
     * @returns {Promise<T[]>} A promise that resolves to an array of all events
     * @throws {Error} If the stream encounters an error during collection
     */
    toArray(): Promise<T[]>;
    /**
     * Cancels the stream, stopping any ongoing data transfer.
     * This is useful for early termination of long-running streams.
     *
     * @returns {void}
     */
    cancel(): void;
}

type ChatCompletionRequest = components['schemas']['ChatCompletionRequest'];
type ChatCompletionResponse = components['schemas']['ChatCompletionResponse'];
type ChatCompletionChunk = components['schemas']['ChatCompletionChunk'];
/**
 * Type-safe Chat service using generated OpenAPI types and native fetch
 */
declare class FetchChatService extends FetchBasedClient {
    constructor(config: ClientConfig);
    /**
     * Create a chat completion with full type safety
     * Overloaded to handle both streaming and non-streaming responses
     */
    create(request: ChatCompletionRequest & {
        stream?: false;
    }, options?: RequestOptions): Promise<ChatCompletionResponse>;
    create(request: ChatCompletionRequest & {
        stream: true;
    }, options?: RequestOptions): Promise<StreamingResponse<ChatCompletionChunk>>;
    private createCompletion;
    private createStream;
    protected createStreamingRequest(request: ChatCompletionRequest, options?: RequestOptions): Promise<Response>;
    /**
     * Count tokens in messages (placeholder - actual implementation would use tiktoken)
     */
    countTokens(messages: ChatCompletionRequest['messages'], _model?: string): number;
    /**
     * Validate that a request fits within model context limits
     */
    validateContextLength(request: ChatCompletionRequest, maxTokens?: number): {
        valid: boolean;
        tokens: number;
        limit: number;
    };
    /**
     * Creates an enhanced streaming chat completion that preserves SSE event types.
     * This allows access to metrics events and other enhanced streaming features.
     *
     * @param request - The chat completion request with stream: true
     * @param options - Optional request configuration
     * @returns A streaming response with enhanced events
     *
     * @example
     * const stream = await chatService.createEnhancedStream({
     *   model: 'gpt-4',
     *   messages: [{ role: 'user', content: 'Hello!' }],
     *   stream: true
     * });
     *
     * for await (const event of stream) {
     *   switch (event.type) {
     *     case 'content':
     *       console.log('Content:', event.data);
     *       break;
     *     case 'metrics':
     *       console.log('Metrics:', event.data);
     *       break;
     *   }
     * }
     */
    createEnhancedStream(request: ChatCompletionRequest & {
        stream: true;
    }, options?: RequestOptions): Promise<EnhancedStreamingResponse<EnhancedStreamEvent>>;
    /**
     * Converts legacy function parameters to the tools format
     * for backward compatibility
     */
    protected convertLegacyFunctions(request: any): any;
}

/**
 * Service for audio operations including speech-to-text, text-to-speech, and audio translation.
 * Provides OpenAI-compatible audio API endpoints for transcription, translation, and speech synthesis.
 *
 * @example
 * ```typescript
 * // Initialize the service
 * const audio = client.audio;
 *
 * // Transcribe audio
 * const transcription = await audio.transcribe({
 *   file: AudioUtils.fromBuffer(audioBuffer, 'speech.mp3'),
 *   model: 'whisper-1'
 * });
 *
 * // Generate speech
 * const speech = await audio.generateSpeech({
 *   model: 'tts-1',
 *   input: 'Hello, world!',
 *   voice: 'alloy'
 * });
 * ```
 */
declare class AudioService extends FetchBasedClient {
    constructor(client: FetchBasedClient);
    /**
     * Transcribes audio to text using speech-to-text models.
     * Supports multiple audio formats and languages with customizable output formats.
     *
     * @param request - The transcription request
     * @param options - Optional request options
     * @returns Promise resolving to transcription response
     *
     * @example
     * ```typescript
     * // Basic transcription
     * const result = await audio.transcribe({
     *   file: AudioUtils.fromBuffer(audioBuffer, 'audio.mp3'),
     *   model: 'whisper-1'
     * });
     * console.log(result.text);
     *
     * // With language and timestamps
     * const detailed = await audio.transcribe({
     *   file: AudioUtils.fromBuffer(audioBuffer, 'audio.mp3'),
     *   model: 'whisper-1',
     *   language: 'en',
     *   response_format: 'verbose_json',
     *   timestamp_granularities: ['word', 'segment']
     * });
     * ```
     */
    transcribe(request: AudioTranscriptionRequest, options?: RequestOptions): Promise<AudioTranscriptionResponse>;
    /**
     * Translates audio to English text using speech-to-text models.
     * @param request The translation request
     * @param options Optional request options
     * @returns Promise resolving to translation response
     */
    translate(request: AudioTranslationRequest, options?: RequestOptions): Promise<AudioTranslationResponse>;
    /**
     * Generates speech from text using text-to-speech models.
     * Supports multiple voices and audio formats with adjustable speed.
     *
     * @param request - The speech generation request
     * @param options - Optional request options
     * @returns Promise resolving to speech response with audio data
     *
     * @example
     * ```typescript
     * // Generate speech with default settings
     * const speech = await audio.generateSpeech({
     *   model: 'tts-1',
     *   input: 'Welcome to our service!',
     *   voice: 'nova'
     * });
     *
     * // High quality with specific format
     * const hdSpeech = await audio.generateSpeech({
     *   model: 'tts-1-hd',
     *   input: 'This is high quality audio.',
     *   voice: 'alloy',
     *   response_format: 'mp3',
     *   speed: 1.0
     * });
     *
     * // Save to file
     * fs.writeFileSync('output.mp3', speech.audio);
     * ```
     */
    generateSpeech(request: TextToSpeechRequest, options?: RequestOptions): Promise<TextToSpeechResponse>;
    /**
     * Processes audio through the hybrid pipeline (STT + LLM + TTS).
     * @param request The hybrid audio processing request
     * @param options Optional request options
     * @returns Promise resolving to hybrid audio response
     */
    processHybrid(request: HybridAudioRequest, options?: RequestOptions): Promise<HybridAudioResponse>;
    /**
     * Creates a simple transcription request for quick speech-to-text conversion.
     * @param audioFile The audio file to transcribe
     * @param model Optional model to use (defaults to 'whisper-1')
     * @param language Optional language code
     * @returns Promise resolving to transcription text
     */
    quickTranscribe(audioFile: AudioFile, model?: TranscriptionModel, language?: string): Promise<string>;
    /**
     * Creates a simple speech generation request for quick text-to-speech conversion.
     * @param text The text to convert to speech
     * @param voice Optional voice to use (defaults to 'alloy')
     * @param model Optional model to use (defaults to 'tts-1')
     * @returns Promise resolving to audio buffer
     */
    quickSpeak(text: string, voice?: Voice, model?: TextToSpeechModel): Promise<Buffer>;
    /**
     * Validates an audio transcription request.
     * @private
     */
    private validateTranscriptionRequest;
    /**
     * Validates an audio translation request.
     * @private
     */
    private validateTranslationRequest;
    /**
     * Validates a text-to-speech request.
     * @private
     */
    private validateSpeechRequest;
    /**
     * Validates a hybrid audio request.
     * @private
     */
    private validateHybridRequest;
    /**
     * Validates an audio file.
     * @private
     */
    private validateAudioFile;
    /**
     * Creates FormData for audio file uploads.
     * @private
     */
    private createAudioFormData;
}
/**
 * Audio utility functions for working with audio files.
 * Provides helper methods for creating AudioFile objects from various sources.
 *
 * @example
 * ```typescript
 * // From Buffer (Node.js)
 * const audioFile = AudioUtils.fromBuffer(buffer, 'audio.mp3', 'audio/mpeg');
 *
 * // From Blob (Browser)
 * const audioFile = AudioUtils.fromBlob(blob, 'recording.wav');
 *
 * // From Base64
 * const audioFile = AudioUtils.fromBase64(base64String, 'speech.mp3');
 * ```
 */
declare class AudioUtils {
    /**
     * Creates an AudioFile from a Buffer with specified filename.
     */
    static fromBuffer(data: Buffer, filename: string, contentType?: string): AudioFile;
    /**
     * Creates an AudioFile from a Blob with specified filename.
     */
    static fromBlob(data: Blob, filename: string): AudioFile;
    /**
     * Creates an AudioFile from a base64 string with specified filename.
     */
    static fromBase64(data: string, filename: string, contentType?: string): AudioFile;
    /**
     * Gets audio file metadata (basic validation).
     */
    static getBasicMetadata(file: AudioFile): AudioMetadata;
    /**
     * Validates if the audio format is supported.
     */
    static isFormatSupported(format: string): boolean;
    /**
     * Gets the appropriate content type for an audio format.
     */
    static getContentType(format: AudioFormat): string;
}

/**
 * Health check response from the API
 */
interface HealthCheckResponse {
    /** Overall health status */
    status: string;
    /** Total duration of all health checks in milliseconds */
    totalDuration: number;
    /** List of individual health check results */
    checks: HealthCheckItem[];
}
/**
 * Individual health check item
 */
interface HealthCheckItem {
    /** Name of the health check */
    name: string;
    /** Status of this health check */
    status: string;
    /** Description of the health check result */
    description?: string;
    /** Duration of this health check in milliseconds */
    duration: number;
    /** Additional data associated with this health check */
    data?: Record<string, unknown>;
    /** Exception information if the health check failed */
    exception?: string;
    /** Tags associated with this health check */
    tags?: string[];
}
/**
 * Health check status enumeration
 */
declare enum HealthStatus {
    /** The component is healthy */
    Healthy = 0,
    /** The component is degraded but still functioning */
    Degraded = 1,
    /** The component is unhealthy */
    Unhealthy = 2
}
/**
 * Health check options for customizing health check behavior
 */
interface HealthCheckOptions {
    /** Timeout for health checks */
    timeout?: number;
    /** Whether to include exception details in the response */
    includeExceptionDetails?: boolean;
    /** Tags to filter health checks by */
    tags?: string[];
    /** Failure status to return if checks fail */
    failureStatus?: HealthStatus;
}
/**
 * Simplified health status for quick checks
 */
interface SimpleHealthStatus {
    /** Whether the system is healthy */
    isHealthy: boolean;
    /** Brief status message */
    message: string;
    /** Timestamp of the health check */
    timestamp: Date;
    /** Response time in milliseconds */
    responseTimeMs: number;
}
/**
 * Health summary with key metrics
 */
interface HealthSummary {
    /** Overall status */
    overallStatus: string;
    /** Total duration */
    totalDuration: number;
    /** Check counts breakdown */
    checkCounts: {
        total: number;
        healthy: number;
        degraded: number;
        unhealthy: number;
    };
    /** Health percentage */
    healthPercentage: number;
    /** Components summary */
    components: Array<{
        name: string;
        status: string;
        duration: number;
        hasData: boolean;
    }>;
}
/**
 * Options for waiting for health status
 */
interface WaitForHealthOptions {
    /** Maximum time to wait */
    timeout: number;
    /** Interval between health check polls */
    pollingInterval?: number;
}

declare class HealthService extends FetchBasedClient {
    constructor(client: FetchBasedClient);
    check(options?: HealthCheckOptions): Promise<HealthCheckResponse>;
    waitForHealth(options?: WaitForHealthOptions): Promise<HealthCheckResponse>;
}

/**
 * Service for image generation, editing, and variation operations.
 * Provides OpenAI-compatible image API endpoints for DALL-E and other image models.
 *
 * @example
 * ```typescript
 * // Initialize the service
 * const images = client.images;
 *
 * // Generate an image
 * const result = await images.generate({
 *   prompt: 'A sunset over mountains',
 *   size: '1024x1024',
 *   quality: 'hd'
 * });
 *
 * // Edit an image
 * const edited = await images.edit({
 *   image: imageFile,
 *   prompt: 'Add a rainbow to the sky',
 *   mask: maskFile
 * });
 * ```
 */
declare class ImagesService extends FetchBasedClient {
    constructor(client: FetchBasedClient);
    /**
     * Creates an image given a text prompt.
     * Supports various sizes, styles, and quality settings based on the model.
     *
     * @param request - The image generation request
     * @param options - Optional request options
     * @returns Promise resolving to image generation response
     *
     * @example
     * ```typescript
     * // Basic image generation
     * const result = await images.generate({
     *   prompt: 'A serene lake at sunset',
     *   n: 1
     * });
     * console.log(result.data[0].url);
     *
     * // High quality with specific size
     * const hdResult = await images.generate({
     *   prompt: 'A futuristic city skyline',
     *   model: 'dall-e-3',
     *   size: '1792x1024',
     *   quality: 'hd',
     *   style: 'vivid'
     * });
     *
     * // Get base64 encoded image
     * const base64Result = await images.generate({
     *   prompt: 'Abstract art',
     *   response_format: 'b64_json'
     * });
     * ```
     */
    generate(request: ImageGenerationRequest, options?: RequestOptions): Promise<ImageGenerationResponse>;
    /**
     * Creates an edited or extended image given an original image and a prompt.
     * The mask specifies which areas should be edited. Transparent areas in the mask indicate where edits should be applied.
     *
     * @param request - The image edit request
     * @param options - Optional request options
     * @returns Promise resolving to image edit response
     *
     * @example
     * ```typescript
     * // Edit with a mask
     * const edited = await images.edit({
     *   image: originalImageFile,
     *   mask: maskFile,
     *   prompt: 'Replace the sky with a starry night',
     *   n: 1
     * });
     *
     * // Edit using image transparency as mask
     * const result = await images.edit({
     *   image: transparentPngFile,
     *   prompt: 'Add a garden in the transparent area',
     *   size: '512x512'
     * });
     * ```
     */
    edit(request: ImageEditRequest, options?: RequestOptions): Promise<ImageEditResponse>;
    /**
     * Creates a variation of a given image.
     * Generates new images that maintain the same general composition but with variations.
     *
     * @param request - The image variation request
     * @param options - Optional request options
     * @returns Promise resolving to image variation response
     *
     * @example
     * ```typescript
     * // Create variations
     * const variations = await images.createVariation({
     *   image: originalImageFile,
     *   n: 3,
     *   size: '1024x1024'
     * });
     *
     * // Get variations as base64
     * const base64Variations = await images.createVariation({
     *   image: imageFile,
     *   n: 2,
     *   response_format: 'b64_json'
     * });
     * ```
     */
    createVariation(request: ImageVariationRequest, options?: RequestOptions): Promise<ImageVariationResponse>;
    /**
     * Creates an image asynchronously given a text prompt.
     * @param request The async image generation request
     * @param options Optional request options
     * @returns Promise resolving to async task information
     */
    generateAsync(request: AsyncImageGenerationRequest, options?: RequestOptions): Promise<AsyncImageGenerationResponse>;
    /**
     * Gets the status of an async image generation task.
     * @param taskId The task identifier
     * @param options Optional request options
     * @returns Promise resolving to the current task status
     */
    getTaskStatus(taskId: string, options?: RequestOptions): Promise<AsyncImageGenerationResponse>;
    /**
     * Cancels a pending or running async image generation task.
     * @param taskId The task identifier
     * @param options Optional request options
     */
    cancelTask(taskId: string, options?: RequestOptions): Promise<void>;
    /**
     * Polls an async image generation task until completion or timeout.
     * Automatically handles retries with configurable intervals and backoff.
     *
     * @param taskId - The task identifier
     * @param pollingOptions - Polling configuration options
     * @param requestOptions - Optional request options
     * @returns Promise resolving to the final generation result
     *
     * @example
     * ```typescript
     * // Start async generation
     * const task = await images.generateAsync({
     *   prompt: 'Complex artistic scene',
     *   quality: 'hd',
     *   size: '1792x1024'
     * });
     *
     * // Poll until complete with default settings
     * const result = await images.pollTaskUntilCompletion(task.task_id);
     *
     * // Custom polling configuration
     * const customResult = await images.pollTaskUntilCompletion(
     *   task.task_id,
     *   {
     *     intervalMs: 2000,
     *     timeoutMs: 300000, // 5 minutes
     *     useExponentialBackoff: true
     *   }
     * );
     * ```
     */
    pollTaskUntilCompletion(taskId: string, pollingOptions?: TaskPollingOptions$1, requestOptions?: RequestOptions): Promise<ImageGenerationResponse>;
}

/**
 * Video generation models and types for the Conduit Core API
 */

/**
 * Request for generating a video from a text prompt
 */
interface VideoGenerationRequest {
    /** The text prompt that describes what video to generate */
    prompt: string;
    /** The model to use for video generation (e.g., "minimax-video") */
    model?: string;
    /** The duration of the video in seconds. Defaults to 5 seconds */
    duration?: number;
    /** The size/resolution of the video (e.g., "1920x1080", "1280x720") */
    size?: string;
    /** Frames per second for the video. Common values: 24, 30, 60 */
    fps?: number;
    /** The style or aesthetic of the video generation */
    style?: string;
    /** The format in which the generated video is returned. Options: "url" (default) or "b64_json" */
    response_format?: 'url' | 'b64_json';
    /** A unique identifier representing your end-user */
    user?: string;
    /** Optional seed for deterministic generation */
    seed?: number;
    /** The number of videos to generate. Defaults to 1 */
    n?: number;
}
/**
 * Response from a video generation request
 */
interface VideoGenerationResponse {
    /** Unix timestamp of when the response was created */
    created: number;
    /** List of generated video data */
    data: VideoData[];
    /** The model used for generation */
    model?: string;
    /** Usage information if available */
    usage?: VideoUsage;
}
/**
 * A single generated video
 */
interface VideoData {
    /** The URL of the generated video, if response_format is "url" */
    url?: string;
    /** The base64-encoded video data, if response_format is "b64_json" */
    b64_json?: string;
    /** The revised prompt that was used for generation */
    revised_prompt?: string;
    /** Additional metadata about the generated video */
    metadata?: VideoMetadata;
}
/**
 * Usage statistics for video generation
 */
interface VideoUsage {
    /** The number of prompt tokens used */
    prompt_tokens: number;
    /** The total number of tokens used */
    total_tokens: number;
    /** The duration processed in seconds */
    duration_seconds?: number;
    /** The total processing time in seconds */
    processing_time_seconds?: number;
}
/**
 * Metadata about a generated video
 */
interface VideoMetadata {
    /** The actual duration of the generated video in seconds */
    duration?: number;
    /** The resolution of the generated video */
    resolution?: string;
    /** The frames per second of the generated video */
    fps?: number;
    /** The file size in bytes */
    file_size_bytes?: number;
    /** The video format/codec */
    format?: string;
    /** The video codec used for encoding */
    codec?: string;
    /** The audio codec used for encoding */
    audio_codec?: string;
    /** The bitrate of the video */
    bitrate?: number;
    /** The MIME type of the video file */
    mime_type?: string;
    /** The seed used for generation, if any */
    seed?: number;
}
/**
 * Request for async video generation
 */
interface AsyncVideoGenerationRequest extends VideoGenerationRequest {
    /** The webhook URL to receive the result when generation is complete */
    webhook_url?: string;
    /** Additional metadata to include with the webhook callback */
    webhook_metadata?: VideoWebhookMetadata;
    /** Additional headers to include with the webhook callback */
    webhook_headers?: Record<string, string>;
    /** The timeout for the generation task in seconds */
    timeout_seconds?: number;
}
/**
 * Response from an async video generation request
 */
interface AsyncVideoGenerationResponse {
    /** The unique task identifier */
    task_id: string;
    /** The current status of the task */
    status: VideoTaskStatus;
    /** The progress percentage (0-100) */
    progress: number;
    /** An optional progress message */
    message?: string;
    /** The estimated time to completion in seconds */
    estimated_time_to_completion?: number;
    /** When the task was created */
    created_at: string;
    /** When the task was last updated */
    updated_at: string;
    /** The generation result, available when status is Completed */
    result?: VideoGenerationResponse;
    /** Error information if the task failed */
    error?: string;
}
/**
 * The status of an async video generation task
 */
declare enum VideoTaskStatus {
    /** Task is waiting to be processed */
    Pending = "Pending",
    /** Task is currently being processed */
    Running = "Running",
    /** Task completed successfully */
    Completed = "Completed",
    /** Task failed with an error */
    Failed = "Failed",
    /** Task was cancelled */
    Cancelled = "Cancelled",
    /** Task timed out */
    TimedOut = "TimedOut"
}
/**
 * Options for polling video task status
 */
interface VideoTaskPollingOptions {
    /** The polling interval in milliseconds */
    intervalMs?: number;
    /** The maximum polling timeout in milliseconds */
    timeoutMs?: number;
    /** Whether to use exponential backoff for polling intervals */
    useExponentialBackoff?: boolean;
    /** The maximum interval between polls in milliseconds when using exponential backoff */
    maxIntervalMs?: number;
}
/**
 * Common video models supported by Conduit
 */
declare const VideoModels: {
    /** MiniMax video generation model */
    readonly MINIMAX_VIDEO: "minimax-video";
    /** Default video model */
    readonly DEFAULT: "minimax-video";
};
/**
 * Common video resolutions
 */
declare const VideoResolutions: {
    /** 720p resolution (1280x720) */
    readonly HD: "1280x720";
    /** 1080p resolution (1920x1080) */
    readonly FULL_HD: "1920x1080";
    /** Vertical 720p (720x1280) */
    readonly VERTICAL_HD: "720x1280";
    /** Vertical 1080p (1080x1920) */
    readonly VERTICAL_FULL_HD: "1080x1920";
    /** Square format (720x720) */
    readonly SQUARE: "720x720";
};
/**
 * Video response formats
 */
declare const VideoResponseFormats: {
    /** Return video as URL (default) */
    readonly URL: "url";
    /** Return video as base64-encoded JSON */
    readonly BASE64_JSON: "b64_json";
};
/**
 * Default values for video generation
 */
declare const VideoDefaults: {
    /** Default duration in seconds */
    readonly DURATION: 5;
    /** Default resolution */
    readonly RESOLUTION: "1280x720";
    /** Default frames per second */
    readonly FPS: 30;
    /** Default response format */
    readonly RESPONSE_FORMAT: "url";
    /** Default polling interval in milliseconds */
    readonly POLLING_INTERVAL_MS: 2000;
    /** Default polling timeout in milliseconds */
    readonly POLLING_TIMEOUT_MS: 600000;
    /** Default maximum polling interval in milliseconds */
    readonly MAX_POLLING_INTERVAL_MS: 30000;
};
/**
 * Capabilities of a video generation model
 */
interface VideoModelCapabilities {
    /** Maximum duration in seconds */
    maxDuration: number;
    /** Supported resolutions */
    supportedResolutions: string[];
    /** Supported FPS values */
    supportedFps: number[];
    /** Whether the model supports custom styles */
    supportsCustomStyles: boolean;
    /** Whether the model supports seed-based generation */
    supportsSeed: boolean;
    /** Maximum number of videos that can be generated in one request */
    maxVideos: number;
}
/**
 * Gets the capabilities for a specific video model
 */
declare function getVideoModelCapabilities(model: string): VideoModelCapabilities;
/**
 * Validates a video generation request
 */
declare function validateVideoGenerationRequest(request: VideoGenerationRequest): void;
/**
 * Validates an async video generation request
 */
declare function validateAsyncVideoGenerationRequest(request: AsyncVideoGenerationRequest): void;
/**
 * Base interface for webhook payloads sent by Conduit
 */
interface WebhookPayloadBase {
    /** The unique identifier for this webhook event */
    event_id: string;
    /** The type of webhook event */
    event_type: string;
    /** The timestamp when the event occurred */
    timestamp: string;
    /** The task ID associated with this event */
    task_id: string;
    /** Optional metadata provided in the original request */
    metadata?: VideoWebhookMetadata;
}
/**
 * Webhook payload sent when video generation is completed
 */
interface VideoCompletionWebhookPayload extends WebhookPayloadBase {
    /** The final status of the video generation task */
    status: VideoTaskStatus;
    /** The generated video result, if successful */
    result?: VideoGenerationResponse;
    /** Error information if the task failed */
    error?: string;
    /** The total processing time in seconds */
    processing_time_seconds?: number;
}
/**
 * Webhook payload sent to provide progress updates during video generation
 */
interface VideoProgressWebhookPayload extends WebhookPayloadBase {
    /** The current status of the video generation task */
    status: VideoTaskStatus;
    /** The progress percentage (0-100) */
    progress: number;
    /** An optional progress message */
    message?: string;
    /** The estimated time to completion in seconds */
    estimated_time_to_completion?: number;
}

/**
 * Service for video generation operations using the Conduit Core API
 */
declare class VideosService {
    private static readonly ASYNC_GENERATIONS_ENDPOINT;
    private readonly clientAdapter;
    constructor(client: FetchBasedClient);
    /**
     * @deprecated The synchronous video generation endpoint does not exist. Use generateAsync() instead.
     * This method has been removed to prevent runtime errors.
     */
    /**
     * Generates videos asynchronously from a text prompt
     */
    generateAsync(request: AsyncVideoGenerationRequest, options?: {
        signal?: AbortSignal;
    }): Promise<AsyncVideoGenerationResponse>;
    /**
     * Gets the status of an async video generation task
     */
    getTaskStatus(taskId: string, options?: {
        signal?: AbortSignal;
    }): Promise<AsyncVideoGenerationResponse>;
    /**
     * Cancels a pending or running async video generation task
     */
    cancelTask(taskId: string, options?: {
        signal?: AbortSignal;
    }): Promise<void>;
    /**
     * Polls an async video generation task until completion or timeout
     */
    pollTaskUntilCompletion(taskId: string, pollingOptions?: VideoTaskPollingOptions, options?: {
        signal?: AbortSignal;
    }): Promise<VideoGenerationResponse>;
    /**
     * Gets the capabilities of a video model
     */
    getModelCapabilities(model: string): VideoModelCapabilities;
    /**
     * Converts a VideoGenerationRequest to the API request format
     */
    private convertToApiRequest;
    /**
     * Converts an AsyncVideoGenerationRequest to the API request format
     */
    private convertToAsyncApiRequest;
}

/**
 * Model capabilities that match the ILLMClient interface.
 */
interface ModelCapabilities {
    chat: boolean;
    chat_stream: boolean;
    embeddings: boolean;
    image_generation: boolean;
    vision: boolean;
    video_generation: boolean;
    video_understanding: boolean;
    function_calling: boolean;
    tool_use: boolean;
    json_mode: boolean;
    max_tokens?: number;
    max_output_tokens?: number;
    supported_image_sizes?: string[];
    supported_video_resolutions?: string[];
    max_video_duration_seconds?: number;
}
/**
 * Represents a discovered model with its capabilities.
 */
interface DiscoveredModel {
    id: string;
    provider: string;
    display_name?: string;
    capabilities: ModelCapabilities;
    metadata?: Record<string, unknown>;
    last_verified: string;
}
/**
 * Response model for getting all models.
 */
interface ModelsDiscoveryResponse {
    data: DiscoveredModel[];
    count: number;
}
/**
 * Response model for provider-specific models.
 */
interface ProviderModelsDiscoveryResponse {
    provider: string;
    data: DiscoveredModel[];
    count: number;
}
/**
 * Specific model capabilities to test.
 */
declare enum ModelCapability {
    Chat = "Chat",
    ChatStream = "ChatStream",
    Embeddings = "Embeddings",
    ImageGeneration = "ImageGeneration",
    Vision = "Vision",
    VideoGeneration = "VideoGeneration",
    VideoUnderstanding = "VideoUnderstanding",
    FunctionCalling = "FunctionCalling",
    ToolUse = "ToolUse",
    JsonMode = "JsonMode"
}
/**
 * Response model for capability testing.
 */
interface CapabilityTestResponse {
    model: string;
    capability: string;
    supported: boolean;
}
/**
 * Individual capability test within a bulk request.
 */
interface CapabilityTest {
    model: string;
    capability: string;
}
/**
 * Request model for bulk capability testing.
 */
interface BulkCapabilityTestRequest {
    tests: CapabilityTest[];
}
/**
 * Result of a single capability test.
 */
interface CapabilityTestResult {
    model: string;
    capability: string;
    supported: boolean;
    error?: string;
}
/**
 * Response model for bulk capability testing.
 */
interface BulkCapabilityTestResponse {
    results: CapabilityTestResult[];
    totalTests: number;
    successfulTests: number;
    failedTests: number;
}
/**
 * Request model for bulk model discovery.
 */
interface BulkModelDiscoveryRequest {
    models: string[];
}
/**
 * Discovery result for a single model.
 */
interface ModelDiscoveryResult {
    model: string;
    provider?: string;
    displayName?: string;
    capabilities: Record<string, boolean>;
    found: boolean;
    error?: string;
}
/**
 * Response model for bulk model discovery.
 */
interface BulkModelDiscoveryResponse {
    results: ModelDiscoveryResult[];
    totalRequested: number;
    foundModels: number;
    notFoundModels: number;
}

/**
 * Service for discovering model capabilities and provider features.
 */
declare class DiscoveryService {
    private readonly baseEndpoint;
    private readonly clientAdapter;
    constructor(client: FetchBasedClient);
    /**
     * Gets all discovered models and their capabilities.
     */
    getModels(options?: RequestOptions): Promise<ModelsDiscoveryResponse>;
    /**
     * Gets models for a specific provider.
     */
    getProviderModels(provider: string, options?: RequestOptions): Promise<ProviderModelsDiscoveryResponse>;
    /**
     * Tests if a model supports a specific capability.
     */
    testModelCapability(model: string, capability: ModelCapability | string, options?: RequestOptions): Promise<CapabilityTestResponse>;
    /**
     * Tests multiple model capabilities in a single request.
     */
    testBulkCapabilities(request: BulkCapabilityTestRequest, options?: RequestOptions): Promise<BulkCapabilityTestResponse>;
    /**
     * Gets discovery information for multiple models in a single request.
     */
    getBulkModels(request: BulkModelDiscoveryRequest, options?: RequestOptions): Promise<BulkModelDiscoveryResponse>;
    /**
     * Refreshes the capability cache for all providers.
     * Requires admin/master key access.
     */
    refreshCapabilities(options?: RequestOptions): Promise<void>;
    /**
     * Static validation helper to test capabilities without making API calls.
     */
    static validateCapabilityTest(test: CapabilityTest): void;
    /**
     * Static validation helper for bulk requests.
     */
    static validateBulkCapabilityRequest(request: BulkCapabilityTestRequest): void;
    /**
     * Static validation helper for bulk model discovery requests.
     */
    static validateBulkModelRequest(request: BulkModelDiscoveryRequest): void;
}

/**
 * Type-safe Conduit Core Client using native fetch
 *
 * Provides full type safety for all operations with TypeScript generics
 * and OpenAPI-generated types, without the complexity of HTTP.
 *
 * @example
 * ```typescript
 * const client = new FetchConduitCoreClient({
 *   apiKey: 'your-api-key',
 *   baseURL: 'https://api.conduit.ai'
 * });
 *
 * // All operations are fully typed
 * const response = await client.chat.create({
 *   model: 'gpt-4',
 *   messages: [{ role: 'user', content: 'Hello' }]
 * });
 * ```
 */
declare class FetchConduitCoreClient extends FetchBasedClient {
    readonly chat: FetchChatService;
    readonly audio: AudioService;
    readonly health: HealthService;
    readonly images: ImagesService;
    readonly videos: VideosService;
    readonly discovery: DiscoveryService;
    constructor(config: ClientConfig);
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
    /**
     * Type guard for checking if an error is a network error
     */
    isNetworkError(error: unknown): error is ConduitError;
}

interface Model {
    id: string;
    object: 'model';
    created: number;
    owned_by: string;
}
interface ModelsResponse {
    object: 'list';
    data: Model[];
}

/**
 * Batch operation status enumeration
 */
declare enum BatchOperationStatusEnum {
    Queued = "Queued",
    Running = "Running",
    Completed = "Completed",
    Failed = "Failed",
    Cancelled = "Cancelled",
    PartiallyCompleted = "PartiallyCompleted"
}
/**
 * Individual spend update item for batch operations
 */
interface SpendUpdateDto {
    /** Virtual key ID to update spend for */
    virtualKeyId: number;
    /** Amount to add to the spend (0.0001 to 1,000,000) */
    amount: number;
    /** Model name associated with the spend */
    model: string;
    /** Provider name associated with the spend */
    provider: string;
    /** Optional metadata for the spend update */
    metadata?: Record<string, unknown>;
}
/**
 * Request for batch spend updates (max 10,000 items)
 */
interface BatchSpendUpdateRequest {
    /** List of spend updates to process */
    spendUpdates: SpendUpdateDto[];
}
/**
 * Individual virtual key update item for batch operations
 */
interface VirtualKeyUpdateDto {
    /** Virtual key ID to update */
    virtualKeyId: number;
    /** New maximum budget for the virtual key */
    maxBudget?: number;
    /** New list of allowed models */
    allowedModels?: string[];
    /** New rate limits configuration */
    rateLimits?: Record<string, unknown>;
    /** Whether the virtual key is enabled */
    isEnabled?: boolean;
    /** New expiration date for the virtual key */
    expiresAt?: string;
    /** New notes for the virtual key */
    notes?: string;
}
/**
 * Request for batch virtual key updates (max 1,000 items, requires admin permissions)
 */
interface BatchVirtualKeyUpdateRequest {
    /** List of virtual key updates to process */
    virtualKeyUpdates: VirtualKeyUpdateDto[];
}
/**
 * Individual webhook send item for batch operations
 */
interface WebhookSendDto {
    /** Webhook URL to send to */
    url: string;
    /** Event type for the webhook */
    eventType: string;
    /** Payload to send in the webhook */
    payload: Record<string, unknown>;
    /** Optional headers to include in the webhook request */
    headers?: Record<string, string>;
    /** Optional secret for webhook signature verification */
    secret?: string;
}
/**
 * Request for batch webhook sends (max 5,000 items)
 */
interface BatchWebhookSendRequest {
    /** List of webhook sends to process */
    webhookSends: WebhookSendDto[];
}
/**
 * Response when starting a batch operation
 */
interface BatchOperationStartResponse {
    /** Unique identifier for the batch operation */
    operationId: string;
    /** Task ID for SignalR real-time updates */
    taskId: string;
    /** URL to check operation status */
    statusUrl: string;
    /** Current operation status */
    status: BatchOperationStatusEnum;
    /** When the operation was started */
    startedAt: string;
}
/**
 * Batch operation progress metadata
 */
interface BatchOperationMetadata {
    /** Total number of items in the batch */
    totalItems: number;
    /** Number of successfully processed items */
    processedItems: number;
    /** Number of failed items */
    failedItems: number;
    /** Processing rate in items per second */
    itemsPerSecond: number;
    /** Estimated time of completion */
    estimatedCompletion?: string;
    /** Operation start time */
    startedAt: string;
    /** Operation completion time */
    completedAt?: string;
}
/**
 * Individual batch item processing result
 */
interface BatchItemResult {
    /** Item index in the batch */
    index: number;
    /** Whether the item was processed successfully */
    success: boolean;
    /** Error message if processing failed */
    errorMessage?: string;
    /** Error code if processing failed */
    errorCode?: string;
    /** Processing timestamp */
    processedAt: string;
}
/**
 * Batch item error details
 */
interface BatchItemError {
    /** Item index in the batch */
    index: number;
    /** Error message */
    message: string;
    /** Error code */
    code?: string;
    /** Exception type if available */
    exceptionType?: string;
    /** Timestamp when error occurred */
    timestamp: string;
}
/**
 * Response containing batch operation status and results
 */
interface BatchOperationStatusResponse {
    /** Unique identifier for the batch operation */
    operationId: string;
    /** Current operation status */
    status: BatchOperationStatusEnum;
    /** Progress metadata */
    metadata: BatchOperationMetadata;
    /** List of individual item results */
    results: BatchItemResult[];
    /** List of errors that occurred during processing */
    errors: BatchItemError[];
    /** Whether the operation can be cancelled */
    canCancel: boolean;
    /** Additional operation details */
    details?: Record<string, unknown>;
}
/**
 * Options for batch operation polling
 */
interface BatchOperationPollOptions {
    /** How often to check the status (default: 5 seconds) */
    pollingInterval?: number;
    /** Maximum time to wait for completion in milliseconds (default: 10 minutes) */
    timeout?: number;
}
/**
 * Validation options for batch operations
 */
interface BatchValidationOptions {
    /** Whether to validate individual items (default: true) */
    validateItems?: boolean;
    /** Whether to validate URL formats for webhooks (default: true) */
    validateUrls?: boolean;
    /** Whether to validate date formats (default: true) */
    validateDates?: boolean;
}
/**
 * Result of batch operation validation
 */
interface BatchValidationResult {
    /** Whether validation passed */
    isValid: boolean;
    /** List of validation errors */
    errors: string[];
    /** Number of items validated */
    itemCount: number;
    /** Validation warnings (non-blocking) */
    warnings?: string[];
}

/**
 * Common type definitions for Core SDK to replace Record<string, any> usage
 */

/**
 * Video generation API request (internal)
 */
interface VideoApiRequest {
    /** The prompt for video generation */
    prompt: string;
    /** Model to use */
    model: string;
    /** Duration in seconds */
    duration?: number;
    /** Video size/resolution */
    size?: string;
    /** Frames per second */
    fps?: number;
    /** Style preset */
    style?: string;
    /** Response format */
    response_format: 'url' | 'b64_json';
    /** User identifier */
    user?: string;
    /** Random seed */
    seed?: number;
    /** Number of videos to generate */
    n: number;
}
/**
 * Async video generation API request (internal)
 */
interface AsyncVideoApiRequest extends VideoApiRequest {
    /** Webhook URL for completion notification */
    webhook_url?: string;
    /** Metadata to include in webhook */
    webhook_metadata?: VideoWebhookMetadata;
    /** Headers for webhook request */
    webhook_headers?: {
        [key: string]: string;
    };
    /** Timeout in seconds for async processing */
    timeout_seconds?: number;
}
/**
 * Notification filter parameters
 */
interface NotificationFilters {
    /** Filter by event type */
    eventType?: string;
    /** Filter by resource type */
    resourceType?: string;
    /** Filter by resource ID */
    resourceId?: string;
    /** Filter by severity */
    severity?: 'info' | 'warning' | 'error' | 'critical';
    /** Filter by read status */
    isRead?: boolean;
    /** Filter by date range */
    dateRange?: {
        start?: string;
        end?: string;
    };
}
/**
 * Generic task result wrapper
 * Used when task results can be of various types
 */
type TaskResult<T = unknown> = T;
/**
 * Task metadata for tracking
 */
interface TaskMetadata {
    /** Task type identifier */
    taskType: string;
    /** Resource being processed */
    resourceId?: string;
    /** User who initiated the task */
    initiatedBy?: string;
    /** Priority level */
    priority?: 'low' | 'normal' | 'high';
    /** Custom attributes */
    attributes?: {
        [key: string]: string | number | boolean;
    };
}

/**
 * Response from a general task status request
 */
interface TaskStatusResponse {
    /** The unique task identifier */
    task_id: string;
    /** The current status of the task */
    status?: string;
    /** The task type */
    task_type?: string;
    /** The progress percentage (0-100) */
    progress: number;
    /** An optional progress message */
    message?: string;
    /** When the task was created */
    created_at: string;
    /** When the task was last updated */
    updated_at: string;
    /** The task result, available when status is completed */
    result?: TaskResult;
    /** Error information if the task failed */
    error?: string;
}
/**
 * Options for polling task status
 */
interface TaskPollingOptions {
    /** The polling interval in milliseconds */
    intervalMs?: number;
    /** The maximum polling timeout in milliseconds */
    timeoutMs?: number;
    /** Whether to use exponential backoff for polling intervals */
    useExponentialBackoff?: boolean;
    /** The maximum interval between polls in milliseconds when using exponential backoff */
    maxIntervalMs?: number;
}
/**
 * Response from a cleanup tasks request
 */
interface CleanupTasksResponse {
    /** The number of tasks that were removed */
    tasks_removed: number;
}
/**
 * Default values for task operations
 */
declare const TaskDefaults: {
    /** Default polling interval in milliseconds */
    readonly POLLING_INTERVAL_MS: 2000;
    /** Default polling timeout in milliseconds */
    readonly POLLING_TIMEOUT_MS: 600000;
    /** Default maximum polling interval in milliseconds */
    readonly MAX_POLLING_INTERVAL_MS: 30000;
};
/**
 * Helper functions for task management
 */
declare const TaskHelpers: {
    /**
     * Creates polling options with sensible defaults
     */
    createPollingOptions(options?: Partial<TaskPollingOptions>): TaskPollingOptions;
};

/**
 * Service for performing batch operations on the Conduit Core API
 */
declare class BatchOperationsService {
    private readonly clientAdapter;
    constructor(client: FetchBasedClient);
    /**
     * Performs a batch spend update operation
     *
     * @param request - The batch spend update request containing up to 10,000 spend updates
     * @returns Promise<BatchOperationStartResponse> The batch operation start response
     * @throws {ConduitCoreError} When the API request fails or request validation fails
     *
     * @example
     * ```typescript
     * const spendUpdates = [
     *   { virtualKeyId: 1, amount: 10.50, model: 'gpt-4', provider: 'openai' },
     *   { virtualKeyId: 2, amount: 5.25, model: 'claude-3', provider: 'anthropic' }
     * ];
     *
     * const startResponse = await coreClient.batchOperations.batchSpendUpdate({
     *   spendUpdates
     * });
     *
     * console.log(`Started batch operation: ${startResponse.operationId}`);
     * console.log(`Track progress with task ID: ${startResponse.taskId}`);
     * ```
     */
    batchSpendUpdate(request: BatchSpendUpdateRequest): Promise<BatchOperationStartResponse>;
    /**
     * Performs a batch virtual key update operation (requires admin permissions)
     *
     * @param request - The batch virtual key update request containing up to 1,000 virtual key updates
     * @returns Promise<BatchOperationStartResponse> The batch operation start response
     * @throws {ConduitCoreError} When the API request fails or request validation fails
     *
     * @example
     * ```typescript
     * const virtualKeyUpdates = [
     *   { virtualKeyId: 1, maxBudget: 1000, isEnabled: true },
     *   { virtualKeyId: 2, allowedModels: ['gpt-4', 'gpt-3.5-turbo'] }
     * ];
     *
     * const startResponse = await coreClient.batchOperations.batchVirtualKeyUpdate({
     *   virtualKeyUpdates
     * });
     *
     * console.log(`Started virtual key batch operation: ${startResponse.operationId}`);
     * ```
     */
    batchVirtualKeyUpdate(request: BatchVirtualKeyUpdateRequest): Promise<BatchOperationStartResponse>;
    /**
     * Performs a batch webhook send operation
     *
     * @param request - The batch webhook send request containing up to 5,000 webhook sends
     * @returns Promise<BatchOperationStartResponse> The batch operation start response
     * @throws {ConduitCoreError} When the API request fails or request validation fails
     *
     * @example
     * ```typescript
     * const webhookSends = [
     *   {
     *     url: 'https://example.com/webhook',
     *     eventType: 'spend_update',
     *     payload: { userId: 123, amount: 10.50 },
     *     headers: { 'X-Custom-Header': 'value' }
     *   }
     * ];
     *
     * const startResponse = await coreClient.batchOperations.batchWebhookSend({
     *   webhookSends
     * });
     *
     * console.log(`Started webhook batch operation: ${startResponse.operationId}`);
     * ```
     */
    batchWebhookSend(request: BatchWebhookSendRequest): Promise<BatchOperationStartResponse>;
    /**
     * Gets the status of a batch operation
     *
     * @param operationId - The unique identifier of the batch operation
     * @returns Promise<BatchOperationStatusResponse> The batch operation status response
     * @throws {ConduitCoreError} When the API request fails
     *
     * @example
     * ```typescript
     * const status = await coreClient.batchOperations.getOperationStatus('batch-123');
     *
     * console.log(`Operation status: ${status.status}`);
     * console.log(`Progress: ${status.metadata.processedItems}/${status.metadata.totalItems}`);
     * console.log(`Success rate: ${((status.metadata.processedItems - status.metadata.failedItems) / status.metadata.processedItems * 100).toFixed(2)}%`);
     *
     * if (status.status === BatchOperationStatusEnum.Completed) {
     *   console.log('Batch operation completed!');
     * } else if (status.status === BatchOperationStatusEnum.Failed) {
     *   console.log('Batch operation failed:', status.errors);
     * }
     * ```
     */
    getOperationStatus(operationId: string): Promise<BatchOperationStatusResponse>;
    /**
     * Cancels a running batch operation
     *
     * @param operationId - The unique identifier of the batch operation to cancel
     * @returns Promise<BatchOperationStatusResponse> The updated batch operation status response
     * @throws {ConduitCoreError} When the API request fails
     *
     * @example
     * ```typescript
     * const canceledStatus = await coreClient.batchOperations.cancelOperation('batch-123');
     * console.log(`Operation canceled. Final status: ${canceledStatus.status}`);
     * ```
     */
    cancelOperation(operationId: string): Promise<BatchOperationStatusResponse>;
    /**
     * Polls a batch operation until completion or timeout
     *
     * @param operationId - The unique identifier of the batch operation
     * @param options - Polling options (interval and timeout)
     * @returns Promise<BatchOperationStatusResponse> The final batch operation status response
     * @throws {Error} When the operation doesn't complete within the timeout
     *
     * @example
     * ```typescript
     * // Poll every 3 seconds for up to 5 minutes
     * const finalStatus = await coreClient.batchOperations.pollOperation('batch-123', {
     *   pollingInterval: 3000,
     *   timeout: 300000
     * });
     *
     * if (finalStatus.status === BatchOperationStatusEnum.Completed) {
     *   console.log('Operation completed successfully!');
     *   console.log(`Processed ${finalStatus.metadata.processedItems} items`);
     * }
     * ```
     */
    pollOperation(operationId: string, options?: BatchOperationPollOptions): Promise<BatchOperationStatusResponse>;
    /**
     * Validates a batch spend update request
     *
     * @param spendUpdates - Array of spend updates to validate
     * @param options - Validation options
     * @returns BatchValidationResult Validation result with errors and warnings
     *
     * @example
     * ```typescript
     * const spendUpdates = [
     *   { virtualKeyId: 1, amount: 10.50, model: 'gpt-4', provider: 'openai' },
     *   { virtualKeyId: 0, amount: -5, model: '', provider: 'invalid' } // Invalid
     * ];
     *
     * const validation = BatchOperationsService.validateSpendUpdateRequest(spendUpdates);
     * if (!validation.isValid) {
     *   console.log('Validation errors:', validation.errors);
     * }
     * ```
     */
    static validateSpendUpdateRequest(spendUpdates: SpendUpdateDto[], options?: BatchValidationOptions): BatchValidationResult;
    /**
     * Validates a batch virtual key update request
     *
     * @param virtualKeyUpdates - Array of virtual key updates to validate
     * @param options - Validation options
     * @returns BatchValidationResult Validation result with errors and warnings
     */
    static validateVirtualKeyUpdateRequest(virtualKeyUpdates: VirtualKeyUpdateDto[], options?: BatchValidationOptions): BatchValidationResult;
    /**
     * Validates a batch webhook send request
     *
     * @param webhookSends - Array of webhook sends to validate
     * @param options - Validation options
     * @returns BatchValidationResult Validation result with errors and warnings
     */
    static validateWebhookSendRequest(webhookSends: WebhookSendDto[], options?: BatchValidationOptions): BatchValidationResult;
    /**
     * Creates a validated batch spend update request
     *
     * @param spendUpdates - Array of spend updates
     * @returns BatchSpendUpdateRequest Validated request object
     * @throws {Error} When validation fails
     */
    static createSpendUpdateRequest(spendUpdates: SpendUpdateDto[]): BatchSpendUpdateRequest;
    /**
     * Creates a validated batch virtual key update request
     *
     * @param virtualKeyUpdates - Array of virtual key updates
     * @returns BatchVirtualKeyUpdateRequest Validated request object
     * @throws {Error} When validation fails
     */
    static createVirtualKeyUpdateRequest(virtualKeyUpdates: VirtualKeyUpdateDto[]): BatchVirtualKeyUpdateRequest;
    /**
     * Creates a validated batch webhook send request
     *
     * @param webhookSends - Array of webhook sends
     * @returns BatchWebhookSendRequest Validated request object
     * @throws {Error} When validation fails
     */
    static createWebhookSendRequest(webhookSends: WebhookSendDto[]): BatchWebhookSendRequest;
}

/**
 * Comprehensive metrics snapshot from the API
 */
interface MetricsSnapshot {
    /** Timestamp when metrics were collected */
    timestamp: Date;
    /** HTTP performance metrics */
    http: HttpMetrics;
    /** Business metrics including costs and usage */
    business: BusinessMetrics;
    /** System resource metrics */
    system: SystemMetrics;
    /** Infrastructure component metrics */
    infrastructure: InfrastructureMetrics;
    /** Provider health status for all providers */
    providerHealth: ProviderHealthStatus[];
}
/**
 * HTTP performance metrics
 */
interface HttpMetrics {
    /** Current requests per second */
    requestsPerSecond: number;
    /** Number of active requests */
    activeRequests: number;
    /** Error rate percentage */
    errorRate: number;
    /** Response time statistics */
    responseTimes: ResponseTimeMetrics;
}
/**
 * Response time metrics
 */
interface ResponseTimeMetrics {
    /** Average response time in milliseconds */
    average: number;
    /** 50th percentile response time */
    p50: number;
    /** 95th percentile response time */
    p95: number;
    /** 99th percentile response time */
    p99: number;
    /** Minimum response time */
    min: number;
    /** Maximum response time */
    max: number;
}
/**
 * Business metrics including costs and usage
 */
interface BusinessMetrics {
    /** Number of active virtual keys */
    activeVirtualKeys: number;
    /** Total requests per minute across all keys */
    totalRequestsPerMinute: number;
    /** Cost-related metrics */
    cost: CostMetrics;
    /** Model usage statistics */
    modelUsage: ModelUsageStats[];
    /** Virtual key statistics */
    virtualKeyStats: VirtualKeyStats[];
}
/**
 * Cost-related metrics
 */
interface CostMetrics {
    /** Cost per minute */
    costPerMinute: number;
    /** Average cost per request */
    averageCostPerRequest: number;
    /** Total cost for current period */
    totalCost: number;
}
/**
 * Model usage statistics
 */
interface ModelUsageStats {
    /** Model name */
    modelName: string;
    /** Requests per minute for this model */
    requestsPerMinute: number;
    /** Total requests for this model */
    totalRequests: number;
    /** Average cost per request */
    averageCost: number;
    /** Error rate for this model */
    errorRate: number;
}
/**
 * Virtual key statistics
 */
interface VirtualKeyStats {
    /** Virtual key ID */
    keyId: string;
    /** Key name */
    keyName: string;
    /** Current spend amount */
    currentSpend: number;
    /** Budget limit */
    budgetLimit?: number;
    /** Requests per minute */
    requestsPerMinute: number;
    /** Whether the key is enabled */
    isEnabled: boolean;
}
/**
 * System resource metrics
 */
interface SystemMetrics {
    /** CPU usage percentage */
    cpuUsagePercent: number;
    /** Memory usage in MB */
    memoryUsageMB: number;
    /** Available memory in MB */
    availableMemoryMB: number;
    /** System uptime */
    uptime: string;
    /** Number of CPU cores */
    cpuCores: number;
    /** Thread count */
    threadCount: number;
}
/**
 * Infrastructure component metrics
 */
interface InfrastructureMetrics {
    /** Database metrics */
    database: DatabaseMetrics;
    /** Redis cache metrics */
    redis: RedisMetrics;
    /** SignalR metrics */
    signalR: SignalRMetrics;
    /** RabbitMQ metrics (if configured) */
    rabbitmq?: RabbitMQMetrics;
}
/**
 * Database connection pool metrics
 */
interface DatabaseMetrics {
    /** Number of active connections */
    activeConnections: number;
    /** Maximum number of connections */
    maxConnections: number;
    /** Pool utilization percentage */
    poolUtilization: number;
    /** Average connection acquisition time */
    averageConnectionAcquisitionTime: number;
    /** Number of failed connections */
    failedConnections: number;
}
/**
 * Redis cache metrics
 */
interface RedisMetrics {
    /** Cache hit rate percentage */
    hitRate: number;
    /** Number of cache hits */
    hits: number;
    /** Number of cache misses */
    misses: number;
    /** Memory usage in MB */
    memoryUsageMB: number;
    /** Number of connected clients */
    connectedClients: number;
    /** Operations per second */
    operationsPerSecond: number;
}
/**
 * SignalR metrics
 */
interface SignalRMetrics {
    /** Number of active connections */
    activeConnections: number;
    /** Messages per second */
    messagesPerSecond: number;
    /** Number of connected groups */
    connectedGroups: number;
    /** Connection errors */
    connectionErrors: number;
}
/**
 * RabbitMQ metrics
 */
interface RabbitMQMetrics {
    /** Number of messages in queue */
    queueDepth: number;
    /** Messages per second */
    messagesPerSecond: number;
    /** Number of consumers */
    consumers: number;
    /** Memory usage in MB */
    memoryUsageMB: number;
}
/**
 * Provider health status
 */
interface ProviderHealthStatus {
    /** Provider name */
    providerName: string;
    /** Whether the provider is healthy */
    isHealthy: boolean;
    /** Last check time */
    lastCheckTime?: Date;
    /** Response time in milliseconds */
    responseTimeMs?: number;
    /** Error message if unhealthy */
    errorMessage?: string;
}
/**
 * Historical metrics request
 */
interface HistoricalMetricsRequest {
    /** Start time for the query */
    startTime: Date;
    /** End time for the query */
    endTime: Date;
    /** Specific metrics to retrieve */
    metricNames: string[];
    /** Interval for data aggregation */
    interval: string;
}
/**
 * Historical metrics response
 */
interface HistoricalMetricsResponse {
    /** Time series data */
    series: MetricSeries[];
    /** Query metadata */
    metadata: {
        startTime: Date;
        endTime: Date;
        interval: string;
        totalPoints: number;
    };
}
/**
 * Metric time series data
 */
interface MetricSeries {
    /** Metric name */
    name: string;
    /** Data points */
    dataPoints: MetricDataPoint[];
    /** Metric metadata */
    metadata: {
        unit: string;
        description: string;
    };
}
/**
 * Individual metric data point
 */
interface MetricDataPoint {
    /** Timestamp */
    timestamp: Date;
    /** Metric value */
    value: number;
}
/**
 * KPI summary for dashboards
 */
interface KPISummary {
    /** Timestamp */
    timestamp: Date;
    /** System health metrics */
    systemHealth: {
        overallHealthPercentage: number;
        errorRate: number;
        responseTimeP95: number;
        activeConnections: number;
        databaseUtilization: number;
    };
    /** Performance metrics */
    performance: {
        requestsPerSecond: number;
        activeRequests: number;
        averageResponseTime: number;
        cacheHitRate: number;
    };
    /** Business metrics */
    business: {
        activeVirtualKeys: number;
        requestsPerMinute: number;
        costBurnRatePerHour: number;
        averageCostPerRequest: number;
    };
    /** Infrastructure metrics */
    infrastructure: {
        cpuUsage: number;
        memoryUsage: number;
        uptime: string;
        signalRConnections: number;
    };
}

/**
 * Service for accessing system metrics and performance data from the Conduit Core API
 */
declare class MetricsService {
    private readonly clientAdapter;
    constructor(client: FetchBasedClient);
    /**
     * Gets the current comprehensive metrics snapshot
     *
     * @returns Promise<MetricsSnapshot> A complete snapshot of current system metrics
     */
    getCurrentMetrics(): Promise<MetricsSnapshot>;
    /**
     * Gets current database connection pool metrics
     *
     * @returns Promise<DatabaseMetrics> Database connection pool metrics
     */
    getDatabasePoolMetrics(): Promise<DatabaseMetrics>;
    /**
     * Gets the raw Prometheus metrics format
     *
     * @returns Promise<string> Prometheus-formatted metrics as a string
     */
    getPrometheusMetrics(): Promise<string>;
    /**
     * Gets historical metrics data for a specified time range
     *
     * @param request - The historical metrics request parameters
     * @returns Promise<HistoricalMetricsResponse> Historical metrics data
     */
    getHistoricalMetrics(request: HistoricalMetricsRequest): Promise<HistoricalMetricsResponse>;
    /**
     * Gets historical metrics for a specific time range with simplified parameters
     *
     * @param startTime - Start time for the metrics query
     * @param endTime - End time for the metrics query
     * @param metricNames - Optional list of specific metrics to retrieve
     * @param interval - Optional interval for data aggregation (default: "5m")
     * @returns Promise<HistoricalMetricsResponse> Historical metrics data
     */
    getHistoricalMetricsSimple(startTime: Date, endTime: Date, metricNames?: string[], interval?: string): Promise<HistoricalMetricsResponse>;
    /**
     * Gets current HTTP performance metrics
     *
     * @returns Promise<HttpMetrics> HTTP performance metrics
     */
    getHttpMetrics(): Promise<HttpMetrics>;
    /**
     * Gets current business metrics
     *
     * @returns Promise<BusinessMetrics> Business metrics including costs and usage
     */
    getBusinessMetrics(): Promise<BusinessMetrics>;
    /**
     * Gets current system resource metrics
     *
     * @returns Promise<SystemMetrics> System resource metrics
     */
    getSystemMetrics(): Promise<SystemMetrics>;
    /**
     * Gets current infrastructure component metrics
     *
     * @returns Promise<InfrastructureMetrics> Infrastructure metrics including database, Redis, and messaging
     */
    getInfrastructureMetrics(): Promise<InfrastructureMetrics>;
    /**
     * Gets current provider health status for all providers
     *
     * @returns Promise<ProviderHealthStatus[]> List of provider health statuses
     */
    getProviderHealth(): Promise<ProviderHealthStatus[]>;
    /**
     * Gets health status for a specific provider
     *
     * @param providerName - The name of the provider
     * @returns Promise<ProviderHealthStatus | null> Provider health status, or null if not found
     */
    getProviderHealthByName(providerName: string): Promise<ProviderHealthStatus | null>;
    /**
     * Gets the top performing models by request volume
     *
     * @param count - Number of top models to return (default: 10)
     * @returns Promise<ModelUsageStats[]> List of top performing models ordered by request volume
     */
    getTopModelsByRequestVolume(count?: number): Promise<ModelUsageStats[]>;
    /**
     * Gets the top spending virtual keys
     *
     * @param count - Number of top virtual keys to return (default: 10)
     * @returns Promise<VirtualKeyStats[]> List of top spending virtual keys ordered by current spend
     */
    getTopSpendingVirtualKeys(count?: number): Promise<VirtualKeyStats[]>;
    /**
     * Gets providers that are currently unhealthy
     *
     * @returns Promise<ProviderHealthStatus[]> List of unhealthy providers
     */
    getUnhealthyProviders(): Promise<ProviderHealthStatus[]>;
    /**
     * Calculates the overall system health percentage
     *
     * @returns Promise<number> System health percentage (0-100)
     */
    getSystemHealthPercentage(): Promise<number>;
    /**
     * Gets the current cost burn rate in USD per hour
     *
     * @returns Promise<number> Current cost burn rate in USD per hour
     */
    getCurrentCostBurnRate(): Promise<number>;
    /**
     * Checks if the system is currently healthy based on configurable thresholds
     *
     * @param options - Health check criteria
     * @returns Promise<boolean> True if the system is healthy based on the specified thresholds
     */
    isSystemHealthy(options?: {
        maxErrorRate?: number;
        maxResponseTime?: number;
        minProviderHealthPercentage?: number;
    }): Promise<boolean>;
    /**
     * Gets a summary of key performance indicators
     *
     * @returns Promise<KPISummary> A summary object with key performance indicators
     */
    getKPISummary(): Promise<KPISummary>;
    /**
     * Gets metrics for the last N minutes
     *
     * @param minutes - Number of minutes to look back
     * @param interval - Data aggregation interval (default: "1m")
     * @returns Promise<HistoricalMetricsResponse> Historical metrics for the specified period
     */
    getMetricsForLastMinutes(minutes: number, interval?: string): Promise<HistoricalMetricsResponse>;
    /**
     * Gets metrics for the last N hours
     *
     * @param hours - Number of hours to look back
     * @param interval - Data aggregation interval (default: "5m")
     * @returns Promise<HistoricalMetricsResponse> Historical metrics for the specified period
     */
    getMetricsForLastHours(hours: number, interval?: string): Promise<HistoricalMetricsResponse>;
    /**
     * Gets metrics for today
     *
     * @param interval - Data aggregation interval (default: "15m")
     * @returns Promise<HistoricalMetricsResponse> Historical metrics for today
     */
    getMetricsForToday(interval?: string): Promise<HistoricalMetricsResponse>;
}

/**
 * Service for retrieving provider model information.
 */
declare class ProviderModelsService {
    private readonly baseEndpoint;
    private readonly clientAdapter;
    constructor(client: FetchBasedClient);
    /**
     * Gets available models for a specified provider.
     * @param providerName - Name of the provider
     * @param forceRefresh - Whether to bypass cache and force refresh
     * @returns List of available model IDs
     */
    getProviderModels(providerName: string, forceRefresh?: boolean, options?: RequestOptions): Promise<string[]>;
    /**
     * Static validation helper to validate provider name.
     */
    static validateProviderName(providerName: string): void;
}

/**
 * Base class for Core SDK SignalR hub connections.
 * Extends the common base class with Core SDK-specific authentication.
 */
declare abstract class BaseSignalRConnection extends BaseSignalRConnection$1 {
    protected readonly virtualKey: string;
    constructor(baseUrl: string, virtualKey: string);
    /**
     * Starts the SignalR connection.
     */
    start(): Promise<void>;
    /**
     * Stops the SignalR connection.
     */
    stop(): Promise<void>;
    /**
     * Waits for the connection to be established.
     */
    waitForConnection(timeoutMs?: number): Promise<boolean>;
}

/**
 * SignalR hub endpoints.
 */
declare const SignalREndpoints: {
    readonly TaskHub: "/hubs/tasks";
    readonly VideoGenerationHub: "/hubs/video-generation";
    readonly ImageGenerationHub: "/hubs/image-generation";
    readonly NavigationStateHub: "/hubs/navigation-state";
};
/**
 * Task hub server interface.
 */
interface ITaskHubServer {
    subscribeToTask(taskId: string): Promise<void>;
    unsubscribeFromTask(taskId: string): Promise<void>;
    subscribeToTaskType(taskType: string): Promise<void>;
    unsubscribeFromTaskType(taskType: string): Promise<void>;
}
/**
 * Video generation hub server interface.
 */
interface IVideoGenerationHubServer {
    subscribeToTask(taskId: string): Promise<void>;
    unsubscribeFromTask(taskId: string): Promise<void>;
}
/**
 * Image generation hub server interface.
 */
interface IImageGenerationHubServer {
    subscribeToTask(taskId: string): Promise<void>;
    unsubscribeFromTask(taskId: string): Promise<void>;
}
/**
 * Task started event data.
 */
interface TaskStartedEvent {
    eventType: 'TaskStarted';
    taskId: string;
    taskType: string;
    metadata: Record<string, unknown>;
}
/**
 * Task progress event data.
 */
interface TaskProgressEvent {
    eventType: 'TaskProgress';
    taskId: string;
    progress: number;
    message?: string;
}
/**
 * Task completed event data.
 */
interface TaskCompletedEvent {
    eventType: 'TaskCompleted';
    taskId: string;
    result: Record<string, unknown>;
}
/**
 * Task failed event data.
 */
interface TaskFailedEvent {
    eventType: 'TaskFailed';
    taskId: string;
    error: string;
    isRetryable: boolean;
}
/**
 * Task cancelled event data.
 */
interface TaskCancelledEvent {
    eventType: 'TaskCancelled';
    taskId: string;
    reason?: string;
}
/**
 * Task timed out event data.
 */
interface TaskTimedOutEvent {
    eventType: 'TaskTimedOut';
    taskId: string;
    timeoutSeconds: number;
}
/**
 * Video generation started event data.
 */
interface VideoGenerationStartedEvent {
    eventType: 'VideoGenerationStarted';
    taskId: string;
    prompt: string;
    estimatedSeconds: number;
}
/**
 * Video generation progress event data.
 */
interface VideoGenerationProgressEvent {
    eventType: 'VideoGenerationProgress';
    taskId: string;
    progress: number;
    currentFrame?: number;
    totalFrames?: number;
    message?: string;
}
/**
 * Video generation completed event data.
 */
interface VideoGenerationCompletedEvent {
    eventType: 'VideoGenerationCompleted';
    taskId: string;
    videoUrl: string;
    duration: number;
    metadata: Record<string, unknown>;
}
/**
 * Image generation started event data.
 */
interface ImageGenerationStartedEvent {
    eventType: 'ImageGenerationStarted';
    taskId: string;
    prompt: string;
    model: string;
}
/**
 * Image generation progress event data.
 */
interface ImageGenerationProgressEvent {
    eventType: 'ImageGenerationProgress';
    taskId: string;
    progress: number;
    stage?: string;
}
/**
 * Image generation completed event data.
 */
interface ImageGenerationCompletedEvent {
    eventType: 'ImageGenerationCompleted';
    taskId: string;
    imageUrl: string;
    metadata: Record<string, unknown>;
}
/**
 * Video generation failed event data.
 */
interface VideoGenerationFailedEvent {
    eventType: 'VideoGenerationFailed';
    taskId: string;
    error: string;
    errorCode?: string;
    isRetryable: boolean;
}
/**
 * Image generation failed event data.
 */
interface ImageGenerationFailedEvent {
    eventType: 'ImageGenerationFailed';
    taskId: string;
    error: string;
    errorCode?: string;
    isRetryable: boolean;
}

/**
 * SignalR client for the Task Hub, providing real-time task progress notifications.
 */
declare class TaskHubClient extends BaseSignalRConnection implements ITaskHubServer {
    /**
     * Gets the hub path for task notifications.
     */
    protected get hubPath(): string;
    /**
     * Event handlers for task notifications.
     */
    onTaskStarted?: (event: TaskStartedEvent) => Promise<void>;
    onTaskProgress?: (event: TaskProgressEvent) => Promise<void>;
    onTaskCompleted?: (event: TaskCompletedEvent) => Promise<void>;
    onTaskFailed?: (event: TaskFailedEvent) => Promise<void>;
    onTaskCancelled?: (event: TaskCancelledEvent) => Promise<void>;
    onTaskTimedOut?: (event: TaskTimedOutEvent) => Promise<void>;
    /**
     * Configures the hub-specific event handlers.
     */
    protected configureHubHandlers(connection: signalR.HubConnection): void;
    /**
     * Subscribe to notifications for a specific task.
     */
    subscribeToTask(taskId: string): Promise<void>;
    /**
     * Unsubscribe from notifications for a specific task.
     */
    unsubscribeFromTask(taskId: string): Promise<void>;
    /**
     * Subscribe to notifications for all tasks of a specific type.
     */
    subscribeToTaskType(taskType: string): Promise<void>;
    /**
     * Unsubscribe from notifications for a task type.
     */
    unsubscribeFromTaskType(taskType: string): Promise<void>;
    /**
     * Subscribe to multiple tasks at once.
     */
    subscribeToTasks(taskIds: string[]): Promise<void>;
    /**
     * Unsubscribe from multiple tasks at once.
     */
    unsubscribeFromTasks(taskIds: string[]): Promise<void>;
    /**
     * Subscribe to multiple task types at once.
     */
    subscribeToTaskTypes(taskTypes: string[]): Promise<void>;
    /**
     * Unsubscribe from multiple task types at once.
     */
    unsubscribeFromTaskTypes(taskTypes: string[]): Promise<void>;
}

/**
 * SignalR client for the Video Generation Hub, providing real-time video generation notifications.
 */
declare class VideoGenerationHubClient extends BaseSignalRConnection implements IVideoGenerationHubServer {
    /**
     * Gets the hub path for video generation notifications.
     */
    protected get hubPath(): string;
    /**
     * Event handlers for video generation notifications.
     */
    onVideoGenerationStarted?: (event: VideoGenerationStartedEvent) => Promise<void>;
    onVideoGenerationProgress?: (event: VideoGenerationProgressEvent) => Promise<void>;
    onVideoGenerationCompleted?: (event: VideoGenerationCompletedEvent) => Promise<void>;
    onVideoGenerationFailed?: (event: VideoGenerationFailedEvent) => Promise<void>;
    /**
     * Configures the hub-specific event handlers.
     */
    protected configureHubHandlers(connection: signalR.HubConnection): void;
    /**
     * Subscribe to notifications for a specific video generation task.
     */
    subscribeToTask(taskId: string): Promise<void>;
    /**
     * Unsubscribe from notifications for a specific video generation task.
     */
    unsubscribeFromTask(taskId: string): Promise<void>;
    /**
     * Subscribe to multiple tasks at once.
     */
    subscribeToTasks(taskIds: string[]): Promise<void>;
    /**
     * Unsubscribe from multiple tasks at once.
     */
    unsubscribeFromTasks(taskIds: string[]): Promise<void>;
}

/**
 * SignalR client for the Image Generation Hub, providing real-time image generation notifications.
 */
declare class ImageGenerationHubClient extends BaseSignalRConnection implements IImageGenerationHubServer {
    /**
     * Gets the hub path for image generation notifications.
     */
    protected get hubPath(): string;
    /**
     * Event handlers for image generation notifications.
     */
    onImageGenerationStarted?: (event: ImageGenerationStartedEvent) => Promise<void>;
    onImageGenerationProgress?: (event: ImageGenerationProgressEvent) => Promise<void>;
    onImageGenerationCompleted?: (event: ImageGenerationCompletedEvent) => Promise<void>;
    onImageGenerationFailed?: (event: ImageGenerationFailedEvent) => Promise<void>;
    /**
     * Configures the hub-specific event handlers.
     */
    protected configureHubHandlers(connection: signalR.HubConnection): void;
    /**
     * Subscribe to notifications for a specific image generation task.
     */
    subscribeToTask(taskId: string): Promise<void>;
    /**
     * Unsubscribe from notifications for a specific image generation task.
     */
    unsubscribeFromTask(taskId: string): Promise<void>;
    /**
     * Subscribe to multiple tasks at once.
     */
    subscribeToTasks(taskIds: string[]): Promise<void>;
    /**
     * Unsubscribe from multiple tasks at once.
     */
    unsubscribeFromTasks(taskIds: string[]): Promise<void>;
}

/**
 * Service for managing SignalR hub connections for real-time notifications.
 */
declare class SignalRService {
    private readonly baseUrl;
    private readonly virtualKey;
    private readonly connections;
    private disposed;
    constructor(baseUrl: string, virtualKey: string);
    /**
     * Gets or creates a TaskHubClient for task progress notifications.
     */
    getTaskHubClient(): TaskHubClient;
    /**
     * Gets or creates a VideoGenerationHubClient for video generation notifications.
     */
    getVideoGenerationHubClient(): VideoGenerationHubClient;
    /**
     * Gets or creates an ImageGenerationHubClient for image generation notifications.
     */
    getImageGenerationHubClient(): ImageGenerationHubClient;
    /**
     * Gets or creates a connection of the specified type.
     */
    private getOrCreateConnection;
    /**
     * Starts all active hub connections.
     */
    startAllConnections(): Promise<void>;
    /**
     * Stops all active hub connections.
     */
    stopAllConnections(): Promise<void>;
    /**
     * Waits for all connections to be established.
     */
    waitForAllConnections(timeoutMs?: number): Promise<boolean>;
    /**
     * Gets the connection status for all hub connections.
     */
    getConnectionStatus(): Record<string, HubConnectionState>;
    /**
     * Checks if all connections are established.
     */
    areAllConnectionsEstablished(): boolean;
    /**
     * Checks if SignalR service is connected.
     */
    isConnected(): boolean;
    /**
     * Subscribes to a task across all relevant hubs.
     */
    subscribeToTask(taskId: string, taskType?: string): Promise<void>;
    /**
     * Unsubscribes from a task across all relevant hubs.
     */
    unsubscribeFromTask(taskId: string, taskType?: string): Promise<void>;
    /**
     * Disposes all SignalR connections.
     */
    dispose(): Promise<void>;
}

declare const CoreModelCapability: typeof ModelCapability$1;
/**
 * Check if a model supports a specific capability
 * @param modelId The model identifier
 * @param capability The capability to check for
 * @returns True if the model supports the capability
 */
declare function modelSupportsCapability(modelId: string, capability: ModelCapability$1): boolean;
/**
 * Get all capabilities supported by a model
 * @param modelId The model identifier
 * @returns Array of supported capabilities
 */
declare function getModelCapabilities(modelId: string): ModelCapability$1[];
/**
 * Validate that a request is compatible with the specified model
 * @param modelId The model identifier
 * @param requestType The type of request being made
 * @returns Validation result with any errors
 */
declare function validateModelCompatibility(modelId: string, requestType: 'chat' | 'image-generation' | 'image-edit' | 'image-variation'): {
    isValid: boolean;
    errors: string[];
    suggestions?: string[];
};
/**
 * Get optimal model recommendations for a specific capability
 * @param capability The desired capability
 * @param preferences Optional preferences for model selection
 * @returns Array of recommended model IDs, ordered by preference
 */
declare function getRecommendedModels(capability: ModelCapability$1, preferences?: {
    prioritizeQuality?: boolean;
    prioritizeSpeed?: boolean;
    prioritizeCost?: boolean;
}): string[];
/**
 * Get user-friendly display name for a capability
 * @param capability The capability to get display name for
 * @returns Human-readable display name
 */
declare function getCapabilityDisplayName(capability: ModelCapability$1): string;
/**
 * Check if two models are functionally equivalent for a given capability
 * @param modelA First model to compare
 * @param modelB Second model to compare
 * @param capability The capability to compare for
 * @returns True if models are equivalent for the capability
 */
declare function areModelsEquivalent(modelA: string, modelB: string, capability: ModelCapability$1): boolean;

/**
 * Service for creating text embeddings using the Conduit Core API
 */
declare class EmbeddingsService {
    private readonly clientAdapter;
    constructor(client: FetchBasedClient);
    /**
     * Creates embeddings for the given input text(s)
     *
     * @param request - The embedding request
     * @param options - Request options
     * @returns Promise<EmbeddingResponse> The embedding response containing the generated embeddings
     * @throws {ConduitError} When the API request fails or validation fails
     *
     * @example
     * ```typescript
     * const response = await client.embeddings.createEmbedding({
     *   input: "Hello, world!",
     *   model: "text-embedding-3-small"
     * });
     *
     * console.log(`Generated ${response.data.length} embeddings`);
     * console.log(`Used ${response.usage.total_tokens} tokens`);
     * ```
     */
    createEmbedding(request: EmbeddingRequest, options?: {
        signal?: AbortSignal;
    }): Promise<EmbeddingResponse>;
    /**
     * Creates embeddings for a single text input
     *
     * @param text - The input text
     * @param model - The model to use (defaults to text-embedding-3-small)
     * @param options - Additional options
     * @returns Promise<number[]> The embedding vector for the input text
     *
     * @example
     * ```typescript
     * const embedding = await client.embeddings.createSingleEmbedding(
     *   "Hello, world!",
     *   "text-embedding-3-small"
     * );
     *
     * console.log(`Embedding dimension: ${embedding.length}`);
     * ```
     */
    createSingleEmbedding(text: string, model?: string, options?: {
        dimensions?: number;
        encoding_format?: 'float' | 'base64';
        user?: string;
        signal?: AbortSignal;
    }): Promise<number[]>;
    /**
     * Creates embeddings for multiple text inputs
     *
     * @param texts - The input texts
     * @param model - The model to use (defaults to text-embedding-3-small)
     * @param options - Additional options
     * @returns Promise<number[][]> A list of embedding vectors for each input text
     *
     * @example
     * ```typescript
     * const embeddings = await client.embeddings.createBatchEmbeddings(
     *   ["Hello", "World", "AI"],
     *   "text-embedding-3-small"
     * );
     *
     * embeddings.forEach((embedding, i) => {
     *   console.log(`Text ${i}: ${embedding.length} dimensions`);
     * });
     * ```
     */
    createBatchEmbeddings(texts: string[], model?: string, options?: {
        dimensions?: number;
        encoding_format?: 'float' | 'base64';
        user?: string;
        signal?: AbortSignal;
    }): Promise<number[][]>;
    /**
     * Finds the most similar texts from a list of candidates to a query text
     *
     * @param query - The query text
     * @param candidates - The list of candidate texts
     * @param options - Search options
     * @returns Promise<Array<{ text: string; similarity: number; index: number }>>
     *          Sorted list of candidates with similarity scores
     *
     * @example
     * ```typescript
     * const candidates = [
     *   "The cat sat on the mat",
     *   "Dogs are loyal animals",
     *   "The feline rested on the rug",
     *   "Birds can fly"
     * ];
     *
     * const results = await client.embeddings.findSimilar(
     *   "A cat is sleeping",
     *   candidates,
     *   { topK: 2 }
     * );
     *
     * results.forEach(result => {
     *   console.log(`"${result.text}" - Similarity: ${result.similarity.toFixed(3)}`);
     * });
     * ```
     */
    findSimilar(query: string, candidates: string[], options?: {
        model?: string;
        topK?: number;
        dimensions?: number;
        signal?: AbortSignal;
    }): Promise<Array<{
        text: string;
        similarity: number;
        index: number;
    }>>;
    /**
     * Calculates the similarity between two texts
     *
     * @param text1 - The first text
     * @param text2 - The second text
     * @param model - The model to use for embeddings
     * @param options - Additional options
     * @returns Promise<number> The cosine similarity between -1 and 1
     *
     * @example
     * ```typescript
     * const similarity = await client.embeddings.calculateSimilarity(
     *   "The weather is nice today",
     *   "It's a beautiful day outside"
     * );
     *
     * console.log(`Similarity: ${(similarity * 100).toFixed(1)}%`);
     * ```
     */
    calculateSimilarity(text1: string, text2: string, model?: string, options?: {
        dimensions?: number;
        signal?: AbortSignal;
    }): Promise<number>;
    /**
     * Groups texts by similarity using embeddings
     *
     * @param texts - The texts to group
     * @param threshold - Similarity threshold for grouping (0-1)
     * @param model - The model to use for embeddings
     * @param options - Additional options
     * @returns Promise<string[][]> Groups of similar texts
     *
     * @example
     * ```typescript
     * const texts = [
     *   "Python programming",
     *   "JavaScript coding",
     *   "Cooking recipes",
     *   "Software development",
     *   "Baking cakes"
     * ];
     *
     * const groups = await client.embeddings.groupBySimilarity(
     *   texts,
     *   0.7 // 70% similarity threshold
     * );
     *
     * groups.forEach((group, i) => {
     *   console.log(`Group ${i + 1}: ${group.join(", ")}`);
     * });
     * ```
     */
    groupBySimilarity(texts: string[], threshold?: number, model?: string, options?: {
        dimensions?: number;
        signal?: AbortSignal;
    }): Promise<string[][]>;
}
/**
 * Helper functions for embeddings
 */
declare const EmbeddingHelpers: {
    /**
     * Normalizes an embedding vector to unit length
     */
    normalize(embedding: number[]): number[];
    /**
     * Calculates euclidean distance between two embeddings
     */
    euclideanDistance(embedding1: number[], embedding2: number[]): number;
    /**
     * Calculates the centroid of multiple embeddings
     */
    centroid(embeddings: number[][]): number[];
};

/**
 * Event emitted when video generation progress is updated
 */
interface VideoProgressEvent {
    taskId: string;
    progress: number;
    status: 'queued' | 'processing' | 'completed' | 'failed';
    estimatedTimeRemaining?: number;
    message?: string;
    metadata?: VideoMetadata;
}
/**
 * Event emitted when image generation progress is updated
 */
interface ImageProgressEvent {
    taskId: string;
    progress: number;
    status: 'queued' | 'processing' | 'completed' | 'failed';
    message?: string;
    images?: ImageData[];
}
/**
 * Event emitted when spend is updated for a virtual key
 */
interface SpendUpdateEvent {
    virtualKeyId: number;
    virtualKeyHash: string;
    amount: number;
    totalSpend: number;
    model: string;
    provider: string;
    timestamp: string;
    remainingBudget?: number;
}
/**
 * Event emitted when spending limit is approaching or exceeded
 */
interface SpendLimitAlertEvent {
    virtualKeyId: number;
    virtualKeyHash: string;
    alertType: 'warning' | 'critical' | 'exceeded';
    currentSpend: number;
    spendLimit: number;
    percentageUsed: number;
    message: string;
    timestamp: string;
}
/**
 * Generic task update event for any async task
 */
interface TaskUpdateEvent {
    taskId: string;
    taskType: 'video' | 'image' | 'batch' | 'other';
    status: 'queued' | 'processing' | 'completed' | 'failed' | 'cancelled';
    progress?: number;
    result?: unknown;
    error?: string;
    metadata?: NotificationMetadata;
}
/**
 * Callback function types for event subscriptions
 */
type VideoProgressCallback = (event: VideoProgressEvent) => void;
type ImageProgressCallback = (event: ImageProgressEvent) => void;
type SpendUpdateCallback = (event: SpendUpdateEvent) => void;
type SpendLimitAlertCallback = (event: SpendLimitAlertEvent) => void;
type TaskUpdateCallback = (event: TaskUpdateEvent) => void;
/**
 * Subscription handle returned when subscribing to events
 */
interface NotificationSubscription {
    /**
     * Unique identifier for this subscription
     */
    id: string;
    /**
     * The event type this subscription is for
     */
    eventType: 'videoProgress' | 'imageProgress' | 'spendUpdate' | 'spendLimitAlert' | 'taskUpdate';
    /**
     * Unsubscribe from this event
     */
    unsubscribe: () => void;
}
/**
 * Options for notification subscriptions
 */
interface NotificationOptions {
    /**
     * Whether to automatically reconnect on connection loss
     */
    autoReconnect?: boolean;
    /**
     * Filter events by specific criteria
     */
    filter?: {
        virtualKeyId?: number;
        taskIds?: string[];
        models?: string[];
        providers?: string[];
    };
    /**
     * Error handler for subscription errors
     */
    onError?: (error: Error) => void;
    /**
     * Handler for connection state changes
     */
    onConnectionStateChange?: (state: 'connected' | 'disconnected' | 'reconnecting') => void;
}

/**
 * Service for managing real-time notifications through SignalR
 */
declare class NotificationsService {
    private signalRService;
    private subscriptions;
    private taskHubClient?;
    private videoHubClient?;
    private imageHubClient?;
    private connectionStateCallbacks;
    private videoCallbacks;
    private imageCallbacks;
    private spendUpdateCallbacks;
    private spendLimitCallbacks;
    private taskCallbacks;
    constructor(signalRService: SignalRService);
    /**
     * Subscribe to video generation progress events
     */
    onVideoProgress(callback: VideoProgressCallback, options?: NotificationOptions): NotificationSubscription;
    /**
     * Subscribe to image generation progress events
     */
    onImageProgress(callback: ImageProgressCallback, options?: NotificationOptions): NotificationSubscription;
    /**
     * Subscribe to spend update events
     */
    onSpendUpdate(callback: SpendUpdateCallback, options?: NotificationOptions): NotificationSubscription;
    /**
     * Subscribe to spend limit alert events
     */
    onSpendLimitAlert(callback: SpendLimitAlertCallback, options?: NotificationOptions): NotificationSubscription;
    /**
     * Subscribe to updates for a specific task
     */
    subscribeToTask(taskId: string, taskType: 'video' | 'image' | 'batch' | 'other', callback: TaskUpdateCallback, options?: NotificationOptions): Promise<NotificationSubscription>;
    /**
     * Unsubscribe from a specific task
     */
    unsubscribeFromTask(taskId: string): Promise<void>;
    /**
     * Unsubscribe from all notifications
     */
    unsubscribeAll(): void;
    /**
     * Get all active subscriptions
     */
    getActiveSubscriptions(): NotificationSubscription[];
    /**
     * Connect to SignalR hubs
     */
    connect(): Promise<void>;
    /**
     * Disconnect from SignalR hubs
     */
    disconnect(): Promise<void>;
    /**
     * Check if connected to SignalR hubs
     */
    isConnected(): boolean;
    private unsubscribe;
    private generateSubscriptionId;
}

export { type AsyncVideoApiRequest, type AsyncVideoGenerationRequest, type AsyncVideoGenerationResponse, type AudioError, type AudioFile, type AudioFormat, type AudioMetadata, type AudioProcessingOptions, AudioService, type AudioTranscriptionRequest, type AudioTranscriptionResponse, type AudioTranslationRequest, type AudioTranslationResponse, AudioUtils, type AudioValidation, type BatchItemError, type BatchItemResult, type BatchOperationMetadata, type BatchOperationPollOptions, type BatchOperationStartResponse, BatchOperationStatusEnum, type BatchOperationStatusResponse, BatchOperationsService, type BatchSpendUpdateRequest, type BatchValidationOptions, type BatchValidationResult, type BatchVirtualKeyUpdateRequest, type BatchWebhookSendRequest, type BulkCapabilityTestRequest, type BulkCapabilityTestResponse, type BulkModelDiscoveryRequest, type BulkModelDiscoveryResponse, type BusinessMetrics, type CapabilityTest, type CapabilityTestResponse, type CapabilityTestResult, type ChatCompletionChoice, type ChatCompletionChunk$1 as ChatCompletionChunk, type ChatCompletionChunkChoice, type ChatCompletionMessage, type ChatCompletionRequest$1 as ChatCompletionRequest, type ChatCompletionResponse$1 as ChatCompletionResponse, type ChatMetadata, type CleanupTasksResponse, type ClientConfig, FetchConduitCoreClient as ConduitCoreClient, ContentHelpers, CoreModelCapability, type CostMetrics, type DatabaseMetrics, type DiscoveredModel, DiscoveryService, type EmbeddingData, EmbeddingEncodingFormats, EmbeddingHelpers, EmbeddingModels, type EmbeddingRequest, type EmbeddingResponse, type EmbeddingUsage, EmbeddingsService, EnhancedSSEEventType, type EnhancedStreamEvent, type EnhancedStreamingResponse, type ErrorResponse, FetchConduitCoreClient, type FinalMetrics, type FinishReason, type FunctionCall, type FunctionDefinition, type HealthCheckItem, type HealthCheckOptions, type HealthCheckResponse, HealthStatus, type HealthSummary, type HistoricalMetricsRequest, type HistoricalMetricsResponse, type HttpMetrics, type HybridAudioRequest, type HybridAudioResponse, type IImageGenerationHubServer, IMAGE_DEFAULTS, IMAGE_MODELS, IMAGE_MODEL_CAPABILITIES, type ITaskHubServer, type IVideoGenerationHubServer, type ImageContent, type ImageData, type ImageEditRequest, type ImageEditResponse, type ImageGenerationCompletedEvent, ImageGenerationHubClient, type ImageGenerationProgressEvent, type ImageGenerationRequest, type ImageGenerationResponse, type ImageGenerationStartedEvent, type ImageModel, type ImageProgressCallback, type ImageProgressEvent, type ImageVariationRequest, type ImageVariationResponse, ImagesService, type InfrastructureMetrics, type KPISummary, type MessageContent, type MetricDataPoint, type MetricSeries, MetricsService, type MetricsSnapshot, type Model, type ModelCapabilities, ModelCapability, type ModelDiscoveryResult, type ModelUsageStats, type ModelsDiscoveryResponse, type ModelsResponse, type NotificationFilters, type NotificationMetadata, type NotificationOptions, type NotificationSubscription, NotificationsService, type ProviderHealthStatus, type ProviderModelsDiscoveryResponse, ProviderModelsService, type RabbitMQMetrics, type RealtimeConnectionRequest, type RealtimeMessage, type RealtimeSession, type RealtimeSessionConfig, type RedisMetrics, type RequestOptions, type ResponseFormat, type ResponseTimeMetrics, SignalREndpoints, type SignalRMetrics, SignalRService, type SimpleHealthStatus, type SpendLimitAlertCallback, type SpendLimitAlertEvent, type SpendUpdateCallback, type SpendUpdateDto, type SpendUpdateEvent, type StreamingMetrics, type SystemMetrics, type TaskCancelledEvent, type TaskCompletedEvent, TaskDefaults, type TaskFailedEvent, TaskHelpers, TaskHubClient, type TaskMetadata, type TaskPollingOptions, type TaskProgressEvent, type TaskResult, type TaskStartedEvent, type TaskStatusResponse, type TaskTimedOutEvent, type TaskUpdateCallback, type TaskUpdateEvent, type TextContent, type TextToSpeechModel, type TextToSpeechRequest, type TextToSpeechResponse, type TimestampGranularity, type Tool, type ToolCall, type ToolParameters, type TranscriptionFormat, type TranscriptionModel, type TranscriptionSegment, type TranscriptionWord, type VideoApiRequest, type VideoCompletionWebhookPayload, type VideoData, VideoDefaults, type VideoGenerationCompletedEvent, VideoGenerationHubClient, type VideoGenerationProgressEvent, type VideoGenerationRequest, type VideoGenerationResponse, type VideoGenerationStartedEvent, type VideoMetadata, type VideoModelCapabilities, VideoModels, type VideoProgressCallback, type VideoProgressEvent, type VideoProgressWebhookPayload, VideoResolutions, VideoResponseFormats, type VideoTaskPollingOptions, VideoTaskStatus, type VideoUsage, type VideoWebhookMetadata, VideosService, type VirtualKeyStats, type VirtualKeyUpdateDto, type Voice, type VoiceSettings, type WaitForHealthOptions, type WebhookPayloadBase, type WebhookSendDto, areModelsEquivalent, calculateCosineSimilarity, convertEmbeddingToFloatArray, getCapabilityDisplayName, getModelCapabilities, getRecommendedModels, getVideoModelCapabilities, isChatCompletionChunk, isFinalMetrics, isStreamingMetrics, isValidMetadata, modelSupportsCapability, parseMetadata, stringifyMetadata, validateAsyncVideoGenerationRequest, validateEmbeddingRequest, validateModelCompatibility, validateVideoGenerationRequest };
