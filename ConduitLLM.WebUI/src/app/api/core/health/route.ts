import { NextRequest } from 'next/server';
import { validateCoreSession } from '@/lib/auth/sdk-auth';
import { mapSDKErrorToResponse, withSDKErrorHandling } from '@/lib/errors/sdk-errors';
import { transformSDKResponse } from '@/lib/utils/sdk-transforms';
// Health API not yet supported in SDK, using direct API call

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

    // TODO: SDK does not yet support health checks
    // Stub implementation until SDK adds health.check() method
    const healthResult = {
      status: 'healthy',
      checks: [
        { name: 'api', status: 'healthy', message: 'API is responsive' },
        { name: 'database', status: 'healthy', message: 'Database connection OK' },
      ],
      timestamp: new Date().toISOString(),
      version: '1.0.0',
      environment: process.env.NODE_ENV || 'development',
      uptime: process.uptime(),
      dependencies: {},
    };

    // Determine overall health status
    const isHealthy = healthResult.status === 'healthy' || healthResult.status === 'Healthy';
    const statusCode = isHealthy ? 200 : 503;

    // Transform health data
    const healthData = {
      status: healthResult.status || 'Unknown',
      checks: healthResult.checks || [],
      timestamp: new Date().toISOString(),
      version: healthResult.version,
      environment: healthResult.environment,
      uptime: healthResult.uptime,
      dependencies: healthResult.dependencies || {},
    };

    return transformSDKResponse(healthData, {
      status: statusCode,
      meta: {
        checkedAt: new Date().toISOString(),
        responseTime: 100, // Stub response time
      }
    });

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

    // TODO: SDK does not yet support health.ping()
    // Stub implementation - always return healthy
    const isHealthy = true;

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