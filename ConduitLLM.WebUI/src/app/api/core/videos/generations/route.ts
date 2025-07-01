import { NextRequest } from 'next/server';
import { validateCoreSession, extractVirtualKey } from '@/lib/auth/sdk-auth';
import { mapSDKErrorToResponse, withSDKErrorHandling } from '@/lib/errors/sdk-errors';
import { transformSDKResponse } from '@/lib/utils/sdk-transforms';
// Video API not yet supported in SDK, using stub implementation
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
    const { virtual_key, ...videoRequest } = body;
    
    // Validate required fields
    if (!videoRequest.prompt) {
      return createValidationError(
        'Prompt is required',
        { missingField: 'prompt' }
      );
    }

    // TODO: SDK does not yet support video generation
    // Stub implementation until SDK adds videos.generate() method
    const result = await withSDKErrorHandling(
      async () => {
        // Generate a fake task ID
        const taskId = `task_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
        
        return {
          task_id: taskId,
          status: 'queued',
          created_at: new Date().toISOString(),
          estimated_time: 120, // 2 minutes
          message: 'Video generation queued. Note: This is a stub - SDK does not yet support video generation.',
        };
      },
      'generate video'
    );

    return transformSDKResponse(result, {
      meta: {
        virtualKeyUsed: virtualKey.substring(0, 8) + '...',
        model: videoRequest.model,
        taskId: result.task_id,
      }
    });
  } catch (error: any) {
    // Handle validation errors specially
    if (error.message?.includes('required')) {
      return createValidationError(error.message);
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

    // TODO: SDK does not yet support video task status checking
    // Stub implementation until SDK adds videos.getTask() and videos.list() methods
    
    if (taskId) {
      // Get specific video generation task status
      const result = await withSDKErrorHandling(
        async () => {
          // Stub response for task status
          return {
            task_id: taskId,
            status: 'processing',
            progress: 50,
            created_at: new Date(Date.now() - 60000).toISOString(), // 1 minute ago
            updated_at: new Date().toISOString(),
            estimated_time_remaining: 60,
            message: 'Processing video... Note: This is a stub - SDK does not yet support video status checking.',
          };
        },
        `get video generation task ${taskId}`
      );

      return transformSDKResponse(result);
    } else {
      // Get video generation history or list
      const result = await withSDKErrorHandling(
        async () => {
          // Stub response for video list
          return {
            items: [],
            total: 0,
            page: params.page,
            pageSize: params.pageSize,
            message: 'No videos found. Note: This is a stub - SDK does not yet support video listing.',
          };
        },
        'list video generations'
      );

      return transformSDKResponse(result);
    }
  } catch (error: any) {
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

    // TODO: SDK does not yet support video task cancellation
    // Stub implementation until SDK adds videos.cancelTask() method
    const result = await withSDKErrorHandling(
      async () => {
        // Stub response for task cancellation
        return {
          success: true,
          message: `Task ${taskId} cancelled successfully. Note: This is a stub - SDK does not yet support video cancellation.`,
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
  } catch (error: any) {
    return mapSDKErrorToResponse(error);
  }
}