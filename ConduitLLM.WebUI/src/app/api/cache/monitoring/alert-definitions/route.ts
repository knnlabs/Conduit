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

    const adminApiUrl = process.env.CONDUIT_ADMIN_API_URL || 'http://localhost:5001';
    const response = await fetch(`${adminApiUrl}/api/cache/monitoring/alert-definitions`, {
      headers: {
        'Authorization': `Bearer ${apiKey}`,
        'X-Master-Key': apiKey,
      },
    });

    if (!response.ok) {
      const error = await response.text();
      console.error('Failed to fetch alert definitions:', error);
      return NextResponse.json(
        { error: 'Failed to fetch alert definitions' },
        { status: response.status }
      );
    }

    const data = await response.json();
    return NextResponse.json(data);
  } catch (error) {
    console.error('Error in alert definitions route:', error);
    return NextResponse.json(
      { error: 'Internal server error' },
      { status: 500 }
    );
  }
}