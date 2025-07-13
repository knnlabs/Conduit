import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { requireAuth } from '@/lib/auth/simple-auth';

// POST /api/config/routing/test - Test routing rules
export async function POST(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const adminClient = getServerAdminClient();
    const testRequest = await req.json();
    
    // SDK doesn't support routing test yet, return a simulated result
    console.warn('Routing test not supported by SDK, returning simulated result');
    
    const mockResult = {
      success: true,
      selectedProvider: testRequest.provider || 'openai-1',
      routingDecision: {
        strategy: 'priority',
        reason: 'Default priority-based routing',
        processingTimeMs: Math.floor(Math.random() * 20) + 5,
        fallbackUsed: false
      },
      matchedRules: [],
      errors: [],
      _warning: 'Test result simulated (SDK support pending)'
    };
    
    return NextResponse.json(mockResult);
  } catch (error) {
    console.error('Error testing routing:', error);
    return handleSDKError(error);
  }
}