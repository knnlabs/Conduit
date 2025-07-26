import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';

// POST /api/admin/security/ip-rules/test - Test an IP address
export async function POST(req: NextRequest) {
  try {
    const body = await req.json() as {
      ipAddress: string;
    };
    
    if (!body.ipAddress) {
      return NextResponse.json({ error: 'IP address is required' }, { status: 400 });
    }
    
    const client = getServerAdminClient();
    const result = await client.ipFilters.checkIp(body.ipAddress) as {
      isAllowed: boolean;
      deniedReason?: string;
      matchedFilter?: unknown;
      filterType?: 'whitelist' | 'blacklist';
      isDefaultAction?: boolean;
    };
    
    // Transform the result to match UI expectations
    const transformedResult = {
      isAllowed: result.isAllowed,
      reason: result.deniedReason ?? (result.isAllowed ? 'IP is allowed' : 'IP is blocked'),
      matchedRule: result.matchedFilter,
      ruleType: (() => {
        if (!result.filterType) return undefined;
        return result.filterType === 'whitelist' ? 'allow' : 'block';
      })(),
      isDefault: result.isDefaultAction ?? false,
    };
    
    return NextResponse.json(transformedResult);
  } catch (error) {
    return handleSDKError(error);
  }
}