import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient, getServerCoreClient } from '@/lib/server/sdk-config';

interface EphemeralKeyRequest {
  purpose?: string; // Optional purpose for logging/tracking
}

interface EphemeralKeyResponse {
  ephemeralKey: string;
  expiresAt: string;
  expiresInSeconds: number;
  coreApiUrl: string; // Include the Core API URL for direct connection
}

// POST /api/auth/ephemeral-key - Generate an ephemeral key for direct API access
export async function POST(request: NextRequest) {
  try {
    const body = await request.json() as EphemeralKeyRequest;
    
    // Get the WebUI's virtual key from Admin API
    const adminClient = getServerAdminClient();
    let webuiVirtualKey: string;
    
    try {
      webuiVirtualKey = await adminClient.system.getWebUIVirtualKey();
    } catch (error) {
      console.error('Failed to get WebUI virtual key:', error);
      return NextResponse.json(
        { error: 'Failed to get WebUI virtual key' },
        { status: 500 }
      );
    }

    // Get request metadata for tracking
    const sourceIP = request.headers.get('x-forwarded-for') ?? 
                     request.headers.get('x-real-ip') ?? 
                     'unknown';
    const userAgent = request.headers.get('user-agent') ?? 'unknown';
    
    // Use Core SDK to generate ephemeral key
    const coreClient = await getServerCoreClient();
    const response = await coreClient.auth.generateEphemeralKey(webuiVirtualKey, {
      metadata: {
        sourceIP,
        userAgent,
        purpose: body.purpose ?? 'web-ui-request'
      }
    });
    
    // Return the ephemeral key with Core API URL
    // Use the external URL that the browser can access
    const result: EphemeralKeyResponse = {
      ...response,
      coreApiUrl: process.env.CONDUIT_API_EXTERNAL_URL ?? 'http://localhost:5000',
    };
    
    return NextResponse.json(result);
  } catch (error) {
    console.error('Error generating ephemeral key:', error);
    return handleSDKError(error);
  }
}