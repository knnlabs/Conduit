import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import type { AdjustBalanceDto } from '@knn_labs/conduit-admin-client';

type Params = Promise<{ id: string }>;

// POST /api/virtualkeys/groups/:id/credits - Add credits to group
export async function POST(req: NextRequest, { params }: { params: Params }) {
  try {
    const { id } = await params;
    const groupId = parseInt(id, 10);
    
    if (isNaN(groupId)) {
      return NextResponse.json(
        { error: 'Invalid group ID' },
        { status: 400 }
      );
    }
    
    const body = await req.json() as AdjustBalanceDto;
    
    // Validate amount
    if (typeof body.amount !== 'number' || body.amount <= 0) {
      return NextResponse.json(
        { error: 'Amount must be a positive number' },
        { status: 400 }
      );
    }
    
    const adminClient = getServerAdminClient();
    const updatedGroup = await adminClient.virtualKeyGroups.adjustBalance(groupId, body);
    
    return NextResponse.json(updatedGroup);
  } catch (error) {
    console.error('[VirtualKeyGroups] Error adding credits:', error);
    return handleSDKError(error);
  }
}