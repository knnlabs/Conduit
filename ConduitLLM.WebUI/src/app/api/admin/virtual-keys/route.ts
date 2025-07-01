import { NextRequest } from 'next/server';
import { withSDKAuth } from '@/lib/auth/sdk-auth';
import { mapSDKErrorToResponse, withSDKErrorHandling } from '@/lib/errors/sdk-errors';
import { transformSDKResponse, transformPaginatedResponse, extractPagination } from '@/lib/utils/sdk-transforms';

export const GET = withSDKAuth(
  async (request, { auth }) => {
    try {
      // Extract query parameters
      const url = new URL(request.url);
      const pageNumber = parseInt(url.searchParams.get('page') || '1');
      const pageSize = parseInt(url.searchParams.get('pageSize') || '20');
      const search = url.searchParams.get('search') || undefined;
      const includeDisabled = url.searchParams.get('includeDisabled') === 'true';
      const sortByField = url.searchParams.get('sortBy');
      const sortOrder = (url.searchParams.get('sortOrder') || 'desc') as 'asc' | 'desc';

      // Create sortBy object if sorting is requested
      const sortBy = sortByField ? {
        field: sortByField,
        direction: sortOrder
      } : undefined;

      // Use the admin client to list virtual keys
      const result = await withSDKErrorHandling(
        async () => auth.adminClient!.virtualKeys.list({
          pageNumber,
          pageSize,
          search,
          sortBy,
          // If includeDisabled is false, filter to only enabled keys
          isEnabled: includeDisabled ? undefined : true,
        }),
        'list virtual keys'
      );

      // The SDK returns an array directly for virtualKeys.list()
      // We need to check if it's a paginated response or just an array
      if (Array.isArray(result)) {
        // Simple array response - create our own pagination
        return transformSDKResponse({
          items: result,
          pagination: {
            page: pageNumber,
            pageSize: pageSize,
            total: result.length,
            totalPages: Math.ceil(result.length / pageSize)
          }
        });
      }

      // If it's already a paginated response, transform it
      return transformSDKResponse(result);
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);

export const POST = withSDKAuth(
  async (request, { auth }) => {
    try {
      // Parse and validate request body
      const body = await request.json();
      
      // Use the admin client to create virtual key
      const result = await withSDKErrorHandling(
        async () => auth.adminClient!.virtualKeys.create({
          keyName: body.keyName,
          allowedModels: body.allowedModels,
          maxBudget: body.maxBudget,
          budgetDuration: body.budgetDuration,
          expiresAt: body.expiresAt,
          metadata: body.metadata,
          rateLimitRpm: body.rateLimitRpm,
          rateLimitRpd: body.rateLimitRpd,
        }),
        'create virtual key'
      );

      // Transform the response
      return transformSDKResponse(result, { 
        status: 201,
        meta: {
          created: true,
          keyId: result.keyInfo?.id,
        }
      });
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);