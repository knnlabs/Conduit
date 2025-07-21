import { NextRequest, NextResponse } from 'next/server';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { handleSDKError } from '@/lib/errors/sdk-errors';

interface RouteParams {
  params: Promise<{
    id: string;
  }>;
}

export async function GET(
  request: NextRequest,
  context: RouteParams
): Promise<NextResponse> {
  try {
    const { id } = await context.params;
    const { searchParams } = new URL(request.url);
    const capability = searchParams.get('capability');

    if (!id) {
      return NextResponse.json(
        { error: 'Virtual key ID is required' },
        { status: 400 }
      );
    }

    const adminClient = getServerAdminClient();
    
    // Use the new previewDiscovery method
    const discoveryPreview = await adminClient.virtualKeys.previewDiscovery(
      id,
      capability ?? undefined
    );

    return NextResponse.json(discoveryPreview);
  } catch (error) {
    return handleSDKError(error);
  }
}