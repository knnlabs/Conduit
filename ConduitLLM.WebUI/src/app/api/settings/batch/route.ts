import { NextRequest, NextResponse } from 'next/server';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { requireAuth } from '@/lib/auth/simple-auth';

// PUT /api/settings/batch - Update multiple settings
export async function PUT(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

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
    console.error('Error updating settings:', error);
    return NextResponse.json(
      { error: 'Failed to update settings' },
      { status: 500 }
    );
  }
}