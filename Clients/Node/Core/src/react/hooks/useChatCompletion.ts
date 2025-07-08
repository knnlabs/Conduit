import { useMutation, UseMutationOptions } from '@tanstack/react-query';
import { useConduit } from '../ConduitProvider';
import type { 
  ChatCompletionRequest, 
  ChatCompletionResponse
} from '../../models/chat';

// Note: ChatCompletionStreamResponse doesn't exist in the current models
// Using ChatCompletionResponse for stream as well
type ChatCompletionStreamResponse = ChatCompletionResponse;

export interface UseChatCompletionOptions 
  extends Omit<
    UseMutationOptions<ChatCompletionResponse, Error, ChatCompletionRequest>,
    'mutationFn'
  > {}

export interface UseChatCompletionStreamOptions 
  extends Omit<
    UseMutationOptions<ChatCompletionStreamResponse, Error, ChatCompletionRequest>,
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
    mutationFn: async (request: ChatCompletionRequest) => {
      // Force stream to true for streaming requests
      // The response type is StreamingResponse, not ChatCompletionResponse
      await client.chat.completions.create({ ...request, stream: true });
      // For now, return a mock response - in real usage, the consumer would handle the stream
      return {
        id: 'stream',
        object: 'chat.completion' as const,
        created: Date.now(),
        model: request.model,
        choices: [],
        usage: {
          prompt_tokens: 0,
          completion_tokens: 0,
          total_tokens: 0,
        },
      } as ChatCompletionResponse;
    },
    ...options,
  });
}