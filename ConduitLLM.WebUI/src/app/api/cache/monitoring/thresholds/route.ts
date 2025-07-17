import { NextRequest, NextResponse } from 'next/server';
import { headers } from 'next/headers';

export async function GET() {
  try {
    const headersList = headers();
    const apiKey = process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY;

    if (!apiKey) {
      return NextResponse.json(
        { error: 'Backend authentication key not configured' },
        { status: 500 }
      );
    }

    const adminApiUrl = process.env.CONDUIT_ADMIN_API_URL || 'http://localhost:5001';
    const response = await fetch(`${adminApiUrl}/api/cache/monitoring/thresholds`, {
      headers: {
        'Authorization': `Bearer ${apiKey}`,
        'X-Master-Key': apiKey,
      },
    });

    if (!response.ok) {
      const error = await response.text();
      console.error('Failed to fetch cache thresholds:', error);
      return NextResponse.json(
        { error: 'Failed to fetch thresholds' },
        { status: response.status }
      );
    }

    const data = await response.json();
    return NextResponse.json(data);
  } catch (error) {
    console.error('Error in cache thresholds route:', error);
    return NextResponse.json(
      { error: 'Internal server error' },
      { status: 500 }
    );
  }
}

export async function PUT(request: NextRequest) {
  try {
    const headersList = headers();
    const apiKey = process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY;

    if (!apiKey) {
      return NextResponse.json(
        { error: 'Backend authentication key not configured' },
        { status: 500 }
      );
    }

    const body = await request.json();
    const adminApiUrl = process.env.CONDUIT_ADMIN_API_URL || 'http://localhost:5001';
    const response = await fetch(`${adminApiUrl}/api/cache/monitoring/thresholds`, {
      method: 'PUT',
      headers: {
        'Authorization': `Bearer ${apiKey}`,
        'X-Master-Key': apiKey,
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(body),
    });

    if (!response.ok) {
      const error = await response.text();
      console.error('Failed to update cache thresholds:', error);
      return NextResponse.json(
        { error: 'Failed to update thresholds' },
        { status: response.status }
      );
    }

    const data = await response.json();
    return NextResponse.json(data);
  } catch (error) {
    console.error('Error in cache thresholds update route:', error);
    return NextResponse.json(
      { error: 'Internal server error' },
      { status: 500 }
    );
  }
}