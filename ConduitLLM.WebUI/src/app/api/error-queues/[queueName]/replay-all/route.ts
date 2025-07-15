import { NextRequest, NextResponse } from 'next/server';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { handleSDKError } from '@/lib/errors/sdk-errors';

export async function POST(
  request: NextRequest,
  { params }: { params: Promise<{ queueName: string }> }
) {
  try {
    const { queueName } = await params;
    const adminClient = getServerAdminClient();
    
    // Get optional message IDs from request body
    let messageIds: string[] | undefined;
    try {
      const body = await request.json();
      messageIds = body.messageIds;
    } catch {
      // No body or invalid JSON, replay all messages
    }
    
    const result = await adminClient.errorQueues.replayAllMessages(queueName, messageIds);
    
    return NextResponse.json(result);
  } catch (error) {
    return handleSDKError(error);
  }
}