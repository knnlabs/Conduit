import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';

// GET /api/virtualkeys/[id] - Get a single virtual key
export async function GET(
  req: NextRequest,
  { params }: { params: Promise<{ id: string }> }
) {

  try {
    const { id } = await params;
    const adminClient = getServerAdminClient();
    const virtualKey = await adminClient.virtualKeys.get(id);
    return NextResponse.json(virtualKey);
  } catch (error) {
    return handleSDKError(error);
  }
}

// PUT /api/virtualkeys/[id] - Update a virtual key
export async function PUT(
  req: NextRequest,
  { params }: { params: Promise<{ id: string }> }
) {

  try {
    const { id } = await params;
    const body = await req.json();
    console.log('[VirtualKeys] Updating virtual key:', { id, body });
    
    const adminClient = getServerAdminClient();
    const virtualKey = await adminClient.virtualKeys.update(id, body);
    
    console.log('[VirtualKeys] Virtual key updated successfully:', virtualKey);
    
    // TODO: Fix SDK - The virtualKeys.update() method should return the updated VirtualKeyDto
    // Currently it returns undefined, which forces us to make an additional GET request
    // This is inconsistent with typical REST API patterns where PUT/PATCH returns the updated resource
    // The Admin API might be returning the updated key, but the SDK isn't properly handling the response
    if (!virtualKey) {
      console.log('[VirtualKeys] Update returned undefined, fetching updated key');
      const updatedKey = await adminClient.virtualKeys.get(id);
      return NextResponse.json(updatedKey);
    }
    
    // Ensure we only return serializable data
    // The SDK might return an object with methods or other non-serializable properties
    const serializedKey = JSON.parse(JSON.stringify(virtualKey));
    return NextResponse.json(serializedKey);
  } catch (error) {
    console.error('[VirtualKeys] Error updating virtual key:', error);
    return handleSDKError(error);
  }
}

// DELETE /api/virtualkeys/[id] - Delete a virtual key
export async function DELETE(
  req: NextRequest,
  { params }: { params: Promise<{ id: string }> }
) {

  try {
    const { id } = await params;
    const adminClient = getServerAdminClient();
    await adminClient.virtualKeys.delete(id);
    return new NextResponse(null, { status: 204 });
  } catch (error) {
    return handleSDKError(error);
  }
}