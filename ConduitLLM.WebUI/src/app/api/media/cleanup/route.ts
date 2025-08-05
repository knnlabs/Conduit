import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import type { MediaCleanupRequest } from '@knn_labs/conduit-admin-client';

export async function POST(request: NextRequest) {
  try {
    const body = await request.json() as { type?: string; daysToKeep?: number };
    const { type, daysToKeep } = body;

    if (!type || !['expired', 'orphaned', 'prune'].includes(type)) {
      return NextResponse.json({ error: 'Invalid cleanup type' }, { status: 400 });
    }

    const cleanupRequest: MediaCleanupRequest = {
      type: type as 'expired' | 'orphaned' | 'prune',
      daysToKeep,
    };

    const adminClient = getServerAdminClient();
    const data = await adminClient.media.cleanupMedia(cleanupRequest);
    
    return NextResponse.json(data);
  } catch (error) {
    console.error('Error cleaning up media:', error);
    return handleSDKError(error);
  }
}