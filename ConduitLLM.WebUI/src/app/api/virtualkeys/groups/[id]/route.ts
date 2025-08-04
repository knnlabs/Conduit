import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';

type Params = Promise<{ id: string }>;

// GET /api/virtualkeys/groups/:id - Get specific group
export async function GET(req: NextRequest, { params }: { params: Params }) {
  try {
    const { id } = await params;
    const groupId = parseInt(id, 10);
    
    if (isNaN(groupId)) {
      return NextResponse.json(
        { error: 'Invalid group ID' },
        { status: 400 }
      );
    }
    
    const adminClient = getServerAdminClient();
    const group = await adminClient.virtualKeyGroups.get(groupId);
    
    return NextResponse.json(group);
  } catch (error) {
    console.warn('[VirtualKeyGroups GET by ID] Error:', error);
    return handleSDKError(error);
  }
}

// DELETE /api/virtualkeys/groups/:id - Delete group
export async function DELETE(req: NextRequest, { params }: { params: Params }) {
  try {
    const { id } = await params;
    const groupId = parseInt(id, 10);
    
    if (isNaN(groupId)) {
      return NextResponse.json(
        { error: 'Invalid group ID' },
        { status: 400 }
      );
    }
    
    const adminClient = getServerAdminClient();
    await adminClient.virtualKeyGroups.delete(groupId);
    
    return new NextResponse(null, { status: 204 });
  } catch (error) {
    console.error('[VirtualKeyGroups] Error deleting group:', error);
    return handleSDKError(error);
  }
}