import { NextResponse } from 'next/server';

const adminApiUrl = process.env.CONDUIT_ADMIN_API_BASE_URL || 'http://localhost:5002';
const masterKey = process.env.CONDUIT_MASTER_KEY || '';

export async function GET(request: NextRequest) {
  try {
    const searchParams = request.nextUrl.searchParams;
    const status = searchParams.get('status');
    const severity = searchParams.get('severity');
    const page = searchParams.get('page') || '1';
    const pageSize = searchParams.get('pageSize') || '20';

    // Build query parameters
    const queryParams = new URLSearchParams({
      page,
      pageSize,
      ...(status && { status }),
      ...(severity && { severity }),
    });

    const response = await fetch(
      `${adminApiUrl}/v1/security/threats?${queryParams}`,
      {
        method: 'GET',
        headers: {
          'Authorization': `Bearer ${masterKey}`,
          'Content-Type': 'application/json',
        },
      }
    );

    if (!response.ok) {
      // If the endpoint doesn't exist yet, return empty data structure
      if (response.status === 404) {
        return NextResponse.json({
          items: [],
          totalCount: 0,
          pageNumber: parseInt(page),
          pageSize: parseInt(pageSize),
          totalPages: 0,
        });
      }
      
      const error = await response.text();
      return NextResponse.json(
        { error: error || 'Failed to fetch threat detections' },
        { status: response.status }
      );
    }

    const data = await response.json();
    return NextResponse.json(data);
  } catch (error: unknown) {
    console.error('Threat detections API error:', error);
    return NextResponse.json(
      { error: (error as { message?: string })?.message || 'Internal server error' },
      { status: 500 }
    );
  }
}

export async function PUT(request: NextRequest) {
  try {
    const { threatId, action } = await request.json();

    if (!threatId || !action) {
      return NextResponse.json(
        { error: 'Threat ID and action are required' },
        { status: 400 }
      );
    }

    const response = await fetch(
      `${adminApiUrl}/v1/security/threats/${threatId}/action`,
      {
        method: 'PUT',
        headers: {
          'Authorization': `Bearer ${masterKey}`,
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ action }),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      return NextResponse.json(
        { error: error || 'Failed to update threat status' },
        { status: response.status }
      );
    }

    const data = await response.json();
    return NextResponse.json(data);
  } catch (error: unknown) {
    console.error('Update threat status error:', error);
    return NextResponse.json(
      { error: (error as { message?: string })?.message || 'Internal server error' },
      { status: 500 }
    );
  }
}