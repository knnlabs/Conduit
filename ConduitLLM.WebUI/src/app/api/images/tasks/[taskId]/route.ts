import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { requireAuth } from '@/lib/auth/simple-auth';
import { getServerCoreClient } from '@/lib/server/coreClient';

// GET /api/images/tasks/[taskId] - Get image generation task status
export async function GET(
  request: NextRequest,
  { params }: { params: Promise<{ taskId: string }> }
) {
  const auth = requireAuth(request);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const { taskId } = await params;
    const coreClient = await getServerCoreClient();
    
    // Get task status from Core SDK
    const status = await coreClient.images.getTaskStatus(taskId);
    
    return NextResponse.json(status);
  } catch (error) {
    return handleSDKError(error);
  }
}

// DELETE /api/images/tasks/[taskId] - Cancel image generation task
export async function DELETE(
  request: NextRequest,
  { params }: { params: Promise<{ taskId: string }> }
) {
  const auth = requireAuth(request);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const { taskId } = await params;
    const coreClient = await getServerCoreClient();
    
    // Cancel task via Core SDK
    await coreClient.images.cancelTask(taskId);
    
    return NextResponse.json({ success: true, message: 'Task cancelled successfully' });
  } catch (error) {
    return handleSDKError(error);
  }
}