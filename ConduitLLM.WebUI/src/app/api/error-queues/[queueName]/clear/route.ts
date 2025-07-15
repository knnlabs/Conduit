import { NextRequest, NextResponse } from 'next/server';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { handleSDKError } from '@/lib/errors/sdk-errors';

export async function DELETE(
  request: NextRequest,
  { params }: { params: Promise<{ queueName: string }> }
) {
  try {
    const { queueName } = await params;
    const adminClient = getServerAdminClient();
    
    const result = await adminClient.errorQueues.clearQueue(queueName);
    
    return NextResponse.json(result);
  } catch (error) {
    return handleSDKError(error);
  }
}