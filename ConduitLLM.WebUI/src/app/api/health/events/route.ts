import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { requireAuth } from '@/lib/auth/simple-auth';
import { getServerAdminClient } from '@/lib/server/adminClient';

// TODO: Remove this mock data when SDK provides health event endpoints
// SDK methods needed:
// - adminClient.system.getHealthEvents(limit?: number) - for retrieving health events
// - adminClient.system.subscribeToHealthEvents() - for real-time health event updates
const mockEvents = [
  {
    id: '1',
    timestamp: new Date(Date.now() - 300000).toISOString(),
    type: 'provider_up' as const,
    message: 'OpenAI provider recovered',
    severity: 'info' as const,
  },
  {
    id: '2',
    timestamp: new Date(Date.now() - 600000).toISOString(),
    type: 'provider_down' as const,
    message: 'OpenAI provider connection failed',
    severity: 'error' as const,
  },
  {
    id: '3',
    timestamp: new Date(Date.now() - 900000).toISOString(),
    type: 'system_issue' as const,
    message: 'High memory usage detected (85%)',
    severity: 'warning' as const,
  },
  {
    id: '4',
    timestamp: new Date(Date.now() - 1200000).toISOString(),
    type: 'system_recovered' as const,
    message: 'Database connection pool restored',
    severity: 'info' as const,
  },
  {
    id: '5',
    timestamp: new Date(Date.now() - 1800000).toISOString(),
    type: 'provider_down' as const,
    message: 'Anthropic provider timeout',
    severity: 'warning' as const,
  },
];

// Generate mock event with warning
function generateMockEvents(limit: number) {
  return {
    events: mockEvents.slice(0, Math.min(limit, mockEvents.length)),
    _warning: 'This data is simulated. SDK health event methods are not yet available.',
  };
}

export async function GET(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const { searchParams } = new URL(req.url);
    const limit = parseInt(searchParams.get('limit') || '10', 10);
    const adminClient = getServerAdminClient();
    
    // For now, return mock data as the SDK method doesn't exist yet
    try {
      // In the future: const events = await adminClient.system.getHealthEvents(limit);
      throw new Error('Method not implemented'); // Force fallback to mock data
    } catch (sdkError) {
      // Fallback to mock data if SDK methods fail
      const mockData = generateMockEvents(limit);
      return NextResponse.json(mockData);
    }
  } catch (error) {
    return handleSDKError(error);
  }
}
