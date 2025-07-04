
import { validateCoreSession, extractVirtualKey } from '@/lib/auth/sdk-auth';
import { mapSDKErrorToResponse, withSDKErrorHandling } from '@/lib/errors/sdk-errors';
import { transformSDKResponse, createStreamingResponse } from '@/lib/utils/sdk-transforms';
import { getServerCoreClient } from '@/lib/clients/server';
import { createValidationError } from '@/lib/utils/route-helpers';

export async function POST(request: NextRequest) {
  try {
    // Validate session - don't require virtual key in session yet
    const validation = await validateCoreSession(request, { requireVirtualKey: false });
    if (!validation.isValid) {
      return new Response(
        JSON.stringify({ error: validation.error || 'Unauthorized' }),
        { status: 401, headers: { 'Content-Type': 'application/json' } }
      );
    }

    // Parse request body
    const body = await request.json();
    
    // Extract virtual key from various sources
    const virtualKey = body.virtual_key || 
                      extractVirtualKey(request) || 
                      validation.session?.virtualKey;
    
    if (!virtualKey) {
      return createValidationError(
        'Virtual key is required. Provide it via virtual_key field, x-virtual-key header, or Authorization header',
        { missingField: 'virtual_key' }
      );
    }

    // Remove virtual_key from body before sending to API
    const { virtual_key: _virtualKey, ...chatRequest } = body;
    
    // Validate required fields
    if (!chatRequest.messages || !Array.isArray(chatRequest.messages)) {
      return createValidationError(
        'Messages array is required',
        { missingField: 'messages' }
      );
    }

    // Get Core client with the virtual key
    const coreClient = getServerCoreClient(virtualKey);
    
    // Check if this is a streaming request
    const isStreaming = chatRequest.stream === true;
    
    if (isStreaming) {
      // Handle streaming response using SDK
      const stream = await withSDKErrorHandling(
        async () => coreClient.chat.completions.create({
          model: chatRequest.model,
          messages: chatRequest.messages,
          stream: true,
          temperature: chatRequest.temperature,
          max_tokens: chatRequest.max_tokens,
          top_p: chatRequest.top_p,
          frequency_penalty: chatRequest.frequency_penalty,
          presence_penalty: chatRequest.presence_penalty,
          stop: chatRequest.stop,
          tools: chatRequest.tools,
          tool_choice: chatRequest.tool_choice,
          response_format: chatRequest.response_format,
          seed: chatRequest.seed,
          logprobs: chatRequest.logprobs,
          top_logprobs: chatRequest.top_logprobs,
          n: chatRequest.n,
          logit_bias: chatRequest.logit_bias,
        }),
        'create chat completion stream'
      );

      // Return SSE stream
      return createStreamingResponse(stream, {
        transformer: (chunk) => {
          // Transform SDK chunk to OpenAI format SSE
          if (chunk.object === 'chat.completion.chunk') {
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
          model: chatRequest.model,
          messages: chatRequest.messages,
          stream: false,
          temperature: chatRequest.temperature,
          max_tokens: chatRequest.max_tokens,
          top_p: chatRequest.top_p,
          frequency_penalty: chatRequest.frequency_penalty,
          presence_penalty: chatRequest.presence_penalty,
          stop: chatRequest.stop,
          tools: chatRequest.tools,
          tool_choice: chatRequest.tool_choice,
          response_format: chatRequest.response_format,
          seed: chatRequest.seed,
          logprobs: chatRequest.logprobs,
          top_logprobs: chatRequest.top_logprobs,
          n: chatRequest.n,
          logit_bias: chatRequest.logit_bias,
        }),
        'create chat completion'
      );

      return transformSDKResponse(completion, {
        meta: {
          virtualKeyUsed: virtualKey.substring(0, 8) + '...',
          streaming: false,
        }
      });
    }
  } catch (error: unknown) {
    // Handle validation errors specially
    if ((error as { message?: string })?.message?.includes('required')) {
      return createValidationError((error as { message?: string })?.message);
    }
    
    return mapSDKErrorToResponse(error);
  }
}

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