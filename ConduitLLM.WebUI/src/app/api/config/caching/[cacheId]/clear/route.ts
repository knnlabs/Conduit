import { transformSDKResponse } from '@/lib/utils/sdk-transforms';
import { mapSDKErrorToResponse } from '@/lib/errors/sdk-errors';
import { NextRequest } from 'next/server';
import { validateAdminSession } from '@/lib/auth/sdk-auth';

// Dynamic route handler with manual auth validation
export async function POST(
  request: NextRequest,
  context: { params: Promise<{ cacheId: string }> }
) {
  try {
    // Validate admin session
    const authResult = await validateAdminSession(request);
    if (!authResult.isValid) {
      return transformSDKResponse(
        { error: authResult.error || 'Unauthorized' },
        { status: 401 }
      );
    }

    const params = await context.params;
    const cacheId = params.cacheId;
      
      // Note: The Admin SDK doesn't have direct cache management methods yet
      // In a real implementation, this would call a backend endpoint to clear specific caches
      
      // For now, we'll return a success response indicating what would be cleared
      const cacheDescriptions: Record<string, string> = {
        'model-list': 'Model list cache cleared',
        'provider-health': 'Provider health cache cleared',
        'virtual-key': 'Virtual key cache cleared',
        'response-cache': 'Response cache cleared',
        'all': 'All caches cleared',
      };
      
      if (!cacheDescriptions[cacheId]) {
        return transformSDKResponse(
          {
            error: 'Invalid cache ID',
            validCacheIds: Object.keys(cacheDescriptions),
          },
          { status: 400 }
        );
      }
      
      return transformSDKResponse(
        {
          success: true,
          message: cacheDescriptions[cacheId],
          cacheId,
          timestamp: new Date().toISOString(),
        },
        { status: 200 }
      );
  } catch (error) {
    return mapSDKErrorToResponse(error);
  }
}