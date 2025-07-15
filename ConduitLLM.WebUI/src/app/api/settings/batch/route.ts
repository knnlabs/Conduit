import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
// PUT /api/settings/batch - Update multiple settings
export async function PUT(req: NextRequest) {

  try {
    const adminClient = getServerAdminClient();
    const settingsToUpdate = await req.json();
    
    // Update each setting
    for (const setting of settingsToUpdate) {
      if (setting.key && setting.value !== undefined) {
        await adminClient.settings.updateGlobalSetting(setting.key, { value: setting.value });
      }
    }
    
    return NextResponse.json({ success: true });
  } catch (error) {
    return handleSDKError(error);
  }
}
