import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { requireAuth } from '@/lib/auth/simple-auth';

// GET /api/settings/[key] - Get a single setting
export async function GET(
  req: NextRequest,
  { params }: { params: Promise<{ key: string }> }
) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const { key } = await params;
    const adminClient = getServerAdminClient();
    const setting = await adminClient.settings.getGlobalSetting(key);
    return NextResponse.json(setting);
  } catch (error) {
    return handleSDKError(error);
  }
}

// PUT /api/settings/[key] - Update a setting
export async function PUT(
  req: NextRequest,
  { params }: { params: Promise<{ key: string }> }
) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const { key } = await params;
    const adminClient = getServerAdminClient();
    const body = await req.json();
    await adminClient.settings.updateGlobalSetting(key, { value: body.value });
    return NextResponse.json({ success: true });
  } catch (error) {
    return handleSDKError(error);
  }
}