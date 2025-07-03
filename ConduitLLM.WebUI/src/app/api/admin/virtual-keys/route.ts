import { NextRequest, NextResponse } from 'next/server';
import { withSDKAuth } from '@/lib/auth/sdk-auth';
import { mapSDKErrorToResponse, withSDKErrorHandling } from '@/lib/errors/sdk-errors';

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

      // Return the SDK response directly
      return NextResponse.json(result);
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

      // Return the SDK response directly
      return NextResponse.json(result, { status: 201 });
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);