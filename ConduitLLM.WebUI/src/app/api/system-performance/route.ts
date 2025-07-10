import { NextRequest, NextResponse } from 'next/server';
import { requireAuth } from '@/lib/auth/simple-auth';
import { getServerAdminClient } from '@/lib/server/adminClient';

// Mock system performance data generator
function generateMockPerformanceData(range: string) {
  const now = Date.now();
  const ranges = {
    '15m': { minutes: 15, points: 15 },
    '1h': { minutes: 60, points: 30 },
    '6h': { minutes: 360, points: 36 },
    '24h': { minutes: 1440, points: 48 },
  };
  
  const { minutes, points } = ranges[range as keyof typeof ranges] || ranges['1h'];
  
  // Generate system metrics
  const metrics = {
    cpu: {
      usage: Math.floor(Math.random() * 40) + 30, // 30-70%
      cores: 8,
      loadAverage: [
        (Math.random() * 2 + 1).toFixed(2),
        (Math.random() * 2 + 0.5).toFixed(2),
        (Math.random() * 2).toFixed(2),
      ].map(Number),
      temperature: Math.floor(Math.random() * 20) + 50, // 50-70°C
    },
    memory: {
      total: 16 * 1024 * 1024 * 1024, // 16GB
      used: Math.floor((8 + Math.random() * 6) * 1024 * 1024 * 1024), // 8-14GB
      percentage: 0,
      swap: {
        total: 8 * 1024 * 1024 * 1024, // 8GB
        used: Math.floor(Math.random() * 2 * 1024 * 1024 * 1024), // 0-2GB
      },
    },
    disk: {
      total: 500 * 1024 * 1024 * 1024, // 500GB
      used: Math.floor((200 + Math.random() * 150) * 1024 * 1024 * 1024), // 200-350GB
      percentage: 0,
      io: {
        read: Math.floor(Math.random() * 50 * 1024 * 1024), // 0-50MB/s
        write: Math.floor(Math.random() * 30 * 1024 * 1024), // 0-30MB/s
      },
    },
    network: {
      in: Math.floor(Math.random() * 100 * 1024 * 1024), // 0-100MB/s
      out: Math.floor(Math.random() * 50 * 1024 * 1024), // 0-50MB/s
      connections: Math.floor(Math.random() * 500) + 100,
      latency: Math.floor(Math.random() * 50) + 10, // 10-60ms
    },
    uptime: 7 * 24 * 60 * 60 * 1000, // 7 days in ms
    processCount: Math.floor(Math.random() * 100) + 150,
    threadCount: Math.floor(Math.random() * 500) + 1000,
  };
  
  // Calculate percentages
  metrics.memory.percentage = Math.round((metrics.memory.used / metrics.memory.total) * 100);
  metrics.disk.percentage = Math.round((metrics.disk.used / metrics.disk.total) * 100);
  
  // Generate performance history
  const history = [];
  for (let i = points - 1; i >= 0; i--) {
    const timestamp = new Date(now - (i * (minutes / points) * 60 * 1000));
    history.push({
      timestamp: timestamp.toISOString(),
      cpu: Math.floor(Math.random() * 40) + 30,
      memory: Math.floor(Math.random() * 30) + 50,
      disk: Math.floor(Math.random() * 20) + 60,
      network: Math.floor(Math.random() * 100),
      responseTime: Math.floor(Math.random() * 200) + 100,
    });
  }
  
  // Generate service status
  const services = [
    {
      name: 'ConduitLLM Core API',
      status: Math.random() > 0.1 ? 'healthy' : 'degraded',
      uptime: 7 * 24 * 60 * 60 * 1000,
      memory: Math.floor(Math.random() * 500 + 200) * 1024 * 1024,
      cpu: Math.floor(Math.random() * 30) + 10,
      lastCheck: new Date().toISOString(),
    },
    {
      name: 'Redis Cache',
      status: Math.random() > 0.05 ? 'healthy' : 'down',
      uptime: 6 * 24 * 60 * 60 * 1000,
      memory: Math.floor(Math.random() * 300 + 100) * 1024 * 1024,
      cpu: Math.floor(Math.random() * 20) + 5,
      lastCheck: new Date().toISOString(),
    },
    {
      name: 'PostgreSQL Database',
      status: 'healthy',
      uptime: 7 * 24 * 60 * 60 * 1000,
      memory: Math.floor(Math.random() * 1000 + 500) * 1024 * 1024,
      cpu: Math.floor(Math.random() * 40) + 20,
      lastCheck: new Date().toISOString(),
    },
    {
      name: 'RabbitMQ',
      status: Math.random() > 0.2 ? 'healthy' : 'degraded',
      uptime: 5 * 24 * 60 * 60 * 1000,
      memory: Math.floor(Math.random() * 400 + 200) * 1024 * 1024,
      cpu: Math.floor(Math.random() * 25) + 15,
      lastCheck: new Date().toISOString(),
    },
    {
      name: 'WebUI Service',
      status: 'healthy',
      uptime: 3 * 24 * 60 * 60 * 1000,
      memory: Math.floor(Math.random() * 200 + 100) * 1024 * 1024,
      cpu: Math.floor(Math.random() * 15) + 5,
      lastCheck: new Date().toISOString(),
    },
  ];
  
  // Generate alerts
  const alerts = [];
  
  if (metrics.cpu.usage > 60) {
    alerts.push({
      id: 'alert-1',
      type: 'cpu',
      severity: metrics.cpu.usage > 80 ? 'critical' : 'warning',
      message: `CPU usage is at ${metrics.cpu.usage}%`,
      timestamp: new Date(now - 5 * 60 * 1000).toISOString(),
      resolved: false,
    });
  }
  
  if (metrics.memory.percentage > 80) {
    alerts.push({
      id: 'alert-2',
      type: 'memory',
      severity: metrics.memory.percentage > 90 ? 'error' : 'warning',
      message: `Memory usage is at ${metrics.memory.percentage}%`,
      timestamp: new Date(now - 10 * 60 * 1000).toISOString(),
      resolved: false,
    });
  }
  
  if (metrics.disk.percentage > 85) {
    alerts.push({
      id: 'alert-3',
      type: 'disk',
      severity: 'warning',
      message: `Disk usage is at ${metrics.disk.percentage}%`,
      timestamp: new Date(now - 30 * 60 * 1000).toISOString(),
      resolved: false,
    });
  }
  
  // Add some resolved alerts
  alerts.push(
    {
      id: 'alert-4',
      type: 'service',
      severity: 'error',
      message: 'Redis Cache connection lost',
      timestamp: new Date(now - 2 * 60 * 60 * 1000).toISOString(),
      resolved: true,
    },
    {
      id: 'alert-5',
      type: 'network',
      severity: 'warning',
      message: 'High network latency detected (>100ms)',
      timestamp: new Date(now - 4 * 60 * 60 * 1000).toISOString(),
      resolved: true,
    }
  );
  
  return {
    metrics,
    history,
    services,
    alerts,
  };
}

export async function GET(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const { searchParams } = new URL(req.url);
    const range = searchParams.get('range') || '1h';
    
    // In production, we would use the Admin SDK like this:
    // const adminClient = getServerAdminClient();
    // const performanceData = await adminClient.system.getPerformanceMetrics({
    //   timeRange: range,
    //   includeHistory: true,
    //   includeServices: true,
    //   includeAlerts: true,
    // });
    
    // For now, return mock data
    const performanceData = generateMockPerformanceData(range);
    
    return NextResponse.json(performanceData);
  } catch (error) {
    console.error('Error fetching system performance:', error);
    return NextResponse.json(
      { error: 'Failed to fetch system performance' },
      { status: 500 }
    );
  }
}