import { NextRequest, NextResponse } from 'next/server';
import { validateSession, createUnauthorizedResponse } from '@/lib/auth/middleware';

export async function GET(request: NextRequest) {
  try {
    // Validate session
    const validation = await validateSession(request);
    if (!validation.isValid) {
      return createUnauthorizedResponse(validation.error);
    }

    // Make direct API call to Conduit Admin API
    const adminApiUrl = process.env.NEXT_PUBLIC_CONDUIT_ADMIN_API_URL;
    const masterKey = process.env.CONDUIT_MASTER_KEY;
    
    if (!adminApiUrl || !masterKey) {
      return NextResponse.json(
        { error: 'Server configuration error' },
        { status: 500 }
      );
    }
    
    try {
      // Try to get real system metrics from Admin API
      const response = await fetch(`${adminApiUrl}/v1/system/metrics`, {
        method: 'GET',
        headers: {
          'Authorization': `Bearer ${masterKey}`,
          'Content-Type': 'application/json',
        },
      });
      
      if (response.ok) {
        const metrics = await response.json();
        return NextResponse.json(metrics);
      }
      
      // If system metrics endpoint doesn't exist, return empty metrics
      if (response.status === 404) {
        return NextResponse.json({
          cpu: {
            usage: 0,
            cores: 0,
            model: 'Unknown',
            frequency: 0,
          },
          memory: {
            total: 0,
            used: 0,
            available: 0,
            usage: 0,
          },
          disk: {
            total: 0,
            used: 0,
            available: 0,
            usage: 0,
          },
          network: {
            bytesReceived: 0,
            bytesSent: 0,
            packetsReceived: 0,
            packetsSent: 0,
          },
        });
      }
      
      const error = await response.text();
      return NextResponse.json(
        { error: error || 'Failed to fetch system metrics' },
        { status: response.status }
      );
    } catch (fallbackError: any) {
      console.error('Fallback system metrics generation failed:', fallbackError);
      return NextResponse.json(
        { error: 'Failed to generate system metrics data' },
        { status: 500 }
      );
    }
  } catch (error: any) {
    console.error('System Metrics API error:', error);
    return NextResponse.json(
      { error: 'Failed to fetch system metrics' },
      { status: 500 }
    );
  }
}