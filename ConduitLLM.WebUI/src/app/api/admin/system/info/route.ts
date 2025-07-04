import { NextRequest, NextResponse } from 'next/server';
import { validateSession, createUnauthorizedResponse } from '@/lib/auth/middleware';
import { getAdminClient } from '@/lib/clients/conduit';
import { reportError } from '@/lib/utils/logging';

export async function GET(request: NextRequest) {
  try {
    // Validate session
    const validation = await validateSession(request);
    if (!validation.isValid) {
      return createUnauthorizedResponse(validation.error);
    }

    try {
      // Use SDK to get system info
      const adminClient = getAdminClient();
      const systemInfo = await adminClient.system.getSystemInfo();
      
      return NextResponse.json(systemInfo);
    } catch (sdkError: unknown) {
      reportError(sdkError, 'Failed to fetch system info from SDK');
      
      // Return error response instead of mock data
      return NextResponse.json(
        { 
          error: 'System information is currently unavailable',
          message: (sdkError as { message?: string })?.message || 'Failed to fetch system information'
        },
        { status: 503 }
      );
    }
  } catch (error: unknown) {
    console.error('System Info API error:', error);
    reportError(error, 'System Info API error');
    return NextResponse.json(
      { error: 'Failed to fetch system information' },
      { status: 500 }
    );
  }
}