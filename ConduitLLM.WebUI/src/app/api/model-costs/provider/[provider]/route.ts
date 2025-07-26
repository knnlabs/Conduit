import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';

interface RouteParams {
  params: Promise<{
    provider: string;
  }>;
}

export async function GET(req: NextRequest, { params }: RouteParams) {
  try {
    const { provider } = await params;
    const providerName = decodeURIComponent(provider);
    

    const adminClient = getServerAdminClient();
    const result = await adminClient.modelCosts.getByProvider(providerName);

    return NextResponse.json(result);
  } catch (error) {
    console.error('[ModelCosts] GET by provider error:', error);
    return handleSDKError(error);
  }
}