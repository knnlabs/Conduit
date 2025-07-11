import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { requireAuth } from '@/lib/auth/simple-auth';
import { getServerAdminClient } from '@/lib/server/adminClient';

// Mock analytics data generator
function generateMockAnalytics(range: string) {
  const now = Date.now();
  const ranges = {
    '24h': { days: 1, points: 24 },
    '7d': { days: 7, points: 7 },
    '30d': { days: 30, points: 30 },
    '90d': { days: 90, points: 30 },
  };
  
  const { days, points } = ranges[range as keyof typeof ranges] || ranges['7d'];
  
  // Generate time series data
  const timeSeries = [];
  for (let i = points - 1; i >= 0; i--) {
    const timestamp = new Date(now - (i * (days / points) * 24 * 60 * 60 * 1000));
    timeSeries.push({
      timestamp: timestamp.toISOString(),
      requests: Math.floor(Math.random() * 10000) + 5000,
      cost: Math.random() * 500 + 100,
      tokens: Math.floor(Math.random() * 1000000) + 500000,
    });
  }
  
  // Calculate metrics with comparison to previous period
  const totalRequests = timeSeries.reduce((sum, point) => sum + point.requests, 0);
  const totalCost = timeSeries.reduce((sum, point) => sum + point.cost, 0);
  const totalTokens = timeSeries.reduce((sum, point) => sum + point.tokens, 0);
  
  const metrics = {
    totalRequests,
    totalCost,
    totalTokens,
    activeVirtualKeys: Math.floor(Math.random() * 20) + 10,
    requestsChange: Math.random() * 40 - 20, // -20% to +20%
    costChange: Math.random() * 30 - 15, // -15% to +15%
    tokensChange: Math.random() * 50 - 25, // -25% to +25%
    virtualKeysChange: Math.random() * 20 - 10, // -10% to +10%
  };
  
  // Provider usage data
  const providers = ['OpenAI', 'Anthropic', 'Azure', 'Google', 'Replicate'];
  const providerUsage = providers.map(provider => {
    const requests = Math.floor(Math.random() * totalRequests / 3);
    return {
      provider,
      requests,
      cost: Math.random() * totalCost / 3,
      tokens: Math.floor(Math.random() * totalTokens / 3),
      percentage: 0, // Will calculate after
    };
  });
  
  // Calculate percentages
  const totalProviderRequests = providerUsage.reduce((sum, p) => sum + p.requests, 0);
  providerUsage.forEach(p => {
    p.percentage = Math.round((p.requests / totalProviderRequests) * 100);
  });
  
  // Model usage data
  const models = [
    { model: 'gpt-4', provider: 'OpenAI' },
    { model: 'gpt-3.5-turbo', provider: 'OpenAI' },
    { model: 'claude-3-opus', provider: 'Anthropic' },
    { model: 'claude-3-sonnet', provider: 'Anthropic' },
    { model: 'gemini-pro', provider: 'Google' },
    { model: 'text-davinci-003', provider: 'OpenAI' },
    { model: 'gpt-4-vision', provider: 'OpenAI' },
    { model: 'claude-3-haiku', provider: 'Anthropic' },
    { model: 'palm-2', provider: 'Google' },
    { model: 'llama-2-70b', provider: 'Replicate' },
  ];
  
  const modelUsage = models.map(({ model, provider }) => ({
    model,
    provider,
    requests: Math.floor(Math.random() * 5000) + 1000,
    cost: Math.random() * 200 + 50,
    tokens: Math.floor(Math.random() * 500000) + 100000,
  })).sort((a, b) => b.requests - a.requests);
  
  // Virtual key usage
  const virtualKeys = [
    'Production API',
    'Development API',
    'Testing Key',
    'Customer A',
    'Customer B',
    'Customer C',
    'Internal Tools',
    'QA Environment',
    'Staging API',
    'Demo Account',
    'Partner Integration',
    'Mobile App',
  ];
  
  const virtualKeyUsage = virtualKeys.map(keyName => ({
    keyName,
    requests: Math.floor(Math.random() * 8000) + 1000,
    cost: Math.random() * 300 + 50,
    tokens: Math.floor(Math.random() * 700000) + 100000,
    lastUsed: new Date(now - Math.random() * 24 * 60 * 60 * 1000).toISOString(),
  })).sort((a, b) => b.requests - a.requests);
  
  // Endpoint usage
  const endpoints = [
    '/v1/chat/completions',
    '/v1/completions',
    '/v1/embeddings',
    '/v1/models',
    '/v1/images/generations',
    '/v1/audio/transcriptions',
    '/v1/moderations',
    '/v1/fine-tunes',
  ];
  
  const endpointUsage = endpoints.map(endpoint => ({
    endpoint,
    requests: Math.floor(Math.random() * 10000) + 1000,
    avgDuration: Math.floor(Math.random() * 800) + 200,
    errorRate: Math.random() * 10, // 0-10%
  })).sort((a, b) => b.requests - a.requests);
  
  return {
    metrics,
    timeSeries,
    providerUsage,
    modelUsage,
    virtualKeyUsage,
    endpointUsage,
  };
}

export async function GET(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const { searchParams } = new URL(req.url);
    const range = searchParams.get('range') || '7d';
    
    // In production, we would use the Admin SDK like this:
    // const adminClient = getServerAdminClient();
    // const analytics = await adminClient.analytics.getUsageAnalytics({
    //   timeRange: range,
    //   includeTimeSeries: true,
    //   includeProviderBreakdown: true,
    //   includeModelBreakdown: true,
    //   includeVirtualKeyBreakdown: true,
    //   includeEndpointBreakdown: true,
    // });
    
    // For now, return mock data
    const analytics = generateMockAnalytics(range);
    
    return NextResponse.json(analytics);
  } catch (error) {
    return handleSDKError(error);
  }
}
