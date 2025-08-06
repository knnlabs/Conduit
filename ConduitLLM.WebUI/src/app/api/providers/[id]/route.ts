import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';

// GET /api/providers/[id] - Get a single provider
export async function GET(
  req: NextRequest,
  { params }: { params: Promise<{ id: string }> }
) {

  try {
    const { id } = await params;
    const adminClient = getServerAdminClient();
    const provider = await adminClient.providers.getById(parseInt(id, 10));
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
    
    // Define a type that matches the actual API response
    type ApiProviderResponse = {
      id?: number;
      providerType?: number;
      providerName?: string;
      baseUrl?: string;
      isEnabled?: boolean;
      organization?: string;
    };
    
    const typedProvider = currentProvider as ApiProviderResponse;
    
    // Build update data ensuring type safety - SDK expects the generated type format
    const updateData: Record<string, unknown> = {
      id: parseInt(id, 10),
      // Handle different field names from frontend
      baseUrl: ((body.apiEndpoint as string | undefined) ?? (body.baseUrl as string | undefined) ?? typedProvider.baseUrl) as string,
      isEnabled: (body.isEnabled as boolean | undefined) ?? typedProvider.isEnabled,
      organization: ((body.organizationId as string | undefined) ?? (body.organization as string | undefined) ?? typedProvider.organization),
    };
    
    // Handle providerName if the backend supports it
    if (body.providerName !== undefined) {
      updateData.providerName = body.providerName as string;
    }
    
    // Note: API keys cannot be updated through the provider update endpoint
    // They must be managed through the separate provider keys endpoints
    
    await adminClient.providers.update(parseInt(id, 10), updateData as Parameters<typeof adminClient.providers.update>[1]);
    
    // Fetch the updated provider to return to the client
    const updatedProvider = await adminClient.providers.getById(parseInt(id, 10));
    return NextResponse.json(updatedProvider);
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