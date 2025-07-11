import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { requireAuth } from '@/lib/auth/simple-auth';

// POST /api/providers/test-config - Test a provider configuration before saving
export async function POST(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const config = await req.json();
    const adminClient = getServerAdminClient();
    const result = await adminClient.providers.testConfig(config);
    return NextResponse.json(result);
  } catch (error) {
    return handleSDKError(error);
  }
}