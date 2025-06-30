import { NextRequest, NextResponse } from 'next/server';
import { validateSession, createUnauthorizedResponse } from '@/lib/auth/middleware';

interface RouteParams {
  params: Promise<{
    id: string;
  }>;
}

export async function GET(request: NextRequest, { params }: RouteParams) {
  const { id } = await params;
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
    
    const response = await fetch(`${adminApiUrl}/v1/virtual-keys/${id}`, {
      method: 'GET',
      headers: {
        'Authorization': `Bearer ${masterKey}`,
        'Content-Type': 'application/json',
      },
    });
    
    if (!response.ok) {
      if (response.status === 404) {
        return NextResponse.json(
          { error: 'Virtual key not found' },
          { status: 404 }
        );
      }
      throw new Error(`API call failed with status: ${response.status}`);
    }
    
    const virtualKey = await response.json();
    
    return NextResponse.json(virtualKey);
  } catch (error: any) {
    console.error('Get Virtual Key API error:', error);
    
    if (error?.status === 404) {
      return NextResponse.json(
        { error: 'Virtual key not found' },
        { status: 404 }
      );
    }
    
    return NextResponse.json(
      { error: 'Failed to fetch virtual key' },
      { status: 500 }
    );
  }
}

export async function PUT(request: NextRequest, { params }: RouteParams) {
  const { id } = await params;
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
    
    const response = await fetch(`${adminApiUrl}/v1/virtual-keys/${id}`, {
      method: 'PUT',
      headers: {
        'Authorization': `Bearer ${masterKey}`,
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(body),
    });
    
    if (!response.ok) {
      if (response.status === 404) {
        return NextResponse.json(
          { error: 'Virtual key not found' },
          { status: 404 }
        );
      }
      throw new Error(`API call failed with status: ${response.status}`);
    }
    
    const updatedVirtualKey = await response.json();
    
    return NextResponse.json(updatedVirtualKey);
  } catch (error: any) {
    console.error('Update Virtual Key API error:', error);
    
    if (error?.status === 404) {
      return NextResponse.json(
        { error: 'Virtual key not found' },
        { status: 404 }
      );
    }
    
    return NextResponse.json(
      { error: 'Failed to update virtual key' },
      { status: 500 }
    );
  }
}

export async function DELETE(request: NextRequest, { params }: RouteParams) {
  const { id } = await params;
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
    
    const response = await fetch(`${adminApiUrl}/v1/virtual-keys/${id}`, {
      method: 'DELETE',
      headers: {
        'Authorization': `Bearer ${masterKey}`,
        'Content-Type': 'application/json',
      },
    });
    
    if (!response.ok) {
      if (response.status === 404) {
        return NextResponse.json(
          { error: 'Virtual key not found' },
          { status: 404 }
        );
      }
      throw new Error(`API call failed with status: ${response.status}`);
    }
    
    return NextResponse.json({ success: true });
  } catch (error: any) {
    console.error('Delete Virtual Key API error:', error);
    
    if (error?.status === 404) {
      return NextResponse.json(
        { error: 'Virtual key not found' },
        { status: 404 }
      );
    }
    
    return NextResponse.json(
      { error: 'Failed to delete virtual key' },
      { status: 500 }
    );
  }
}