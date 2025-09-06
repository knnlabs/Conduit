import { getBrowserCoreClient } from './browserCoreClient';
import { 
  isChatCompletionChunk,
  isStreamingMetrics,
  isFinalMetrics,
  type StreamingCallbacks, 
  type StreamMessageOptions
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
    try {
      // Get the SDK client with ephemeral key
      const client = await getBrowserCoreClient();
      
      // Create abort controller for cancellation
      this.abortController = new AbortController();
      
      // Prepare the chat request
      const chatRequest = {
        messages: [
          ...(options.messages ?? []),
          { 
            role: 'user' as const, 
            content: message,
            // Include images if provided
            ...(options.images && { images: options.images })
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

      // Track content and performance
      let totalContent = '';
      const startTime = Date.now();
      let firstTokenTime: number | null = null;

      // Process the stream
      for await (const data of stream) {
        // Handle different event types from the stream
        if (isChatCompletionChunk(data)) {
          // Track first token time for performance metrics
          if (!firstTokenTime && data.choices?.[0]?.delta?.content) {
            firstTokenTime = Date.now();
          }

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
          // Handle final metrics - this has the accurate token counts
          // Cast to unknown first, then to expected shape to satisfy ESLint
          const finalMetrics = data as unknown as {
            model?: string;
            total_tokens?: number;
            completion_tokens?: number;
            prompt_tokens?: number;
            time_to_first_token_ms?: number;
            tokens_per_second?: number;
            completion_tokens_per_second?: number;
            provider?: string;
          };
          
          // Calculate final timings
          const totalTime = Date.now() - startTime;
          const timeToFirstToken = firstTokenTime ? firstTokenTime - startTime : undefined;
          
          // Build metadata from final metrics (using correct field names)
          const metadata = {
            model: finalMetrics.model ?? options.model,
            finishReason: 'stop' as const, // FinalMetrics indicate completion
            tokensUsed: finalMetrics.total_tokens ?? undefined,
            completionTokens: finalMetrics.completion_tokens ?? undefined,
            promptTokens: finalMetrics.prompt_tokens ?? undefined,
            latency: totalTime,
            timeToFirstToken: timeToFirstToken ?? finalMetrics.time_to_first_token_ms ?? undefined,
            tokensPerSecond: finalMetrics.tokens_per_second ?? finalMetrics.completion_tokens_per_second ?? undefined,
            streaming: true,
            provider: finalMetrics.provider ?? undefined
          };

          if (callbacks.onComplete) {
            callbacks.onComplete({
              content: totalContent,
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
          // Convert error to expected format
          const errorMessage = error instanceof Error ? error : new Error(String(error));
          callbacks.onError(errorMessage);
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