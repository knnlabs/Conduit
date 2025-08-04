import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';

type Params = Promise<{ id: string }>;

// GET /api/virtualkeys/groups/:id/keys - Get all keys in group
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
    const keys = await adminClient.virtualKeyGroups.getKeys(groupId);
    
    return NextResponse.json(keys);
  } catch (error) {
    console.warn('[VirtualKeyGroups GET keys] Error:', error);
    return handleSDKError(error);
  }
}