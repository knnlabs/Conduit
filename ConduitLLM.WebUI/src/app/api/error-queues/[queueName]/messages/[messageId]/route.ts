import { NextRequest, NextResponse } from 'next/server';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { requireAuth } from '@/lib/auth/simple-auth';

export async function GET(
  request: NextRequest,
  { params }: { params: Promise<{ queueName: string; messageId: string }> }
) {
  const auth = requireAuth(request);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const adminClient = getServerAdminClient();
    const { queueName: rawQueueName, messageId: rawMessageId } = await params;
    const queueName = decodeURIComponent(rawQueueName);
    const messageId = decodeURIComponent(rawMessageId);
    
    const result = await adminClient.errorQueues.getErrorMessage(queueName, messageId);
    
    return NextResponse.json(result);
  } catch (error: any) {
    console.error('Error fetching error message:', error);
    return NextResponse.json(
      { error: error.message || 'Failed to fetch error message' },
      { status: error.statusCode || 500 }
    );
  }
}

export async function DELETE(
  request: NextRequest,
  { params }: { params: Promise<{ queueName: string; messageId: string }> }
) {
  const auth = requireAuth(request);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    // TODO: Implement delete message endpoint in Admin API
    // For now, return a mock response
    return NextResponse.json({ success: true });
  } catch (error: any) {
    console.error('Error deleting error message:', error);
    return NextResponse.json(
      { error: error.message || 'Failed to delete error message' },
      { status: error.statusCode || 500 }
    );
  }
}