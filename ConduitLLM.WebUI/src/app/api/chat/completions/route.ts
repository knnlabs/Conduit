import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerCoreClient } from '@/lib/server/coreClient';
// POST /api/chat/completions - Create chat completions using Core SDK
export async function POST(request: NextRequest) {

  try {
    const body = await request.json();
    const coreClient = await getServerCoreClient();
    
    // Check if streaming is requested
    if (body.stream === true) {
      // Create a TransformStream for SSE
      const encoder = new TextEncoder();
      const stream = new TransformStream();
      const writer = stream.writable.getWriter();
      
      // Start the streaming request
      (async () => {
        try {
          // Create the request with explicit stream: true type
          const streamResponse = await coreClient.chat.create({
            ...body,
            stream: true,
          }) as any; // Type assertion to work around TypeScript inference issue
          
          // Handle the async iterator from the SDK
          for await (const chunk of streamResponse) {
            // Format as SSE
            const data = `data: ${JSON.stringify(chunk)}\n\n`;
            await writer.write(encoder.encode(data));
          }
          
          // Send the [DONE] message
          await writer.write(encoder.encode('data: [DONE]\n\n'));
        } catch (error: any) {
          console.error('Streaming error:', error);
          const errorData = `data: ${JSON.stringify({ error: error.message })}\n\n`;
          await writer.write(encoder.encode(errorData));
        } finally {
          await writer.close();
        }
      })();
      
      // Return SSE response
      return new Response(stream.readable, {
        headers: {
          'Content-Type': 'text/event-stream',
          'Cache-Control': 'no-cache',
          'Connection': 'keep-alive',
        },
      });
    } else {
      // Non-streaming request
      const result = await coreClient.chat.create({
        ...body,
        stream: false,
      });
      
      return NextResponse.json(result);
    }
  } catch (error) {
    return handleSDKError(error);
  }
}