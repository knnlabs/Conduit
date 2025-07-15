import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';

export async function GET(req: NextRequest) {
  // This endpoint is deprecated - the analytics endpoints it relies on don't exist
  return NextResponse.json(
    { error: 'Virtual key analytics endpoints are not available in the backend API' },
    { status: 501 }
  );
}
