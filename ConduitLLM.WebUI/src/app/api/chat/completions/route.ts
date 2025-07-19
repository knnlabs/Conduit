import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerCoreClient } from '@/lib/server/coreClient';
// Note: ChatCompletionChunk import temporarily removed due to SDK export issues

// POST /api/chat/completions - Create chat completions using Core SDK
export async function POST(request: NextRequest) {

  try {
    const coreClient = await getServerCoreClient();
    const body = await request.json() as unknown as Parameters<typeof coreClient.chat.create>[0];
    
    // Check if streaming is requested
    if (body.stream === true) {
      // Create a TransformStream for SSE
      const encoder = new TextEncoder();
      const stream = new TransformStream();
      const writer = stream.writable.getWriter();
      
      // Start the streaming request
      void (async () => {
        try {
          // Create the streaming request - the SDK will handle validation
          const streamResponse = await coreClient.chat.create(body);
          
          // Handle the async iterator from the SDK
          let chunkCount = 0;
          for await (const chunk of streamResponse) {
            // Debug first few chunks to see what we're receiving
            if (process.env.NODE_ENV === 'development' && chunkCount < 3) {
              console.warn('SDK chunk:', JSON.stringify(chunk, null, 2));
              chunkCount++;
            }
            
            // The SDK returns ChatCompletionChunk objects
            // Format as SSE data event
            const data = `data: ${JSON.stringify(chunk)}\n\n`;
            await writer.write(encoder.encode(data));
            
            // Check for metrics in the chunk - using generic type due to SDK export issues
            const typedChunk = chunk as { performance?: Record<string, unknown> };
            if (typedChunk.performance && Object.keys(typedChunk.performance).length > 0) {
              const metricsEvent = `event: metrics\ndata: ${JSON.stringify(typedChunk.performance)}\n\n`;
              await writer.write(encoder.encode(metricsEvent));
            }
          }
          
          // Send the [DONE] message
          await writer.write(encoder.encode('data: [DONE]\n\n'));
        } catch (error: unknown) { // Keep as unknown - generic error handling for various API errors
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
      const result = await coreClient.chat.create(body);
      
      return NextResponse.json(result);
    }
  } catch (error) {
    return handleSDKError(error);
  }
}