import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';

interface SettingUpdateRequest {
  value: unknown;
}
// GET /api/settings/[key] - Get a single setting
export async function GET(
  req: NextRequest,
  { params }: { params: Promise<{ key: string }> }
) {

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

  try {
    const { key } = await params;
    const adminClient = getServerAdminClient();
    const body = await req.json() as SettingUpdateRequest;
    await adminClient.settings.updateGlobalSetting(key as string, { value: body.value as string });
    return NextResponse.json({ success: true });
  } catch (error) {
    return handleSDKError(error);
  }
}