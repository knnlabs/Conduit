import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';

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
    
    // In development mode with DISABLE_CLERK_AUTH=true, 
    // we return the master key directly without calling the Admin API
    // In production, this would call the Admin API to generate a real ephemeral key
    
    const isDevelopment = process.env.DISABLE_CLERK_AUTH === 'true';
    
    if (isDevelopment) {
      // Development mode: return the master key directly
      const result: EphemeralMasterKeyResponse = {
        ephemeralMasterKey: masterKey,
        expiresAt: new Date(Date.now() + 3600000).toISOString(), // 1 hour from now
        expiresInSeconds: 3600,
        adminApiUrl: process.env.CONDUIT_ADMIN_API_EXTERNAL_URL ?? 'http://localhost:5002',
      };
      
      return NextResponse.json(result);
    }
    
    // Production mode: call the Admin API's ephemeral master key endpoint
    const adminApiUrl = process.env.CONDUIT_ADMIN_API_BASE_URL ?? 'http://admin-api:5002';
    const masterKeyHeader = 'X-Master-Key';
    const ephemeralKeyResponse = await fetch(`${adminApiUrl}/api/admin/auth/ephemeral-master-key`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        [masterKeyHeader]: masterKey,
      },
      body: JSON.stringify({
        metadata: {
          sourceIP,
          userAgent,
          purpose: body.purpose ?? 'web-ui-request'
        }
      }),
    });

    if (!ephemeralKeyResponse.ok) {
      const errorText = await ephemeralKeyResponse.text();
      console.error('Failed to generate ephemeral master key:', errorText);
      throw new Error(`Failed to generate ephemeral master key: ${ephemeralKeyResponse.status}`);
    }

    const response = await ephemeralKeyResponse.json() as EphemeralMasterKeyResponse;
    
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