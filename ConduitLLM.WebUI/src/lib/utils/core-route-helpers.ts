import { NextRequest, NextResponse } from 'next/server';
import { validateCoreSession, extractVirtualKey } from '@/lib/auth/sdk-auth';
import { mapSDKErrorToResponse } from '@/lib/errors/sdk-errors';
import { transformSDKResponse } from '@/lib/utils/sdk-transforms';
import { createValidationError as createValidationErrorResponse } from '@/lib/utils/route-helpers';
import { generateRequestId } from '@/lib/utils/logging';

export interface CoreRouteOptions {
  requireVirtualKey?: boolean;
  validateBody?: (body: unknown) => ValidationResult;
  validateFormData?: (formData: FormData) => Promise<ValidationResult>;
  logContext?: string;
  parseAsFormData?: boolean;
}

export interface ValidationResult {
  isValid: boolean;
  error?: string;
  details?: Record<string, unknown>;
}

export interface CoreRouteContext {
  request: NextRequest;
  virtualKey: string;
  session: NonNullable<Awaited<ReturnType<typeof validateCoreSession>>['session']>;
  user?: unknown;
  requestId: string;
}

type CoreRouteHandler<TBody = unknown, TResponse = unknown> = (
  context: CoreRouteContext,
  body: TBody
) => Promise<NextResponse | TResponse>;

type CoreFormDataRouteHandler<TResponse = unknown> = (
  context: CoreRouteContext,
  formData: FormData
) => Promise<NextResponse | TResponse>;

export function createCoreRoute<TBody = unknown>(
  options: CoreRouteOptions & { parseAsFormData?: false },
  handler: CoreRouteHandler<TBody>
): (request: NextRequest) => Promise<NextResponse>;

export function createCoreRoute(
  options: CoreRouteOptions & { parseAsFormData: true },
  handler: CoreFormDataRouteHandler
): (request: NextRequest) => Promise<NextResponse>;

export function createCoreRoute<TBody = unknown>(
  options: CoreRouteOptions,
  handler: CoreRouteHandler<TBody> | CoreFormDataRouteHandler
) {
  return async function(request: NextRequest): Promise<NextResponse> {
    const requestId = generateRequestId();
    const startTime = performance.now();
    
    try {
      const validation = await validateCoreSession(request, { 
        requireVirtualKey: options.requireVirtualKey ?? false 
      });
      
      if (!validation.isValid) {
        logSecurityEvent('session_validation_failed', { 
          requestId, 
          error: validation.error,
          context: options.logContext 
        });
        return createAuthenticationErrorResponse(validation.error || undefined);
      }

      let body: TBody | FormData;
      let virtualKey: string | null;

      if (options.parseAsFormData) {
        const formData = await request.formData();
        body = formData;

        if (options.validateFormData) {
          const formValidation = await options.validateFormData(formData);
          if (!formValidation.isValid) {
            logRequestError('form_validation_failed', { 
              requestId, 
              error: formValidation.error,
              context: options.logContext
            });
            return createValidationError(formValidation.error || 'Validation failed', formValidation.details);
          }
        }

        virtualKey = formData.get('virtual_key') as string || 
                    extractVirtualKey(request) || 
                    validation.session?.virtualKey || 
                    null;
      } else {
        try {
          body = await request.json() as TBody;
        } catch (parseError) {
          logRequestError('body_parse_failed', { requestId, parseError });
          return createValidationError('Invalid JSON in request body');
        }

        if (options.validateBody) {
          const bodyValidation = options.validateBody(body);
          if (!bodyValidation.isValid) {
            logRequestError('body_validation_failed', { 
              requestId, 
              error: bodyValidation.error,
              body: sanitizeBodyForLogging(body)
            });
            return createValidationError(bodyValidation.error || 'Validation failed', bodyValidation.details);
          }
        }

        virtualKey = extractVirtualKeyFromSources(body, request, validation.session);
      }
      
      if (!virtualKey) {
        logSecurityEvent('virtual_key_missing', { requestId, context: options.logContext });
        return createValidationError(
          'Virtual key is required. Provide it via virtual_key field, x-virtual-key header, or Authorization header',
          { missingField: 'virtual_key' }
        );
      }

      const context: CoreRouteContext = {
        request,
        virtualKey,
        session: validation.session!,
        user: undefined, // user is not available in SDKAuthResult
        requestId
      };

      const result = await handler(context, body as TBody & FormData);
      
      const response = result instanceof NextResponse ? result : transformSDKResponse(result);
      
      const endTime = performance.now();
      logRequestSuccess(options.logContext || 'core_route', {
        requestId,
        duration: endTime - startTime,
        statusCode: response.status
      });

      return response;

    } catch (error) {
      const endTime = performance.now();
      
      logRequestError(options.logContext || 'core_route', {
        requestId,
        duration: endTime - startTime,
        error: error instanceof Error ? error.message : String(error),
        stack: error instanceof Error ? error.stack : undefined
      });

      return mapSDKErrorToResponse(error);
    }
  };
}

function extractVirtualKeyFromSources(
  body: unknown, 
  request: NextRequest, 
  session: unknown
): string | null {
  if (body && typeof body === 'object' && 'virtual_key' in body) {
    return (body as { virtual_key?: string }).virtual_key || null;
  }
  
  const headerKey = extractVirtualKey(request);
  if (headerKey) {
    return headerKey;
  }
  
  if (session && typeof session === 'object' && 'virtualKey' in session) {
    return (session as { virtualKey?: string }).virtualKey || null;
  }
  
  return null;
}

function createAuthenticationErrorResponse(error?: string): NextResponse {
  return NextResponse.json(
    { 
      error: error || 'Authentication required',
      code: 'AUTHENTICATION_FAILED',
      timestamp: new Date().toISOString()
    },
    { 
      status: 401,
      headers: { 
        'WWW-Authenticate': 'Bearer'
      }
    }
  );
}

function createValidationError(
  message: string, 
  details?: Record<string, unknown>
): NextResponse {
  return NextResponse.json(
    { 
      error: message,
      code: 'VALIDATION_FAILED',
      details,
      timestamp: new Date().toISOString()
    },
    { 
      status: 400
    }
  );
}

function logSecurityEvent(event: string, data: Record<string, unknown>): void {
  console.warn(`[SECURITY] ${event}:`, data);
}

function logRequestError(context: string, data: Record<string, unknown>): void {
  console.error(`[ERROR] ${context}:`, data);
}

function logRequestSuccess(context: string, data: Record<string, unknown>): void {
  console.log(`[SUCCESS] ${context}:`, data);
}

function sanitizeBodyForLogging(body: unknown): unknown {
  if (!body || typeof body !== 'object') return body;
  
  const sanitized = { ...body as Record<string, unknown> };
  const sensitiveFields = ['password', 'token', 'secret', 'api_key', 'virtual_key'];
  
  for (const field of sensitiveFields) {
    if (field in sanitized) {
      sanitized[field] = '[REDACTED]';
    }
  }
  
  return sanitized;
}