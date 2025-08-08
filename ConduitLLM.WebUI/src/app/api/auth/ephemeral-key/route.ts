import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/sdk-config';

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
    
    // Get the WebUI's virtual key
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

    // Get Core API URL - use internal URL for server-to-server communication
    const coreApiUrl = process.env.CONDUIT_API_BASE_URL ?? 'http://localhost:5000';
    
    // Get request metadata for tracking
    const sourceIP = request.headers.get('x-forwarded-for') ?? 
                     request.headers.get('x-real-ip') ?? 
                     'unknown';
    const userAgent = request.headers.get('user-agent') ?? 'unknown';
    
    // Call Core API to generate ephemeral key using the WebUI's virtual key
    const response = await fetch(`${coreApiUrl}/v1/auth/ephemeral-key`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${webuiVirtualKey}`,
      },
      body: JSON.stringify({
        metadata: {
          sourceIP,
          userAgent,
          purpose: body.purpose ?? 'web-ui-request',
          requestId: crypto.randomUUID(),
        }
      }),
    });

    if (!response.ok) {
      const errorText = await response.text();
      console.error('Failed to generate ephemeral key:', response.status, errorText);
      
      return NextResponse.json(
        { error: 'Failed to generate ephemeral key' },
        { status: response.status }
      );
    }

    const data = await response.json() as Omit<EphemeralKeyResponse, 'coreApiUrl'>;
    
    // Return the ephemeral key with Core API URL
    // Use the external URL that the browser can access
    const result: EphemeralKeyResponse = {
      ...data,
      coreApiUrl: process.env.CONDUIT_API_EXTERNAL_URL ?? 'http://localhost:5000',
    };
    
    return NextResponse.json(result);
  } catch (error) {
    console.error('Error generating ephemeral key:', error);
    return handleSDKError(error);
  }
}