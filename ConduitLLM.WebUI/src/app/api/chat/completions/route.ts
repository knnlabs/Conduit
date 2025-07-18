import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerCoreClient } from '@/lib/server/coreClient';
import { type ChatCompletionChunk } from '@knn_labs/conduit-core-client';

// POST /api/chat/completions - Create chat completions using Core SDK
export async function POST(request: NextRequest) {

  try {
    const body: unknown = await request.json();
    const coreClient = await getServerCoreClient();
    
    // Check if streaming is requested
    if (typeof body === 'object' && body !== null && 'stream' in body && (body as { stream?: boolean }).stream === true) {
      // Create a TransformStream for SSE
      const encoder = new TextEncoder();
      const stream = new TransformStream();
      const writer = stream.writable.getWriter();
      
      // Start the streaming request
      void (async () => {
        try {
          // Create the request with explicit stream: true type
          const requestData = {
            ...(body as Record<string, unknown>),
            stream: true,
          } as Record<string, unknown>;
          const streamResponse = (await coreClient.chat.create(requestData as unknown as Parameters<typeof coreClient.chat.create>[0])) as unknown as AsyncIterable<ChatCompletionChunk>;
          
          // Handle the async iterator from the SDK
          for await (const chunk of streamResponse) {
            // Format as SSE
            const data = `data: ${JSON.stringify(chunk)}

`;
            await writer.write(encoder.encode(data));
          }
          
          // Send the [DONE] message
          await writer.write(encoder.encode('data: [DONE]\n\n'));
        } catch (error: unknown) {
          console.error('Streaming error:', error);
          const errorMessage = error instanceof Error ? error.message : 'Unknown error';
          const errorData = `data: ${JSON.stringify({ error: errorMessage })}

`;
          await writer.write(encoder.encode(errorData));
        } finally {
          await writer.close();
        }
      })();
      
      // Return SSE response
      return new Response(stream.readable, {
        headers: new Headers([
          ['Content-Type', 'text/event-stream'],
          ['Cache-Control', 'no-cache'],
          ['Connection', 'keep-alive'],
        ]),
      });
    } else {
      // Non-streaming request
      const requestData = {
        ...(body as Record<string, unknown>),
        stream: false,
      } as Record<string, unknown>;
      const result = await coreClient.chat.create(requestData as unknown as Parameters<typeof coreClient.chat.create>[0]);
      
      return NextResponse.json(result);
    }
  } catch (error) {
    return handleSDKError(error);
  }
}