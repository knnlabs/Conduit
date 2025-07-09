import { useMutation, UseMutationOptions } from '@tanstack/react-query';
import { useConduit } from '../ConduitProvider';
import type { 
  ChatCompletionRequest, 
  ChatCompletionResponse,
  ChatCompletionChunk
} from '../../models/chat';

export interface UseChatCompletionOptions 
  extends Omit<
    UseMutationOptions<ChatCompletionResponse, Error, ChatCompletionRequest>,
    'mutationFn'
  > {}

export interface StreamingOptions {
  onChunk: (chunk: ChatCompletionChunk) => void;
  onComplete?: () => void;
  onError?: (error: Error) => void;
}

export interface UseChatCompletionStreamOptions 
  extends Omit<
    UseMutationOptions<ChatCompletionResponse, Error, ChatCompletionRequest & { streamingOptions?: StreamingOptions }>,
    'mutationFn'
  > {}

export function useChatCompletion(options?: UseChatCompletionOptions) {
  const { client } = useConduit();

  return useMutation({
    mutationFn: async (request: ChatCompletionRequest) => {
      // Ensure stream is false for non-streaming requests
      return await client.chat.completions.create({ ...request, stream: false });
    },
    ...options,
  });
}

export function useChatCompletionStream(options?: UseChatCompletionStreamOptions) {
  const { client } = useConduit();

  return useMutation({
    mutationFn: async ({ streamingOptions, ...request }: ChatCompletionRequest & { streamingOptions?: StreamingOptions }) => {
      try {
        // Force stream to true for streaming requests
        const stream = await client.chat.completions.create({ ...request, stream: true });
        
        // Accumulate chunks to build the final response
        let aggregatedContent = '';
        let lastChunk: ChatCompletionChunk | null = null;
        
        // Process stream chunks
        for await (const chunk of stream) {
          lastChunk = chunk;
          
          // Call the onChunk callback if provided
          streamingOptions?.onChunk(chunk);
          
          // Accumulate content from the chunk
          if (chunk.choices?.[0]?.delta?.content) {
            aggregatedContent += chunk.choices[0].delta.content;
          }
        }
        
        // Call onComplete if provided
        streamingOptions?.onComplete?.();
        
        // Return a complete response assembled from the chunks
        return {
          id: lastChunk?.id || 'stream',
          object: 'chat.completion' as const,
          created: lastChunk?.created || Date.now(),
          model: lastChunk?.model || request.model,
          system_fingerprint: lastChunk?.system_fingerprint,
          choices: [{
            index: 0,
            message: {
              role: 'assistant',
              content: aggregatedContent,
              tool_calls: lastChunk?.choices?.[0]?.delta?.tool_calls || undefined,
            },
            finish_reason: lastChunk?.choices?.[0]?.finish_reason || 'stop',
          }],
          usage: lastChunk?.usage || {
            prompt_tokens: 0,
            completion_tokens: 0,
            total_tokens: 0,
          },
        } as ChatCompletionResponse;
      } catch (error) {
        // Call onError if provided
        streamingOptions?.onError?.(error as Error);
        throw error;
      }
    },
    ...options,
  });
}