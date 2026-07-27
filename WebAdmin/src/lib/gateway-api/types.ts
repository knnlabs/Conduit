import type { components, operations } from "@/generated/gateway-api";

export type GatewaySchemas = components["schemas"];
export type GatewayOperations = operations;
export type GatewayVideoGenerationRequest =
  components["schemas"]["VideoGenerationRequest"];
export type GatewayVideoGenerationTaskResponse =
  components["schemas"]["VideoGenerationTaskResponse"];
export type GatewayFunctionExecutionRequest =
  components["schemas"]["FunctionExecutionRequest"];

export enum ModelCapability {
  Chat = "chat",
  ChatStream = "chat_stream",
  Embeddings = "embeddings",
  ImageGeneration = "image_generation",
  Vision = "vision",
  VideoGeneration = "video_generation",
  VideoUnderstanding = "video_understanding",
  FunctionCalling = "function_calling",
  ToolUse = "tool_use",
  JsonMode = "json_mode",
}

export interface DiscoveredModel {
  id: string;
  provider: string | number;
  display_name?: string;
  capabilities: Record<string, boolean | number | string[] | undefined>;
  metadata?: Record<string, unknown>;
  last_verified: string;
  [key: string]: unknown;
}

export interface DiscoveryResponse {
  data: DiscoveredModel[];
  count: number;
}

export interface ImageAttachment {
  url: string;
  base64?: string;
  mimeType: string;
  size: number;
  name: string;
  detail?: "auto" | "low" | "high";
  kind?: "image" | "pdf" | "audio" | "video";
  parser?: "auto" | "native" | "cloudflare-ai" | "mistral-ocr";
}
export type ChatAttachment = ImageAttachment;

export interface TextContent {
  type: "text";
  text: string;
}
export interface ImageContent {
  type: "image_url";
  image_url: { url: string; detail?: "auto" | "low" | "high" };
}
export interface VideoContent {
  type: "video_url";
  video_url: { url: string };
}
export interface AudioContent {
  type: "input_audio";
  input_audio: { data: string; format: string };
}
export interface FileContent {
  type: "file";
  file: { filename?: string; file_data?: string; file_id?: string };
}
export interface ProviderContent {
  type: string;
  [key: string]: unknown;
}
export type MessageContentPart =
  | TextContent
  | ImageContent
  | VideoContent
  | AudioContent
  | FileContent
  | ProviderContent;
export type MessageContent = string | null | MessageContentPart[];

export interface ChatCompletionChunk {
  id: string;
  object: string;
  created: number;
  model: string;
  choices: Array<{
    index: number;
    delta: {
      role?: string;
      content?: string;
      reasoning?: string;
      channel?: string;
      tool_calls?: unknown;
      [key: string]: unknown;
    };
    finish_reason?: string | null;
  }>;
  usage?: {
    prompt_tokens?: number;
    completion_tokens?: number;
    total_tokens?: number;
  };
}

export interface StreamingMetrics {
  request_id?: string;
  elapsed_ms?: number;
  tokens_generated?: number;
  current_tokens_per_second?: number;
  time_to_first_token_ms?: number;
  avg_inter_token_latency_ms?: number;
}

export interface FinalMetrics {
  total_latency_ms?: number;
  time_to_first_token_ms?: number;
  tokens_per_second?: number;
  prompt_tokens_per_second?: number;
  completion_tokens_per_second?: number;
  provider?: string;
  model?: string;
  streaming?: boolean;
  avg_inter_token_latency_ms?: number;
  prompt_tokens?: number;
  completion_tokens?: number;
  total_tokens?: number;
}

export interface StreamingErrorEvent {
  error: string;
}
export interface ReasoningEvent {
  content: string;
}
export interface ToolExecutingEvent {
  tool_call_id?: string;
  function_name?: string;
  status: string;
  result?: unknown;
  cost?: number;
  error_message?: string;
  function_execution_id?: string;
}
export interface ToolResultEvent {
  tool_call_id: string;
  result: unknown;
  error?: string;
}
export type ChatStreamEvent =
  | ChatCompletionChunk
  | StreamingMetrics
  | FinalMetrics
  | StreamingErrorEvent
  | ReasoningEvent
  | ToolExecutingEvent
  | ToolResultEvent;

