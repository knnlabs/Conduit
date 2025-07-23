import { NextRequest, NextResponse } from 'next/server';
import { bd as FetchConduitAdminClient } from './FetchConduitAdminClient-Db_qplg5.mjs';
import '@knn_labs/conduit-common';

type HttpMethod = 'GET' | 'POST' | 'PUT' | 'DELETE' | 'PATCH';
type JsonValue = string | number | boolean | null | JsonObject | JsonArray;
type JsonObject = {
    [key: string]: JsonValue;
};
type JsonArray = JsonValue[];
interface RouteContext {
    params: Promise<Record<string, string | string[]>>;
    searchParams: URLSearchParams;
    request: NextRequest;
    body?: JsonValue | FormData | string;
}
interface AdminRouteHandlerContext<TBody = JsonValue> {
    client: FetchConduitAdminClient;
    searchParams: URLSearchParams;
    params: Record<string, string | string[]>;
    body?: TBody;
    request: NextRequest;
}
type AdminRouteHandler<TResponse = unknown, TBody = JsonValue> = (context: AdminRouteHandlerContext<TBody>) => Promise<TResponse>;
interface AdminRouteOptions {
    method?: HttpMethod;
}
declare function createAdminRoute<TResponse = unknown, TBody = JsonValue>(handler: AdminRouteHandler<TResponse, TBody>, _options?: AdminRouteOptions): (request: NextRequest, context: RouteContext) => Promise<NextResponse>;
declare const GET: (handler: AdminRouteHandler) => (request: NextRequest, context: RouteContext) => Promise<NextResponse>;
declare const POST: (handler: AdminRouteHandler) => (request: NextRequest, context: RouteContext) => Promise<NextResponse>;
declare const PUT: (handler: AdminRouteHandler) => (request: NextRequest, context: RouteContext) => Promise<NextResponse>;
declare const DELETE: (handler: AdminRouteHandler) => (request: NextRequest, context: RouteContext) => Promise<NextResponse>;
declare const PATCH: (handler: AdminRouteHandler) => (request: NextRequest, context: RouteContext) => Promise<NextResponse>;

export { type AdminRouteHandler, type AdminRouteHandlerContext, DELETE, GET, PATCH, POST, PUT, createAdminRoute };
