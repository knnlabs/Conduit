import { NextResponse } from 'next/server';
import { mapSDKErrorToResponse, withSDKErrorHandling } from '@/lib/errors/sdk-errors';
import { createDynamicRouteHandler } from '@/lib/utils/route-helpers';

export const PUT = createDynamicRouteHandler<{ providerId: string }>(
  async (request, { params, adminClient }) => {
    try {
      const { providerId } = params;

      const body = await request.json();
      
      // Update audio provider
      const result = await withSDKErrorHandling(
        async () => adminClient!.audioConfiguration.updateProvider(providerId, {
          name: body.name,
          baseUrl: body.baseUrl || body.endpoint,
          apiKey: body.apiKey,
          isEnabled: body.isEnabled,
          supportedOperations: body.supportedOperations,
          priority: body.priority,
          timeoutSeconds: body.timeoutSeconds,
          settings: body.settings,
        }),
        'update audio provider'
      );

      return NextResponse.json(result);
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);

export const DELETE = createDynamicRouteHandler<{ providerId: string }>(
  async (request, { params, adminClient }) => {
    try {
      const { providerId } = params;
      
      // Delete audio provider
      await withSDKErrorHandling(
        async () => adminClient!.audioConfiguration.deleteProvider(providerId),
        'delete audio provider'
      );

      return NextResponse.json({ success: true });
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);