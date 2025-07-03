import { NextRequest } from 'next/server';
import { withSDKAuth } from '@/lib/auth/sdk-auth';
import { mapSDKErrorToResponse, withSDKErrorHandling } from '@/lib/errors/sdk-errors';
import { transformSDKResponse, transformPaginatedResponse, extractPagination } from '@/lib/utils/sdk-transforms';
import { parseQueryParams } from '@/lib/utils/route-helpers';

export const GET = withSDKAuth(
  async (request, { auth }) => {
    try {
      const params = parseQueryParams(request);
      
      // List all provider metadata
      const result = await withSDKErrorHandling(
        async () => auth.adminClient!.providers.list(),
        'list providers'
      );

      // Convert to array and apply filters
      const resultArray = Array.from(result);
      const filteredResult = resultArray.filter(provider => {
        if (params.get('providerName')) {
          return provider.providerName.toLowerCase().includes(params.get('providerName')!.toLowerCase());
        }
        return true;
      });

      return transformPaginatedResponse(filteredResult, {
        page: params.page,
        pageSize: params.pageSize,
        total: filteredResult.length,
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
      
      // Create provider credential (not provider metadata)
      const result = await withSDKErrorHandling(
        async () => auth.adminClient!.providers.create({
          providerName: body.providerName,
          apiKey: body.apiKey,
          apiEndpoint: body.apiUrl || body.apiEndpoint,
          organizationId: body.organizationId,
          additionalConfig: body.additionalSettings ? JSON.stringify(body.additionalSettings) : body.additionalConfig,
          isEnabled: body.isEnabled ?? true,
        }),
        'create provider credential'
      );

      // Test connection if requested
      if (body.testConnection) {
        try {
          await withSDKErrorHandling(
            async () => auth.adminClient!.providers.testConnection({
              providerName: body.providerName,
              apiKey: body.apiKey,
              apiEndpoint: body.apiUrl || body.apiEndpoint,
              organizationId: body.organizationId,
              additionalConfig: body.additionalSettings ? JSON.stringify(body.additionalSettings) : body.additionalConfig,
            }),
            'test provider connection'
          );
        } catch (testError) {
          // Log test failure but still return created provider
          console.warn('Provider credential created but connection test failed:', testError);
        }
      }

      return transformSDKResponse(result, {
        status: 201,
        meta: {
          created: true,
          credentialId: result.id,
          connectionTested: body.testConnection || false,
        }
      });
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);