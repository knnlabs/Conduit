import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';

export async function POST(req: NextRequest) {
  try {
    const body = await req.json();
    
    // Expecting an array of model costs to import
    if (!Array.isArray(body)) {
      return NextResponse.json(
        { error: 'Expected an array of model costs' },
        { status: 400 }
      );
    }

    console.log('[ModelCosts] Import request, count:', body.length);

    const adminClient = getServerAdminClient();
    const result = await adminClient.modelCosts.import(body);

    console.log('[ModelCosts] Import success:', result);
    return NextResponse.json(result);
  } catch (error) {
    console.error('[ModelCosts] Import error:', error);
    return handleSDKError(error);
  }
}