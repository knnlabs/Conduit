import { NextRequest } from 'next/server';
import { withSDKAuth } from '@/lib/auth/sdk-auth';

// Generic route params interface for dynamic routes
export interface DynamicRouteParams<T = any> {
  params: Promise<T>;
}

// Helper to create route handlers with dynamic params support
export function createDynamicRouteHandler<TParams = any>(
  handler: (
    request: NextRequest,
    context: { params: TParams; auth: any }
  ) => Promise<Response>,
  authOptions?: Parameters<typeof withSDKAuth>[1]
) {
  return async (
    request: NextRequest,
    routeContext: DynamicRouteParams<TParams>
  ) => {
    const params = await routeContext.params;
    
    return withSDKAuth(
      (req, authContext) => handler(req, {
        params,
        auth: authContext.auth,
      }),
      authOptions
    )(request, { params });
  };
}

// Batch operation helper
export async function processBatchOperation<T, R>(
  items: T[],
  operation: (item: T, index: number) => Promise<R>,
  options: {
    concurrency?: number;
    continueOnError?: boolean;
  } = {}
): Promise<Array<{ success: boolean; data?: R; error?: any; index: number }>> {
  const { concurrency = 5, continueOnError = true } = options;
  const results: Array<{ success: boolean; data?: R; error?: any; index: number }> = [];
  
  // Process in batches
  for (let i = 0; i < items.length; i += concurrency) {
    const batch = items.slice(i, i + concurrency);
    const batchPromises = batch.map(async (item, batchIndex) => {
      const index = i + batchIndex;
      try {
        const data = await operation(item, index);
        return { success: true, data, index };
      } catch (error) {
        if (!continueOnError) throw error;
        return { success: false, error, index };
      }
    });
    
    const batchResults = await Promise.all(batchPromises);
    results.push(...batchResults);
  }
  
  return results;
}

// Query parameter parsing helpers
export function parseQueryParams(request: NextRequest) {
  const url = new URL(request.url);
  
  return {
    // Pagination
    page: parseInt(url.searchParams.get('page') || '1'),
    pageSize: parseInt(url.searchParams.get('pageSize') || '20'),
    
    // Filtering
    search: url.searchParams.get('search') || undefined,
    filter: url.searchParams.get('filter') || undefined,
    
    // Sorting
    sortBy: url.searchParams.get('sortBy') || undefined,
    sortOrder: (url.searchParams.get('sortOrder') || 'desc') as 'asc' | 'desc',
    
    // Date range
    startDate: url.searchParams.get('startDate') || undefined,
    endDate: url.searchParams.get('endDate') || undefined,
    
    // Boolean flags
    includeDisabled: url.searchParams.get('includeDisabled') === 'true',
    includeDeleted: url.searchParams.get('includeDeleted') === 'true',
    
    // Custom getter for any param
    get: (key: string) => url.searchParams.get(key),
    getAll: (key: string) => url.searchParams.getAll(key),
    has: (key: string) => url.searchParams.has(key),
  };
}

// Validate required fields helper
export function validateRequiredFields<T extends Record<string, any>>(
  data: T,
  requiredFields: Array<keyof T>
): { isValid: boolean; missingFields: string[] } {
  const missingFields = requiredFields.filter(field => 
    data[field] === undefined || data[field] === null || data[field] === ''
  );
  
  return {
    isValid: missingFields.length === 0,
    missingFields: missingFields as string[],
  };
}

// Create consistent validation error response
export function createValidationError(
  message: string,
  details?: Record<string, any>
): Response {
  return new Response(
    JSON.stringify({
      error: {
        type: 'VALIDATION',
        message,
        details,
      },
    }),
    {
      status: 400,
      headers: { 'Content-Type': 'application/json' },
    }
  );
}

// Create file download response
export function createFileResponse(
  data: Buffer | Uint8Array | string,
  options: {
    filename: string;
    contentType: string;
    disposition?: 'inline' | 'attachment';
  }
): Response {
  const { filename, contentType, disposition = 'attachment' } = options;

  return new Response(data, {
    headers: {
      'Content-Type': contentType,
      'Content-Disposition': `${disposition}; filename="${filename}"`,
      'Cache-Control': 'no-cache',
    },
  });
}