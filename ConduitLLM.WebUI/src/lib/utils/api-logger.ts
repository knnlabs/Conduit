import { NextRequest } from 'next/server';

/**
 * Simple request/response logger for API routes
 * In development, logs everything to console
 * In production, could send to a proper logging service
 */
export class ApiLogger {
  private routeName: string;
  private startTime: number;

  constructor(routeName: string) {
    this.routeName = routeName;
    this.startTime = Date.now();
  }

  logRequest(request: NextRequest, body?: any) {
    if (process.env.NODE_ENV === 'development') {
      console.log(`\n=== API REQUEST: ${this.routeName} ===`);
      console.log(`Method: ${request.method}`);
      console.log(`URL: ${request.url}`);
      console.log(`Headers:`, Object.fromEntries(request.headers.entries()));
      if (body) {
        console.log(`Body:`, JSON.stringify(body, null, 2));
      }
      console.log(`=================================\n`);
    }
  }

  logResponse(status: number, body?: any) {
    if (process.env.NODE_ENV === 'development') {
      const duration = Date.now() - this.startTime;
      console.log(`\n=== API RESPONSE: ${this.routeName} ===`);
      console.log(`Status: ${status}`);
      console.log(`Duration: ${duration}ms`);
      if (body) {
        console.log(`Body:`, JSON.stringify(body, null, 2));
      }
      console.log(`==================================\n`);
    }
  }

  logError(error: any) {
    const duration = Date.now() - this.startTime;
    console.error(`\n=== API ERROR: ${this.routeName} ===`);
    console.error(`Duration: ${duration}ms`);
    console.error(`Error:`, error);
    console.error(`Stack:`, error?.stack);
    console.error(`================================\n`);
  }
}

/**
 * Middleware wrapper that logs all requests/responses
 */
export function withLogging(routeName: string, handler: Function) {
  return async (...args: any[]) => {
    const logger = new ApiLogger(routeName);
    const request = args[0] as NextRequest;
    
    try {
      // Log request
      let body;
      if (request.method !== 'GET' && request.method !== 'HEAD') {
        try {
          body = await request.clone().json();
        } catch {
          // Not JSON body, ignore
        }
      }
      logger.logRequest(request, body);
      
      // Execute handler
      const response = await handler(...args);
      
      // Log response
      if (response instanceof Response) {
        const responseBody = await response.clone().json().catch(() => null);
        logger.logResponse(response.status, responseBody);
      }
      
      return response;
    } catch (error) {
      logger.logError(error);
      throw error;
    }
  };
}