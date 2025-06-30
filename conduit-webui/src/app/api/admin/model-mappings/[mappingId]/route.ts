import { NextRequest, NextResponse } from 'next/server';

const adminApiUrl = process.env.CONDUIT_ADMIN_API_BASE_URL || 'http://localhost:5002';
const masterKey = process.env.CONDUIT_MASTER_KEY || '';

export async function PUT(
  request: NextRequest,
  { params }: { params: Promise<{ mappingId: string }> }
) {
  const { mappingId } = await params;

  if (!masterKey) {
    return NextResponse.json({ error: 'Server configuration error' }, { status: 500 });
  }

  try {
    const body = await request.json();
    
    const response = await fetch(`${adminApiUrl}/v1/model-mappings/${mappingId}`, {
      method: 'PUT',
      headers: {
        'Authorization': `Bearer ${masterKey}`,
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(body),
    });

    if (!response.ok) {
      const errorData = await response.text();
      return NextResponse.json(
        { error: errorData || 'Failed to update model mapping' },
        { status: response.status }
      );
    }

    const data = await response.json();
    return NextResponse.json(data);
  } catch (error) {
    console.error('Update model mapping error:', error);
    return NextResponse.json(
      { error: 'Failed to update model mapping' },
      { status: 500 }
    );
  }
}

export async function DELETE(
  request: NextRequest,
  { params }: { params: Promise<{ mappingId: string }> }
) {
  const { mappingId } = await params;

  if (!masterKey) {
    return NextResponse.json({ error: 'Server configuration error' }, { status: 500 });
  }

  try {
    const response = await fetch(`${adminApiUrl}/v1/model-mappings/${mappingId}`, {
      method: 'DELETE',
      headers: {
        'Authorization': `Bearer ${masterKey}`,
        'Content-Type': 'application/json',
      },
    });

    if (!response.ok) {
      const errorData = await response.text();
      return NextResponse.json(
        { error: errorData || 'Failed to delete model mapping' },
        { status: response.status }
      );
    }

    return NextResponse.json({ message: 'Model mapping deleted successfully' });
  } catch (error) {
    console.error('Delete model mapping error:', error);
    return NextResponse.json(
      { error: 'Failed to delete model mapping' },
      { status: 500 }
    );
  }
}