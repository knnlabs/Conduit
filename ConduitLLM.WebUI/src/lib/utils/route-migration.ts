import { NextRequest, NextResponse } from 'next/server';
import { withSDKAuth, SDKAuthContext } from '@/lib/auth/sdk-auth';
import { mapSDKErrorToResponse, withSDKErrorHandling } from '@/lib/errors/unified-error-handler';
import { getServerAdminClient } from '@/lib/clients/server';

type TransformFunction<T> = (data: T) => unknown;

export interface MigrationOptions {
  requireAdmin?: boolean;
  requireCore?: boolean;
  requireVirtualKey?: boolean;
  errorContext?: string;
}

export function createLegacyToSDKMigration<TResponse>(
  legacyOperation: (client: ReturnType<typeof getServerAdminClient>) => Promise<TResponse>,
  options?: MigrationOptions
) {
  return withSDKAuth(
    async (_request: NextRequest, context: SDKAuthContext) => {
      try {
        if (!context.adminClient) {
          throw new Error('Admin client not available');
        }

        const result = await withSDKErrorHandling(
          async () => legacyOperation(context.adminClient!),
          options?.errorContext || 'legacy operation'
        );

        return NextResponse.json(result);
      } catch (error) {
        return mapSDKErrorToResponse(error);
      }
    },
    { requireAdmin: options?.requireAdmin ?? true }
  );
}

export function createCustomSDKRoute<TResponse>(
  operation: (
    client: ReturnType<typeof getServerAdminClient>, 
    request: NextRequest
  ) => Promise<TResponse>,
  responseTransform?: TransformFunction<TResponse>,
  options?: MigrationOptions
) {
  return withSDKAuth(
    async (request: NextRequest, context: SDKAuthContext) => {
      try {
        if (!context.adminClient) {
          throw new Error('Admin client not available');
        }

        const result = await withSDKErrorHandling(
          async () => operation(context.adminClient!, request),
          options?.errorContext || 'custom operation'
        );

        const transformed = responseTransform ? responseTransform(result) : result;
        return NextResponse.json(transformed);
      } catch (error) {
        return mapSDKErrorToResponse(error);
      }
    },
    {
      requireAdmin: options?.requireAdmin ?? true,
      requireCore: options?.requireCore,
      requireVirtualKey: options?.requireVirtualKey,
    }
  );
}

export function createSDKRouteWithParams<TParams, TResponse>(
  extractParams: (request: NextRequest) => TParams | Promise<TParams>,
  operation: (
    client: ReturnType<typeof getServerAdminClient>,
    params: TParams
  ) => Promise<TResponse>,
  options?: MigrationOptions & {
    statusCode?: number;
    responseTransform?: TransformFunction<TResponse>;
  }
) {
  return withSDKAuth(
    async (request: NextRequest, context: SDKAuthContext) => {
      try {
        if (!context.adminClient) {
          throw new Error('Admin client not available');
        }

        const params = await extractParams(request);
        
        const result = await withSDKErrorHandling(
          async () => operation(context.adminClient!, params),
          options?.errorContext || 'parameterized operation'
        );

        const transformed = options?.responseTransform 
          ? options.responseTransform(result) 
          : result;
          
        return NextResponse.json(transformed, { 
          status: options?.statusCode || 200 
        });
      } catch (error) {
        return mapSDKErrorToResponse(error);
      }
    },
    {
      requireAdmin: options?.requireAdmin ?? true,
      requireCore: options?.requireCore,
      requireVirtualKey: options?.requireVirtualKey,
    }
  );
}

export function createDirectAPIToSDKMigration<TResponse>(
  sdkOperation: (client: ReturnType<typeof getServerAdminClient>) => Promise<TResponse>,
  options?: MigrationOptions & {
    fallbackResponse?: TResponse;
    handleNotFound?: boolean;
  }
) {
  return withSDKAuth(
    async (_request: NextRequest, context: SDKAuthContext) => {
      try {
        if (!context.adminClient) {
          throw new Error('Admin client not available');
        }

        const result = await withSDKErrorHandling(
          async () => sdkOperation(context.adminClient!),
          options?.errorContext || 'direct API migration'
        );

        return NextResponse.json(result);
      } catch (error: unknown) {
        // Handle 404 errors with fallback if specified
        if (
          options?.handleNotFound && 
          error && 
          typeof error === 'object' && 
          'statusCode' in error &&
          error.statusCode === 404 &&
          options.fallbackResponse !== undefined
        ) {
          return NextResponse.json(options.fallbackResponse);
        }

        return mapSDKErrorToResponse(error);
      }
    },
    {
      requireAdmin: options?.requireAdmin ?? true,
      requireCore: options?.requireCore,
      requireVirtualKey: options?.requireVirtualKey,
    }
  );
}

export function standardizeResponse<T>(data: T): T {
  return data;
}

export { mapSDKErrorToResponse, withSDKErrorHandling } from '@/lib/errors/sdk-errors';