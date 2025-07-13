import { NextRequest, NextResponse } from 'next/server';
import { requireAuth } from '@/lib/auth/simple-auth';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { handleSDKError } from '@/lib/errors/sdk-errors';

// POST /api/admin/security/ip-rules/bulk - Bulk operations
export async function POST(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const body = await req.json();
    const { operation, ruleIds } = body;
    const adminClient = getServerAdminClient();
    
    if (!adminClient.ipFilters) {
      return NextResponse.json({ error: 'IP filtering not available' }, { status: 501 });
    }

    switch (operation) {
      case 'enable':
      case 'disable': {
        const updatedRules = await adminClient.ipFilters.bulkUpdate(operation, ruleIds);
        // Transform SDK response to match frontend expectations
        const transformedRules = updatedRules.map(filter => ({
          id: filter.id.toString(),
          ipAddress: filter.ipAddressOrCidr,
          action: filter.filterType === 'whitelist' ? 'allow' : 'block',
          description: filter.description || '',
          createdAt: filter.createdAt,
          isEnabled: filter.isEnabled,
          lastMatchedAt: filter.lastMatchedAt,
          matchCount: filter.matchCount,
        }));
        return NextResponse.json(transformedRules);
      }
      
      case 'delete': {
        const result = await adminClient.ipFilters.bulkDelete(ruleIds);
        return NextResponse.json(result);
      }
      
      default:
        return NextResponse.json(
          { error: `Invalid operation: ${operation}` },
          { status: 400 }
        );
    }
  } catch (error) {
    return handleSDKError(error);
  }
}