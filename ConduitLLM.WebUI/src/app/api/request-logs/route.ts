import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';

// Type definitions for SDK responses
interface RequestLogDto {
  id: string;
  timestamp: string;
  status: string;
  duration: number;
  virtualKeyId: string;
  virtualKeyName?: string;
  provider: string;
  model: string;
  inputTokens: number;
  outputTokens: number;
  cost?: number;
  errorMessage?: string;
  userAgent?: string;
  ipAddress?: string;
}

interface RequestLogResponse {
  items: RequestLogDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

interface MockRequestLog {
  id: string;
  timestamp: string;
  method: string;
  path: string;
  statusCode: number;
  duration: number;
  virtualKeyId: string;
  virtualKeyName: string;
  provider: string;
  model: string;
  tokenUsage?: {
    prompt: number;
    completion: number;
    total: number;
  };
  cost?: number;
  error?: string;
  userAgent: string;
  ipAddress: string;
  requestBody?: unknown;
  responseBody?: unknown;
}

interface WebUIRequestLog {
  id: string;
  timestamp: string;
  method: string;
  path: string;
  statusCode: number;
  duration: number;
  virtualKeyId: string;
  virtualKeyName: string;
  provider: string;
  model: string;
  tokenUsage: {
    prompt: number;
    completion: number;
    total: number;
  };
  cost?: number;
  error?: string;
  userAgent?: string;
  ipAddress?: string;
  requestBody?: unknown;
  responseBody?: unknown;
}

function getStatusCode(status: string): number {
  if (status === 'success') {
    return 200;
  }
  if (status === 'timeout') {
    return 408;
  }
  return 500;
}

// Mock request logs data - in production this would come from the Admin API
function generateMockLogs(count: number = 100): MockRequestLog[] {
  const methods = ['GET', 'POST', 'PUT', 'DELETE'];
  const paths = [
    '/v1/chat/completions',
    '/v1/completions',
    '/v1/embeddings',
    '/v1/models',
    '/v1/images/generations',
    '/v1/audio/transcriptions',
  ];
  const providers = ['OpenAI', 'Anthropic', 'Azure', 'Google', 'Replicate'];
  const models = ['gpt-4', 'gpt-3.5-turbo', 'claude-3-opus', 'claude-3-sonnet', 'gemini-pro'];
  const virtualKeys = ['Production API', 'Development API', 'Testing Key', 'Customer A', 'Customer B'];
  
  const logs: MockRequestLog[] = [];
  const now = Date.now();
  
  for (let i = 0; i < count; i++) {
    const isError = Math.random() < 0.1;
    let statusCode = 200;
    if (isError) {
      statusCode = Math.random() < 0.5 ? 400 : 500;
    }
    const hasTokenUsage = Math.random() < 0.7 && !isError;
    
    logs.push({
      id: `log-${i + 1}`,
      timestamp: new Date(now - i * 60000 * Math.random() * 60).toISOString(),
      method: methods[Math.floor(Math.random() * methods.length)],
      path: paths[Math.floor(Math.random() * paths.length)],
      statusCode,
      duration: Math.floor(Math.random() * 2000) + 100,
      virtualKeyId: `vk-${Math.floor(Math.random() * 5) + 1}`,
      virtualKeyName: virtualKeys[Math.floor(Math.random() * virtualKeys.length)],
      provider: providers[Math.floor(Math.random() * providers.length)],
      model: models[Math.floor(Math.random() * models.length)],
      tokenUsage: hasTokenUsage ? {
        prompt: Math.floor(Math.random() * 1000) + 100,
        completion: Math.floor(Math.random() * 500) + 50,
        total: 0,
      } : undefined,
      cost: hasTokenUsage ? Math.random() * 0.5 : undefined,
      error: isError ? 'API rate limit exceeded' : undefined,
      userAgent: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36',
      ipAddress: `192.168.1.${Math.floor(Math.random() * 255)}`,
      requestBody: Math.random() < 0.5 ? {
        model: models[Math.floor(Math.random() * models.length)],
        messages: [
          { role: 'user', content: 'Sample request' }
        ],
        temperature: 0.7,
      } : undefined,
      responseBody: !isError && Math.random() < 0.5 ? {
        id: `chatcmpl-${Math.random().toString(36).substring(2, 11)}`,
        object: 'chat.completion',
        created: Math.floor(Date.now() / 1000),
        choices: [
          { message: { role: 'assistant', content: 'Sample response' } }
        ],
      } : undefined,
    });
  }
  
  // Calculate total for token usage
  logs.forEach(log => {
    if (log.tokenUsage) {
      log.tokenUsage.total = log.tokenUsage.prompt + log.tokenUsage.completion;
    }
  });
  
  return logs;
}

export async function GET(req: NextRequest) {

  try {
    const { searchParams } = new URL(req.url);
    const adminClient = getServerAdminClient();
    
    try {
      // Use the Admin SDK to fetch real request logs
      const logsResponse = await adminClient.analytics.getRequestLogs({
        page: parseInt(searchParams.get('page') ?? '1', 10),
        pageSize: parseInt(searchParams.get('pageSize') ?? '20', 10),
        startDate: searchParams.get('dateFrom') ?? undefined,
        endDate: searchParams.get('dateTo') ?? undefined,
        virtualKeyId: searchParams.get('virtualKeyId') ?? undefined,
        provider: searchParams.get('provider') ?? undefined,
        model: searchParams.get('model') ?? undefined,
        statusCode: searchParams.get('statusCode') ? parseInt(searchParams.get('statusCode') ?? '', 10) : undefined,
        // TODO: SDK should support search parameter for filtering logs
        // search: searchParams.get('search') || undefined,
      }) as unknown as RequestLogResponse;
      
      // Transform the response to match the expected format
      // TODO: The SDK response should match the WebUI expectations
      const logs: WebUIRequestLog[] = logsResponse.items.map((log: RequestLogDto): WebUIRequestLog => ({
        id: log.id,
        timestamp: log.timestamp,
        method: 'POST', // RequestLogDto doesn't have method, default to POST
        path: '/v1/chat/completions', // RequestLogDto doesn't have path
        statusCode: getStatusCode(log.status),
        duration: log.duration,
        virtualKeyId: log.virtualKeyId,
        virtualKeyName: log.virtualKeyName ?? `Key ${String(log.virtualKeyId)}`,
        provider: log.provider,
        model: log.model,
        tokenUsage: {
          prompt: log.inputTokens,
          completion: log.outputTokens,
          total: log.inputTokens + log.outputTokens,
        },
        cost: log.cost,
        error: log.errorMessage,
        userAgent: log.userAgent,
        ipAddress: log.ipAddress,
        requestBody: undefined, // Not available in RequestLogDto
        responseBody: undefined, // Not available in RequestLogDto
      }));
      
      // Apply client-side search filtering if needed
      // TODO: Remove this once SDK supports search parameter
      let filteredLogs: WebUIRequestLog[] = logs;
      const search = searchParams.get('search');
      if (search) {
        filteredLogs = logs.filter((log: WebUIRequestLog) => {
          return log.path?.toLowerCase().includes(search.toLowerCase()) ||
                  log.id?.toLowerCase().includes(search.toLowerCase()) ||
                  log.model?.toLowerCase().includes(search.toLowerCase()) ||
                  log.provider?.toLowerCase().includes(search.toLowerCase());
        });
      }
      
      // Apply status code group filtering (2xx, 4xx, 5xx)
      const statusCodeParam = searchParams.get('statusCode');
      if (statusCodeParam?.endsWith('xx')) {
        const statusGroup = parseInt(statusCodeParam[0], 10);
        filteredLogs = filteredLogs.filter((log: WebUIRequestLog) => 
          Math.floor(log.statusCode / 100) === statusGroup
        );
      }
      
      return NextResponse.json({ 
        logs: filteredLogs,
        totalCount: logsResponse.totalCount,
        page: logsResponse.page,
        pageSize: logsResponse.pageSize,
        totalPages: logsResponse.totalPages,
      });
    } catch (sdkError) {
      // If SDK call fails, fall back to mock data
      console.warn('Failed to fetch request logs from SDK, using mock data:', sdkError);
      const logs = generateMockLogs();
      
      // Apply the same filtering logic to mock data
      let filteredLogs: MockRequestLog[] = logs;
      
      const search = searchParams.get('search');
      if (search) {
        filteredLogs = filteredLogs.filter(log => 
          log.path.toLowerCase().includes(search.toLowerCase()) ||
          log.id.toLowerCase().includes(search.toLowerCase())
        );
      }
      
      const virtualKeyId = searchParams.get('virtualKeyId');
      if (virtualKeyId) {
        filteredLogs = filteredLogs.filter(log => log.virtualKeyName === virtualKeyId);
      }
      
      const provider = searchParams.get('provider');
      if (provider) {
        filteredLogs = filteredLogs.filter(log => log.provider === provider);
      }
      
      const statusCode = searchParams.get('statusCode');
      if (statusCode) {
        if (statusCode === '2xx') {
          filteredLogs = filteredLogs.filter(log => log.statusCode >= 200 && log.statusCode < 300);
        } else if (statusCode === '4xx') {
          filteredLogs = filteredLogs.filter(log => log.statusCode >= 400 && log.statusCode < 500);
        } else if (statusCode === '5xx') {
          filteredLogs = filteredLogs.filter(log => log.statusCode >= 500);
        }
      }
      
      const method = searchParams.get('method');
      if (method) {
        filteredLogs = filteredLogs.filter(log => log.method === method);
      }
      
      return NextResponse.json({ 
        logs: filteredLogs,
        warning: 'Using mock data due to SDK error.',
      });
    }
  } catch (error) {
    return handleSDKError(error);
  }
}
