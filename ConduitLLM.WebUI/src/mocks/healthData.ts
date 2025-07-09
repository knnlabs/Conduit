export const mockHealthData = {
  status: 'healthy',
  timestamp: new Date().toISOString(),
  services: {
    coreApi: { status: 'healthy', latency: 12 },
    adminApi: { status: 'healthy', latency: 15 },
    database: { status: 'healthy', latency: 8 },
    cache: { status: 'healthy', latency: 2 },
  },
  systemHealth: {
    cpuUsage: 45,
    memoryUsage: 62,
    diskUsage: 38,
    uptime: 2592000, // 30 days in seconds
  },
  version: '1.0.0',
  environment: 'production',
};