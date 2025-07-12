import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { requireAuth } from '@/lib/auth/simple-auth';
import { getServerCoreClient } from '@/lib/server/coreClient';

// GET /api/videos/tasks/[taskId] - Get video generation task status
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
    const coreClient = getServerCoreClient();
    
    // Get task status from Core SDK
    const status = await coreClient.videos.getTaskStatus(taskId);
    
    return NextResponse.json(status);
  } catch (error) {
    return handleSDKError(error);
  }
}

// DELETE /api/videos/tasks/[taskId] - Cancel video generation task
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
    const coreClient = getServerCoreClient();
    
    // Cancel task via Core SDK
    await coreClient.videos.cancelTask(taskId);
    
    return NextResponse.json({ success: true, message: 'Task cancelled successfully' });
  } catch (error) {
    return handleSDKError(error);
  }
}