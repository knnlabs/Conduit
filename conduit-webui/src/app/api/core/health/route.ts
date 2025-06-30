import { NextRequest, NextResponse } from 'next/server';
import { validateSession, createUnauthorizedResponse } from '@/lib/auth/middleware';

export async function GET(request: NextRequest) {
  try {
    // Validate session
    const validation = await validateSession(request);
    if (!validation.isValid) {
      return createUnauthorizedResponse(validation.error);
    }

    // Check Core API health by making a direct request
    const coreApiUrl = process.env.NEXT_PUBLIC_CONDUIT_CORE_API_URL || 'http://localhost:5000';
    const healthUrl = `${coreApiUrl}/v1/health`;

    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), 10000); // 10 second timeout

    try {
      const response = await fetch(healthUrl, {
        method: 'GET',
        headers: {
          'Accept': 'application/json',
        },
        signal: controller.signal,
      });

      clearTimeout(timeoutId);

      if (!response.ok) {
        return NextResponse.json(
          { 
            status: 'Unhealthy',
            message: `Core API returned ${response.status}`,
            timestamp: new Date().toISOString(),
          },
          { status: 503 }
        );
      }

      const healthData = await response.json();
      
      return NextResponse.json({
        status: healthData.status || 'Unknown',
        checks: healthData.checks || [],
        timestamp: new Date().toISOString(),
        coreApiUrl,
      });

    } catch (fetchError: any) {
      clearTimeout(timeoutId);
      
      if (fetchError.name === 'AbortError') {
        return NextResponse.json(
          { 
            status: 'Unhealthy',
            message: 'Core API health check timed out',
            timestamp: new Date().toISOString(),
          },
          { status: 504 }
        );
      }

      return NextResponse.json(
        { 
          status: 'Unavailable',
          message: 'Cannot connect to Core API',
          error: fetchError.message,
          timestamp: new Date().toISOString(),
        },
        { status: 503 }
      );
    }

  } catch (error: any) {
    console.error('Core API health check error:', error);
    return NextResponse.json(
      { 
        status: 'Error',
        message: 'Health check failed',
        timestamp: new Date().toISOString(),
      },
      { status: 500 }
    );
  }
}