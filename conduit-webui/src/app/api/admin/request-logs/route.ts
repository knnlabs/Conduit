import { NextRequest, NextResponse } from 'next/server';

const adminApiUrl = process.env.CONDUIT_ADMIN_API_BASE_URL || 'http://localhost:5002';
const masterKey = process.env.CONDUIT_MASTER_KEY || '';

export async function GET(request: NextRequest) {
  try {
    const searchParams = request.nextUrl.searchParams;
    const startDate = searchParams.get('startDate');
    const endDate = searchParams.get('endDate');
    const status = searchParams.get('status');
    const method = searchParams.get('method');
    const endpoint = searchParams.get('endpoint');
    const virtualKeyId = searchParams.get('virtualKeyId');
    const page = searchParams.get('page') || '1';
    const pageSize = searchParams.get('pageSize') || '50';

    // Build query parameters
    const queryParams = new URLSearchParams({
      page,
      pageSize,
      ...(startDate && { startDate }),
      ...(endDate && { endDate }),
      ...(status && { status }),
      ...(method && { method }),
      ...(endpoint && { endpoint }),
      ...(virtualKeyId && { virtualKeyId }),
    });

    const response = await fetch(
      `${adminApiUrl}/v1/request-logs?${queryParams}`,
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
        { error: error || 'Failed to fetch request logs' },
        { status: response.status }
      );
    }

    const data = await response.json();
    return NextResponse.json(data);
  } catch (error: any) {
    console.error('Request logs API error:', error);
    return NextResponse.json(
      { error: error.message || 'Internal server error' },
      { status: 500 }
    );
  }
}

// Export endpoint for request logs
export async function POST(request: NextRequest) {
  try {
    const { format, filters } = await request.json();

    if (!format || !['csv', 'json', 'excel'].includes(format)) {
      return NextResponse.json(
        { error: 'Invalid export format. Supported formats: csv, json, excel' },
        { status: 400 }
      );
    }

    const queryParams = new URLSearchParams({
      format,
      ...(filters?.startDate && { startDate: filters.startDate }),
      ...(filters?.endDate && { endDate: filters.endDate }),
      ...(filters?.status && { status: filters.status }),
      ...(filters?.method && { method: filters.method }),
      ...(filters?.virtualKeyId && { virtualKeyId: filters.virtualKeyId }),
    });

    const response = await fetch(
      `${adminApiUrl}/v1/request-logs/export?${queryParams}`,
      {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${masterKey}`,
          'Content-Type': 'application/json',
        },
      }
    );

    if (!response.ok) {
      const error = await response.text();
      return NextResponse.json(
        { error: error || 'Failed to export request logs' },
        { status: response.status }
      );
    }

    // Get the file data
    const blob = await response.blob();
    const headers = new Headers();
    
    // Set appropriate content type based on format
    const contentTypes = {
      csv: 'text/csv',
      json: 'application/json',
      excel: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
    };
    
    headers.set('Content-Type', contentTypes[format as keyof typeof contentTypes]);
    headers.set('Content-Disposition', `attachment; filename="request-logs-${new Date().toISOString()}.${format}"`);

    return new NextResponse(blob, { headers });
  } catch (error: any) {
    console.error('Export request logs error:', error);
    return NextResponse.json(
      { error: error.message || 'Internal server error' },
      { status: 500 }
    );
  }
}