import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import type { IpFilterDto, CreateIpFilterDto } from '@knn_labs/conduit-admin-client';

// Helper function to map SDK filter types to UI action types
function mapFilterTypeToAction(filterType: 'whitelist' | 'blacklist'): 'allow' | 'block' {
  return filterType === 'whitelist' ? 'allow' : 'block';
}

// Helper function to map UI action types to SDK filter types
function mapActionToFilterType(action: 'allow' | 'block'): 'whitelist' | 'blacklist' {
  return action === 'allow' ? 'whitelist' : 'blacklist';
}

interface UIIpRule {
  id: string;
  ipAddress: string;
  action: 'allow' | 'block';
  description?: string;
  createdAt: string;
  isEnabled: boolean;
  lastMatchedAt?: string;
  matchCount: number;
}

// Helper function to transform SDK IP filter to UI format
function transformIpFilter(filter: IpFilterDto): UIIpRule {
  return {
    id: filter.id.toString(), // UI expects string IDs
    ipAddress: filter.ipAddressOrCidr,
    action: mapFilterTypeToAction(filter.filterType),
    description: filter.description,
    createdAt: filter.createdAt,
    isEnabled: filter.isEnabled,
    lastMatchedAt: filter.lastMatchedAt,
    matchCount: filter.matchCount ?? 0,
  };
}

// GET /api/admin/security/ip-rules - List all IP rules
export async function GET() {
  try {
    const client = getServerAdminClient();
    const response = await client.ipFilter.list() as { items: IpFilterDto[] };
    const filters = response.items;
    
    // Transform the filters to match the UI expectations
    const transformedFilters = filters.map(transformIpFilter);
    
    return NextResponse.json(transformedFilters);
  } catch (error) {
    return handleSDKError(error);
  }
}

// POST /api/admin/security/ip-rules - Create new IP rule
export async function POST(req: NextRequest) {
  try {
    const body = await req.json() as {
      ipAddress: string;
      action: 'allow' | 'block';
      description?: string;
    };
    
    const client = getServerAdminClient();
    
    // Create the IP filter using the SDK
    const createRequest: CreateIpFilterDto = {
      name: body.description ?? `${body.action} ${body.ipAddress}`,
      ipAddressOrCidr: body.ipAddress,
      filterType: mapActionToFilterType(body.action),
      isEnabled: true,
      description: body.description,
    };
    
    const createdFilter = await client.ipFilter.create(createRequest);
    
    // Transform the response to match UI expectations
    const transformedFilter = transformIpFilter(createdFilter);
    
    return NextResponse.json(transformedFilter);
  } catch (error) {
    return handleSDKError(error);
  }
}