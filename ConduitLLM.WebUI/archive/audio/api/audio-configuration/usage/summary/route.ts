import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
// GET /api/audio-configuration/usage/summary - Get audio usage summary
export async function GET(req: NextRequest) {

  try {
    // Audio configuration is not yet available in the current SDK version
    return NextResponse.json({ error: 'Audio configuration not available' }, { status: 501 });
  } catch (error) {
    console.error('Error fetching audio usage summary:', error);
    return handleSDKError(error);
  }
}