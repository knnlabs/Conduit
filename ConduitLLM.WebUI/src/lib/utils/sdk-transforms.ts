import { NextResponse } from 'next/server';

// Transform SDK responses to consistent API responses
export function transformSDKResponse<T>(
  data: T,
  options: {
    status?: number;
    headers?: Record<string, string>;
    meta?: Record<string, any>;
  } = {}
): NextResponse {
  const { status = 200, headers = {}, meta } = options;

  const response = {
    data,
    ...(meta && { meta }),
  };

  return NextResponse.json(response, { status, headers });
}

// Transform paginated responses
export function transformPaginatedResponse<T>(
  items: T[],
  pagination: {
    page: number;
    pageSize: number;
    total: number;
  },
  options: {
    status?: number;
    headers?: Record<string, string>;
  } = {}
): NextResponse {
  const { page, pageSize, total } = pagination;
  const totalPages = Math.ceil(total / pageSize);

  return transformSDKResponse(items, {
    ...options,
    meta: {
      pagination: {
        page,
        pageSize,
        total,
        totalPages,
        hasMore: page < totalPages,
      },
    },
  });
}

// Transform streaming responses
export function createStreamingResponse(
  stream: AsyncIterable<any>,
  options: {
    headers?: Record<string, string>;
    transformer?: (chunk: any) => string;
  } = {}
): Response {
  const { headers = {}, transformer = (chunk) => JSON.stringify(chunk) + '\n' } = options;

  const encoder = new TextEncoder();
  const readable = new ReadableStream({
    async start(controller) {
      try {
        for await (const chunk of stream) {
          const transformed = transformer(chunk);
          controller.enqueue(encoder.encode(transformed));
        }
      } catch (error) {
        controller.error(error);
      } finally {
        controller.close();
      }
    },
  });

  return new Response(readable, {
    headers: {
      'Content-Type': 'text/event-stream',
      'Cache-Control': 'no-cache',
      'Connection': 'keep-alive',
      ...headers,
    },
  });
}

// Transform batch operation responses
export function transformBatchResponse<T, E = any>(
  results: Array<{ success: boolean; data?: T; error?: E; index: number }>,
  options: {
    status?: number;
    headers?: Record<string, string>;
  } = {}
): NextResponse {
  const successful = results.filter(r => r.success);
  const failed = results.filter(r => !r.success);

  return transformSDKResponse(
    {
      successful: successful.map(r => ({ index: r.index, data: r.data })),
      failed: failed.map(r => ({ index: r.index, error: r.error })),
    },
    {
      ...options,
      meta: {
        total: results.length,
        successful: successful.length,
        failed: failed.length,
      },
    }
  );
}

// Transform file/media responses
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
    },
  });
}

// Helper to extract pagination from SDK responses
export function extractPagination(sdkResponse: any): {
  page: number;
  pageSize: number;
  total: number;
} | null {
  // Handle different SDK pagination formats
  if (sdkResponse.pagination) {
    return {
      page: sdkResponse.pagination.page || 1,
      pageSize: sdkResponse.pagination.pageSize || 20,
      total: sdkResponse.pagination.total || 0,
    };
  }

  if (sdkResponse.meta?.pagination) {
    return {
      page: sdkResponse.meta.pagination.currentPage || 1,
      pageSize: sdkResponse.meta.pagination.perPage || 20,
      total: sdkResponse.meta.pagination.totalItems || 0,
    };
  }

  // Check for array response with metadata
  if (Array.isArray(sdkResponse.data) && sdkResponse.totalCount !== undefined) {
    return {
      page: 1,
      pageSize: sdkResponse.data.length,
      total: sdkResponse.totalCount,
    };
  }

  return null;
}

// Transform webhook payloads
export function transformWebhookPayload(
  event: string,
  data: any,
  metadata?: Record<string, any>
): any {
  return {
    event,
    timestamp: new Date().toISOString(),
    data,
    ...(metadata && { metadata }),
  };
}

// Transform error details for logging
export function transformErrorForLogging(error: any): Record<string, any> {
  return {
    message: error.message,
    type: error.type || error.name || 'Unknown',
    statusCode: error.statusCode || error.response?.status,
    context: error.context,
    stack: process.env.NODE_ENV === 'development' ? error.stack : undefined,
    timestamp: new Date().toISOString(),
    ...(error.response && {
      response: {
        status: error.response.status,
        data: error.response.data,
        headers: error.response.headers,
      },
    }),
  };
}

// Sanitize SDK responses to remove sensitive data
export function sanitizeResponse<T extends Record<string, any>>(
  data: T,
  sensitiveFields: string[] = ['apiKey', 'masterKey', 'password', 'secret']
): T {
  const sanitized = { ...data } as any;

  for (const field of sensitiveFields) {
    if (field in sanitized) {
      sanitized[field] = '[REDACTED]';
    }
  }

  // Recursively sanitize nested objects
  for (const [key, value] of Object.entries(sanitized)) {
    if (value && typeof value === 'object' && !Array.isArray(value)) {
      sanitized[key] = sanitizeResponse(value, sensitiveFields);
    }
  }

  return sanitized as T;
}