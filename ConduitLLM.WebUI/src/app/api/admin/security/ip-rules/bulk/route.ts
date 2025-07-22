import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';

// POST /api/admin/security/ip-rules/bulk - Bulk operations
export async function POST(req: NextRequest) {
  try {
    const body = await req.json() as {
      operation: 'enable' | 'disable' | 'delete';
      ruleIds: string[];
    };
    
    const client = getServerAdminClient();
    
    // Convert string IDs to numbers
    const numericIds = body.ruleIds.map(id => id.toString());
    
    if (body.operation === 'delete') {
      const result = await client.ipFilters.bulkDelete(numericIds);
      return NextResponse.json(result);
    } else if (body.operation === 'enable' || body.operation === 'disable') {
      const updatedFilters = await client.ipFilters.bulkUpdate(body.operation, numericIds);
      
      // Transform the response to match UI expectations
      const transformedFilters = updatedFilters.map(filter => ({
        id: filter.id.toString(),
        ipAddress: filter.ipAddressOrCidr,
        action: filter.filterType === 'whitelist' ? 'allow' : 'block',
        description: filter.description,
        createdAt: filter.createdAt,
        isEnabled: filter.isEnabled,
        lastMatchedAt: filter.lastMatchedAt,
        matchCount: filter.matchCount ?? 0,
      }));
      
      return NextResponse.json(transformedFilters);
    } else {
      return NextResponse.json({ error: 'Invalid operation' }, { status: 400 });
    }
  } catch (error) {
    return handleSDKError(error);
  }
}