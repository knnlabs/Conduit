import { NextRequest, NextResponse } from 'next/server';
import { validateSession, createUnauthorizedResponse } from '@/lib/auth/middleware';

export async function POST(request: NextRequest) {
  try {
    // Validate session
    const validation = await validateSession(request);
    if (!validation.isValid) {
      return createUnauthorizedResponse(validation.error);
    }

    // Parse multipart form data for file upload
    const formData = await request.formData();
    
    // Extract virtual key from form data or headers
    const virtualKey = formData.get('virtual_key') as string || request.headers.get('x-virtual-key');
    
    if (!virtualKey) {
      return NextResponse.json(
        { error: 'Virtual key is required' },
        { status: 400 }
      );
    }

    // Extract audio file
    const audioFile = formData.get('file') as File;
    if (!audioFile) {
      return NextResponse.json(
        { error: 'Audio file is required' },
        { status: 400 }
      );
    }

    // Prepare form data for the API call (remove virtual_key)
    const apiFormData = new FormData();
    for (const [key, value] of formData.entries()) {
      if (key !== 'virtual_key') {
        apiFormData.append(key, value);
      }
    }
    
    // Make direct API call to Conduit Core API
    const coreApiUrl = process.env.NEXT_PUBLIC_CONDUIT_CORE_API_URL || 'http://localhost:5000';
    const response = await fetch(`${coreApiUrl}/v1/audio/transcriptions`, {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${virtualKey}`,
      },
      body: apiFormData,
    });

    if (!response.ok) {
      const errorData = await response.json().catch(() => ({ error: 'Unknown error' }));
      throw {
        status: response.status,
        message: errorData.error || `HTTP ${response.status}`,
      };
    }

    const transcription = await response.json();
    
    return NextResponse.json(transcription);
  } catch (error: any) {
    console.error('Audio Transcription API error:', error);
    
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
    
    if (error?.status === 413) {
      return NextResponse.json(
        { error: 'File too large' },
        { status: 413 }
      );
    }
    
    if (error?.status === 415) {
      return NextResponse.json(
        { error: 'Unsupported media type' },
        { status: 415 }
      );
    }
    
    return NextResponse.json(
      { error: 'Failed to transcribe audio' },
      { status: 500 }
    );
  }
}

export async function GET(request: NextRequest) {
  try {
    // Validate session for getting transcription history
    const validation = await validateSession(request);
    if (!validation.isValid) {
      return createUnauthorizedResponse(validation.error);
    }

    const { searchParams } = new URL(request.url);
    const virtualKey = searchParams.get('virtual_key') || request.headers.get('x-virtual-key');
    
    if (!virtualKey) {
      return NextResponse.json(
        { error: 'Virtual key is required' },
        { status: 400 }
      );
    }

    // Make direct API call to get transcription history
    const coreApiUrl = process.env.NEXT_PUBLIC_CONDUIT_CORE_API_URL || 'http://localhost:5000';
    const response = await fetch(`${coreApiUrl}/v1/audio/transcriptions`, {
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

    const history = await response.json();
    
    return NextResponse.json(history);
  } catch (error: any) {
    console.error('Audio Transcription History API error:', error);
    return NextResponse.json(
      { error: 'Failed to fetch transcription history' },
      { status: 500 }
    );
  }
}