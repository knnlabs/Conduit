import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import type { ProviderCredentialDto, UpdateProviderCredentialDto } from '@knn_labs/conduit-admin-client';

// GET /api/providers/[id] - Get a single provider
export async function GET(
  req: NextRequest,
  { params }: { params: Promise<{ id: string }> }
): Promise<NextResponse<ProviderCredentialDto | { error: string }>> {

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
): Promise<NextResponse<ProviderCredentialDto | { error: string }>> {

  try {
    const { id } = await params;
    const adminClient = getServerAdminClient();
    const body: UpdateProviderCredentialDto = await req.json() as UpdateProviderCredentialDto;
    const provider: ProviderCredentialDto = await adminClient.providers.update(parseInt(id, 10), body);
    return NextResponse.json(provider);
  } catch (error) {
    return handleSDKError(error);
  }
}

// DELETE /api/providers/[id] - Delete a provider
export async function DELETE(
  req: NextRequest,
  { params }: { params: Promise<{ id: string }> }
): Promise<NextResponse<null | { error: string }>> {

  try {
    const { id } = await params;
    const adminClient = getServerAdminClient();
    await adminClient.providers.deleteById(parseInt(id, 10));
    return new NextResponse(null, { status: 204 });
  } catch (error) {
    return handleSDKError(error);
  }
}