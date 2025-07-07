import { withSDKAuth } from '@/lib/auth/sdk-auth';
import { mapSDKErrorToResponse, withSDKErrorHandling } from '@/lib/errors/sdk-errors';
import { transformSDKResponse } from '@/lib/utils/sdk-transforms';

export const POST = withSDKAuth(
  async (request, context) => {
    try {
      const body = await request.json();
      
      // Test connection with provider configuration
      // This tests a configuration before saving it
      const result = await withSDKErrorHandling(
        async () => context.adminClient!.providers.testConnection({
          providerName: body.providerName, // Now this will be the provider type from the form
          apiKey: body.apiKey,
          apiEndpoint: body.apiEndpoint,
          organizationId: body.organizationId,
          additionalConfig: body.additionalConfig,
        }),
        'test provider connection'
      );

      return transformSDKResponse(result, {
        meta: {
          tested: true,
          timestamp: new Date().toISOString(),
        }
      });
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);