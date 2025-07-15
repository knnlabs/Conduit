import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';

export async function GET(req: NextRequest) {
  try {
    const { searchParams } = new URL(req.url);
    const range = searchParams.get('range') || '1h';
    
    const adminClient = getServerAdminClient();
    
    // Initialize empty response structure
    let metrics = {
      cpu: { usage: 0, cores: 0, loadAverage: [], temperature: null },
      memory: { total: 0, used: 0, percentage: 0, swap: { total: 0, used: 0 } },
      disk: { total: 0, used: 0, percentage: 0, io: { read: 0, write: 0 } },
      network: { in: 0, out: 0, connections: 0, latency: 0 },
      uptime: 0,
      processCount: 0,
      threadCount: 0,
    };
    let history: any[] = [];
    let services: any[] = [];
    let transformedAlerts: any[] = [];

    try {
      // Try to get system info first (this is most likely to work)
      const systemInfo = await adminClient.system.getSystemInfo();
      if (systemInfo) {
        metrics.uptime = systemInfo.uptime * 1000; // Convert to milliseconds
        
        // Basic service status from system info
        services = [
          {
            name: 'Conduit Core API',
            status: 'healthy',
            uptime: systemInfo.uptime * 1000,
            memory: 0,
            cpu: 0,
            lastCheck: new Date().toISOString(),
          }
        ];
      }
    } catch (error) {
      console.warn('Failed to fetch system info:', error);
    }

    try {
      // Try to get system metrics (may not be available in all deployments)
      const systemMetrics = await adminClient.monitoring.getSystemMetrics();
      if (systemMetrics) {
        metrics.cpu = {
          usage: systemMetrics.cpu?.usage || 0,
          cores: systemMetrics.cpu?.cores?.length || 0,
          loadAverage: [], // Not available
          temperature: null, // Not available
        };
        
        metrics.memory = {
          total: systemMetrics.memory?.total || 0,
          used: systemMetrics.memory?.used || 0,
          percentage: systemMetrics.memory?.used && systemMetrics.memory?.total 
            ? Math.round((systemMetrics.memory.used / systemMetrics.memory.total) * 100) 
            : 0,
          swap: { total: 0, used: 0 }, // Not available
        };
        
        metrics.disk = {
          total: systemMetrics.disk?.devices?.[0]?.totalSpace || 0,
          used: systemMetrics.disk?.devices?.[0]?.usedSpace || 0,
          percentage: systemMetrics.disk?.devices?.[0]?.usagePercent || 0,
          io: {
            read: systemMetrics.disk?.totalReadBytes || 0,
            write: systemMetrics.disk?.totalWriteBytes || 0,
          },
        };
        
        metrics.network = {
          in: systemMetrics.network?.totalBytesReceived || 0,
          out: systemMetrics.network?.totalBytesSent || 0,
          connections: 0, // Not available
          latency: 0, // Not available
        };
        
        metrics.processCount = systemMetrics.processes?.length || 0;
        metrics.threadCount = systemMetrics.processes?.reduce((sum: number, p: any) => sum + (p.threads || 0), 0) || 0;
      }
    } catch (error) {
      console.warn('Failed to fetch system metrics:', error);
    }

    try {
      // Try to get performance metrics history
      const performanceMetrics = await adminClient.metrics.getPerformanceMetrics({
        timeRange: range,
        resolution: range === '15m' ? 'minute' : range === '1h' ? 'minute' : 'hour',
      });
      
      if (performanceMetrics?.timeSeries) {
        history = performanceMetrics.timeSeries.map((point: any) => ({
          timestamp: point.timestamp,
          cpu: point.cpuUsage || 0,
          memory: point.memoryUsage || 0,
          disk: 0, // Not available in time series
          network: point.throughput || 0,
          responseTime: point.responseTime || 0,
        }));
      }
    } catch (error) {
      console.warn('Failed to fetch performance metrics:', error);
    }

    try {
      // Try to get alerts
      const alerts = await adminClient.monitoring.listAlerts({
        status: 'active',
      });
      
      if (alerts?.data) {
        transformedAlerts = alerts.data.map((alert: any) => ({
          id: alert.id,
          type: alert.type || 'system',
          severity: alert.severity,
          message: alert.message,
          timestamp: alert.createdAt,
          resolved: alert.status === 'resolved',
        }));
      }
    } catch (error) {
      console.warn('Failed to fetch alerts:', error);
    }

    return NextResponse.json({
      metrics,
      history,
      services,
      alerts: transformedAlerts,
      _info: 'Data availability depends on monitoring service configuration',
    });
  } catch (error) {
    return handleSDKError(error);
  }
}
