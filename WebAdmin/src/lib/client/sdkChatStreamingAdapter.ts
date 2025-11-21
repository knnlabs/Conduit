import { getBrowserCoreClient } from './browserCoreClient';
import {
  isChatCompletionChunk,
  isStreamingMetrics,
  isFinalMetrics,
  buildMessageContent,
  type StreamingCallbacks,
  type StreamMessageOptions,
  type StreamingError
} from '@knn_labs/conduit-core-client';


/**
 * Adapter class that uses the SDK client directly for chat streaming
 * instead of making raw fetch calls to API endpoints.
 */
export class SDKChatStreamingAdapter {
  private abortController: AbortController | null = null;

  constructor(
    private config: {
      timeoutMs?: number;
      trackPerformanceMetrics?: boolean;
      showTokensPerSecond?: boolean;
      useServerMetrics?: boolean;
      enableLogging?: boolean;
    }
  ) {}

  /**
   * Stream a chat message using the SDK client
   */
  async streamMessage(
    message: string,
    options: StreamMessageOptions,
    callbacks: StreamingCallbacks
  ): Promise<void> {
    // Track content and tool calls for callbacks (declared outside try to be accessible in catch)
    let totalContent = '';
    let totalReasoning = '';
    const toolCalls: Array<{
      id: string;
      type: 'function';
      function: {
        name: string;
        arguments: string;
      };
    }> = [];

    try {
      // Get the SDK client with ephemeral key
      const client = await getBrowserCoreClient();
      
      // Create abort controller for cancellation
      this.abortController = new AbortController();
      
      // Prepare the chat request
      const chatRequest = {
        messages: [
          ...(options.systemPrompt ? [{ role: 'system' as const, content: options.systemPrompt }] : []),
          ...(options.messages ?? []),
          {
            role: 'user' as const,
            content: buildMessageContent(message, options.images)
          }
        ],
        model: options.model,
        temperature: options.temperature,
        max_tokens: options.maxTokens,
        top_p: options.topP,
        frequency_penalty: options.frequencyPenalty,
        presence_penalty: options.presencePenalty,
        seed: options.seed,
        stop: options.stop,
        response_format: options.responseFormat ? { type: options.responseFormat } : undefined,
        stream: true as const,
        function_configuration_ids: options.functionConfigurationIds,
        // Include dynamic parameters
        ...(options.dynamicParameters ?? {})
      };

      // Start callback
      if (callbacks.onStart) {
        callbacks.onStart();
      }

      // Use SDK to create streaming chat
      const stream = await client.chat.create(chatRequest, {
        signal: this.abortController.signal
      });

      // Process the stream
      for await (const data of stream) {
        // Handle different event types from the stream
        if (isChatCompletionChunk(data)) {
          // Handle chunk callback
          if (callbacks.onChunk) {
            // Transform SDK chunk to match expected callback type
            // The main difference is finish_reason can be null in SDK but callback expects string | undefined
            const transformedChunk = {
              ...data,
              choices: data.choices?.map(choice => ({
                ...choice,
                finish_reason: choice.finish_reason ?? undefined
              })) ?? []
            };
            callbacks.onChunk(transformedChunk as Parameters<typeof callbacks.onChunk>[0]);
          }

          // Handle content updates
          const content = data.choices?.[0]?.delta?.content;
          if (content) {
            totalContent += content;

            if (callbacks.onContent) {
              callbacks.onContent(content, totalContent);
            }
          }

          // Also handle reasoning content (for models like gpt-oss-20b that output to reasoning)
          const reasoning = data.choices?.[0]?.delta?.reasoning;
          if (reasoning) {
            totalReasoning += reasoning;
          }

          // Handle tool calls in streaming response
          const deltaToolCalls = data.choices?.[0]?.delta?.tool_calls as Array<{
            index?: number;
            id?: string;
            type?: 'function';
            function?: {
              name?: string;
              arguments?: string;
            };
          }> | undefined;

          if (deltaToolCalls && Array.isArray(deltaToolCalls)) {
            for (const toolCall of deltaToolCalls) {
              const index = toolCall.index ?? 0;

              // Initialize tool call if it doesn't exist
              if (!toolCalls[index]) {
                toolCalls[index] = {
                  id: toolCall.id ?? `call_${index}`,
                  type: 'function',
                  function: {
                    name: toolCall.function?.name ?? '',
                    arguments: toolCall.function?.arguments ?? ''
                  }
                };
              } else {
                // Append to existing tool call
                if (toolCall.id) {
                  toolCalls[index].id = toolCall.id;
                }
                if (toolCall.function?.name) {
                  toolCalls[index].function.name += toolCall.function.name;
                }
                if (toolCall.function?.arguments) {
                  toolCalls[index].function.arguments += toolCall.function.arguments;
                }
              }
            }
          }

          // Check for completion in chunk (some providers send finish_reason in a chunk)
          const finishReason = data.choices?.[0]?.finish_reason;
          if (finishReason) {
            // Some providers send finish_reason without final metrics
            // We'll handle completion here if needed
          }
        } else if (isStreamingMetrics(data)) {
          // Handle streaming metrics updates
          // Cast to unknown first, then to expected shape to satisfy ESLint
          const metrics = data as unknown as {
            current_tokens_per_second?: number;
            tokens_per_second?: number;
            [key: string]: unknown;
          };
          
          // Update tokens per second if available
          if (callbacks.onTokensPerSecond && this.config.showTokensPerSecond) {
            const tokensPerSecond = metrics.current_tokens_per_second ?? metrics.tokens_per_second;
            if (tokensPerSecond !== undefined && typeof tokensPerSecond === 'number') {
              callbacks.onTokensPerSecond(tokensPerSecond);
            }
          }
          
          // Pass metrics to callback if available
          if (callbacks.onMetrics) {
            callbacks.onMetrics(data as Parameters<typeof callbacks.onMetrics>[0]);
          }
        } else if (isFinalMetrics(data)) {
          // Handle final metrics - this has the accurate token counts and timing
          // Cast to unknown first, then to expected shape to satisfy ESLint
          const finalMetrics = data as unknown as {
            model?: string;
            total_tokens?: number;
            completion_tokens?: number;
            prompt_tokens?: number;
            total_latency_ms?: number;
            time_to_first_token_ms?: number;
            tokens_per_second?: number;
            completion_tokens_per_second?: number;
            provider?: string;
          };

          // Build metadata from server-provided final metrics
          // The backend calculates accurate timing using Stopwatch from request start to completion
          const metadata = {
            model: finalMetrics.model ?? options.model,
            finishReason: 'stop' as const, // FinalMetrics indicate completion
            tokensUsed: finalMetrics.total_tokens ?? undefined,
            completionTokens: finalMetrics.completion_tokens ?? undefined,
            promptTokens: finalMetrics.prompt_tokens ?? undefined,
            latency: finalMetrics.total_latency_ms ?? undefined,
            timeToFirstToken: finalMetrics.time_to_first_token_ms ?? undefined,
            tokensPerSecond: finalMetrics.tokens_per_second ?? finalMetrics.completion_tokens_per_second ?? undefined,
            streaming: true,
            provider: finalMetrics.provider ?? undefined,
            toolCalls: toolCalls.length > 0 ? toolCalls : undefined
          };

          if (callbacks.onComplete) {
            // Use reasoning as fallback if no regular content was received
            // This handles models like gpt-oss-20b that non-deterministically output to reasoning
            const finalContent = totalContent.length > 0 ? totalContent : totalReasoning;

            callbacks.onComplete({
              content: finalContent,
              metadata
            });
          }
          break; // Stream is complete
        }
      }
    } catch (error) {
      // Handle abort separately
      if (error instanceof Error && error.name === 'AbortError') {
        if (callbacks.onAbort) {
          callbacks.onAbort();
        }
      } else {
        if (callbacks.onError) {
          // Convert error to StreamingError
          let streamingError: StreamingError;

          if (error && typeof error === 'object' && 'status' in error && 'code' in error) {
            // Already a StreamingError from SDK
            streamingError = error as StreamingError;
          } else {
            // Create StreamingError from generic error
            const baseError = error instanceof Error ? error : new Error(String(error));
            const partialContent = totalContent.length > 0 ? totalContent : totalReasoning;
            streamingError = Object.assign(baseError, {
              context: partialContent.length > 0 ? `Partial content: ${partialContent.slice(0, 100)}...` : undefined,
              retryable: false
            } as Partial<StreamingError>);
          }

          callbacks.onError(streamingError);
        }
      }
    } finally {
      this.abortController = null;
    }
  }

  /**
   * Abort the current streaming operation
   */
  abort(): void {
    if (this.abortController) {
      this.abortController.abort();
      this.abortController = null;
    }
  }

  /**
   * Clean up resources
   */
  dispose(): void {
    this.abort();
  }
}