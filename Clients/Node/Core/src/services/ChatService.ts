import type { BaseClient } from '../client/BaseClient';
import type { RequestOptions } from '../client/types';
import type { 
  ChatCompletionRequest, 
  ChatCompletionResponse,
  ChatCompletionChunk 
} from '../models/chat';
import { validateChatCompletionRequest } from '../utils/validation';
import { streamAsyncIterator } from '../utils/streaming';
import { API_ENDPOINTS, HTTP_METHODS } from '../constants';

export class ChatService {
  constructor(private readonly client: BaseClient) {}

  async create(
    request: ChatCompletionRequest & { stream?: false },
    options?: RequestOptions
  ): Promise<ChatCompletionResponse>;
  async create(
    request: ChatCompletionRequest & { stream: true },
    options?: RequestOptions
  ): Promise<AsyncGenerator<ChatCompletionChunk, void, unknown>>;
  async create(
    request: ChatCompletionRequest,
    options?: RequestOptions
  ): Promise<ChatCompletionResponse | AsyncGenerator<ChatCompletionChunk, void, unknown>> {
    validateChatCompletionRequest(request);

    if (request.stream === true) {
      return this.createStream(request as ChatCompletionRequest & { stream: true }, options);
    }

    return this.createCompletion(request, options);
  }

  private async createCompletion(
    request: ChatCompletionRequest,
    options?: RequestOptions
  ): Promise<ChatCompletionResponse> {
    return this.client['request']<ChatCompletionResponse>(
      {
        method: HTTP_METHODS.POST,
        url: API_ENDPOINTS.V1.CHAT.COMPLETIONS,
        data: request,
      },
      options
    );
  }

  private async createStream(
    request: ChatCompletionRequest & { stream: true },
    options?: RequestOptions
  ): Promise<AsyncGenerator<ChatCompletionChunk, void, unknown>> {
    const response = await this.client['client'].post(API_ENDPOINTS.V1.CHAT.COMPLETIONS, request, {
      responseType: 'stream',
      signal: options?.signal,
      timeout: 0,
      headers: {
        ...options?.headers,
        ...(options?.correlationId && { 'X-Correlation-Id': options.correlationId }),
      },
    });

    return streamAsyncIterator(response.data as NodeJS.ReadableStream);
  }
}