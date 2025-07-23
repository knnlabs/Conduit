import { FetchBasedClient } from '../client/FetchBasedClient';
import { HttpMethod } from '../client/HttpMethod';
import type { ClientConfig, RequestOptions } from '../client/types';
import type { components } from '../generated/core-api';
import type { StreamingResponse } from '../models/streaming';
import type { EnhancedStreamEvent } from '../models/enhanced-streaming';
import type { EnhancedStreamingResponse } from '../models/enhanced-streaming-response';
import { createWebStream } from '../utils/web-streaming';
import { createEnhancedWebStream } from '../utils/enhanced-web-streaming';
import { API_ENDPOINTS } from '../constants';

// Type aliases for better readability
type ChatCompletionRequest = components['schemas']['ChatCompletionRequest'];
type ChatCompletionResponse = components['schemas']['ChatCompletionResponse'];
type ChatCompletionChunk = components['schemas']['ChatCompletionChunk'];

/**
 * Type-safe Chat service using generated OpenAPI types and native fetch
 */
export class FetchChatService extends FetchBasedClient {
  constructor(config: ClientConfig) {
    super(config);
  }

  /**
   * Create a chat completion with full type safety
   * Overloaded to handle both streaming and non-streaming responses
   */
  async create(
    request: ChatCompletionRequest & { stream?: false },
    options?: RequestOptions
  ): Promise<ChatCompletionResponse>;
  async create(
    request: ChatCompletionRequest & { stream: true },
    options?: RequestOptions
  ): Promise<StreamingResponse<ChatCompletionChunk>>;
  async create(
    request: ChatCompletionRequest,
    options?: RequestOptions
  ): Promise<ChatCompletionResponse | StreamingResponse<ChatCompletionChunk>> {
    // Convert legacy function parameters to tools
    const processedRequest = this.convertLegacyFunctions(request);
    
    // Skip validation for now due to type mismatches with generated types
    // validateChatCompletionRequest(processedRequest);

    if (processedRequest.stream === true) {
      return this.createStream(processedRequest as ChatCompletionRequest & { stream: true }, options);
    }

    return this.createCompletion(processedRequest, options);
  }

  private async createCompletion(
    request: ChatCompletionRequest,
    options?: RequestOptions
  ): Promise<ChatCompletionResponse> {
    const response = await this.post<ChatCompletionResponse, ChatCompletionRequest>(
      API_ENDPOINTS.V1.CHAT.COMPLETIONS,
      request,
      options
    );
    
    return response;
  }

  private async createStream(
    request: ChatCompletionRequest & { stream: true },
    options?: RequestOptions
  ): Promise<StreamingResponse<ChatCompletionChunk>> {
    // For streaming, we need to handle the response differently
    const response = await this.createStreamingRequest(request, options);
    const stream = response.body;
    if (!stream) {
      throw new Error('Response body is not a stream');
    }
    
    // Return stream without validation
    
    return createWebStream<ChatCompletionChunk>(stream, options);
  }

  private async createStreamingRequest(
    request: ChatCompletionRequest,
    options?: RequestOptions
  ): Promise<Response> {
    const url = `${this.config.baseURL}${API_ENDPOINTS.V1.CHAT.COMPLETIONS}`;
    const controller = new AbortController();
    
    // Set up timeout
    const timeoutId = options?.timeout ?? this.config.timeout
      ? setTimeout(() => controller.abort(), options?.timeout ?? this.config.timeout)
      : undefined;

    try {
      const response = await fetch(url, {
        method: HttpMethod.POST,
        headers: {
          'Authorization': `Bearer ${this.config.apiKey}`,
          'Content-Type': 'application/json',
          'User-Agent': '@conduit/core/1.0.0',
          ...options?.headers,
        },
        body: JSON.stringify(request),
        signal: options?.signal ?? controller.signal,
      });

      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }

      return response;
    } finally {
      if (timeoutId) {
        clearTimeout(timeoutId);
      }
    }
  }

  /**
   * Convert legacy function format to tools format for backward compatibility
   */
  private convertLegacyFunctions(request: ChatCompletionRequest): ChatCompletionRequest {
    const legacyRequest = request as ChatCompletionRequest & { 
      functions?: Array<{
        name: string;
        description?: string;
        parameters?: Record<string, unknown>;
      }>;
      function_call?: string | { name: string };
    };

    if (!legacyRequest.functions || legacyRequest.functions.length === 0) {
      return request;
    }

    // Convert functions to tools - OpenAPI spec expects Record<string, never> for empty params
    const tools: NonNullable<ChatCompletionRequest['tools']> = legacyRequest.functions.map(func => ({
      type: 'function' as const,
      function: {
        name: func.name,
        description: func.description,
        // If parameters exist and have properties, we need to handle the type mismatch
        // The OpenAPI spec incorrectly types this as Record<string, never>
        parameters: func.parameters && Object.keys(func.parameters).length > 0 
          ? func.parameters as Record<string, never>
          : {} as Record<string, never>,
      }
    }))

    // Convert function_call to tool_choice
    let tool_choice: ChatCompletionRequest['tool_choice'];
    if (legacyRequest.function_call) {
      if (typeof legacyRequest.function_call === 'string') {
        tool_choice = legacyRequest.function_call as 'none' | 'auto';
      } else {
        tool_choice = {
          type: 'function',
          function: { name: legacyRequest.function_call.name }
        };
      }
    }

    // Remove legacy function properties from the request
    // eslint-disable-next-line @typescript-eslint/no-unused-vars
    const { functions, function_call, ...cleanRequest } = legacyRequest;
    
    return {
      ...cleanRequest,
      tools,
      tool_choice,
    };
  }

  /**
   * Count tokens in messages (placeholder - actual implementation would use tiktoken)
   */
  countTokens(
    messages: ChatCompletionRequest['messages'],
    _model: string = 'gpt-4'
  ): number {
    // Rough estimation: 4 characters per token
    const text = messages.map(m => 
      typeof m.content === 'string' ? m.content : JSON.stringify(m.content)
    ).join(' ');
    return Math.ceil(text.length / 4);
  }

  /**
   * Validate that a request fits within model context limits
   */
  validateContextLength(
    request: ChatCompletionRequest,
    maxTokens?: number
  ): { valid: boolean; tokens: number; limit: number } {
    const tokens = this.countTokens(request.messages, request.model);
    const limit = maxTokens ?? 8192; // Default limit
    
    return {
      valid: tokens <= limit,
      tokens,
      limit,
    };
  }

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
  async createEnhancedStream(
    request: ChatCompletionRequest & { stream: true },
    options?: RequestOptions
  ): Promise<EnhancedStreamingResponse<EnhancedStreamEvent>> {
    const processedRequest = this.convertLegacyFunctions(request);

    const response = await this.createStreamingRequest(processedRequest, options);
    const stream = response.body;
    
    if (!stream) {
      throw new Error('Response body is not a stream');
    }

    // Use enhanced web streaming that preserves SSE event types
    return createEnhancedWebStream(
      stream,
      {
        signal: options?.signal,
      }
    );
  }
}