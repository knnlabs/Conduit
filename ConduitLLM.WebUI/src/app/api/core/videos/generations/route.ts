
import { NextRequest } from 'next/server';
import { validateCoreSession, extractVirtualKey } from '@/lib/auth/sdk-auth';
import { mapSDKErrorToResponse, withSDKErrorHandling } from '@/lib/errors/sdk-errors';
import { transformSDKResponse } from '@/lib/utils/sdk-transforms';
import { getServerCoreClient } from '@/lib/clients/server';
import { createValidationError, parseQueryParams } from '@/lib/utils/route-helpers';

export async function POST(request: NextRequest) {
  try {
    // Validate session - don't require virtual key in session yet
    const validation = await validateCoreSession(request, { requireVirtualKey: false });
    if (!validation.isValid) {
      return new Response(
        JSON.stringify({ error: validation.error || 'Unauthorized' }),
        { status: 401, headers: { 'Content-Type': 'application/json' } }
      );
    }

    // Parse request body
    const body = await request.json();
    
    // Extract virtual key from various sources
    const virtualKey = body.virtual_key || 
                      extractVirtualKey(request) || 
                      validation.session?.virtualKey;
    
    if (!virtualKey) {
      return createValidationError(
        'Virtual key is required. Provide it via virtual_key field, x-virtual-key header, or Authorization header',
        { missingField: 'virtual_key' }
      );
    }

    // Remove virtual_key from body before sending to API
    const { virtual_key: _virtualKey, ...videoRequest } = body;
    
    // Validate required fields
    if (!videoRequest.prompt) {
      return createValidationError(
        'Prompt is required',
        { missingField: 'prompt' }
      );
    }

    // Use SDK video generation (async only)
    const client = getServerCoreClient(virtualKey);
    
    const result = await withSDKErrorHandling(
      async () => (client as { videos: { generateAsync: (params: unknown) => Promise<{ task_id: string }> } }).videos.generateAsync({
        prompt: videoRequest.prompt,
        model: videoRequest.model,
        duration: videoRequest.duration,
        size: videoRequest.size,
        fps: videoRequest.fps,
        style: videoRequest.style,
        response_format: videoRequest.response_format,
        user: videoRequest.user,
        seed: videoRequest.seed,
        n: videoRequest.n,
        webhook_url: videoRequest.webhook_url,
        webhook_metadata: videoRequest.webhook_metadata,
        webhook_headers: videoRequest.webhook_headers,
        timeout_seconds: videoRequest.timeout_seconds,
      }),
      'generate video'
    );

    return transformSDKResponse(result, {
      meta: {
        virtualKeyUsed: virtualKey.substring(0, 8) + '...',
        model: videoRequest.model,
        taskId: result.task_id,
      }
    });
  } catch (error: unknown) {
    // Handle validation errors specially
    if ((error as { message?: string })?.message?.includes('required')) {
      return createValidationError((error as { message?: string })?.message || 'Validation error');
    }
    
    return mapSDKErrorToResponse(error);
  }
}

export async function GET(request: NextRequest) {
  try {
    // Validate session - don't require virtual key in session yet
    const validation = await validateCoreSession(request, { requireVirtualKey: false });
    if (!validation.isValid) {
      return new Response(
        JSON.stringify({ error: validation.error || 'Unauthorized' }),
        { status: 401, headers: { 'Content-Type': 'application/json' } }
      );
    }

    // Parse query parameters
    const params = parseQueryParams(request);
    const taskId = params.get('task_id');
    
    // Extract virtual key from various sources
    const virtualKey = params.get('virtual_key') || 
                      extractVirtualKey(request) || 
                      validation.session?.virtualKey;
    
    if (!virtualKey) {
      return createValidationError(
        'Virtual key is required. Provide it via virtual_key parameter, x-virtual-key header, or Authorization header',
        { missingField: 'virtual_key' }
      );
    }

    // Use SDK for video task operations
    const client = getServerCoreClient(virtualKey);
    
    if (taskId) {
      // Get specific video generation task status
      const result = await withSDKErrorHandling(
        async () => (client as { videos: { getTaskStatus: (taskId: string) => Promise<unknown> } }).videos.getTaskStatus(taskId),
        `get video generation task ${taskId}`
      );

      return transformSDKResponse(result);
    } else {
      // Video listing is not supported by the API - return empty list
      const result = await withSDKErrorHandling(
        async () => {
          // The API doesn't support listing video generations
          return {
            items: [],
            total: 0,
            page: params.page,
            pageSize: params.pageSize,
            message: 'Video generation history is not available. Check specific task IDs for status.',
          };
        },
        'list video generations'
      );

      return transformSDKResponse(result);
    }
  } catch (error: unknown) {
    return mapSDKErrorToResponse(error);
  }
}

// Support for DELETE to cancel video generation
export async function DELETE(request: NextRequest) {
  try {
    // Validate session
    const validation = await validateCoreSession(request, { requireVirtualKey: false });
    if (!validation.isValid) {
      return new Response(
        JSON.stringify({ error: validation.error || 'Unauthorized' }),
        { status: 401, headers: { 'Content-Type': 'application/json' } }
      );
    }

    // Parse query parameters
    const params = parseQueryParams(request);
    const taskId = params.get('task_id');
    
    if (!taskId) {
      return createValidationError(
        'Task ID is required',
        { missingField: 'task_id' }
      );
    }
    
    // Extract virtual key
    const virtualKey = params.get('virtual_key') || 
                      extractVirtualKey(request) || 
                      validation.session?.virtualKey;
    
    if (!virtualKey) {
      return createValidationError(
        'Virtual key is required',
        { missingField: 'virtual_key' }
      );
    }

    // Use SDK for video task cancellation
    const client = getServerCoreClient(virtualKey);
    
    const result = await withSDKErrorHandling(
      async () => {
        await (client as { videos: { cancelTask: (taskId: string) => Promise<void> } }).videos.cancelTask(taskId);
        return {
          success: true,
          message: `Task ${taskId} cancelled successfully.`,
        };
      },
      `cancel video generation task ${taskId}`
    );

    return transformSDKResponse(result, {
      meta: {
        cancelled: true,
        taskId,
      }
    });
  } catch (error: unknown) {
    return mapSDKErrorToResponse(error);
  }
}