import { NextRequest, NextResponse } from 'next/server';

const adminApiUrl = process.env.CONDUIT_ADMIN_API_BASE_URL || 'http://localhost:5002';
const masterKey = process.env.CONDUIT_MASTER_KEY || '';

export async function POST(request: NextRequest) {
  if (!masterKey) {
    return NextResponse.json({ error: 'Server configuration error' }, { status: 500 });
  }

  try {
    const response = await fetch(`${adminApiUrl}/v1/model-mappings/discover`, {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${masterKey}`,
        'Content-Type': 'application/json',
      },
    });

    if (!response.ok) {
      const errorData = await response.text();
      return NextResponse.json(
        { error: errorData || 'Failed to discover models' },
        { status: response.status }
      );
    }

    const data = await response.json();
    return NextResponse.json(data);
  } catch (error) {
    console.error('Model discovery error:', error);
    return NextResponse.json(
      { error: 'Failed to discover models' },
      { status: 500 }
    );
  }
}