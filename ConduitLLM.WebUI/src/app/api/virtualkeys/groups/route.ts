import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import type { 
  CreateVirtualKeyGroupRequestDto 
} from '@knn_labs/conduit-admin-client';

// GET /api/virtualkeys/groups - List all groups
export async function GET() {
  try {
    const adminClient = getServerAdminClient();
    const groups = await adminClient.virtualKeyGroups.list();
    
    return NextResponse.json(groups);
  } catch (error) {
    console.warn('[VirtualKeyGroups GET] Error:', error);
    return handleSDKError(error);
  }
}

// POST /api/virtualkeys/groups - Create a new group
export async function POST(req: NextRequest) {
  try {
    const body = await req.json() as CreateVirtualKeyGroupRequestDto;
    
    // Validate required fields
    if (!body.groupName || typeof body.groupName !== 'string' || body.groupName.trim().length === 0) {
      return NextResponse.json(
        { error: 'Group name is required and must be a non-empty string' },
        { status: 400 }
      );
    }
    
    const adminClient = getServerAdminClient();
    const group = await adminClient.virtualKeyGroups.create(body);
    
    return NextResponse.json(group, { status: 201 });
  } catch (error) {
    console.error('[VirtualKeyGroups] Error creating group:', error);
    return handleSDKError(error);
  }
}