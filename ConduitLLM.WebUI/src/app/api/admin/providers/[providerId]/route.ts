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
  async (request, { params: _params }) => {
    try {
      // No need to extract providerId as it's not used
      await request.json();
      
      // Provider metadata cannot be updated directly.
      // To manage provider settings, use provider credentials API instead.
      return NextResponse.json(
        { 
          error: 'Provider metadata cannot be updated directly',
          message: 'Use provider credentials API to manage provider configurations' 
        },
        { status: 400 }
      );
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);

export const DELETE = createDynamicRouteHandler<{ providerId: string }>(
  async (_request, { params: _params }) => {
    try {
      // No need to extract providerId as it's not used
      
      // Provider metadata cannot be deleted directly.
      // To remove provider access, use provider credentials API instead.
      return NextResponse.json(
        { 
          error: 'Provider metadata cannot be deleted directly',
          message: 'Use provider credentials API to remove provider configurations' 
        },
        { status: 400 }
      );
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);