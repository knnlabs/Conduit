import { NextResponse } from 'next/server';
import { auth } from '@clerk/nextjs/server';

/**
 * GET /api/auth/client-config
 * Returns client configuration for authenticated users
 * This endpoint provides the necessary configuration for client-side SDK usage
 */
export async function GET() {
  try {
    // Verify user is authenticated
    const { userId } = await auth();
    if (!userId) {
      return NextResponse.json(
        { error: 'Unauthorized' },
        { status: 401 }
      );
    }
    
    // In production, you might want to create user-specific virtual keys
    // For now, we'll use the WebUI virtual key from environment
    const apiKey = process.env.NEXT_PUBLIC_CONDUIT_API_KEY ?? process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY;
    // Use CONDUIT_API_EXTERNAL_URL for browser-accessible API endpoints
    const baseURL = process.env.CONDUIT_API_EXTERNAL_URL ?? 'http://localhost:5000';
    
    if (!apiKey) {
      console.error('No API key available for client configuration');
      return NextResponse.json(
        { error: 'Configuration error' },
        { status: 500 }
      );
    }
    
    // Return client configuration
    return NextResponse.json({
      apiKey,
      baseURL,
      // Include other non-sensitive configuration if needed
      features: {
        signalR: true,
        videoProgressTracking: true,
      }
    });
  } catch (error) {
    console.error('Error in client config endpoint:', error);
    return NextResponse.json(
      { error: 'Internal server error' },
      { status: 500 }
    );
  }
}