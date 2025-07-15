import { NextRequest, NextResponse } from 'next/server';
import { requireAuth } from '@/lib/auth/simple-auth';
import { handleSDKError } from '@/lib/errors/sdk-errors';

// GET /api/admin/security/ip-rules/export - Export IP rules
export async function GET(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    // IP filtering is not yet available in the current SDK version
    return NextResponse.json({ error: 'IP filtering not available' }, { status: 501 });
  } catch (error) {
    return handleSDKError(error);
  }
}