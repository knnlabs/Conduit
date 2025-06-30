import { NextRequest, NextResponse } from 'next/server';

const adminApiUrl = process.env.CONDUIT_ADMIN_API_BASE_URL || 'http://localhost:5002';
const masterKey = process.env.CONDUIT_MASTER_KEY || '';

export async function GET(request: NextRequest) {
  try {
    const searchParams = request.nextUrl.searchParams;
    const status = searchParams.get('status');
    const type = searchParams.get('type');
    const page = searchParams.get('page') || '1';
    const pageSize = searchParams.get('pageSize') || '20';

    // Build query parameters
    const queryParams = new URLSearchParams({
      page,
      pageSize,
      ...(status && { status }),
      ...(type && { type }),
    });

    const response = await fetch(
      `${adminApiUrl}/v1/security/ip-rules?${queryParams}`,
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
        { error: error || 'Failed to fetch IP rules' },
        { status: response.status }
      );
    }

    const data = await response.json();
    return NextResponse.json(data);
  } catch (error: any) {
    console.error('IP rules API error:', error);
    return NextResponse.json(
      { error: error.message || 'Internal server error' },
      { status: 500 }
    );
  }
}

export async function POST(request: NextRequest) {
  try {
    const body = await request.json();

    // Validate required fields
    if (!body.ipAddress || !body.action) {
      return NextResponse.json(
        { error: 'IP address and action are required' },
        { status: 400 }
      );
    }

    const response = await fetch(
      `${adminApiUrl}/v1/security/ip-rules`,
      {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${masterKey}`,
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(body),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      return NextResponse.json(
        { error: error || 'Failed to create IP rule' },
        { status: response.status }
      );
    }

    const data = await response.json();
    return NextResponse.json(data);
  } catch (error: any) {
    console.error('Create IP rule error:', error);
    return NextResponse.json(
      { error: error.message || 'Internal server error' },
      { status: 500 }
    );
  }
}