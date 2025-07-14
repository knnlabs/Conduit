// Type declarations for optional Next.js dependency
/// <reference types="node" />

declare module 'next/server' {
  export interface NextRequest extends Request {
    nextUrl: URL & { search: string };
    headers: Headers;
    method: string;
    json(): Promise<any>;
    text(): Promise<string>;
    formData(): Promise<FormData>;
  }

  export class NextResponse extends Response {
    static json(body: any, init?: ResponseInit): NextResponse;
  }
}