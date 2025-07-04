import { NextResponse } from 'next/server';
import { mapSDKErrorToResponse, withSDKErrorHandling } from '@/lib/errors/sdk-errors';
import { createDynamicRouteHandler } from '@/lib/utils/route-helpers';

export const GET = createDynamicRouteHandler<{ id: string }>(
  async (request, { params, auth }) => {
    try {
      const { id } = params;
      
      // Get virtual key details including usage stats
      const result = await withSDKErrorHandling(
        async () => auth.adminClient!.virtualKeys.get(id, {
          includeUsageStats: true,
          includeSpendHistory: false,
        }),
        `get virtual key ${id}`
      );

      // Return the SDK response directly
      return NextResponse.json(result);
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);

export const PUT = createDynamicRouteHandler<{ id: string }>(
  async (request, { params, auth }) => {
    try {
      const { id } = params;
      const body = await request.json();
      
      // Update virtual key using SDK
      const result = await withSDKErrorHandling(
        async () => auth.adminClient!.virtualKeys.update(id, {
          keyName: body.keyName,
          description: body.description,
          allowedModels: body.allowedModels,
          maxBudget: body.maxBudget,
          budgetDuration: body.budgetDuration,
          rateLimits: body.rateLimits,
          ipWhitelist: body.ipWhitelist,
          metadata: body.metadata,
          isEnabled: body.isEnabled,
          expiresAt: body.expiresAt,
        }),
        `update virtual key ${id}`
      );

      return NextResponse.json(result);
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);

export const DELETE = createDynamicRouteHandler<{ id: string }>(
  async (request, { params, auth }) => {
    try {
      const { id } = params;
      
      // Delete virtual key using SDK
      await withSDKErrorHandling(
        async () => auth.adminClient!.virtualKeys.delete(id),
        `delete virtual key ${id}`
      );

      return NextResponse.json(
        { success: true, message: 'Virtual key deleted successfully' },
        { status: 200 }
      );
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);