export interface StreamingError extends Error {
  status?: number;
  code?: string;
  context?: string;
  retryable?: boolean;
}

export interface RetryInfo {
  error: StreamingError;
  attempt: number;
  maxAttempts: number;
  delayMs: number;
}

export interface StreamMessageOptions {
  model: string;
  stream: true;
  messages?: Array<{
    role: "system" | "user" | "assistant";
    content: string;
    attachments?: ChatAttachment[];
    images?: ImageAttachment[];
  }>;
  attachments?: ChatAttachment[];
  images?: ImageAttachment[];
  systemPrompt?: string;
  temperature?: number;
  maxTokens?: number;
  topP?: number;
  frequencyPenalty?: number;
  presencePenalty?: number;
  seed?: number;
  stop?: string[];
  responseFormat?: "text" | "json_object";
  functionConfigurationIds?: number[];
  dynamicParameters?: Record<string, unknown>;
}

export interface StreamingCallbacks {
  onChunk?: (chunk: ChatCompletionChunk) => void;
  onContent?: (content: string, totalContent: string) => void;
  onReasoning?: (reasoning: string, totalReasoning: string) => void;
  onToolExecuting?: (event: ToolExecutingEvent) => void;
  onToolResult?: (event: ToolResultEvent) => void;
  onMetrics?: (metrics: StreamingMetrics | FinalMetrics) => void;
  onTokensPerSecond?: (tokensPerSecond: number) => void;
  onError?: (error: StreamingError) => void;
  onComplete?: (response: {
    content: string;
    metadata?: {
      model?: string;
      finishReason?: string;
      tokensUsed?: number;
      completionTokens?: number;
      promptTokens?: number;
      latency?: number;
      timeToFirstToken?: number;
      tokensPerSecond?: number;
      streaming?: boolean;
      provider?: string;
      toolCalls?: Array<{
        id: string;
        type: "function";
        function: { name: string; arguments: string };
      }>;
    };
  }) => void;
  onStart?: () => void;
  onAbort?: () => void;
  onRetrying?: (info: RetryInfo) => void;
}

export interface VideoProgress {
  percentage: number;
  status: string;
  message?: string;
}

export interface VideoProgressCallbacks {
  onStarted?: (taskId: string, estimatedSeconds: number) => void;
  onProgress?: (progress: VideoProgress) => void;
  onCompleted?: (result: VideoGenerationResponse) => void;
  onFailed?: (error: string, isRetryable?: boolean) => void;
}

export interface VideoGenerationResponse {
  created: number;
  data: Array<{ url?: string; b64_json?: string; [key: string]: unknown }>;
  [key: string]: unknown;
}

export interface VideoTaskResponse {
  task_id: string;
  status: string;
  progress: number;
  message?: string;
  error?: string;
  result?: VideoGenerationResponse;
  estimated_time_to_completion: number;
  created_at: string;
  updated_at: string;
  check_status_url?: string;
}

export interface MediaUploadOptions {
  mediaType?: "Image" | "Video" | "Audio";
  onProgress?: (loaded: number, total: number) => void;
  signal?: AbortSignal;
  headers?: Record<string, string>;
}

export interface MediaUploadResponse {
  success: boolean;
  url: string;
  [key: string]: unknown;
}

export interface GatewayClientConfig {
  apiKey: string;
  baseURL: string;
  timeout?: number;
}

export interface FunctionExecutionResponse {
  id: string;
  functionId: number;
  status: string;
  input?: Record<string, unknown>;
  output?: Record<string, unknown>;
  error?: string;
  createdAt: string;
  startedAt?: string;
  completedAt?: string;
  durationMs?: number;
  cost: {
    estimated?: number;
    actual?: number;
    currency: string;
    breakdown?: Record<string, unknown>;
  };
}

export interface RequestOptions {
  signal?: AbortSignal;
  timeout?: number;
  headers?: Record<string, string>;
}
