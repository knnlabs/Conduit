import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { requireAuth } from '@/lib/auth/simple-auth';

// Mock system health data - in production, this would come from actual system monitoring
function generateSystemHealth() {
  const cpuUsage = Math.floor(Math.random() * 60) + 20;
  const memoryUsage = Math.floor(Math.random() * 70) + 20;
  const diskUsage = Math.floor(Math.random() * 80) + 10;
  
  const isHealthy = cpuUsage < 80 && memoryUsage < 80 && diskUsage < 90;
  const isDegraded = cpuUsage >= 80 || memoryUsage >= 80 || diskUsage >= 90;
  
  return {
    overall: isHealthy ? 'healthy' : isDegraded ? 'degraded' : 'unhealthy',
    components: {
      api: {
        status: 'healthy',
        message: 'API responding normally',
        lastChecked: new Date().toISOString(),
      },
      database: {
        status: Math.random() > 0.05 ? 'healthy' : 'degraded',
        message: Math.random() > 0.05 ? 'Database connections stable' : 'High connection count',
        lastChecked: new Date().toISOString(),
      },
      cache: {
        status: Math.random() > 0.1 ? 'healthy' : 'degraded',
        message: Math.random() > 0.1 ? 'Cache hit rate normal' : 'Low cache hit rate',
        lastChecked: new Date().toISOString(),
      },
      queue: {
        status: 'healthy',
        message: 'Message queue processing normally',
        lastChecked: new Date().toISOString(),
      },
    },
    metrics: {
      cpu: cpuUsage,
      memory: memoryUsage,
      disk: diskUsage,
      activeConnections: Math.floor(Math.random() * 100) + 20,
    },
  };
}

export async function GET(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const systemHealth = generateSystemHealth();
    return NextResponse.json(systemHealth);
  } catch (error) {
    return handleSDKError(error);
  }
}
