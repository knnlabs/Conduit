import { NextRequest, NextResponse } from 'next/server';

const adminApiUrl = process.env.CONDUIT_ADMIN_API_BASE_URL || 'http://localhost:5002';
const masterKey = process.env.CONDUIT_MASTER_KEY || '';

export async function GET(request: NextRequest) {
  if (!masterKey) {
    return NextResponse.json({ error: 'Server configuration error' }, { status: 500 });
  }

  try {
    const response = await fetch(`${adminApiUrl}/v1/system/settings`, {
      method: 'GET',
      headers: {
        'Authorization': `Bearer ${masterKey}`,
        'Content-Type': 'application/json',
      },
    });

    if (!response.ok) {
      // Return default settings if API endpoint doesn't exist
      if (response.status === 404) {
        return NextResponse.json({
          systemName: 'Conduit LLM Platform',
          description: 'Unified LLM API Gateway and Management Platform',
          enableLogging: true,
          logLevel: 'Information',
          maxConcurrentRequests: 100,
          requestTimeoutSeconds: 30,
          cacheTimeoutMinutes: 30,
          enableRateLimiting: false,
          maxRequestsPerMinute: 1000,
          rateLimitWindowSeconds: 60,
          enableIpFiltering: false,
          enableRequestValidation: true,
          maxFailedAttempts: 5,
          enablePerformanceTracking: true,
          enableHealthChecks: true,
          healthCheckIntervalMinutes: 5,
        });
      }
      
      const errorData = await response.text();
      return NextResponse.json(
        { error: errorData || 'Failed to fetch settings' },
        { status: response.status }
      );
    }

    const data = await response.json();
    return NextResponse.json(data);
  } catch (error) {
    console.error('Fetch settings error:', error);
    return NextResponse.json(
      { error: 'Failed to fetch settings' },
      { status: 500 }
    );
  }
}

export async function PUT(request: NextRequest) {
  if (!masterKey) {
    return NextResponse.json({ error: 'Server configuration error' }, { status: 500 });
  }

  try {
    const body = await request.json();
    
    const response = await fetch(`${adminApiUrl}/v1/system/settings`, {
      method: 'PUT',
      headers: {
        'Authorization': `Bearer ${masterKey}`,
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(body),
    });

    if (!response.ok) {
      const errorData = await response.text();
      return NextResponse.json(
        { error: errorData || 'Failed to update settings' },
        { status: response.status }
      );
    }

    const data = await response.json();
    return NextResponse.json(data);
  } catch (error) {
    console.error('Update settings error:', error);
    return NextResponse.json(
      { error: 'Failed to update settings' },
      { status: 500 }
    );
  }
}