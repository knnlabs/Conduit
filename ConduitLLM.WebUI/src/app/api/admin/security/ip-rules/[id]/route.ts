import { NextRequest, NextResponse } from 'next/server';
import { requireAuth } from '@/lib/auth/simple-auth';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { handleSDKError } from '@/lib/errors/sdk-errors';

// GET /api/admin/security/ip-rules/[id]
export async function GET(
  req: NextRequest,
  { params }: { params: Promise<{ id: string }> }
) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const { id } = await params;
    const adminClient = getServerAdminClient();
    
    if (!adminClient.ipFilters) {
      return NextResponse.json({ error: 'IP filtering not available' }, { status: 501 });
    }

    const filter = await adminClient.ipFilters.getById(parseInt(id));
    
    return NextResponse.json({
      id: filter.id.toString(),
      ipAddress: filter.ipAddressOrCidr,
      action: filter.filterType === 'whitelist' ? 'allow' : 'block',
      description: filter.description || '',
      createdAt: filter.createdAt,
      isEnabled: filter.isEnabled,
      lastMatchedAt: filter.lastMatchedAt,
      matchCount: filter.matchCount,
    });
  } catch (error) {
    return handleSDKError(error);
  }
}

// PATCH /api/admin/security/ip-rules/[id]
export async function PATCH(
  req: NextRequest,
  { params }: { params: Promise<{ id: string }> }
) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const { id } = await params;
    const body = await req.json();
    const adminClient = getServerAdminClient();
    
    if (!adminClient.ipFilters) {
      return NextResponse.json({ ...body, id });
    }

    await adminClient.ipFilters.update(parseInt(id), {
      id: parseInt(id),
      name: body.description,
      ipAddressOrCidr: body.ipAddress,
      filterType: body.action === 'allow' ? 'whitelist' : 'blacklist',
      description: body.description,
      isEnabled: body.isEnabled,
    });

    // Get the updated filter to return
    const updatedFilter = await adminClient.ipFilters.getById(parseInt(id));

    return NextResponse.json({
      id: updatedFilter.id.toString(),
      ipAddress: updatedFilter.ipAddressOrCidr,
      action: updatedFilter.filterType === 'whitelist' ? 'allow' : 'block',
      description: updatedFilter.description || '',
      createdAt: updatedFilter.createdAt,
      isEnabled: updatedFilter.isEnabled,
      lastMatchedAt: updatedFilter.lastMatchedAt,
      matchCount: updatedFilter.matchCount,
    });
  } catch (error) {
    return handleSDKError(error);
  }
}

// DELETE /api/admin/security/ip-rules/[id]
export async function DELETE(
  req: NextRequest,
  { params }: { params: Promise<{ id: string }> }
) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const { id } = await params;
    const adminClient = getServerAdminClient();
    
    if (!adminClient.ipFilters) {
      return new NextResponse(null, { status: 204 });
    }

    await adminClient.ipFilters.deleteById(parseInt(id));
    
    return new NextResponse(null, { status: 204 });
  } catch (error) {
    return handleSDKError(error);
  }
}