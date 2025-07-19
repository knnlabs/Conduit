import type { FetchBasedClient } from '../client/FetchBasedClient';
import { createClientAdapter, type IFetchBasedClientAdapter } from '../client/ClientAdapter';
import type { RequestOptions, ClientConfig } from '../client/types';
import type { 
  ChatCompletionRequest, 
  ChatCompletionResponse,
  ChatCompletionChunk 
} from '../models/chat';
import type { StreamingResponse } from '../models/streaming';
import { validateChatCompletionRequest } from '../utils/validation';
import { createTypedStream } from '../utils/streaming';
import { API_ENDPOINTS } from '../constants';
import { ControllableChatStream, type ControllableStream, type StreamControlOptions } from '../models/streaming-controls';

interface FetchBasedClientWithConfig extends FetchBasedClient {
  config: Required<Omit<ClientConfig, 'onError' | 'onRequest' | 'onResponse'>> & 
    Pick<ClientConfig, 'onError' | 'onRequest' | 'onResponse'>;
}

export class ChatService {
  private readonly clientAdapter: IFetchBasedClientAdapter;
  private readonly config: ClientConfig;

  constructor(private readonly client: FetchBasedClient) {
    this.clientAdapter = createClientAdapter(client);
    // Access config through type assertion - this is a known architectural limitation
    // The config is protected in FetchBasedClient, but we need it for streaming
    this.config = (client as FetchBasedClientWithConfig).config;
  }

  /**
   * Creates a chat completion with the specified model.
   * 
   * @param request - The chat completion request
   * @param options - Optional request configuration
   * @returns A promise that resolves to either a complete response or a streaming response
   * 
   * @example
   * // Non-streaming request
   * const response = await chatService.create({
   *   model: 'gpt-4',
   *   messages: [{ role: 'user', content: 'Hello!' }],
   *   stream: false
   * });
   * 
   * @example
   * // Streaming request
   * const stream = await chatService.create({
   *   model: 'gpt-4',
   *   messages: [{ role: 'user', content: 'Hello!' }],
   *   stream: true
   * });
   * for await (const chunk of stream) {
   *   console.log(chunk.choices[0]?.delta?.content);
   * }
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
    
    validateChatCompletionRequest(processedRequest);

    if (processedRequest.stream === true) {
      return this.createStream(processedRequest as ChatCompletionRequest & { stream: true }, options);
    }

    return this.createCompletion(processedRequest, options);
  }

  private async createCompletion(
    request: ChatCompletionRequest,
    options?: RequestOptions
  ): Promise<ChatCompletionResponse> {
    return this.clientAdapter.post<ChatCompletionResponse, ChatCompletionRequest>(
      API_ENDPOINTS.V1.CHAT.COMPLETIONS,
      request,
      options
    );
  }

  private async createStream(
    request: ChatCompletionRequest & { stream: true },
    options?: RequestOptions
  ): Promise<StreamingResponse<ChatCompletionChunk>> {
    // Create streaming request using fetch API directly
    const response = await this.createStreamingRequest(request, options);
    const stream = response.body;
    
    if (!stream) {
      throw new Error('Response body is not a stream');
    }

    // Use web streaming for browser compatibility
    return createTypedStream<ChatCompletionChunk>(
      stream as ReadableStream, // Type assertion needed due to stream type differences
      {
        signal: options?.signal,
      }
    );
  }

  private async createStreamingRequest(
    request: ChatCompletionRequest,
    options?: RequestOptions
  ): Promise<Response> {
    const url = `${this.config.baseURL}${API_ENDPOINTS.V1.CHAT.COMPLETIONS}`;
    const controller = new AbortController();
    
    // Merge signals if provided
    if (options?.signal) {
      options.signal.addEventListener('abort', () => controller.abort());
    }
    
    // Set up timeout
    const timeoutId = options?.timeout ?? this.config.timeout
      ? setTimeout(() => controller.abort(), options?.timeout ?? this.config.timeout)
      : undefined;

    try {
      const response = await fetch(url, {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${this.config.apiKey}`,
          'Content-Type': 'application/json',
          'User-Agent': '@conduit/core/1.0.0',
          ...this.config.headers,
          ...options?.headers,
          ...(options?.correlationId && { 'X-Correlation-Id': options.correlationId }),
        },
        body: JSON.stringify(request),
        signal: controller.signal,
      });

      clearTimeout(timeoutId);

      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }

      return response;
    } catch (error) {
      clearTimeout(timeoutId);
      throw error;
    }
  }

  /**
   * Converts legacy function parameters to the new tools format.
   * This maintains backward compatibility with the older function calling API.
   * 
   * @param request - The chat completion request that may contain legacy function parameters
   * @returns The request with functions converted to tools format
   * 
   * @private
   */
  private convertLegacyFunctions(request: ChatCompletionRequest): ChatCompletionRequest {
    const processedRequest = { ...request };
    
    // Convert legacy functions to tools
    if (processedRequest.functions && !processedRequest.tools) {
      processedRequest.tools = processedRequest.functions.map(fn => ({
        type: 'function' as const,
        function: {
          name: fn.name,
          description: fn.description,
          parameters: fn.parameters,
        },
      }));
      delete processedRequest.functions;
    }
    
    // Convert legacy function_call to tool_choice
    if (processedRequest.function_call && !processedRequest.tool_choice) {
      if (processedRequest.function_call === 'none') {
        processedRequest.tool_choice = 'none';
      } else if (processedRequest.function_call === 'auto') {
        processedRequest.tool_choice = 'auto';
      } else if (typeof processedRequest.function_call === 'object' && processedRequest.function_call.name) {
        processedRequest.tool_choice = {
          type: 'function',
          function: { name: processedRequest.function_call.name },
        };
      }
      delete processedRequest.function_call;
    }
    
    return processedRequest;
  }

  /**
   * Creates a chat completion stream with pause/resume/cancel controls.
   * 
   * @param request - The chat completion request (must have stream: true)
   * @param options - Optional request configuration with stream control options
   * @returns A controllable stream that can be paused, resumed, or cancelled
   * 
   * @example
   * const stream = await chatService.createControllable({
   *   model: 'gpt-4',
   *   messages: [{ role: 'user', content: 'Tell me a long story' }],
   *   stream: true
   * });
   * 
   * // Use the stream
   * for await (const chunk of stream) {
   *   console.log(chunk.choices[0]?.delta?.content);
   *   
   *   // Pause after 5 chunks
   *   if (chunkCount++ > 5) {
   *     stream.pause();
   *     setTimeout(() => stream.resume(), 1000);
   *   }
   * }
   */
  async createControllable(
    request: ChatCompletionRequest & { stream: true },
    options?: RequestOptions & { streamControl?: StreamControlOptions }
  ): Promise<ControllableStream<ChatCompletionChunk>> {
    const processedRequest = this.convertLegacyFunctions(request);
    validateChatCompletionRequest(processedRequest);

    const baseStream = await this.createStream(
      processedRequest as ChatCompletionRequest & { stream: true },
      options
    );

    return new ControllableChatStream(baseStream, options?.streamControl ?? {});
  }
}