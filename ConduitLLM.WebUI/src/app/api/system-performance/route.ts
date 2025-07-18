import { NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';

export async function GET() {
  try {
    const adminClient = getServerAdminClient();
    
    // Initialize empty response structure
    const metrics = {
      cpu: { usage: 0, cores: 0, loadAverage: [], temperature: null },
      memory: { total: 0, used: 0, percentage: 0, swap: { total: 0, used: 0 } },
      disk: { total: 0, used: 0, percentage: 0, io: { read: 0, write: 0 } },
      network: { in: 0, out: 0, connections: 0, latency: 0 },
      uptime: 0,
      processCount: 0,
      threadCount: 0,
    };
    interface HistoryPoint {
      timestamp: string;
      cpu: number;
      memory: number;
      disk: number;
      network: { in: number; out: number; };
    }
    const history: HistoryPoint[] = [];
    interface ServiceStatus {
      name: string;
      status: string;
      uptime: number;
      memory: number;
      cpu: number;
      lastCheck: string;
    }
    let services: ServiceStatus[] = [];
    interface Alert {
      id: string;
      type: string;
      severity: string;
      message: string;
      timestamp: string;
      resolved: boolean;
    }
    let transformedAlerts: Alert[] = [];

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
        metrics.threadCount = systemMetrics.processes?.reduce((sum: number, p: { threads?: number }) => sum + (p.threads ?? 0), 0) ?? 0;
      }
    } catch (error) {
      console.warn('Failed to fetch system metrics:', error);
    }

    // Performance metrics history not available - endpoints don't exist
    // history will remain empty array

    try {
      // Try to get alerts
      const alerts = await adminClient.monitoring.listAlerts({
        status: 'active',
      });
      
      if (alerts?.data) {
        transformedAlerts = alerts.data.map((alert) => ({
          id: alert.id,
          type: alert.condition?.type ?? 'system',
          severity: alert.severity,
          message: alert.name || 'System alert',
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
      info: 'Data availability depends on monitoring service configuration',
    });
  } catch (error) {
    return handleSDKError(error);
  }
}
