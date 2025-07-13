import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { requireAuth } from '@/lib/auth/simple-auth';

// POST /api/audio-configuration/[providerId]/test - Test audio provider connection
export async function POST(
  req: NextRequest,
  { params }: { params: Promise<{ providerId: string }> }
) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const { providerId } = await params;
    const adminClient = getServerAdminClient();
    const result = await adminClient.audio.testProvider(providerId);
    
    return NextResponse.json(result);
  } catch (error) {
    console.error('Error testing audio provider:', error);
    return handleSDKError(error);
  }
}