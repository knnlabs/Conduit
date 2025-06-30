import { NextRequest, NextResponse } from 'next/server';
import { validateSession, createUnauthorizedResponse } from '@/lib/auth/middleware';

export async function POST(request: NextRequest) {
  try {
    // Validate session
    const validation = await validateSession(request);
    if (!validation.isValid) {
      return createUnauthorizedResponse(validation.error);
    }

    // Parse request body
    const body = await request.json();
    
    // Extract virtual key from request body or headers
    const virtualKey = body.virtual_key || request.headers.get('x-virtual-key');
    
    if (!virtualKey) {
      return NextResponse.json(
        { error: 'Virtual key is required' },
        { status: 400 }
      );
    }

    // Remove virtual_key from body before sending to API
    const { virtual_key, ...apiBody } = body;
    
    // Check if this is a streaming request
    const isStreaming = apiBody.stream === true;
    
    // Make direct API call to Conduit Core API
    const coreApiUrl = process.env.NEXT_PUBLIC_CONDUIT_CORE_API_URL || 'http://localhost:5000';
    const response = await fetch(`${coreApiUrl}/v1/chat/completions`, {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${virtualKey}`,
        'Content-Type': 'application/json',
        'Accept': isStreaming ? 'text/event-stream' : 'application/json',
      },
      body: JSON.stringify(apiBody),
    });

    if (!response.ok) {
      const errorData = await response.json().catch(() => ({ error: 'Unknown error' }));
      throw {
        status: response.status,
        message: errorData.error || `HTTP ${response.status}`,
      };
    }

    if (isStreaming) {
      // Handle streaming response
      if (!response.body) {
        throw new Error('No response body for streaming request');
      }

      // Pass through the streaming response
      return new NextResponse(response.body, {
        headers: {
          'Content-Type': 'text/event-stream',
          'Cache-Control': 'no-cache',
          'Connection': 'keep-alive',
        },
      });
    } else {
      // Handle non-streaming response
      const completion = await response.json();
      return NextResponse.json(completion);
    }
  } catch (error: any) {
    console.error('Chat Completions API error:', error);
    
    // Handle specific error types
    if (error?.status === 401) {
      return NextResponse.json(
        { error: 'Invalid virtual key' },
        { status: 401 }
      );
    }
    
    if (error?.status === 400) {
      return NextResponse.json(
        { error: error?.message || 'Invalid request' },
        { status: 400 }
      );
    }
    
    return NextResponse.json(
      { error: 'Failed to create chat completion' },
      { status: 500 }
    );
  }
}