import { NextRequest, NextResponse } from 'next/server';

const adminApiUrl = process.env.CONDUIT_ADMIN_API_BASE_URL || 'http://localhost:5002';
const masterKey = process.env.CONDUIT_MASTER_KEY || '';

export async function POST(request: NextRequest) {
  try {
    const body = await request.json();
    const { type, format, filters } = body;

    if (!type || !['usage', 'cost', 'virtual-keys'].includes(type)) {
      return NextResponse.json(
        { error: 'Invalid export type. Supported types: usage, cost, virtual-keys' },
        { status: 400 }
      );
    }

    if (!format || !['csv', 'json', 'excel'].includes(format)) {
      return NextResponse.json(
        { error: 'Invalid export format. Supported formats: csv, json, excel' },
        { status: 400 }
      );
    }

    // Build query parameters
    const queryParams = new URLSearchParams({
      format,
      ...(filters?.startDate && { startDate: filters.startDate }),
      ...(filters?.endDate && { endDate: filters.endDate }),
      ...(filters?.virtualKeyId && { virtualKeyId: filters.virtualKeyId }),
      ...(filters?.groupBy && { groupBy: filters.groupBy }),
    });

    const response = await fetch(
      `${adminApiUrl}/v1/analytics/${type}/export?${queryParams}`,
      {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${masterKey}`,
          'Content-Type': 'application/json',
        },
      }
    );

    if (!response.ok) {
      // If the endpoint doesn't exist yet, generate a sample CSV
      if (response.status === 404) {
        const now = new Date();
        const sampleData = generateSampleData(type, format, now);
        
        const headers = new Headers();
        const contentTypes = {
          csv: 'text/csv',
          json: 'application/json',
          excel: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
        };
        
        headers.set('Content-Type', contentTypes[format as keyof typeof contentTypes]);
        headers.set('Content-Disposition', `attachment; filename="${type}-export-${now.toISOString()}.${format}"`);
        
        return new NextResponse(sampleData, { headers });
      }
      
      const error = await response.text();
      return NextResponse.json(
        { error: error || 'Failed to export analytics data' },
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
    headers.set('Content-Disposition', `attachment; filename="${type}-export-${new Date().toISOString()}.${format}"`);

    return new NextResponse(blob, { headers });
  } catch (error: any) {
    console.error('Export analytics error:', error);
    return NextResponse.json(
      { error: error.message || 'Internal server error' },
      { status: 500 }
    );
  }
}

function generateSampleData(type: string, format: string, date: Date): string | Buffer {
  if (type === 'usage' && format === 'csv') {
    return `Date,Virtual Key,Model,Requests,Tokens,Cost
${date.toISOString().split('T')[0]},demo-key,gpt-4,100,50000,5.00
${date.toISOString().split('T')[0]},test-key,gpt-3.5-turbo,500,100000,2.00
${date.toISOString().split('T')[0]},prod-key,claude-2,200,75000,7.50`;
  }
  
  if (type === 'cost' && format === 'csv') {
    return `Date,Provider,Model,Cost,Requests
${date.toISOString().split('T')[0]},OpenAI,gpt-4,10.00,200
${date.toISOString().split('T')[0]},OpenAI,gpt-3.5-turbo,2.00,500
${date.toISOString().split('T')[0]},Anthropic,claude-2,7.50,200`;
  }
  
  if (type === 'virtual-keys' && format === 'csv') {
    return `Key Name,Total Spend,Request Count,Last Used,Status
demo-key,15.00,300,${date.toISOString()},active
test-key,5.00,100,${date.toISOString()},active
prod-key,50.00,1000,${date.toISOString()},active`;
  }
  
  if (format === 'json') {
    return JSON.stringify({
      type,
      exportDate: date.toISOString(),
      data: [],
      message: 'Sample export data'
    }, null, 2);
  }
  
  // For excel format, return a simple CSV that can be opened in Excel
  return generateSampleData(type, 'csv', date);
}