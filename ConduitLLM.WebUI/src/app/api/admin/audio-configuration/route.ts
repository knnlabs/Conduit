import { NextResponse } from 'next/server';
import { withSDKAuth } from '@/lib/auth/sdk-auth';
import { mapSDKErrorToResponse, withSDKErrorHandling } from '@/lib/errors/sdk-errors';

export const GET = withSDKAuth(
  async (request, context) => {
    try {
      // Get audio providers
      const result = await withSDKErrorHandling(
        async () => context.adminClient!.audioConfiguration.getProviders(),
        'list audio providers'
      );

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
      
      // Create audio provider
      const result = await withSDKErrorHandling(
        async () => context.adminClient!.audioConfiguration.createProvider({
          name: body.name,
          baseUrl: body.baseUrl || body.endpoint,
          apiKey: body.apiKey,
          isEnabled: body.isEnabled ?? true,
          supportedOperations: body.supportedOperations || [],
          priority: body.priority || 1,
          timeoutSeconds: body.timeoutSeconds || 30,
          settings: body.settings || {},
        }),
        'create audio provider'
      );

      return NextResponse.json(result, { status: 201 });
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);