import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
// GET /api/settings - Get all settings
export async function GET(req: NextRequest) {

  try {
    const adminClient = getServerAdminClient();
    const response = await adminClient.settings.getGlobalSettings();
    
    // Handle different response formats from the SDK
    let settings: any[] = [];
    if (Array.isArray(response)) {
      settings = response;
    } else if (response && typeof response === 'object' && 'settings' in response) {
      settings = response.settings;
    }
    
    // Return just the settings array
    return NextResponse.json(settings);
  } catch (error) {
    return handleSDKError(error);
  }
}
