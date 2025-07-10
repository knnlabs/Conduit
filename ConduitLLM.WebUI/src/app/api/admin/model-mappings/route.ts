import { NextRequest, NextResponse } from 'next/server';
import { getAdminAuth } from '@/lib/auth/admin';
import { config } from '@/config/environment';

const ADMIN_API_URL = config.api.server?.adminUrl || 'http://localhost:5002';
const MASTER_KEY = config.auth.masterKey;

export async function GET(request: NextRequest) {
  try {
    // Verify admin authentication
    const auth = await getAdminAuth(request);
    if (!auth.isAuthenticated) {
      return NextResponse.json(
        { error: 'Unauthorized' },
        { status: 401 }
      );
    }

    // Forward request to Admin API with master key
    const response = await fetch(`${ADMIN_API_URL}/api/ModelProviderMapping`, {
      method: 'GET',
      headers: {
        'X-API-Key': MASTER_KEY,
        'Content-Type': 'application/json',
      },
    });

    if (!response.ok) {
      const error = await response.text();
      return NextResponse.json(
        { error: `Admin API error: ${error}` },
        { status: response.status }
      );
    }

    const data = await response.json();
    return NextResponse.json(data);
  } catch (error) {
    console.error('Model mappings API error:', error);
    return NextResponse.json(
      { error: 'Internal server error' },
      { status: 500 }
    );
  }
}

export async function POST(request: NextRequest) {
  try {
    // Verify admin authentication
    const auth = await getAdminAuth(request);
    if (!auth.isAuthenticated) {
      return NextResponse.json(
        { error: 'Unauthorized' },
        { status: 401 }
      );
    }

    const body = await request.json();

    // Forward request to Admin API with master key
    const response = await fetch(`${ADMIN_API_URL}/api/ModelProviderMapping`, {
      method: 'POST',
      headers: {
        'X-API-Key': MASTER_KEY,
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(body),
    });

    if (!response.ok) {
      const error = await response.text();
      return NextResponse.json(
        { error: `Admin API error: ${error}` },
        { status: response.status }
      );
    }

    const data = await response.json();
    return NextResponse.json(data);
  } catch (error) {
    console.error('Model mappings API error:', error);
    return NextResponse.json(
      { error: 'Internal server error' },
      { status: 500 }
    );
  }
}