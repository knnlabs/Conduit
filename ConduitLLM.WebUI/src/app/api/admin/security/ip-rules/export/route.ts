import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';

// GET /api/admin/security/ip-rules/export - Export IP rules
export async function GET(req: NextRequest) {
  try {
    const searchParams = req.nextUrl.searchParams;
    const format = searchParams.get('format') as 'json' | 'csv' || 'json';
    
    if (format !== 'json' && format !== 'csv') {
      return NextResponse.json({ error: 'Invalid format. Must be json or csv' }, { status: 400 });
    }
    
    const client = getServerAdminClient();
    const blob = await client.ipFilters.export(format);
    
    // Convert blob to buffer for NextResponse
    const arrayBuffer = await blob.arrayBuffer();
    const buffer = Buffer.from(arrayBuffer);
    
    const headers = {
      'Content-Type': format === 'csv' ? 'text/csv' : 'application/json',
      'Content-Disposition': `attachment; filename="ip-rules.${format}"`,
    };
    
    return new NextResponse(buffer, { headers });
  } catch (error) {
    return handleSDKError(error);
  }
}