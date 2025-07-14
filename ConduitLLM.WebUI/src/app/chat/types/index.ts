export interface ImageAttachment {
  url: string;
  base64?: string;
  mimeType: string;
  size: number;
  name: string;
}

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
    properties: Record<string, any>;
    required?: string[];
  };
}

export interface ModelWithCapabilities {
  id: string;
  providerId: string;
  displayName: string;
  maxContextTokens?: number;
  supportsVision?: boolean;
  supportsFunctionCalling?: boolean;
  supportsToolUsage?: boolean;
  supportsJsonMode?: boolean;
  supportsStreaming?: boolean;
}