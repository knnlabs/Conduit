import { NextRequest, NextResponse } from 'next/server';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { handleSDKError } from '@/lib/errors/sdk-errors';

export async function POST(
  request: NextRequest,
  { params }: { params: Promise<{ queueName: string; messageId: string }> }
) {
  try {
    const { queueName, messageId } = await params;
    const adminClient = getServerAdminClient();
    
    const result = await adminClient.errorQueues.replayMessage(queueName, messageId);
    
    return NextResponse.json(result);
  } catch (error) {
    return handleSDKError(error);
  }
}