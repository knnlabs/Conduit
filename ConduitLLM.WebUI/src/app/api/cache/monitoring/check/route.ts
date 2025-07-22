import { NextResponse } from 'next/server';

export async function POST() {
  try {

    const apiKey = process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY;

    if (!apiKey) {
      return NextResponse.json(
        { error: 'Backend authentication key not configured' },
        { status: 500 }
      );
    }

    const adminApiUrl = process.env.CONDUIT_ADMIN_API_BASE_URL ?? 'http://localhost:5002';
    const response = await fetch(`${adminApiUrl}/api/cache/monitoring/check`, {
      method: 'POST',
      headers: new Headers([
        ['Authorization', `Bearer ${apiKey}`],
        ['X-Master-Key', apiKey],
      ]),
    });

    if (!response.ok) {
      const error = await response.text();
      console.error('Failed to force cache check:', error);
      return NextResponse.json(
        { error: 'Failed to perform check' },
        { status: response.status }
      );
    }

    const data: unknown = await response.json();
    return NextResponse.json(data);
  } catch (error) {
    console.error('Error in cache check route:', error);
    return NextResponse.json(
      { error: 'Internal server error' },
      { status: 500 }
    );
  }
}