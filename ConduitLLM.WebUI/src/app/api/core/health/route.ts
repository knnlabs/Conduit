import { NextRequest, NextResponse } from 'next/server';
import { validateCoreSession } from '@/lib/auth/sdk-auth';
import { mapSDKErrorToResponse, withSDKErrorHandling } from '@/lib/errors/sdk-errors';
import { getServerCoreClient } from '@/lib/clients/server';

export async function GET(request: NextRequest) {
  try {
    // Validate session
    const validation = await validateCoreSession(request, { requireVirtualKey: false });
    if (!validation.isValid) {
      return new Response(
        JSON.stringify({ error: validation.error || 'Unauthorized' }),
        { status: 401, headers: { 'Content-Type': 'application/json' } }
      );
    }

    // Use SDK health service
    const client = getServerCoreClient(validation.session?.virtualKey || '');
    const healthResult = await withSDKErrorHandling(
      async () => (client as any).health.getFullHealth(),
      'get health status'
    );

    // Determine overall health status
    const isHealthy = healthResult.status === 'Healthy';
    const statusCode = isHealthy ? 200 : 503;

    // Return the SDK response directly
    return NextResponse.json(healthResult, { status: statusCode });

  } catch (error: any) {
    // Handle timeout errors
    if (error.code === 'ETIMEDOUT' || error.message?.includes('timeout')) {
      return new Response(
        JSON.stringify({
          status: 'Unhealthy',
          message: 'Core API health check timed out',
          timestamp: new Date().toISOString(),
        }),
        { 
          status: 504,
          headers: { 'Content-Type': 'application/json' }
        }
      );
    }

    // Handle connection errors
    if (error.code === 'ECONNREFUSED' || error.code === 'ENOTFOUND') {
      return new Response(
        JSON.stringify({
          status: 'Unavailable',
          message: 'Cannot connect to Core API',
          error: error.message,
          timestamp: new Date().toISOString(),
        }),
        { 
          status: 503,
          headers: { 'Content-Type': 'application/json' }
        }
      );
    }

    // Default error response
    return mapSDKErrorToResponse(error);
  }
}

// Support for HEAD requests (lightweight health check)
export async function HEAD(request: NextRequest) {
  try {
    // Quick validation without full session check
    const authHeader = request.headers.get('authorization');
    if (authHeader && !authHeader.startsWith('Bearer ')) {
      return new Response(null, { status: 401 });
    }

    // Use SDK for lightweight liveness check
    const validation = await validateCoreSession(request, { requireVirtualKey: false });
    const client = getServerCoreClient(validation.session?.virtualKey || '');
    
    const isHealthy = await (client as any).health.isSystemHealthy();

    return new Response(null, { 
      status: isHealthy ? 200 : 503,
      headers: {
        'X-Health-Status': isHealthy ? 'healthy' : 'unhealthy',
        'X-Checked-At': new Date().toISOString(),
      }
    });

  } catch (error) {
    return new Response(null, { 
      status: 503,
      headers: {
        'X-Health-Status': 'error',
        'X-Checked-At': new Date().toISOString(),
      }
    });
  }
}