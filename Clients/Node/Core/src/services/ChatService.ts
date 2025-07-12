import type { FetchBasedClient } from '../client/FetchBasedClient';
import { HttpMethod } from '../client/HttpMethod';
import type { RequestOptions } from '../client/types';
import type { 
  ChatCompletionRequest, 
  ChatCompletionResponse,
  ChatCompletionChunk 
} from '../models/chat';
import type { StreamingResponse } from '../models/streaming';
import { validateChatCompletionRequest } from '../utils/validation';
import { createTypedStream } from '../utils/streaming';
import { API_ENDPOINTS } from '../constants';

export class ChatService {
  constructor(private readonly client: FetchBasedClient) {}

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
    return this.client['request']<ChatCompletionResponse>(
      {
        method: HttpMethod.POST,
        url: API_ENDPOINTS.V1.CHAT.COMPLETIONS,
        data: request,
      },
      options
    );
  }

  private async createStream(
    request: ChatCompletionRequest & { stream: true },
    options?: RequestOptions
  ): Promise<StreamingResponse<ChatCompletionChunk>> {
    const response = await this.client['client'].post(API_ENDPOINTS.V1.CHAT.COMPLETIONS, request, {
      responseType: 'stream',
      signal: options?.signal,
      timeout: 0,
      headers: {
        ...options?.headers,
        ...(options?.correlationId && { 'X-Correlation-Id': options.correlationId }),
      },
    });

    return createTypedStream<ChatCompletionChunk>(
      response.data as NodeJS.ReadableStream,
      {
        signal: options?.signal,
      }
    );
  }

  /**
   * Converts legacy function parameters to the new tools format
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
}