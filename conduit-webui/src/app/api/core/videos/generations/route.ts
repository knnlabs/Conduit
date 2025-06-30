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
    
    // Make direct API call to Conduit Core API
    const coreApiUrl = process.env.NEXT_PUBLIC_CONDUIT_CORE_API_URL || 'http://localhost:5000';
    const response = await fetch(`${coreApiUrl}/v1/videos/generations`, {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${virtualKey}`,
        'Content-Type': 'application/json',
        'Accept': 'application/json',
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

    const videoGeneration = await response.json();
    
    return NextResponse.json(videoGeneration);
  } catch (error: any) {
    console.error('Video Generation API error:', error);
    
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
    
    if (error?.status === 429) {
      return NextResponse.json(
        { error: 'Rate limit exceeded' },
        { status: 429 }
      );
    }
    
    return NextResponse.json(
      { error: 'Failed to generate video' },
      { status: 500 }
    );
  }
}

export async function GET(request: NextRequest) {
  try {
    // Validate session for getting video generation status/history
    const validation = await validateSession(request);
    if (!validation.isValid) {
      return createUnauthorizedResponse(validation.error);
    }

    const { searchParams } = new URL(request.url);
    const virtualKey = searchParams.get('virtual_key') || request.headers.get('x-virtual-key');
    const taskId = searchParams.get('task_id');
    
    if (!virtualKey) {
      return NextResponse.json(
        { error: 'Virtual key is required' },
        { status: 400 }
      );
    }

    // Build URL with optional task_id parameter
    const coreApiUrl = process.env.NEXT_PUBLIC_CONDUIT_CORE_API_URL || 'http://localhost:5000';
    const url = new URL(`${coreApiUrl}/v1/videos/generations`);
    if (taskId) {
      url.searchParams.append('task_id', taskId);
    }
    
    // Make direct API call
    const response = await fetch(url.toString(), {
      method: 'GET',
      headers: {
        'Authorization': `Bearer ${virtualKey}`,
        'Accept': 'application/json',
      },
    });

    if (!response.ok) {
      const errorData = await response.json().catch(() => ({ error: 'Unknown error' }));
      throw {
        status: response.status,
        message: errorData.error || `HTTP ${response.status}`,
      };
    }

    const result = await response.json();
    return NextResponse.json(result);
  } catch (error: any) {
    console.error('Video Generation Status/History API error:', error);
    return NextResponse.json(
      { error: 'Failed to fetch video generation data' },
      { status: 500 }
    );
  }
}