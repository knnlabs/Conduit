
import { withSDKAuth } from '@/lib/auth/sdk-auth';
import { mapSDKErrorToResponse, withSDKErrorHandling } from '@/lib/errors/sdk-errors';
import { transformSDKResponse } from '@/lib/utils/sdk-transforms';
import { parseQueryParams } from '@/lib/utils/route-helpers';

// Default metrics structure
const DEFAULT_METRICS = {
  cpu: {
    usage: 0,
    cores: 0,
    model: 'Unknown',
    frequency: 0,
  },
  memory: {
    total: 0,
    used: 0,
    available: 0,
    usage: 0,
  },
  disk: {
    total: 0,
    used: 0,
    available: 0,
    usage: 0,
  },
  network: {
    bytesReceived: 0,
    bytesSent: 0,
    packetsReceived: 0,
    packetsSent: 0,
  },
  process: {
    uptime: 0,
    threads: 0,
    handles: 0,
  },
};

export const GET = withSDKAuth(
  async (request, { auth }) => {
    try {
      const params = parseQueryParams(request);
      const includeHistory = params.get('includeHistory') === 'true';
      const duration = params.get('duration') || '5m'; // 5 minutes, 1h, 24h, etc.
      
      // Get system metrics
      const result = await withSDKErrorHandling(
        async () => auth.adminClient!.system.getSystemMetrics(),
        'get system metrics'
      );

      return transformSDKResponse(result, {
        meta: {
          timestamp: new Date().toISOString(),
          duration,
          includeHistory,
        }
      });
    } catch (error: unknown) {
      // Return default metrics for 404
      if ((error as Record<string, unknown>)?.statusCode === 404 || (error as Record<string, unknown>)?.type === 'NOT_FOUND') {
        return transformSDKResponse(DEFAULT_METRICS, {
          meta: {
            isDefault: true,
            timestamp: new Date().toISOString(),
          }
        });
      }
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);

// Get performance metrics (requests per second, latency, etc.)
export const POST = withSDKAuth(
  async (request, { auth }) => {
    try {
      const body = await request.json();
      const { metricType, interval } = body;
      
      // Get system metrics (using the same method as there's no separate performance metrics)
      const result = await withSDKErrorHandling(
        async () => auth.adminClient!.system.getSystemMetrics(),
        'get system metrics'
      );

      return transformSDKResponse(result, {
        meta: {
          metricType,
          interval,
          timestamp: new Date().toISOString(),
        }
      });
    } catch (error: unknown) {
      // Return empty metrics for 404
      if ((error as Record<string, unknown>)?.statusCode === 404 || (error as Record<string, unknown>)?.type === 'NOT_FOUND') {
        return transformSDKResponse({
          requestsPerSecond: 0,
          averageLatency: 0,
          p95Latency: 0,
          p99Latency: 0,
          errorRate: 0,
          successRate: 100,
          activeConnections: 0,
          queuedRequests: 0,
        }, {
          meta: {
            isDefault: true,
            timestamp: new Date().toISOString(),
          }
        });
      }
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);