import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/sdk-config';

interface EphemeralMasterKeyRequest {
  purpose?: string; // Optional purpose for logging/tracking
}

interface EphemeralMasterKeyResponse {
  ephemeralMasterKey: string;
  expiresAt: string;
  expiresInSeconds: number;
  adminApiUrl: string; // Include the Admin API URL for direct connection
}

// POST /api/auth/ephemeral-master-key - Generate an ephemeral master key for direct API access
export async function POST(request: NextRequest) {
  try {
    const body = await request.json() as EphemeralMasterKeyRequest;
    
    // Get master key from environment
    const masterKey = process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY;
    if (!masterKey) {
      console.error('Master key not configured');
      return NextResponse.json(
        { error: 'Master key not configured' },
        { status: 500 }
      );
    }

    // Get request metadata for tracking
    const sourceIP = request.headers.get('x-forwarded-for') ?? 
                     request.headers.get('x-real-ip') ?? 
                     'unknown';
    const userAgent = request.headers.get('user-agent') ?? 'unknown';
    
    // Use Admin SDK to generate ephemeral master key
    const adminClient = getServerAdminClient();
    const response = await adminClient.auth.generateEphemeralMasterKey(masterKey, {
      metadata: {
        sourceIP,
        userAgent,
        purpose: body.purpose ?? 'web-ui-request'
      }
    });
    
    // Return the ephemeral master key with Admin API URL
    // Use the external URL that the browser can access
    const result: EphemeralMasterKeyResponse = {
      ...response,
      adminApiUrl: process.env.CONDUIT_ADMIN_API_EXTERNAL_URL ?? 'http://localhost:5002',
    };
    
    return NextResponse.json(result);
  } catch (error) {
    console.error('Error generating ephemeral master key:', error);
    return handleSDKError(error);
  }
}