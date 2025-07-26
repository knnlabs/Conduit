import type { RequestOptions } from '../client/types';
import type { StreamingResponse } from '../models/streaming';
import type { EnhancedStreamingResponse } from '../models/enhanced-streaming-response';
import type { EnhancedStreamEvent } from '../models/enhanced-streaming';
import type { ChatCompletionRequest } from '../models/chat';
import { createWebStream } from '../utils/web-streaming';
import { createEnhancedWebStream } from '../utils/enhanced-web-streaming';

/**
 * Base class for services that support streaming responses.
 * Provides common functionality for both standard and enhanced streaming.
 * 
 * @abstract
 */
export abstract class BaseStreamingService {
  /**
   * Creates a streaming request and returns the response
   * @abstract
   */
  protected abstract createStreamingRequest(
    request: unknown,
    options?: RequestOptions
  ): Promise<Response>;

  /**
   * Creates a standard streaming response
   * 
   * @template T The type of chunks in the stream
   * @param request The request object
   * @param options Optional request configuration
   * @returns A streaming response
   * @protected
   */
  protected async createStandardStream<T>(
    request: unknown,
    options?: RequestOptions
  ): Promise<StreamingResponse<T>> {
    const response = await this.createStreamingRequest(request, options);
    const stream = response.body;
    
    if (!stream) {
      throw new Error('Response body is not a stream');
    }

    return createWebStream<T>(
      stream,
      {
        signal: options?.signal,
        timeout: options?.timeout,
      }
    );
  }

  /**
   * Creates an enhanced streaming response that preserves SSE event types.
   * This allows access to metrics events and other enhanced streaming features.
   * 
   * @param request The request object
   * @param options Optional request configuration
   * @returns An enhanced streaming response with typed events
   * @protected
   */
  protected async createEnhancedStreamInternal(
    request: unknown,
    options?: RequestOptions
  ): Promise<EnhancedStreamingResponse<EnhancedStreamEvent>> {
    const response = await this.createStreamingRequest(request, options);
    const stream = response.body;
    
    if (!stream) {
      throw new Error('Response body is not a stream');
    }

    return createEnhancedWebStream(
      stream,
      {
        signal: options?.signal,
        timeout: options?.timeout,
      }
    );
  }

  /**
   * Converts legacy function parameters to the tools format
   * for backward compatibility
   * 
   * @param request The request object
   * @returns The processed request
   * @protected
   */
  protected convertLegacyFunctions(request: ChatCompletionRequest): ChatCompletionRequest {
    if (request.functions && !request.tools) {
      request.tools = request.functions.map((fn) => ({
        type: 'function' as const,
        function: fn
      }));
      delete request.functions;
    }
    
    if (request.function_call && !request.tool_choice) {
      if (typeof request.function_call === 'string') {
        request.tool_choice = request.function_call as 'none' | 'auto';
      } else {
        request.tool_choice = {
          type: 'function',
          function: request.function_call
        };
      }
      delete request.function_call;
    }
    
    return request;
  }
}