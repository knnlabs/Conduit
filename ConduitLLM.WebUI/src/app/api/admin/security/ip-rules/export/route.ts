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
    // Export endpoint no longer exists in the API
    // Fetch all filters and format them manually
    const filters = await client.ipFilters.list();
    
    let content: string;
    let contentType: string;
    
    if (format === 'csv') {
      // Create CSV content
      const headers = ['ID', 'Name', 'IP/CIDR', 'Type', 'Enabled', 'Description', 'Created', 'Match Count'];
      const rows = filters.map(f => [
        f.id,
        f.name,
        f.ipAddressOrCidr,
        f.filterType,
        f.isEnabled,
        f.description ?? '',
        f.createdAt,
        f.matchCount ?? 0
      ]);
      
      content = [headers, ...rows].map(row => row.map(cell => `"${String(cell).replace(/"/g, '""')}"`).join(',')).join('\n');
      contentType = 'text/csv';
    } else {
      // Create JSON content
      content = JSON.stringify(filters, null, 2);
      contentType = 'application/json';
    }
    
    const blob = new Blob([content], { type: contentType });
    
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