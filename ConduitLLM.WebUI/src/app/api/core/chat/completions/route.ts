import { createCoreRoute } from '@/lib/utils/core-route-helpers';
import { validateChatCompletionRequest } from '@/lib/utils/core-route-validators';
import { withSDKErrorHandling } from '@/lib/errors/sdk-errors';
import { transformSDKResponse, createStreamingResponse } from '@/lib/utils/sdk-transforms';
import { getServerCoreClient } from '@/lib/clients/server';

export const POST = createCoreRoute(
  {
    requireVirtualKey: false,
    validateBody: validateChatCompletionRequest,
    logContext: 'chat_completions'
  },
  async ({ virtualKey }, body) => {
    // Remove virtual_key from body before sending to API
    const { virtual_key: _virtualKey, ...chatRequest } = body as Record<string, unknown>;
    
    // Get Core client with the virtual key
    const coreClient = getServerCoreClient(virtualKey);
    
    // Check if this is a streaming request
    const isStreaming = chatRequest.stream === true;
    
    if (isStreaming) {
      // Handle streaming response using SDK
      const stream = await withSDKErrorHandling(
        async () => coreClient.chat.completions.create({
          ...chatRequest,
          stream: true,
        } as never),
        'create chat completion stream'
      );

      // Return SSE stream
      return createStreamingResponse(stream as unknown as AsyncIterable<unknown>, {
        transformer: (chunk: unknown) => {
          // Transform SDK chunk to OpenAI format SSE
          if (typeof chunk === 'object' && chunk !== null && 'object' in chunk && (chunk as Record<string, unknown>).object === 'chat.completion.chunk') {
            return `data: ${JSON.stringify(chunk)}\n\n`;
          }
          // Handle special tokens
          if (chunk === '[DONE]') {
            return 'data: [DONE]\n\n';
          }
          return `data: ${JSON.stringify(chunk)}\n\n`;
        }
      });
    } else {
      // Handle non-streaming response
      const completion = await withSDKErrorHandling(
        async () => coreClient.chat.completions.create({
          ...chatRequest,
          stream: false,
        } as never),
        'create chat completion'
      );

      return transformSDKResponse(completion, {
        meta: {
          virtualKeyUsed: virtualKey.substring(0, 8) + '...',
          streaming: false,
        }
      });
    }
  }
);

// Support for GET to check endpoint availability
export async function GET(_request: Request) {
  return transformSDKResponse({
    endpoint: '/v1/chat/completions',
    methods: ['POST'],
    description: 'Create chat completions with streaming support',
    authentication: 'Virtual key required',
    models: [
      'gpt-4',
      'gpt-4-turbo',
      'gpt-3.5-turbo',
      'claude-3-opus',
      'claude-3-sonnet',
      'claude-3-haiku',
      'llama-3.1-405b',
      'mistral-large',
    ],
  });
}