import { getBrowserCoreClient } from './browserCoreClient';
import type { 
  StreamingCallbacks, 
  StreamMessageOptions
} from '@knn_labs/conduit-core-client';

// Define local types to match SDK expectations
interface ChatCompletionChunk {
  id?: string;
  object?: string;
  created?: number;
  model?: string;
  choices: Array<{
    index: number;
    delta: {
      role?: string;
      content?: string;
      reasoning?: string;
      channel?: string;
      [key: string]: unknown;
    };
    finish_reason?: string;
  }>;
  usage?: {
    prompt_tokens?: number;
    completion_tokens?: number;
    total_tokens?: number;
  };
}

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
      let tokenCount = 0;
      const startTime = Date.now();
      let firstTokenTime: number | null = null;

      // Process the stream
      for await (const chunk of stream) {
        // Track first token time for performance metrics
        if (!firstTokenTime && chunk.choices?.[0]?.delta?.content) {
          firstTokenTime = Date.now();
        }

        // Handle chunk callback
        if (callbacks.onChunk) {
          // Convert SDK chunk to our local type
          const localChunk: ChatCompletionChunk = {
            id: chunk.id,
            object: chunk.object,
            created: chunk.created,
            model: chunk.model,
            choices: chunk.choices?.map(choice => ({
              index: choice.index,
              delta: {
                role: choice.delta?.role,
                content: choice.delta?.content,
                reasoning: choice.delta?.reasoning,
                channel: choice.delta?.channel,
                ...(choice.delta ?? {})
              },
              finish_reason: choice.finish_reason ?? undefined
            })) ?? [],
            usage: chunk.usage
          };
          callbacks.onChunk(localChunk as unknown as ChatCompletionChunk);
        }

        // Handle content updates
        const content = chunk.choices?.[0]?.delta?.content;
        if (content) {
          totalContent += content;
          tokenCount++;
          
          if (callbacks.onContent) {
            callbacks.onContent(content, totalContent);
          }

          // Calculate tokens per second if requested
          if (callbacks.onTokensPerSecond && this.config.showTokensPerSecond) {
            const elapsedSeconds = (Date.now() - startTime) / 1000;
            if (elapsedSeconds > 0) {
              const tokensPerSecond = tokenCount / elapsedSeconds;
              callbacks.onTokensPerSecond(tokensPerSecond);
            }
          }
        }

        // Check for completion
        const finishReason = chunk.choices?.[0]?.finish_reason;
        if (finishReason) {
          // Calculate final performance metrics
          const totalTime = Date.now() - startTime;
          const timeToFirstToken = firstTokenTime ? firstTokenTime - startTime : undefined;
          
          const metadata = {
            model: options.model,
            finish_reason: finishReason,
            total_tokens: tokenCount,
            completion_tokens: tokenCount,
            prompt_tokens: undefined, // Would need to track separately
            total_time_ms: totalTime,
            time_to_first_token_ms: timeToFirstToken,
            tokens_per_second: tokenCount / (totalTime / 1000),
            ...(chunk.usage && { usage: chunk.usage })
          };

          if (callbacks.onComplete) {
            callbacks.onComplete({
              content: totalContent,
              metadata
            });
          }
          break;
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