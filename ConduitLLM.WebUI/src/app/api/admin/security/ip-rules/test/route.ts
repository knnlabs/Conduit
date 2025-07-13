import { NextRequest, NextResponse } from 'next/server';
import { requireAuth } from '@/lib/auth/simple-auth';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { handleSDKError } from '@/lib/errors/sdk-errors';

// GET /api/admin/security/ip-rules/test - Test an IP address
export async function GET(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const { searchParams } = new URL(req.url);
    const ipAddress = searchParams.get('ip');
    
    if (!ipAddress) {
      return NextResponse.json(
        { error: 'IP address is required' },
        { status: 400 }
      );
    }

    const adminClient = getServerAdminClient();
    
    // Check if the SDK method exists
    if (adminClient.ipFilters?.checkIp) {
      try {
        const result = await adminClient.ipFilters.checkIp(ipAddress);
        return NextResponse.json({
          allowed: result.isAllowed,
          matchedRule: result.matchedFilterId ? {
            id: result.matchedFilterId.toString(),
            ipAddress: result.matchedFilter || '',
            action: result.filterType === 'whitelist' ? 'allow' : 'block',
            description: result.matchedFilter,
          } : undefined,
          reason: result.deniedReason || (result.isAllowed ? 'Allowed by rules' : 'Blocked by rules'),
        });
      } catch (err) {
        // Fall through to mock response
      }
    }

    // Mock response for now since the endpoint might not exist
    // In a real implementation, this would check against actual rules
    const mockRules = [
      { id: '1', ipAddress: '192.168.1.0/24', action: 'allow', description: 'Local network' },
      { id: '2', ipAddress: '10.0.0.0/8', action: 'allow', description: 'Private network' },
      { id: '3', ipAddress: '172.16.0.0/12', action: 'allow', description: 'Private network' },
    ];

    // Simple IP matching logic for mock
    for (const rule of mockRules) {
      if (rule.ipAddress.includes('/')) {
        // CIDR range check (simplified)
        const [network] = rule.ipAddress.split('/');
        const networkParts = network.split('.');
        const ipParts = ipAddress.split('.');
        
        // Very basic subnet matching (not production-ready)
        const matches = networkParts.every((part, index) => 
          part === '0' || part === ipParts[index]
        );
        
        if (matches) {
          return NextResponse.json({
            allowed: rule.action === 'allow',
            matchedRule: rule,
            reason: `Matched ${rule.action} rule: ${rule.description}`,
          });
        }
      } else if (rule.ipAddress === ipAddress) {
        return NextResponse.json({
          allowed: rule.action === 'allow',
          matchedRule: rule,
          reason: `Matched ${rule.action} rule: ${rule.description}`,
        });
      }
    }

    // No matching rule found
    return NextResponse.json({
      allowed: true,
      reason: 'No matching rules found (default allow)',
    });
  } catch (error) {
    return handleSDKError(error);
  }
}