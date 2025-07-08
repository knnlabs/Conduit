import { NextResponse } from 'next/server';
import { withSDKAuth } from '@/lib/auth/sdk-auth';
import { mapSDKErrorToResponse, withSDKErrorHandling } from '@/lib/errors/sdk-errors';
import { parseQueryParams, validateRequiredFields, createValidationError } from '@/lib/utils/route-helpers';

export const GET = withSDKAuth(
  async (request, context) => {
    try {
      const params = parseQueryParams(request);
      
      // List model mappings with filtering
      const result = await withSDKErrorHandling(
        async () => context.adminClient!.modelMappings.list({
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

      // Return the SDK response directly
      return NextResponse.json(result);
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);

export const POST = withSDKAuth(
  async (request, context) => {
    try {
      const body = await request.json();
      
      // Validate required fields
      const validation = validateRequiredFields(body, ['modelId', 'providerId', 'providerModelId']);
      if (!validation.isValid) {
        return createValidationError(
          'Missing required fields',
          { missingFields: validation.missingFields }
        );
      }
      
      // Create model mapping directly with the SDK DTO structure
      const result = await withSDKErrorHandling(
        async () => context.adminClient!.modelMappings.create(body),
        'create model mapping'
      );

      // Return the SDK response directly
      return NextResponse.json(result, { status: 201 });
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);