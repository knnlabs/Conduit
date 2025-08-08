import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import type { ModelProviderMappingDto } from '@knn_labs/conduit-admin-client';

// GET /api/model-mappings/[id] - Get a single model mapping
export async function GET(
  req: NextRequest,
  { params }: { params: Promise<{ id: string }> }
) {

  try {
    const { id } = await params;
    const adminClient = getServerAdminClient();
    const mapping = await adminClient.modelMappings.getById(parseInt(id, 10));
    return NextResponse.json(mapping);
  } catch (error) {
    return handleSDKError(error);
  }
}

// PUT /api/model-mappings/[id] - Update a model mapping
export async function PUT(
  req: NextRequest,
  { params }: { params: Promise<{ id: string }> }
) {

  try {
    const { id } = await params;
    const adminClient = getServerAdminClient();
    const body = await req.json() as unknown as Partial<ModelProviderMappingDto>;
    
    // First get the existing mapping to preserve required fields
    const existingMapping = await adminClient.modelMappings.getById(parseInt(id, 10));
    
    // Simple merge strategy - existing values + incoming changes
    const transformedBody: ModelProviderMappingDto = {
      ...existingMapping, // Start with all existing values
      ...body,            // Overlay the changes
      id: parseInt(id, 10) // Ensure ID matches route
    };
    
    // Update via Admin SDK (now fixed)
    await adminClient.modelMappings.update(parseInt(id, 10), transformedBody);
    const updatedMapping = await adminClient.modelMappings.getById(parseInt(id, 10));
    
    return NextResponse.json(updatedMapping);
  } catch (error: unknown) {
    
    return handleSDKError(error);
  }
}

// DELETE /api/model-mappings/[id] - Delete a model mapping
export async function DELETE(
  req: NextRequest,
  { params }: { params: Promise<{ id: string }> }
) {

  try {
    const { id } = await params;
    const adminClient = getServerAdminClient();
    await adminClient.modelMappings.deleteById(parseInt(id, 10));
    return new NextResponse(null, { status: 204 });
  } catch (error) {
    return handleSDKError(error);
  }
}