/**
 * System resource metrics
 */
export interface SystemResourceMetrics {
  cpu: MonitoringCpuMetrics;
  memory: MonitoringMemoryMetrics;
  disk: DiskMetrics;
  network: NetworkMetrics;
  processes: ProcessMetrics[];
  timestamp: string;
}

/**
 * Extended CPU metrics
 */
export interface MonitoringCpuMetrics {
  usage: number;
  userTime: number;
  systemTime: number;
  idleTime: number;
  cores: CpuCoreMetrics[];
}

/**
 * CPU core metrics
 */
export interface CpuCoreMetrics {
  coreId: number;
  usage: number;
  frequency: number;
  temperature?: number;
}

/**
 * Extended memory metrics
 */
export interface MonitoringMemoryMetrics {
  total: number;
  used: number;
  free: number;
  available: number;
  cached: number;
  buffers: number;
  swapTotal: number;
  swapUsed: number;
  swapFree: number;
}

/**
 * Disk metrics
 */
export interface DiskMetrics {
  devices: DiskDeviceMetrics[];
  totalReadBytes: number;
  totalWriteBytes: number;
  readOpsPerSecond: number;
  writeOpsPerSecond: number;
}

/**
 * Disk device metrics
 */
export interface DiskDeviceMetrics {
  device: string;
  mountPoint: string;
  totalSpace: number;
  usedSpace: number;
  freeSpace: number;
  usagePercent: number;
  readBytes: number;
  writeBytes: number;
  ioBusy: number;
}

/**
 * Network metrics
 */
export interface NetworkMetrics {
  interfaces: NetworkInterfaceMetrics[];
  totalBytesReceived: number;
  totalBytesSent: number;
  packetsReceived: number;
  packetsSent: number;
  errors: number;
  dropped: number;
}

/**
 * Network interface metrics
 */
export interface NetworkInterfaceMetrics {
  name: string;
  bytesReceived: number;
  bytesSent: number;
  packetsReceived: number;
  packetsSent: number;
  errors: number;
  dropped: number;
  status: 'up' | 'down';
}

/**
 * Process metrics
 */
export interface ProcessMetrics {
  pid: number;
  name: string;
  cpuUsage: number;
  memoryUsage: number;
  threads: number;
  handles: number;
  startTime: string;
}