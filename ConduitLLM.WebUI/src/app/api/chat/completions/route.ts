import { NextRequest } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerCoreClient } from '@/lib/server/sdk-config';
import type { ChatCompletionRequest } from '@knn_labs/conduit-core-client';

// POST /api/chat/completions - Create chat completions using Core SDK
export async function POST(request: NextRequest) {
  try {
    const body = await request.json() as ChatCompletionRequest;
    const coreClient = await getServerCoreClient();
    
    // Handle both streaming and non-streaming requests
    if (body.stream) {
      // For streaming, we need to return a proper SSE response
      // The SDK returns an async iterable for streaming
      const stream = await coreClient.chat.create({
        ...body,
        stream: true
      });

      // Create a ReadableStream directly instead of using TransformStream (Next.js 15 compatibility)
      const encoder = new TextEncoder();
      const readableStream = new ReadableStream({
        async start(controller) {
          try {
            for await (const chunk of stream) {
              // Format as Server-Sent Events
              const sseData = `data: ${JSON.stringify(chunk)}\n\n`;
              controller.enqueue(encoder.encode(sseData));
            }
            // Send the final [DONE] message
            controller.enqueue(encoder.encode('data: [DONE]\n\n'));
            controller.close();
          } catch (error) {
            // Send error as SSE event
            const errorData = {
              error: {
                message: error instanceof Error ? error.message : 'Stream error',
                type: 'stream_error'
              }
            };
            controller.enqueue(encoder.encode(`data: ${JSON.stringify(errorData)}\n\n`));
            controller.close();
          }
        }
      });

      // Return streaming response with proper headers
      const headers = new Headers();
      headers.set('Content-Type', 'text/event-stream');
      headers.set('Cache-Control', 'no-cache');
      headers.set('Connection', 'keep-alive');
      
      return new Response(readableStream, { headers });
    } else {
      // Non-streaming request
      const result = await coreClient.chat.create({
        ...body,
        stream: false
      });
      
      return Response.json(result);
    }
  } catch (error) {
    // For non-streaming errors, use the standard error handler
    return handleSDKError(error);
  }
}

// OPTIONS for CORS if needed
export async function OPTIONS() {
  const headers = new Headers();
  headers.set('Access-Control-Allow-Origin', '*');
  headers.set('Access-Control-Allow-Methods', 'POST, OPTIONS');
  headers.set('Access-Control-Allow-Headers', 'Content-Type, Authorization');
  
  return new Response(null, {
    status: 200,
    headers,
  });
}