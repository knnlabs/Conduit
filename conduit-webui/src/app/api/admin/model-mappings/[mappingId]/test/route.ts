import { NextRequest, NextResponse } from 'next/server';

const adminApiUrl = process.env.CONDUIT_ADMIN_API_BASE_URL || 'http://localhost:5002';
const masterKey = process.env.CONDUIT_MASTER_KEY || '';

export async function POST(
  request: NextRequest,
  { params }: { params: Promise<{ mappingId: string }> }
) {
  const { mappingId } = await params;

  if (!masterKey) {
    return NextResponse.json({ error: 'Server configuration error' }, { status: 500 });
  }

  try {
    const response = await fetch(`${adminApiUrl}/v1/model-mappings/${mappingId}/test`, {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${masterKey}`,
        'Content-Type': 'application/json',
      },
    });

    if (!response.ok) {
      const errorData = await response.text();
      return NextResponse.json(
        { error: errorData || 'Failed to test model' },
        { status: response.status }
      );
    }

    const data = await response.json();
    return NextResponse.json(data);
  } catch (error) {
    console.error('Model test error:', error);
    return NextResponse.json(
      { error: 'Failed to test model' },
      { status: 500 }
    );
  }
}