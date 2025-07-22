import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import type { IpFilterDto, UpdateIpFilterDto } from '@knn_labs/conduit-admin-client';

interface RouteParams {
  params: Promise<{
    id: string;
  }>;
}

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

// GET /api/admin/security/ip-rules/[id]
export async function GET(
  req: NextRequest,
  { params }: RouteParams
) {
  try {
    const { id: idStr } = await params;
    const client = getServerAdminClient();
    const id = parseInt(idStr, 10);
    
    if (isNaN(id)) {
      return NextResponse.json({ error: 'Invalid ID' }, { status: 400 });
    }
    
    const filter = await client.ipFilters.getById(id);
    const transformedFilter = transformIpFilter(filter);
    
    return NextResponse.json(transformedFilter);
  } catch (error) {
    return handleSDKError(error);
  }
}

// PATCH /api/admin/security/ip-rules/[id]
export async function PATCH(
  req: NextRequest,
  { params }: RouteParams
) {
  try {
    const body = await req.json() as {
      ipAddress?: string;
      action?: 'allow' | 'block';
      description?: string;
      isEnabled?: boolean;
    };
    
    const { id: idStr } = await params;
    const client = getServerAdminClient();
    const id = parseInt(idStr, 10);
    
    if (isNaN(id)) {
      return NextResponse.json({ error: 'Invalid ID' }, { status: 400 });
    }
    
    // Build update request
    const updateRequest: UpdateIpFilterDto = {
      id,
    };
    
    if (body.ipAddress !== undefined) {
      updateRequest.ipAddressOrCidr = body.ipAddress;
    }
    
    if (body.action !== undefined) {
      updateRequest.filterType = mapActionToFilterType(body.action);
    }
    
    if (body.description !== undefined) {
      updateRequest.description = body.description;
    }
    
    if (body.isEnabled !== undefined) {
      updateRequest.isEnabled = body.isEnabled;
    }
    
    await client.ipFilters.update(id, updateRequest);
    
    // Get the updated filter to return
    const updatedFilter = await client.ipFilters.getById(id);
    const transformedFilter = transformIpFilter(updatedFilter);
    
    return NextResponse.json(transformedFilter);
  } catch (error) {
    return handleSDKError(error);
  }
}

// DELETE /api/admin/security/ip-rules/[id]
export async function DELETE(
  req: NextRequest,
  { params }: RouteParams
) {
  try {
    const { id: idStr } = await params;
    const client = getServerAdminClient();
    const id = parseInt(idStr, 10);
    
    if (isNaN(id)) {
      return NextResponse.json({ error: 'Invalid ID' }, { status: 400 });
    }
    
    await client.ipFilters.deleteById(id);
    
    return NextResponse.json({ success: true });
  } catch (error) {
    return handleSDKError(error);
  }
}