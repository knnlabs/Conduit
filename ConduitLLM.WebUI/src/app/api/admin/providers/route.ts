import { NextRequest } from 'next/server';
import { withSDKAuth } from '@/lib/auth/sdk-auth';
import { mapSDKErrorToResponse, withSDKErrorHandling } from '@/lib/errors/sdk-errors';
import { transformSDKResponse, transformPaginatedResponse, extractPagination } from '@/lib/utils/sdk-transforms';
import { parseQueryParams } from '@/lib/utils/route-helpers';

export const GET = withSDKAuth(
  async (request, { auth }) => {
    try {
      const params = parseQueryParams(request);
      
      // List all providers with optional filtering
      const result = await withSDKErrorHandling(
        async () => auth.adminClient!.providers.list({
          isEnabled: params.includeDisabled ? undefined : true,
          providerName: params.get('providerName') || undefined,
          pageNumber: params.page,
          pageSize: params.pageSize,
        }),
        'list providers'
      );

      // The SDK returns an array directly
      return transformPaginatedResponse(result, {
        page: params.page,
        pageSize: params.pageSize,
        total: result.length,
      });
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);

export const POST = withSDKAuth(
  async (request, { auth }) => {
    try {
      const body = await request.json();
      
      // Create provider configuration
      const result = await withSDKErrorHandling(
        async () => auth.adminClient!.providers.create({
          providerName: body.providerName,
          apiKey: body.apiKey,
          apiEndpoint: body.apiUrl || body.apiEndpoint,
          organizationId: body.organizationId,
          additionalConfig: body.additionalSettings ? JSON.stringify(body.additionalSettings) : body.additionalConfig,
          isEnabled: body.isEnabled ?? true,
        }),
        'create provider'
      );

      // Test connection if requested
      if (body.testConnection) {
        try {
          await withSDKErrorHandling(
            async () => auth.adminClient!.providers.testConnectionById(result.id),
            'test provider connection'
          );
        } catch (testError) {
          // Log test failure but still return created provider
          console.warn('Provider created but connection test failed:', testError);
        }
      }

      return transformSDKResponse(result, {
        status: 201,
        meta: {
          created: true,
          providerId: result.id,
          connectionTested: body.testConnection || false,
        }
      });
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);