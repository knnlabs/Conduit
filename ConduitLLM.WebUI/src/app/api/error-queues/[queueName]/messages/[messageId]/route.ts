import { NextRequest, NextResponse } from 'next/server';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { handleSDKError } from '@/lib/errors/sdk-errors';

export async function GET(
  request: NextRequest,
  { params }: { params: Promise<{ queueName: string; messageId: string }> }
) {
  try {
    const { queueName, messageId } = await params;
    
    const adminClient = getServerAdminClient();
    const response = await adminClient.errorQueues.getErrorMessage(queueName, messageId);
    
    return NextResponse.json(response);
  } catch (error) {
    return handleSDKError(error);
  }
}

export async function DELETE(
  request: NextRequest,
  { params }: { params: Promise<{ queueName: string; messageId: string }> }
) {
  try {
    const { queueName, messageId } = await params;
    const adminClient = getServerAdminClient();
    
    const result = await adminClient.errorQueues.deleteMessage(queueName, messageId);
    
    return NextResponse.json(result);
  } catch (error) {
    return handleSDKError(error);
  }
}