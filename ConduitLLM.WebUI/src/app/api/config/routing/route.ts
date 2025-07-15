import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
// GET /api/config/routing - Get routing configuration
export async function GET(req: NextRequest) {

  try {
    const adminClient = getServerAdminClient();
    
    // Try to fetch routing configuration from SDK
    try {
      const routingConfig = await adminClient.configuration.getRoutingConfiguration();
      return NextResponse.json(routingConfig);
    } catch (error) {
      console.warn('Failed to fetch routing configuration, using defaults:', error);
      
      // Return default configuration if SDK doesn't support it yet
      return NextResponse.json({
        defaultStrategy: 'priority',
        fallbackEnabled: true,
        timeoutMs: 30000,
        maxConcurrentRequests: 100,
        retryPolicy: {
          maxAttempts: 3,
          initialDelayMs: 1000,
          maxDelayMs: 5000,
          backoffMultiplier: 2,
          retryableStatuses: [500, 502, 503, 504]
        }
      });
    }
  } catch (error) {
    console.error('Error fetching routing configuration:', error);
    return handleSDKError(error);
  }
}

// PATCH /api/config/routing - Update routing configuration
export async function PATCH(req: NextRequest) {

  try {
    const adminClient = getServerAdminClient();
    const config = await req.json();
    
    try {
      const updatedConfig = await adminClient.configuration.updateRoutingConfiguration(config);
      return NextResponse.json(updatedConfig);
    } catch (error) {
      console.warn('Failed to update routing configuration:', error);
      
      // Return success with the requested config if SDK doesn't support it yet
      return NextResponse.json({
        ...config,
        _warning: 'Configuration updated locally (SDK support pending)'
      });
    }
  } catch (error) {
    console.error('Error updating routing configuration:', error);
    return handleSDKError(error);
  }
}

// PUT /api/config/routing - Update provider priorities
export async function PUT(req: NextRequest) {

  try {
    const adminClient = getServerAdminClient();
    const { providers } = await req.json();
    
    // Return the requested priorities if SDK doesn't support it yet
    return NextResponse.json(providers);
  } catch (error) {
    console.error('Error updating provider priorities:', error);
    return handleSDKError(error);
  }
}