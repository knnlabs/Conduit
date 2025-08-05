import { NextResponse } from 'next/server';

export async function GET(
  request: Request,
  { params }: { params: { modelId: string } }
) {
  try {
    const apiUrl = process.env.CONDUIT_API_URL || 'http://localhost:5000';
    const apiKey = process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY;
    
    if (!apiKey) {
      console.error('CONDUIT_API_TO_API_BACKEND_AUTH_KEY not configured');
      return NextResponse.json({ error: 'Server configuration error' }, { status: 500 });
    }

    const response = await fetch(`${apiUrl}/v1/models/${params.modelId}/metadata`, {
      headers: {
        'Authorization': `Bearer ${apiKey}`,
        'Content-Type': 'application/json',
      },
    });

    if (!response.ok) {
      if (response.status === 404) {
        return NextResponse.json(null, { status: 200 });
      }
      throw new Error(`Failed to fetch model metadata: ${response.statusText}`);
    }

    const data = await response.json() as unknown;
    return NextResponse.json(data);
  } catch (error) {
    console.error('Error fetching model metadata:', error);
    return NextResponse.json(
      { error: 'Failed to fetch model metadata' },
      { status: 500 }
    );
  }
}