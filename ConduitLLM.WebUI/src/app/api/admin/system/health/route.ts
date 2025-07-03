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
      // Use SDK to get system health
      const adminClient = getAdminClient();
      const health = await adminClient.system.getHealth();
      
      // Transform SDK response to match expected format
      const transformedHealth = {
        status: health.status,
        timestamp: health.timestamp,
        services: Object.entries(health.checks).map(([name, check]) => ({
          name,
          status: check.status === 'healthy' ? 'running' : check.status === 'degraded' ? 'degraded' : 'stopped',
          health: check.status,
          description: check.description,
          duration: check.duration,
          error: check.error,
        })),
        totalDuration: health.totalDuration,
      };

      return NextResponse.json(transformedHealth);
    } catch (sdkError: any) {
      reportError(sdkError, 'Failed to fetch system health from SDK');
      
      // Return error response instead of mock data
      return NextResponse.json(
        { 
          error: 'System health is currently unavailable',
          message: sdkError.message || 'Failed to fetch system health'
        },
        { status: 503 }
      );
    }
  } catch (error: any) {
    console.error('System Health API error:', error);
    reportError(error, 'System Health API error');
    return NextResponse.json(
      { error: 'Failed to fetch system health' },
      { status: 500 }
    );
  }
}