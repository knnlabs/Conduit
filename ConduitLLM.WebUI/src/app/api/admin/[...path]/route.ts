import { NextRequest, NextResponse } from 'next/server';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { requireAuth } from '@/lib/auth/simple-auth';

// Generic proxy handler for all admin API routes
async function handler(
  req: NextRequest,
  { params }: { params: Promise<{ path: string[] }> }
) {
  // Check auth
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const { path } = await params;
    const pathStr = path.join('/');
    
    // The SDK adds /api to the base URL, so requests come in as /api/admin/api/*
    // We need to strip the extra /api and forward to the actual admin API
    const actualPath = pathStr.startsWith('api/') ? pathStr.substring(4) : pathStr;
    
    // Get the admin API URL from environment
    const adminApiUrl = process.env.CONDUIT_ADMIN_API_URL || 'http://admin:8080';
    const targetUrl = `${adminApiUrl}/api/${actualPath}${req.nextUrl.search}`;
    
    // Forward the request
    const headers = new Headers(req.headers);
    headers.set('X-API-Key', process.env.CONDUIT_WEBUI_AUTH_KEY || '');
    
    const response = await fetch(targetUrl, {
      method: req.method,
      headers,
      body: req.body ? await req.text() : undefined,
    });
    
    // Forward the response
    const data = await response.text();
    return new NextResponse(data, {
      status: response.status,
      headers: {
        'Content-Type': response.headers.get('Content-Type') || 'application/json',
      },
    });
  } catch (error) {
    console.error('Admin API proxy error:', error);
    return NextResponse.json(
      { error: 'Failed to proxy request to admin API' },
      { status: 500 }
    );
  }
}

// Export all HTTP methods
export const GET = handler;
export const POST = handler;
export const PUT = handler;
export const DELETE = handler;
export const PATCH = handler;