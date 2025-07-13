import { NextRequest, NextResponse } from 'next/server';
import { requireAuth } from '@/lib/auth/simple-auth';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { handleSDKError } from '@/lib/errors/sdk-errors';

// GET /api/admin/security/ip-rules - List all IP rules
export async function GET(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const adminClient = getServerAdminClient();
    
    // Check if ipFilters service exists (graceful degradation)
    if (!adminClient.ipFilters) {
      // Return mock data if service not available
      return NextResponse.json([
        {
          id: '1',
          ipAddress: '192.168.1.1',
          action: 'allow',
          description: 'Development machine',
          createdAt: new Date().toISOString(),
        }
      ]);
    }

    const filters = await adminClient.ipFilters.list();
    
    // Transform SDK response to match frontend expectations
    const rules = filters.map(filter => ({
      id: filter.id.toString(),
      ipAddress: filter.ipAddressOrCidr,
      action: filter.filterType === 'whitelist' ? 'allow' : 'block',
      description: filter.description || '',
      createdAt: filter.createdAt,
      isEnabled: filter.isEnabled,
      lastMatchedAt: filter.lastMatchedAt,
      matchCount: filter.matchCount,
    }));

    return NextResponse.json(rules);
  } catch (error) {
    return handleSDKError(error);
  }
}

// POST /api/admin/security/ip-rules - Create new IP rule
export async function POST(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const body = await req.json();
    const adminClient = getServerAdminClient();
    
    if (!adminClient.ipFilters) {
      // Return mock response if service not available
      return NextResponse.json({
        id: Date.now().toString(),
        ...body,
        createdAt: new Date().toISOString(),
      });
    }

    const result = await adminClient.ipFilters.create({
      name: body.description || `IP Rule ${body.ipAddress}`,
      ipAddressOrCidr: body.ipAddress,
      filterType: body.action === 'allow' ? 'whitelist' : 'blacklist',
      isEnabled: true,
      description: body.description,
    });

    return NextResponse.json({
      id: result.id.toString(),
      ipAddress: result.ipAddressOrCidr,
      action: result.filterType === 'whitelist' ? 'allow' : 'block',
      description: result.description || '',
      createdAt: result.createdAt,
      isEnabled: result.isEnabled,
      lastMatchedAt: result.lastMatchedAt,
      matchCount: result.matchCount,
    });
  } catch (error) {
    return handleSDKError(error);
  }
}