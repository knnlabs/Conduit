import { NextRequest, NextResponse } from 'next/server';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import type { QueueClearResponse } from '@knn_labs/conduit-admin-client';

export async function DELETE(
  request: NextRequest,
  { params }: { params: Promise<{ queueName: string }> }
) {
  try {
    const { queueName } = await params;
    const adminClient = getServerAdminClient();
    
    const result = await (adminClient.errorQueues.clearQueue as (queueName: string) => Promise<QueueClearResponse>)(queueName);
    
    return NextResponse.json(result);
  } catch (error) {
    return handleSDKError(error);
  }
}