import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerCoreClient } from '@/lib/server/sdk-config';
import type { AsyncVideoGenerationRequest } from '@/app/videos/types';

// POST /api/videos/generate - Generate videos using Core SDK
export async function POST(request: NextRequest) {
  try {
    const body = await request.json() as AsyncVideoGenerationRequest & { useProgressTracking?: boolean };
    const coreClient = await getServerCoreClient();
    
    // Check if client wants to use the new progress tracking method
    // Note: Server-side SDK doesn't have SignalR, so we still use generateAsync
    // The client will need to establish its own SignalR connection for real-time updates
    const result = await coreClient.videos.generateAsync(body);
    
    // Log the actual response structure for debugging
    console.warn('Video generation API response:', JSON.stringify(result, null, 2));
    
    // Define proper types for both PascalCase (from Core API) and snake_case (expected by SDK)
    interface CoreApiResponse {
      TaskId?: string;
      taskId?: string;  // camelCase variant
      Status?: string;
      status?: string;
      CreatedAt?: string;
      createdAt?: string;  // camelCase variant
      EstimatedCompletionTime?: number;
      estimatedCompletionTime?: number;  // camelCase variant
      // Snake case variants that SDK might return
      task_id?: string;
      created_at?: string;
      estimated_time_to_completion?: number;
      message?: string;
      checkStatusUrl?: string;
    }
    
    // Cast to our known response type
    const typedResult = result as unknown as CoreApiResponse;
    
    // The Core API returns various casing formats depending on the SDK version
    // We need to handle all cases: PascalCase, camelCase, and snake_case
    const taskId = typedResult.task_id ?? typedResult.taskId ?? typedResult.TaskId;
    const status = typedResult.status ?? typedResult.Status ?? 'pending';
    const createdAt = typedResult.created_at ?? typedResult.createdAt ?? typedResult.CreatedAt;
    
    if (!taskId) {
      console.error('Invalid response from Core API, missing task_id:', result);
      throw new Error('Invalid response from video generation API - no task ID');
    }
    
    // Return a normalized response with snake_case fields
    return NextResponse.json({
      task_id: taskId,
      status: status,
      created_at: createdAt,
      message: typedResult.message ?? 'Video generation started',
      estimated_time_to_completion: typedResult.estimated_time_to_completion ?? typedResult.estimatedCompletionTime ?? typedResult.EstimatedCompletionTime,
      // SignalR token removed - clients will use ephemeral keys
      // Add a flag to indicate that client-side progress tracking is available
      supportsProgressTracking: body.useProgressTracking ?? false
    });
  } catch (error) {
    return handleSDKError(error);
  }
}
