import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerCoreClient } from '@/lib/server/sdk-config';
import type { ChatCompletionRequest } from '@/app/chat/types';

// POST /api/chat/completions - Create chat completions using Core SDK
export async function POST(request: NextRequest) {

  try {
    const coreClient = await getServerCoreClient();
    const body = await request.json() as ChatCompletionRequest;
    
    // Check if streaming is requested
    if (body.stream === true) {
      // Create a TransformStream for SSE
      const encoder = new TextEncoder();
      const stream = new TransformStream();
      const writer = stream.writable.getWriter();
      
      // Start the streaming request
      void (async () => {
        try {
          // Create the enhanced streaming request to get SSE events with types
          const streamResponse = await coreClient.chat.createEnhancedStream({
            ...body,
            stream: true
          });
          
          // Handle the async iterator from the SDK
          let chunkCount = 0;
          for await (const event of streamResponse) {
            // Debug first few events to see what we're receiving
            if (process.env.NODE_ENV === 'development' && chunkCount < 3) {
              console.warn('SDK event:', JSON.stringify(event, null, 2));
              chunkCount++;
            }
            
            // Handle different event types
            const eventType = event.type as string;
            switch (eventType) {
              case 'content': {
                // Regular chat completion chunk
                const data = `data: ${JSON.stringify(event.data)}\n\n`;
                await writer.write(encoder.encode(data));
                break;
              }
                
              case 'metrics': {
                // Live metrics update
                const metricsEvent = `event: metrics\ndata: ${JSON.stringify(event.data)}\n\n`;
                await writer.write(encoder.encode(metricsEvent));
                break;
              }
                
              case 'metrics-final': {
                // Final metrics
                const finalMetricsEvent = `event: metrics-final\ndata: ${JSON.stringify(event.data)}\n\n`;
                await writer.write(encoder.encode(finalMetricsEvent));
                break;
              }
                
              case 'error': {
                // Handle error events
                const errorEvent = `event: error\ndata: ${JSON.stringify(event.data)}\n\n`;
                await writer.write(encoder.encode(errorEvent));
                console.error('Error event from SDK:', event.data);
                break;
              }
                
              case 'done':
                // Stream is done
                break;
                
              default:
                // Unknown event type, log it
                console.warn('Unknown event type:', eventType);
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
      const result = await coreClient.chat.create({
        ...body,
        stream: false
      });
      
      return NextResponse.json(result);
    }
  } catch (error) {
    return handleSDKError(error);
  }
}