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
    
    const response = await fetch(`${adminApiUrl}/v1/virtual-keys`, {
      method: 'GET',
      headers: {
        'Authorization': `Bearer ${masterKey}`,
        'Content-Type': 'application/json',
      },
    });
    
    if (!response.ok) {
      throw new Error(`API call failed with status: ${response.status}`);
    }
    
    const virtualKeys = await response.json();
    
    return NextResponse.json(virtualKeys);
  } catch (error: any) {
    console.error('Virtual Keys API error:', error);
    return NextResponse.json(
      { error: 'Failed to fetch virtual keys' },
      { status: 500 }
    );
  }
}

export async function POST(request: NextRequest) {
  try {
    // Validate session
    const validation = await validateSession(request);
    if (!validation.isValid) {
      return createUnauthorizedResponse(validation.error);
    }

    // Parse request body
    const body = await request.json();
    
    // Make direct API call to Conduit Admin API
    const adminApiUrl = process.env.NEXT_PUBLIC_CONDUIT_ADMIN_API_URL;
    const masterKey = process.env.CONDUIT_MASTER_KEY;
    
    if (!adminApiUrl || !masterKey) {
      return NextResponse.json(
        { error: 'Server configuration error' },
        { status: 500 }
      );
    }
    
    const response = await fetch(`${adminApiUrl}/v1/virtual-keys`, {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${masterKey}`,
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(body),
    });
    
    if (!response.ok) {
      throw new Error(`API call failed with status: ${response.status}`);
    }
    
    const newVirtualKey = await response.json();
    
    return NextResponse.json(newVirtualKey, { status: 201 });
  } catch (error: any) {
    console.error('Create Virtual Key API error:', error);
    return NextResponse.json(
      { error: 'Failed to create virtual key' },
      { status: 500 }
    );
  }
}