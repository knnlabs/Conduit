// ImageAttachment moved to SDK - import from @knn_labs/conduit-core-client
import type { ImageAttachment } from '@knn_labs/conduit-core-client';
export type { ImageAttachment };

// Content types for chat messages (similar to SDK types)
export interface TextContent {
  type: 'text';
  text: string;
}

export interface ImageContent {
  type: 'image_url';
  image_url: {
    url: string;
    detail?: 'auto' | 'low' | 'high';
  };
}

export type MessageContent = string | Array<TextContent | ImageContent>;

// Content helpers
export const ContentHelpers = {
  text: (text: string): TextContent => ({
    type: 'text',
    text,
  }),
  
  imageUrl: (url: string, detail?: 'auto' | 'low' | 'high'): ImageContent => ({
    type: 'image_url',
    image_url: {
      url,
      detail,
    },
  }),
  
  imageBase64: (base64: string, mimeType: string, detail?: 'auto' | 'low' | 'high'): ImageContent => ({
    type: 'image_url',
    image_url: {
      url: `data:${mimeType};base64,${base64}`,
      detail,
    },
  }),
};

export type ChatErrorType = 'rate_limit' | 'model_not_found' | 'auth_error' | 'network_error' | 'server_error';

export interface ChatMessage {
  id: string;
  role: 'user' | 'assistant' | 'system' | 'function';
  content: string;
  images?: ImageAttachment[];
  timestamp: Date;
  model?: string;
  functionCall?: {
    name: string;
    arguments: string;
  };
  toolCalls?: Array<{
    id: string;
    type: 'function';
    function: {
      name: string;
      arguments: string;
    };
  }>;
  name?: string;
  metadata?: {
    tokensUsed?: number;
    tokensPerSecond?: number;
    latency?: number;
    finishReason?: string;
    provider?: string;
    model?: string;
    promptTokens?: number;
    completionTokens?: number;
    timeToFirstToken?: number;
    streaming?: boolean;
  };
  error?: {
    type: ChatErrorType;
    code?: string;           // Provider-specific error code
    statusCode?: number;     // HTTP status code
    retryAfter?: number;     // Seconds until retry allowed (for rate limits)
    suggestions?: string[];  // Actionable suggestions (e.g., alternative models)
    technical?: string;      // Technical details for developers
    recoverable: boolean;    // Whether error can be automatically retried
  };
}

export interface ChatSession {
  id: string;
  title: string;
  messages: ChatMessage[];
  model: string;
  createdAt: Date;
  updatedAt: Date;
  parameters: ChatParameters;
}

export interface ChatParameters {
  temperature: number;
  maxTokens: number;
  topP: number;
  frequencyPenalty: number;
  presencePenalty: number;
  systemPrompt?: string;
  responseFormat?: 'text' | 'json_object';
  seed?: number;
  stop?: string[];
  stream?: boolean;
}

export interface ChatPreset {
  id: string;
  name: string;
  description: string;
  icon: string;
  parameters: Partial<ChatParameters>;
}

export interface ConversationStarter {
  id: string;
  title: string;
  prompt: string;
  category: string;
  icon: string;
}

export interface FunctionDefinition {
  name: string;
  description?: string;
  parameters?: {
    type: 'object';
    properties: Record<string, unknown>;
    required?: string[];
  };
}

export interface ToolDefinition {
  type: 'function';
  function: FunctionDefinition;
}

export type ToolChoice = 'auto' | 'none' | 'required' | { type: 'function'; function: { name: string } };

export interface ModelWithCapabilities {
  id: string;
  providerId: string;
  providerName?: string;
  displayName: string;
  maxContextTokens?: number;
  supportsVision?: boolean;
  supportsFunctionCalling?: boolean;
  supportsToolUsage?: boolean;
  supportsJsonMode?: boolean;
  supportsStreaming?: boolean;
}

export interface ChatCompletionRequest {
  messages: Array<{
    role: 'system' | 'user' | 'assistant';
    content: MessageContent;
  }>;
  model: string;
  stream?: boolean;
  temperature?: number;
  max_tokens?: number;
  top_p?: number;
  frequency_penalty?: number;
  presence_penalty?: number;
  seed?: number;
  stop?: string[];
  response_format?: {
    type: 'json_object';
  };
}

export interface ChatCompletionResponse {
  id: string;
  object: string;
  created: number;
  model: string;
  choices: Array<{
    index: number;
    message: {
      role: string;
      content: string;
    };
    ['finish_reason']: string;
  }>;
  usage?: {
    ['prompt_tokens']: number;
    ['completion_tokens']: number;
    ['total_tokens']: number;
  };
}