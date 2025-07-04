import { NextResponse } from 'next/server';
import { withSDKAuth } from '@/lib/auth/sdk-auth';
import { mapSDKErrorToResponse, withSDKErrorHandling } from '@/lib/errors/sdk-errors';

export const GET = withSDKAuth(
  async (_request, { auth }) => {
    try {
      // Use Admin SDK to get system health which includes Core API status
      const healthData = await withSDKErrorHandling(
        async () => auth.adminClient!.system.getHealth(),
        'get system health'
      );

      // Extract Core API specific health information
      const coreApiHealth = {
        status: healthData.status || 'Unknown',
        message: '',
        timestamp: new Date().toISOString(),
        checks: healthData.checks || [],
        totalDuration: healthData.totalDuration || '',
      };

      // Determine if Core API is healthy
      const isHealthy = coreApiHealth.status === 'healthy';
      const isDegraded = coreApiHealth.status === 'degraded';
      
      // Set appropriate status code
      let statusCode = 200;
      if (!isHealthy) {
        statusCode = isDegraded ? 503 : 503;
      }

      return NextResponse.json(coreApiHealth, { status: statusCode });

    } catch (error: unknown) {
      // If we can't reach the Admin API, try a direct health check as fallback
      const coreApiUrl = process.env.CONDUIT_API_BASE_URL || process.env.NEXT_PUBLIC_CONDUIT_CORE_API_URL;
      
      if (coreApiUrl) {
        try {
          const healthUrl = new URL('/health', coreApiUrl);
          const response = await fetch(healthUrl.toString(), {
            method: 'GET',
            headers: {
              'Content-Type': 'application/json',
            },
            signal: AbortSignal.timeout(5000), // 5 second timeout
          });

          const healthData = await response.json();
          const statusCode = response.ok && healthData.status === 'Healthy' ? 200 : 503;
          return NextResponse.json(healthData, { status: statusCode });
        } catch (_fallbackError) {
          // Fallback also failed
          return NextResponse.json(
            {
              status: 'Unavailable',
              message: 'Cannot determine Core API health status',
              timestamp: new Date().toISOString(),
            },
            { status: 503 }
          );
        }
      }

      // No fallback available
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true } // Admin access required for health checks
);

// Support for HEAD requests (lightweight health check)
export const HEAD = withSDKAuth(
  async (_request, { auth }) => {
    try {
      // Quick health check using Admin SDK
      const healthData = await auth.adminClient!.system.getHealth();
      const isHealthy = healthData.status === 'healthy';

      return new Response(null, { 
        status: isHealthy ? 200 : 503,
        headers: {
          'X-Health-Status': isHealthy ? 'healthy' : 'unhealthy',
          'X-Core-API-Status': healthData.status || 'unknown',
          'X-Checked-At': new Date().toISOString(),
        }
      });

    } catch (_error) {
      return new Response(null, { 
        status: 503,
        headers: {
          'X-Health-Status': 'error',
          'X-Checked-At': new Date().toISOString(),
        }
      });
    }
  },
  { requireAdmin: true }
);