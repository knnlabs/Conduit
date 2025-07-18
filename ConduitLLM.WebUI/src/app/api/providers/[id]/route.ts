import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import type { ProviderCredentialDto } from '@knn_labs/conduit-admin-client';

// GET /api/providers/[id] - Get a single provider
export async function GET(
  req: NextRequest,
  { params }: { params: Promise<{ id: string }> }
) {

  try {
    const { id } = await params;
    const adminClient = getServerAdminClient();
    const provider: ProviderCredentialDto = await adminClient.providers.getById(parseInt(id, 10));
    return NextResponse.json(provider);
  } catch (error) {
    return handleSDKError(error);
  }
}

// PUT /api/providers/[id] - Update a provider
export async function PUT(
  req: NextRequest,
  { params }: { params: Promise<{ id: string }> }
) {

  try {
    const { id } = await params;
    const adminClient = getServerAdminClient();
    const body = await req.json() as Record<string, unknown>;
    
    // Get the current provider data to merge with updates
    const currentProvider = await adminClient.providers.getById(parseInt(id, 10));
    
    // Build update data ensuring type safety - SDK expects the generated type format
    const updateData = {
      id: parseInt(id, 10),
      apiBase: (body.apiBase as string | undefined) ?? currentProvider.apiBase,
      apiKey: (body.apiKey as string | undefined) ?? currentProvider.apiKey,
      isEnabled: (body.isEnabled as boolean | undefined) ?? currentProvider.isEnabled,
      organization: (body.organization as string | undefined) ?? currentProvider.organization
    };
    
    const provider = await adminClient.providers.update(parseInt(id, 10), updateData);
    return NextResponse.json(provider);
  } catch (error) {
    return handleSDKError(error);
  }
}

// DELETE /api/providers/[id] - Delete a provider
export async function DELETE(
  req: NextRequest,
  { params }: { params: Promise<{ id: string }> }
) {

  try {
    const { id } = await params;
    const adminClient = getServerAdminClient();
    await adminClient.providers.deleteById(parseInt(id, 10));
    return new NextResponse(null, { status: 204 });
  } catch (error) {
    return handleSDKError(error);
  }
}