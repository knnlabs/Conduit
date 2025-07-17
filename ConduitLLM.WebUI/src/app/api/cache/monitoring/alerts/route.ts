import { NextRequest, NextResponse } from 'next/server';
import { headers } from 'next/headers';

export async function GET(request: NextRequest) {
  try {
    const headersList = headers();
    const apiKey = process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY;

    if (!apiKey) {
      return NextResponse.json(
        { error: 'Backend authentication key not configured' },
        { status: 500 }
      );
    }

    const searchParams = request.nextUrl.searchParams;
    const count = searchParams.get('count') ?? '10';

    const adminApiUrl = process.env.CONDUIT_ADMIN_API_URL ?? 'http://localhost:5001';
    const response = await fetch(`${adminApiUrl}/api/cache/monitoring/alerts?count=${count}`, {
      headers: {
        'Authorization': `Bearer ${apiKey}`,
        'X-Master-Key': apiKey,
      },
    });

    if (!response.ok) {
      const error = await response.text();
      console.error('Failed to fetch cache alerts:', error);
      return NextResponse.json(
        { error: 'Failed to fetch alerts' },
        { status: response.status }
      );
    }

    const data = await response.json();
    return NextResponse.json(data);
  } catch (error) {
    console.error('Error in cache alerts route:', error);
    return NextResponse.json(
      { error: 'Internal server error' },
      { status: 500 }
    );
  }
}

export async function DELETE() {
  try {
    headers(); // Keep the headers() call for side effects if needed
    const apiKey = process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY;

    if (!apiKey) {
      return NextResponse.json(
        { error: 'Backend authentication key not configured' },
        { status: 500 }
      );
    }

    const adminApiUrl = process.env.CONDUIT_ADMIN_API_URL ?? 'http://localhost:5001';
    const response = await fetch(`${adminApiUrl}/api/cache/monitoring/alerts`, {
      method: 'DELETE',
      headers: {
        'Authorization': `Bearer ${apiKey}`,
        'X-Master-Key': apiKey,
      },
    });

    if (!response.ok) {
      const error = await response.text();
      console.error('Failed to clear cache alerts:', error);
      return NextResponse.json(
        { error: 'Failed to clear alerts' },
        { status: response.status }
      );
    }

    const data = await response.json();
    return NextResponse.json(data);
  } catch (error) {
    console.error('Error in cache alerts clear route:', error);
    return NextResponse.json(
      { error: 'Internal server error' },
      { status: 500 }
    );
  }
}