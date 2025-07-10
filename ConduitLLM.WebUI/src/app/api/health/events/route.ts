import { NextRequest, NextResponse } from 'next/server';
import { requireAuth } from '@/lib/auth/simple-auth';

// Mock health events - in production, these would be stored and retrieved from a database
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

export async function GET(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const { searchParams } = new URL(req.url);
    const limit = parseInt(searchParams.get('limit') || '10', 10);
    
    // Return the most recent events up to the limit
    const events = mockEvents.slice(0, Math.min(limit, mockEvents.length));
    
    return NextResponse.json(events);
  } catch (error) {
    console.error('Error fetching health events:', error);
    return NextResponse.json(
      { error: 'Failed to fetch health events' },
      { status: 500 }
    );
  }
}