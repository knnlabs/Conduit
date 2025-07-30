export interface ImageAttachment {
  url: string;
  base64?: string;
  mimeType: string;
  size: number;
  name: string;
}

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