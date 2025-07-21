import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';

interface RoutingTestRequest {
  provider?: string;
  model?: string;
  request?: unknown;
}

// POST /api/config/routing/test - Test routing rules
export async function POST(req: NextRequest) {

  try {
    const testRequest = await req.json() as RoutingTestRequest;
    
    // SDK doesn't support routing test yet, return a simulated result
    console.warn('Routing test not supported by SDK, returning simulated result');
    
    const mockResult = {
      success: true,
      selectedProvider: testRequest.provider ?? 'openai-1',
      routingDecision: {
        strategy: 'priority',
        reason: 'Default priority-based routing',
        processingTimeMs: Math.floor(Math.random() * 20) + 5,
        fallbackUsed: false
      },
      matchedRules: [],
      errors: [],
      warningMessage: 'Test result simulated (SDK support pending)'
    };
    
    return NextResponse.json(mockResult);
  } catch (error) {
    console.error('Error testing routing:', error);
    return handleSDKError(error);
  }
}