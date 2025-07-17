import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';

interface RouteParams {
  params: Promise<{
    pattern: string;
  }>;
}

export async function GET(req: NextRequest, { params }: RouteParams) {
  try {
    const { pattern: patternStr } = await params;
    const pattern = decodeURIComponent(patternStr);
    
    console.log('[ModelCosts] GET by pattern:', pattern);

    const adminClient = getServerAdminClient();
    const result = await adminClient.modelCosts.getByPattern(pattern);

    return NextResponse.json(result);
  } catch (error) {
    console.error('[ModelCosts] GET by pattern error:', error);
    return handleSDKError(error);
  }
}