import { NextRequest, NextResponse } from 'next/server';

const adminApiUrl = process.env.CONDUIT_ADMIN_API_BASE_URL || 'http://localhost:5002';
const masterKey = process.env.CONDUIT_MASTER_KEY || '';

export async function POST(request: NextRequest) {
  if (!masterKey) {
    return NextResponse.json({ error: 'Server configuration error' }, { status: 500 });
  }

  try {
    const body = await request.json();
    
    const response = await fetch(`${adminApiUrl}/v1/providers/test-connection`, {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${masterKey}`,
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(body),
    });

    if (!response.ok) {
      const errorData = await response.text();
      return NextResponse.json(
        { error: errorData || 'Failed to test connection' },
        { status: response.status }
      );
    }

    const data = await response.json();
    return NextResponse.json(data);
  } catch (error) {
    console.error('Test connection error:', error);
    return NextResponse.json(
      { error: 'Failed to test connection' },
      { status: 500 }
    );
  }
}