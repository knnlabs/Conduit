import type { Usage, ResponseFormat, Tool, ToolCall, FinishReason, PerformanceMetrics } from './common';
import type { ToolParameters } from './metadata';

/**
 * Text content part for multi-modal messages
 */
export interface TextContent {
  type: 'text';
  text: string;
}

/**
 * Image content part for multi-modal messages
 */
export interface ImageContent {
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
export type MessageContent = string | null | Array<TextContent | ImageContent>;

export interface ChatCompletionMessage {
  role: 'system' | 'user' | 'assistant' | 'tool';
  content: MessageContent;
  name?: string;
  tool_calls?: ToolCall[];
  tool_call_id?: string;
}

export interface ChatCompletionRequest {
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
  tools?: Tool[];
  tool_choice?: 'none' | 'auto' | { type: 'function'; function: { name: string } };
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
  function_call?: 'none' | 'auto' | { name: string };
}

export interface ChatCompletionChoice {
  index: number;
  message: ChatCompletionMessage;
  logprobs?: unknown;
  finish_reason: FinishReason;
}

export interface ChatCompletionResponse {
  id: string;
  object: 'chat.completion';
  created: number;
  model: string;
  system_fingerprint?: string;
  choices: ChatCompletionChoice[];
  usage: Usage;
  performance?: PerformanceMetrics;
}

export interface ChatCompletionChunkChoice {
  index: number;
  delta: Partial<ChatCompletionMessage>;
  logprobs?: unknown;
  finish_reason: FinishReason;
}

export interface ChatCompletionChunk {
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
export const ContentHelpers = {
  /**
   * Creates a text content part
   */
  text(text: string): TextContent {
    return { type: 'text', text };
  },

  /**
   * Creates an image content part from a URL
   */
  imageUrl(url: string, detail?: 'low' | 'high' | 'auto'): ImageContent {
    return {
      type: 'image_url',
      image_url: { url, detail }
    };
  },

  /**
   * Creates an image content part from base64 data
   */
  imageBase64(base64Data: string, mimeType: string = 'image/jpeg', detail?: 'low' | 'high' | 'auto'): ImageContent {
    return {
      type: 'image_url',
      image_url: {
        url: `data:${mimeType};base64,${base64Data}`,
        detail
      }
    };
  },

  /**
   * Checks if content contains images
   */
  hasImages(content: MessageContent): boolean {
    if (!Array.isArray(content)) return false;
    return content.some(part => part.type === 'image_url');
  },

  /**
   * Extracts text from multi-modal content
   */
  extractText(content: MessageContent): string {
    if (typeof content === 'string') return content;
    if (!content) return '';
    if (!Array.isArray(content)) return '';
    
    return content
      .filter((part): part is TextContent => part.type === 'text')
      .map(part => part.text)
      .join(' ');
  },

  /**
   * Extracts images from multi-modal content
   */
  extractImages(content: MessageContent): ImageContent[] {
    if (!Array.isArray(content)) return [];
    return content.filter((part): part is ImageContent => part.type === 'image_url');
  }
};