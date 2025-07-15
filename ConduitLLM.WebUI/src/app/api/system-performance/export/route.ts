import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';

export async function GET(req: NextRequest) {
  try {
    const { searchParams } = new URL(req.url);
    const range = searchParams.get('range') || '1h';
    
    const adminClient = getServerAdminClient();
    
    // SDK export method may not be available, proceed directly to fallback
    
    // Fallback: generate CSV from current system state
    let csvContent = `Timestamp,CPU %,Memory %,Disk %,Network In (MB/s),Network Out (MB/s),Response Time (ms)\n`;
    
    try {
      // Get current system metrics
      const systemInfo = await adminClient.system.getSystemInfo();
      const systemMetrics = await adminClient.monitoring.getSystemMetrics();
      const performanceMetrics = await adminClient.metrics.getPerformanceMetrics({
        timeRange: range,
        resolution: 'minute',
      });
      
      // Add historical data if available
      if (performanceMetrics?.timeSeries?.length > 0) {
        performanceMetrics.timeSeries.forEach((point: any) => {
          csvContent += `${point.timestamp},${point.cpuUsage || 0},${point.memoryUsage || 0},0,${point.throughput || 0},0,${point.responseTime || 0}\n`;
        });
      } else {
        // Add at least current point if no historical data
        const now = new Date().toISOString();
        const cpuUsage = systemMetrics?.cpu?.usage || 0;
        const memoryUsage = systemMetrics?.memory?.used && systemMetrics?.memory?.total 
          ? Math.round((systemMetrics.memory.used / systemMetrics.memory.total) * 100) 
          : 0;
        csvContent += `${now},${cpuUsage},${memoryUsage},0,0,0,0\n`;
      }
      
      // Add summary information
      csvContent += `\nSystem Information:\n`;
      csvContent += `Version,${systemInfo?.version || 'Unknown'}\n`;
      csvContent += `Environment,${systemInfo?.environment || 'Unknown'}\n`;
      csvContent += `Uptime (seconds),${systemInfo?.uptime || 0}\n`;
      
      if (systemMetrics) {
        csvContent += `CPU Cores,${systemMetrics.cpu?.cores?.length || 0}\n`;
        csvContent += `Total Memory (bytes),${systemMetrics.memory?.total || 0}\n`;
        csvContent += `Used Memory (bytes),${systemMetrics.memory?.used || 0}\n`;
        csvContent += `Process Count,${systemMetrics.processes?.length || 0}\n`;
      }
      
    } catch (error) {
      console.warn('Failed to get system data for export:', error);
      csvContent += `# Export failed: Unable to retrieve system performance data\n`;
      csvContent += `# Error: ${error}\n`;
      csvContent += `# Time Range: ${range}\n`;
      csvContent += `# Generated: ${new Date().toISOString()}\n`;
    }

    return new NextResponse(csvContent, {
      headers: {
        'Content-Type': 'text/csv',
        'Content-Disposition': `attachment; filename="system-performance-${range}-${new Date().toISOString()}.csv"`,
      },
    });
  } catch (error) {
    return handleSDKError(error);
  }
}