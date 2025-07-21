import { NextRequest } from 'next/server';

/**
 * Simple request/response logger for API routes
 * In development, logs everything to console
 * In production, could send to a proper logging service
 */
type ApiHandler = (
  req: NextRequest,
  ...args: unknown[] // Generic handler args - can be any parameters
) => Promise<Response | undefined> | Promise<Response>;

export class ApiLogger {
  private routeName: string;
  private startTime: number;

  constructor(routeName: string) {
    this.routeName = routeName;
    this.startTime = Date.now();
  }

    logRequest(request: NextRequest, body?: unknown) { // Generic request body - can be any JSON structure
    if (process.env.NODE_ENV === 'development') {
            console.warn(`\n=== API REQUEST: ${this.routeName} ===`);
            console.warn(`Method: ${request.method}`);
            console.warn(`URL: ${request.url}`);
            console.warn(`Headers:`, Object.fromEntries(request.headers.entries()));
      if (body) {
                console.warn(`Body:`, JSON.stringify(body, null, 2));
      }
            console.warn(`=================================\n`);
    }
  }

    logResponse(status: number, body?: unknown) { // Generic response body - can be any JSON structure
    if (process.env.NODE_ENV === 'development') {
      const duration = Date.now() - this.startTime;
            console.warn(`\n=== API RESPONSE: ${this.routeName} ===`);
            console.warn(`Status: ${status}`);
            console.warn(`Duration: ${duration}ms`);
      if (body) {
                console.warn(`Body:`, JSON.stringify(body, null, 2));
      }
            console.warn(`==================================\n`);
    }
  }

    logError(error: unknown) { // Generic error - can be any error type
    const duration = Date.now() - this.startTime;
    console.error(`\n=== API ERROR: ${this.routeName} ===`);
    console.error(`Duration: ${duration}ms`);
    console.error(`Error:`, error);
        if (error instanceof Error) {
      console.error(`Stack:`, error.stack);
    }
    console.error(`================================\n`);
  }
}

/**
 * Middleware wrapper that logs all requests/responses
 */
export function withLogging(routeName: string, handler: ApiHandler) {
      return async (req: NextRequest, ...args: unknown[]) => { // Generic handler args - can be any parameters
    const logger = new ApiLogger(routeName);
        const request = req;
    
    try {
      // Log request
      let body: unknown; // Generic request body - can be any JSON structure
      if (request.method !== 'GET' && request.method !== 'HEAD') {
        try {
          body = await request.clone().json() as unknown; // Parse any JSON body type
        } catch {
          // Not JSON body, ignore
        }
      }
      logger.logRequest(request, body);
      
      // Execute handler
            const response = await handler(req, ...args);
      
      // Log response
      if (response instanceof Response) {
        const responseBody: unknown = await response.clone().json().catch(() => null); // Parse any JSON response type
        logger.logResponse(response.status, responseBody);
      }
      
      return response;
    } catch (error) {
      logger.logError(error);
      throw error;
    }
  };
}