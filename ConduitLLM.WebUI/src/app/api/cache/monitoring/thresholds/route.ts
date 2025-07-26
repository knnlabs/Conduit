import { NextRequest, NextResponse } from 'next/server';

export async function GET() {
  try {
    const apiKey = process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY;

    if (!apiKey) {
      return NextResponse.json(
        { error: 'Backend authentication key not configured' },
        { status: 500 }
      );
    }

    const adminApiUrl = process.env.CONDUIT_ADMIN_API_BASE_URL ?? 'http://localhost:5002';
    const response = await fetch(`${adminApiUrl}/api/cache/monitoring/thresholds`, {
      headers: new Headers([
        ['Authorization', `Bearer ${apiKey}`],
        ['X-Master-Key', apiKey],
      ]),
    });

    if (!response.ok) {
      const error = await response.text();
      console.error('Failed to fetch cache thresholds:', error);
      return NextResponse.json(
        { error: 'Failed to fetch thresholds' },
        { status: response.status }
      );
    }

    const data: unknown = await response.json();
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
    const apiKey = process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY;

    if (!apiKey) {
      return NextResponse.json(
        { error: 'Backend authentication key not configured' },
        { status: 500 }
      );
    }

    const body: unknown = await request.json();
    const adminApiUrl = process.env.CONDUIT_ADMIN_API_BASE_URL ?? 'http://localhost:5002';
    const response = await fetch(`${adminApiUrl}/api/cache/monitoring/thresholds`, {
      method: 'PUT',
      headers: new Headers([
        ['Authorization', `Bearer ${apiKey}`],
        ['X-Master-Key', apiKey],
        ['Content-Type', 'application/json'],
      ]),
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

    const data: unknown = await response.json();
    return NextResponse.json(data);
  } catch (error) {
    console.error('Error in cache thresholds update route:', error);
    return NextResponse.json(
      { error: 'Internal server error' },
      { status: 500 }
    );
  }
}