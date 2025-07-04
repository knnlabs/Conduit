import { NextResponse } from 'next/server';

// Transform SDK responses to consistent API responses
export function transformSDKResponse<T>(
  data: T,
  options: {
    status?: number;
    headers?: Record<string, string>;
    meta?: Record<string, unknown>;
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
  stream: AsyncIterable<unknown>,
  options: {
    headers?: Record<string, string>;
    transformer?: (chunk: unknown) => string;
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
export function transformBatchResponse<T, E = unknown>(
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
export function extractPagination(sdkResponse: unknown): {
  page: number;
  pageSize: number;
  total: number;
} | null {
  // Handle different SDK pagination formats
  if (sdkResponse && typeof sdkResponse === 'object' && 'pagination' in sdkResponse) {
    const response = sdkResponse as {
      pagination: {
        page?: number;
        pageSize?: number;
        total?: number;
      };
    };
    return {
      page: response.pagination.page || 1,
      pageSize: response.pagination.pageSize || 20,
      total: response.pagination.total || 0,
    };
  }

  const responseWithMeta = sdkResponse as {
    meta?: {
      pagination?: {
        currentPage?: number;
        perPage?: number;
        totalItems?: number;
      };
    };
    data?: unknown[];
    totalCount?: number;
  };

  if (responseWithMeta.meta?.pagination) {
    return {
      page: responseWithMeta.meta.pagination.currentPage || 1,
      pageSize: responseWithMeta.meta.pagination.perPage || 20,
      total: responseWithMeta.meta.pagination.totalItems || 0,
    };
  }

  // Check for array response with metadata
  if (Array.isArray(responseWithMeta.data) && responseWithMeta.totalCount !== undefined) {
    return {
      page: 1,
      pageSize: responseWithMeta.data.length,
      total: responseWithMeta.totalCount,
    };
  }

  return null;
}

// Transform webhook payloads
export function transformWebhookPayload(
  event: string,
  data: unknown,
  metadata?: Record<string, unknown>
): unknown {
  return {
    event,
    timestamp: new Date().toISOString(),
    data,
    ...(metadata && { metadata }),
  };
}

// Transform error details for logging
export function transformErrorForLogging(error: unknown): Record<string, unknown> {
  const err = error as Record<string, unknown>;
  const result: Record<string, unknown> = {
    message: err.message || 'Unknown error',
    type: err.type || err.name || 'Unknown',
    statusCode: err.statusCode || (err.response as {status?: number})?.status,
    context: err.context,
    stack: process.env.NODE_ENV === 'development' ? err.stack : undefined,
    timestamp: new Date().toISOString(),
  };
  
  if (err.response) {
    result.response = {
      status: (err.response as {status?: number}).status,
      data: (err.response as {data?: unknown}).data,
      headers: (err.response as {headers?: unknown}).headers,
    };
  }
  
  return result;
}

// Sanitize SDK responses to remove sensitive data
export function sanitizeResponse<T extends Record<string, unknown>>(
  data: T,
  sensitiveFields: string[] = ['apiKey', 'masterKey', 'password', 'secret']
): T {
  const sanitized = { ...data } as Record<string, unknown>;

  for (const field of sensitiveFields) {
    if (field in sanitized) {
      sanitized[field] = '[REDACTED]';
    }
  }

  // Recursively sanitize nested objects
  for (const [key, value] of Object.entries(sanitized)) {
    if (value && typeof value === 'object' && !Array.isArray(value)) {
      sanitized[key] = sanitizeResponse(value as Record<string, unknown>, sensitiveFields);
    }
  }

  return sanitized as T;
}