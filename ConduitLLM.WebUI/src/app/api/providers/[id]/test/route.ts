import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { requireAuth } from '@/lib/auth/simple-auth';
import { getServerAdminClient } from '@/lib/server/adminClient';

// POST /api/providers/[id]/test - Test a specific provider connection
export async function POST(
  request: NextRequest,
  context: { params: Promise<{ id: string }> }
) {
  const auth = requireAuth(request);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const { id } = await context.params;
    
    // Convert string ID to number for the SDK
    const numericId = parseInt(id, 10);
    if (isNaN(numericId)) {
      return NextResponse.json(
        { error: 'Invalid provider ID: must be a number' },
        { status: 400 }
      );
    }
    
    const adminClient = getServerAdminClient();
    const result = await adminClient.providers.testConnectionById(numericId);

    return NextResponse.json({
      ...result,
      meta: {
        tested: true,
        providerId: id,
        timestamp: new Date().toISOString(),
      }
    });
  } catch (error) {
    return handleSDKError(error);
  }
}
