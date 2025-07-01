import { NextRequest } from 'next/server';
import { withSDKAuth } from '@/lib/auth/sdk-auth';
import { mapSDKErrorToResponse, withSDKErrorHandling } from '@/lib/errors/sdk-errors';
import { transformSDKResponse, transformPaginatedResponse, extractPagination } from '@/lib/utils/sdk-transforms';
import { parseQueryParams, validateRequiredFields, createValidationError } from '@/lib/utils/route-helpers';

export const GET = withSDKAuth(
  async (request, { auth }) => {
    try {
      const params = parseQueryParams(request);
      
      // List model mappings with filtering
      const result = await withSDKErrorHandling(
        async () => auth.adminClient!.modelMappings.list({
          pageNumber: params.page,
          pageSize: params.pageSize,
          modelId: params.get('modelName') || undefined,
          providerId: params.get('providerName') || undefined,
          isEnabled: params.includeDisabled ? undefined : true,
          sortBy: params.sortBy && params.sortOrder ? {
            field: params.sortBy,
            direction: params.sortOrder as 'asc' | 'desc'
          } : undefined,
        }),
        'list model mappings'
      );

      // The SDK returns an array directly, so we need to create our own pagination
      return transformPaginatedResponse(result, {
        page: params.page,
        pageSize: params.pageSize,
        total: result.length, // This is not ideal as it only shows current page count
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
      
      // Validate required fields
      const validation = validateRequiredFields(body, ['modelName', 'providerId']);
      if (!validation.isValid) {
        return createValidationError(
          'Missing required fields',
          { missingFields: validation.missingFields }
        );
      }
      
      // Create model mapping
      const result = await withSDKErrorHandling(
        async () => auth.adminClient!.modelMappings.create({
          modelId: body.modelName,
          providerId: body.providerId,
          providerModelId: body.providerModelName || body.modelName,
          priority: body.priority ?? 100,
          isEnabled: body.isEnabled ?? true,
          metadata: body.metadata ? JSON.stringify(body.metadata) : undefined,
        }),
        'create model mapping'
      );

      return transformSDKResponse(result, {
        status: 201,
        meta: {
          created: true,
          mappingId: result.id,
        }
      });
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);