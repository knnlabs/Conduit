import { NextRequest, NextResponse } from 'next/server';
import { FetchConduitAdminClient as ConduitAdminClient } from '../FetchConduitAdminClient';
import { ConduitError, serializeError, isConduitError } from '../utils/errors';

type HttpMethod = 'GET' | 'POST' | 'PUT' | 'DELETE' | 'PATCH';

// Define proper types for request bodies
type JsonValue = string | number | boolean | null | JsonObject | JsonArray;
type JsonObject = { [key: string]: JsonValue };
type JsonArray = JsonValue[];

interface RouteContext {
  params: Promise<Record<string, string | string[]>>;
  searchParams: URLSearchParams;
  request: NextRequest;
  body?: JsonValue | FormData | string;
}

export interface AdminRouteHandlerContext<TBody = JsonValue> {
  client: ConduitAdminClient;
  searchParams: URLSearchParams;
  params: Record<string, string | string[]>;
  body?: TBody;
  request: NextRequest;
}

export type AdminRouteHandler<TResponse = unknown, TBody = JsonValue> = (
  context: AdminRouteHandlerContext<TBody>
) => Promise<TResponse>;

interface AdminRouteOptions {
  method?: HttpMethod;
}

function isServerEnvironment(): boolean {
  return typeof globalThis === 'object' && !('window' in globalThis);
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
      error: serialized.message ?? 'Internal server error',
      statusCode: 500,
      timestamp: new Date().toISOString(),
      details: isDevelopment ? serialized : undefined,
    },
    { status: 500 }
  );
}

async function parseRequestBody(request: NextRequest): Promise<JsonValue | FormData | string | undefined> {
  const contentType = request.headers.get('content-type');
  
  if (!contentType) {
    return undefined;
  }

  try {
    if (contentType.includes('application/json')) {
      return await request.json() as JsonValue;
    }
    
    if (contentType.includes('multipart/form-data')) {
      return await request.formData();
    }
    
    if (contentType.includes('application/x-www-form-urlencoded')) {
      const text = await request.text();
      const entries = Object.fromEntries(new URLSearchParams(text));
      return entries as JsonObject;
    }
    
    return await request.text();
  } catch (error) {
    throw new ConduitError('Invalid request body', 400, 'INVALID_REQUEST_BODY', { 
      details: { error: 'Invalid request body' },
      originalError: error 
    });
  }
}

export function createAdminRoute<TResponse = unknown, TBody = JsonValue>(
  handler: AdminRouteHandler<TResponse, TBody>,
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

      const apiUrl = process.env.CONDUIT_ADMIN_API_URL ?? process.env.CONDUIT_API_URL;
      if (!apiUrl) {
        throw new Error(
          'CONDUIT_ADMIN_API_URL or CONDUIT_API_URL environment variable is not set. ' +
          'This is required to connect to the Conduit admin API.'
        );
      }

      const client = new ConduitAdminClient({
        masterKey: authKey,
        baseUrl: apiUrl,
      });

      const searchParams = new URLSearchParams(request.nextUrl.search);
      const params = await context.params;
      
      let body: JsonValue | FormData | string | undefined;
      if (request.method !== 'GET' && request.method !== 'DELETE') {
        body = await parseRequestBody(request);
      }

      const result = await handler({
        client,
        searchParams,
        params,
        body: body as TBody | undefined,
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