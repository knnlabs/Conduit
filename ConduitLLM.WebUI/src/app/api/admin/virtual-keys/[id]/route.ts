import { NextResponse } from 'next/server';
import { mapSDKErrorToResponse, withSDKErrorHandling } from '@/lib/errors/sdk-errors';
import { createDynamicRouteHandler } from '@/lib/utils/route-helpers';

export const GET = createDynamicRouteHandler<{ id: string }>(
  async (request, { params, adminClient }) => {
    try {
      const { id } = params;
      
      // Get virtual key details
      const result = await withSDKErrorHandling(
        async () => adminClient!.virtualKeys.getById(Number(id)),
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
  async (request, { params, adminClient }) => {
    try {
      const { id } = params;
      const body = await request.json();
      
      // Update virtual key using SDK - only include fields that can be updated
      const updateData: Record<string, unknown> = {};
      
      if (body.keyName !== undefined) updateData.keyName = body.keyName;
      if (body.allowedModels !== undefined) updateData.allowedModels = body.allowedModels;
      if (body.maxBudget !== undefined) updateData.maxBudget = body.maxBudget;
      if (body.budgetDuration !== undefined) updateData.budgetDuration = body.budgetDuration;
      if (body.rateLimits !== undefined) updateData.rateLimits = body.rateLimits;
      if (body.ipWhitelist !== undefined) updateData.ipWhitelist = body.ipWhitelist;
      if (body.metadata !== undefined) updateData.metadata = body.metadata;
      if (body.isEnabled !== undefined) updateData.isEnabled = body.isEnabled;
      if (body.expiresAt !== undefined) updateData.expiresAt = body.expiresAt;
      
      const result = await withSDKErrorHandling(
        async () => adminClient!.virtualKeys.update(Number(id), updateData),
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
  async (request, { params, adminClient }) => {
    try {
      const { id } = params;
      
      // Delete virtual key using SDK
      await withSDKErrorHandling(
        async () => adminClient!.virtualKeys.deleteById(Number(id)),
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