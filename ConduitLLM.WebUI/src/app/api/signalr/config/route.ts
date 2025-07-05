import { NextRequest, NextResponse } from 'next/server';
import { config } from '@/config/environment';

export interface SignalRConfigResponse {
  coreUrl: string;
  adminUrl: string;
}

export async function GET(_request: NextRequest) {
  try {
    // Return the external URLs that browsers can connect to
    const response: SignalRConfigResponse = {
      coreUrl: config.api.external!.coreUrl,
      adminUrl: config.api.external!.adminUrl,
    };

    return NextResponse.json(response);
  } catch (error) {
    console.error('SignalR config error:', error);
    return NextResponse.json(
      { error: 'Failed to get SignalR configuration' },
      { status: 500 }
    );
  }
}