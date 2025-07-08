import { NextRequest, NextResponse } from 'next/server';
import { ConduitAdminClient } from '../client/ConduitAdminClient';
import { ConduitError, serializeError, isConduitError } from '../utils/errors';

type HttpMethod = 'GET' | 'POST' | 'PUT' | 'DELETE' | 'PATCH';

interface RouteContext {
  params: Promise<Record<string, string | string[]>>;
  searchParams: URLSearchParams;
  request: NextRequest;
  body?: any;
}

export interface AdminRouteHandlerContext {
  client: ConduitAdminClient;
  searchParams: URLSearchParams;
  params: Record<string, string | string[]>;
  body?: any;
  request: NextRequest;
}

export type AdminRouteHandler<T = any> = (context: AdminRouteHandlerContext) => Promise<T>;

interface AdminRouteOptions {
  method?: HttpMethod;
}

function isServerEnvironment(): boolean {
  return typeof (globalThis as any).window === 'undefined';
}

function mapErrorToResponse(error: unknown): NextResponse {
  const serialized = serializeError(error);
  
  if (isConduitError(error)) {
    return NextResponse.json(
      serialized,
      { status: error.statusCode }
    );
  }

  // For non-Conduit errors, return a generic 500 error
  const isDevelopment = process.env.NODE_ENV === 'development';
  return NextResponse.json(
    {
      ...serialized,
      error: serialized.message || 'Internal server error',
      statusCode: 500,
      timestamp: new Date().toISOString(),
      details: isDevelopment ? serialized : undefined,
    },
    { status: 500 }
  );
}

async function parseRequestBody(request: NextRequest): Promise<any> {
  const contentType = request.headers.get('content-type');
  
  if (!contentType) {
    return undefined;
  }

  try {
    if (contentType.includes('application/json')) {
      return await request.json();
    }
    
    if (contentType.includes('multipart/form-data')) {
      return await request.formData();
    }
    
    if (contentType.includes('application/x-www-form-urlencoded')) {
      const text = await request.text();
      return Object.fromEntries(new URLSearchParams(text));
    }
    
    return await request.text();
  } catch (error) {
    throw new ConduitError('Invalid request body', 400, 'INVALID_REQUEST_BODY', { 
      details: { error: 'Invalid request body' },
      originalError: error 
    });
  }
}

export function createAdminRoute<T = any>(
  handler: AdminRouteHandler<T>,
  _options: AdminRouteOptions = {}
): (request: NextRequest, context: RouteContext) => Promise<NextResponse> {
  if (!isServerEnvironment()) {
    throw new Error(
      'createAdminRoute can only be used in server-side route handlers. ' +
      'It cannot be imported or used in client components.'
    );
  }

  return async (request: NextRequest, context: RouteContext) => {
    try {
      const authKey = process.env.CONDUIT_WEBUI_AUTH_KEY;
      if (!authKey) {
        throw new Error(
          'CONDUIT_WEBUI_AUTH_KEY environment variable is not set. ' +
          'This is required for admin authentication.'
        );
      }

      const apiUrl = process.env.CONDUIT_ADMIN_API_URL || process.env.CONDUIT_API_URL;
      if (!apiUrl) {
        throw new Error(
          'CONDUIT_ADMIN_API_URL or CONDUIT_API_URL environment variable is not set. ' +
          'This is required to connect to the Conduit admin API.'
        );
      }

      const client = new ConduitAdminClient({
        masterKey: authKey,
        adminApiUrl: apiUrl,
      });

      const searchParams = new URLSearchParams(request.nextUrl.search);
      const params = await context.params;
      
      let body: any;
      if (request.method !== 'GET' && request.method !== 'DELETE') {
        body = await parseRequestBody(request);
      }

      const result = await handler({
        client,
        searchParams,
        params,
        body,
        request,
      });

      if (result instanceof NextResponse) {
        return result;
      }

      if (result instanceof Response) {
        return new NextResponse(result.body, {
          status: result.status,
          statusText: result.statusText,
          headers: result.headers,
        });
      }

      return NextResponse.json(result);
    } catch (error) {
      return mapErrorToResponse(error);
    }
  };
}

export const GET = (handler: AdminRouteHandler) => createAdminRoute(handler, { method: 'GET' });
export const POST = (handler: AdminRouteHandler) => createAdminRoute(handler, { method: 'POST' });
export const PUT = (handler: AdminRouteHandler) => createAdminRoute(handler, { method: 'PUT' });
export const DELETE = (handler: AdminRouteHandler) => createAdminRoute(handler, { method: 'DELETE' });
export const PATCH = (handler: AdminRouteHandler) => createAdminRoute(handler, { method: 'PATCH' });