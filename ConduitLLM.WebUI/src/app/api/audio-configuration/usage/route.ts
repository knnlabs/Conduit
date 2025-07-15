import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { requireAuth } from '@/lib/auth/simple-auth';

// GET /api/audio-configuration/usage - Get audio usage data
export async function GET(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    // Audio configuration is not yet available in the current SDK version
    return NextResponse.json({ error: 'Audio configuration not available' }, { status: 501 });
  } catch (error) {
    console.error('Error fetching audio usage:', error);
    return handleSDKError(error);
  }
}