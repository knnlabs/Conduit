import { NextResponse } from 'next/server';
import { mapSDKErrorToResponse, withSDKErrorHandling } from '@/lib/errors/sdk-errors';
import { createDynamicRouteHandler } from '@/lib/utils/route-helpers';

export const GET = createDynamicRouteHandler<{ providerId: string }>(
  async (request, { params, adminClient }) => {
    try {
      const { providerId } = params;
      
      // Get all providers and find the specific one
      const providers = await withSDKErrorHandling(
        async () => adminClient!.providers.list(),
        'list providers to find specific provider'
      );
      
      // Find the specific provider by name (providerId is actually the provider name)
      const provider = Array.from(providers).find(p => p.providerName === providerId);
      
      if (!provider) {
        return NextResponse.json(
          { error: `Provider ${providerId} not found` },
          { status: 404 }
        );
      }
      
      const result = provider;

      return NextResponse.json(result);
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);

export const PUT = createDynamicRouteHandler<{ providerId: string }>(
  async (request, { params, adminClient }) => {
    try {
      const { providerId } = params;
      const body = await request.json();
      
      // Convert string ID to number for the SDK
      const numericId = parseInt(providerId, 10);
      if (isNaN(numericId)) {
        throw new Error('Invalid provider ID: must be a number');
      }
      
      // Update provider using the admin client
      await withSDKErrorHandling(
        async () => adminClient!.providers.update(numericId, body),
        'update provider'
      );

      return NextResponse.json({ success: true });
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);

export const DELETE = createDynamicRouteHandler<{ providerId: string }>(
  async (_request, { params, adminClient }) => {
    try {
      const { providerId } = params;
      
      // Delete provider using the admin client
      await withSDKErrorHandling(
        async () => adminClient!.providers.deleteById(parseInt(providerId)),
        'delete provider'
      );

      return NextResponse.json({ success: true });
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);