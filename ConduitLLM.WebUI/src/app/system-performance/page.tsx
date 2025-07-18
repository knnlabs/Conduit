'use client';

import {
  Stack,
  Title,
  Text,
  Group,
  Grid,
  Card,
  Progress,
  Badge,
  ThemeIcon,
  Table,
  ScrollArea,
  Button,
  Select,
  Switch,
  Skeleton,
  Paper,
  Alert,
} from '@mantine/core';
import {
  LineChart,
} from '@mantine/charts';
import {
  IconCpu,
  IconDatabase,
  IconServer,
  IconActivity,
  IconNetwork,
  IconClock,
  IconRefresh,
  IconDownload,
  IconAlertTriangle,
  IconCircleCheck,
} from '@tabler/icons-react';
import { useState, useEffect, useCallback } from 'react';
import { CardSkeleton } from '@/components/common/LoadingState';
import { formatters } from '@/lib/utils/formatters';

interface SystemMetrics {
  cpu: {
    usage: number;
    cores: number;
    loadAverage: number[];
    temperature?: number;
  };
  memory: {
    used: number;
    total: number;
    percentage: number;
    swap: {
      used: number;
      total: number;
    };
  };
  disk: {
    used: number;
    total: number;
    percentage: number;
    io: {
      read: number;
      write: number;
    };
  };
  network: {
    in: number;
    out: number;
    connections: number;
    latency: number;
  };
  uptime: number;
  processCount: number;
  threadCount: number;
}

interface PerformanceHistory {
  timestamp: string;
  cpu: number;
  memory: number;
  disk: number;
  network: number;
  responseTime: number;
}

interface ServiceStatus {
  name: string;
  status: 'healthy' | 'degraded' | 'down';
  uptime: number;
  memory: number;
  cpu: number;
  lastCheck: string;
}

interface PerformanceAlert {
  id: string;
  type: 'cpu' | 'memory' | 'disk' | 'network' | 'service';
  severity: 'warning' | 'error' | 'critical';
  message: string;
  timestamp: string;
  resolved: boolean;
}

export default function SystemPerformancePage() {
  const [timeRange, setTimeRange] = useState('1h');
  const [autoRefresh, setAutoRefresh] = useState(true);
  const [isLoading, setIsLoading] = useState(true);
  const [metrics, setMetrics] = useState<SystemMetrics | null>(null);
  const [history, setHistory] = useState<PerformanceHistory[]>([]);
  const [services, setServices] = useState<ServiceStatus[]>([]);
  const [alerts, setAlerts] = useState<PerformanceAlert[]>([]);

  const fetchPerformanceData = useCallback(async () => {
    try {
      setIsLoading(true);
      const response = await fetch(`/api/system-performance?range=${timeRange}`);
      if (!response.ok) {
        throw new Error('Failed to fetch performance data');
      }
      const data = await response.json();
      
      setMetrics(data.metrics);
      setHistory(data.history);
      setServices(data.services);
      setAlerts(data.alerts);
    } catch (error) {
      console.error('Error fetching performance data:', error);
    } finally {
      setIsLoading(false);
    }
  }, [timeRange]);

  useEffect(() => {
    void fetchPerformanceData();
    
    if (autoRefresh) {
      const interval = setInterval(() => void fetchPerformanceData(), 10000); // Refresh every 10 seconds
      return () => clearInterval(interval);
    }
  }, [fetchPerformanceData, autoRefresh]);

  const getCPUColor = (usage: number): string => {
    if (usage < 50) return 'green';
    if (usage < 80) return 'yellow';
    return 'red';
  };

  const getMemoryColor = (percentage: number): string => {
    if (percentage < 60) return 'blue';
    if (percentage < 85) return 'orange';
    return 'red';
  };

  const getDiskColor = (percentage: number): string => {
    if (percentage < 70) return 'cyan';
    if (percentage < 90) return 'orange';
    return 'red';
  };

  const getServiceStatusColor = (status: string): string => {
    switch (status) {
      case 'healthy': return 'green';
      case 'degraded': return 'yellow';
      case 'down': return 'red';
      default: return 'gray';
    }
  };

  const getAlertColor = (severity: string): string => {
    switch (severity) {
      case 'warning': return 'yellow';
      case 'error': return 'orange';
      case 'critical': return 'red';
      default: return 'gray';
    }
  };

  const handleExport = async () => {
    try {
      const response = await fetch(`/api/system-performance/export?range=${timeRange}`);
      if (!response.ok) throw new Error('Export failed');

      const blob = await response.blob();
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `system-performance-${timeRange}-${new Date().toISOString()}.csv`;
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      document.body.removeChild(a);
    } catch (error) {
      console.error('Export failed:', error);
    }
  };

  return (
    <Stack gap="xl">
      <Group justify="space-between">
        <div>
          <Title order={1}>System Performance</Title>
          <Text c="dimmed">Live system metrics and resource utilization from Conduit monitoring</Text>
        </div>
        <Group>
          <Select
            value={timeRange}
            onChange={(value) => setTimeRange(value ?? '1h')}
            data={[
              { value: '15m', label: 'Last 15 Minutes' },
              { value: '1h', label: 'Last Hour' },
              { value: '6h', label: 'Last 6 Hours' },
              { value: '24h', label: 'Last 24 Hours' },
            ]}
          />
          <Switch
            label="Auto-refresh"
            checked={autoRefresh}
            onChange={(event) => setAutoRefresh(event.currentTarget.checked)}
          />
          <Button
            variant="light"
            leftSection={<IconDownload size={16} />}
            onClick={() => void handleExport()}
          >
            Export
          </Button>
          <Button
            variant="light"
            leftSection={<IconRefresh size={16} />}
            onClick={() => void fetchPerformanceData()}
            loading={isLoading}
          >
            Refresh
          </Button>
        </Group>
      </Group>

      {/* Information Alert */}
      {!isLoading && (
        <Alert
          icon={<IconActivity size={16} />}
          title="Performance Monitoring"
          color="blue"
        >
          System performance data is retrieved from Conduit&apos;s monitoring services. Data availability depends on monitoring configuration and may be limited in some environments.
        </Alert>
      )}

      {/* Resource Usage Cards */}
      <Grid>
        <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
          {isLoading ? (
            <CardSkeleton height={180} />
          ) : (
            <Card withBorder>
              <Group justify="space-between" mb="xs">
                <Text size="sm" c="dimmed" tt="uppercase" fw={600}>
                  CPU Usage
                </Text>
                <ThemeIcon color={getCPUColor(metrics?.cpu.usage ?? 0)} variant="light" size="sm">
                  <IconCpu size={16} />
                </ThemeIcon>
              </Group>
              <Group align="flex-end" gap="xs" mb="md">
                <Text size="2xl" fw={700}>{metrics?.cpu.usage ?? 0}%</Text>
                <Text size="sm" c="dimmed" mb={5}>
                  {metrics?.cpu.cores ?? 0} cores
                </Text>
              </Group>
              <Progress
                value={metrics?.cpu.usage ?? 0}
                color={getCPUColor(metrics?.cpu.usage ?? 0)}
                size="md"
                radius="md"
              />
              <Text size="xs" c="dimmed" mt="xs">
                Load: {metrics?.cpu.loadAverage?.join(', ') ?? 'N/A'}
              </Text>
            </Card>
          )}
        </Grid.Col>

        <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
          {isLoading ? (
            <CardSkeleton height={180} />
          ) : (
            <Card withBorder>
              <Group justify="space-between" mb="xs">
                <Text size="sm" c="dimmed" tt="uppercase" fw={600}>
                  Memory Usage
                </Text>
                <ThemeIcon color={getMemoryColor(metrics?.memory.percentage ?? 0)} variant="light" size="sm">
                  <IconDatabase size={16} />
                </ThemeIcon>
              </Group>
              <Group align="flex-end" gap="xs" mb="md">
                <Text size="2xl" fw={700}>{metrics?.memory.percentage ?? 0}%</Text>
                <Text size="sm" c="dimmed" mb={5}>
                  {formatters.fileSize(metrics?.memory.used ?? 0)} / {formatters.fileSize(metrics?.memory.total ?? 0)}
                </Text>
              </Group>
              <Progress
                value={metrics?.memory.percentage ?? 0}
                color={getMemoryColor(metrics?.memory.percentage ?? 0)}
                size="md"
                radius="md"
              />
              <Text size="xs" c="dimmed" mt="xs">
                Swap: {formatters.fileSize(metrics?.memory.swap.used ?? 0)} / {formatters.fileSize(metrics?.memory.swap.total ?? 0)}
              </Text>
            </Card>
          )}
        </Grid.Col>

        <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
          {isLoading ? (
            <CardSkeleton height={180} />
          ) : (
            <Card withBorder>
              <Group justify="space-between" mb="xs">
                <Text size="sm" c="dimmed" tt="uppercase" fw={600}>
                  Disk Usage
                </Text>
                <ThemeIcon color={getDiskColor(metrics?.disk.percentage ?? 0)} variant="light" size="sm">
                  <IconServer size={16} />
                </ThemeIcon>
              </Group>
              <Group align="flex-end" gap="xs" mb="md">
                <Text size="2xl" fw={700}>{metrics?.disk.percentage ?? 0}%</Text>
                <Text size="sm" c="dimmed" mb={5}>
                  {formatters.fileSize(metrics?.disk.used ?? 0)} / {formatters.fileSize(metrics?.disk.total ?? 0)}
                </Text>
              </Group>
              <Progress
                value={metrics?.disk.percentage ?? 0}
                color={getDiskColor(metrics?.disk.percentage ?? 0)}
                size="md"
                radius="md"
              />
              <Text size="xs" c="dimmed" mt="xs">
                I/O: ↓{formatters.fileSize(metrics?.disk.io.read ?? 0)}/s ↑{formatters.fileSize(metrics?.disk.io.write ?? 0)}/s
              </Text>
            </Card>
          )}
        </Grid.Col>

        <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
          {isLoading ? (
            <CardSkeleton height={180} />
          ) : (
            <Card withBorder>
              <Group justify="space-between" mb="xs">
                <Text size="sm" c="dimmed" tt="uppercase" fw={600}>
                  Network
                </Text>
                <ThemeIcon color="green" variant="light" size="sm">
                  <IconNetwork size={16} />
                </ThemeIcon>
              </Group>
              <Group align="flex-end" gap="xs" mb="md">
                <Text size="lg" fw={700}>
                  ↓{formatters.fileSize(metrics?.network.in ?? 0)}/s
                </Text>
                <Text size="lg" fw={700}>
                  ↑{formatters.fileSize(metrics?.network.out ?? 0)}/s
                </Text>
              </Group>
              <Progress
                value={50}
                color="green"
                size="md"
                radius="md"
              />
              <Text size="xs" c="dimmed" mt="xs">
                {metrics?.network.connections ?? 0} connections • {metrics?.network.latency ?? 0}ms latency
              </Text>
            </Card>
          )}
        </Grid.Col>
      </Grid>

      {/* System Info */}
      <Grid>
        <Grid.Col span={{ base: 12, md: 4 }}>
          <Card withBorder>
            <Group gap="xs" mb="md">
              <ThemeIcon color="blue" variant="light" size="sm">
                <IconClock size={16} />
              </ThemeIcon>
              <Text fw={500}>System Information</Text>
            </Group>
            {isLoading ? (
              <Skeleton height={100} />
            ) : (
              <Stack gap="xs">
                <Group justify="space-between">
                  <Text size="sm" c="dimmed">Uptime</Text>
                  <Text size="sm" fw={500}>{formatters.duration(metrics?.uptime ?? 0, { format: 'long' })}</Text>
                </Group>
                <Group justify="space-between">
                  <Text size="sm" c="dimmed">Processes</Text>
                  <Text size="sm" fw={500}>{metrics?.processCount ?? 0}</Text>
                </Group>
                <Group justify="space-between">
                  <Text size="sm" c="dimmed">Threads</Text>
                  <Text size="sm" fw={500}>{metrics?.threadCount ?? 0}</Text>
                </Group>
                <Group justify="space-between">
                  <Text size="sm" c="dimmed">CPU Temperature</Text>
                  <Text size="sm" fw={500}>{metrics?.cpu.temperature ? `${metrics.cpu.temperature}°C` : 'N/A'}</Text>
                </Group>
              </Stack>
            )}
          </Card>
        </Grid.Col>

        {/* Active Alerts */}
        <Grid.Col span={{ base: 12, md: 8 }}>
          <Card withBorder>
            <Group justify="space-between" mb="md">
              <Group gap="xs">
                <ThemeIcon color="orange" variant="light" size="sm">
                  <IconAlertTriangle size={16} />
                </ThemeIcon>
                <Text fw={500}>Active Alerts</Text>
              </Group>
              <Badge color="orange" variant="light">
                {alerts.filter(a => !a.resolved).length} Active
              </Badge>
            </Group>
            {isLoading ? (
              <Skeleton height={150} />
            ) : (
              <ScrollArea h={150}>
                <Stack gap="xs">
                  {alerts.filter(a => !a.resolved).length === 0 ? (
                    <Text size="sm" c="dimmed" ta="center" py="md">
                      No active alerts
                    </Text>
                  ) : (
                    alerts.filter(a => !a.resolved).map(alert => (
                      <Alert
                        key={alert.id}
                        color={getAlertColor(alert.severity)}
                        title={alert.type.toUpperCase()}
                        icon={<IconAlertTriangle size={16} />}
                      >
                        <Group justify="space-between">
                          <Text size="sm">{alert.message}</Text>
                          <Text size="xs" c="dimmed">{formatters.date(alert.timestamp)}</Text>
                        </Group>
                      </Alert>
                    ))
                  )}
                </Stack>
              </ScrollArea>
            )}
          </Card>
        </Grid.Col>
      </Grid>

      {/* Performance History Chart */}
      <Card withBorder>
        <Card.Section withBorder inheritPadding py="xs">
          <Text fw={500}>Performance History</Text>
        </Card.Section>
        <Card.Section inheritPadding py="md">
          {isLoading ? (
            <Skeleton height={300} />
          ) : history.length > 0 ? (
            <LineChart
              h={300}
              data={history}
              dataKey="timestamp"
              series={[
                { name: 'cpu', color: 'blue.6', label: 'CPU %' },
                { name: 'memory', color: 'green.6', label: 'Memory %' },
                { name: 'disk', color: 'orange.6', label: 'Disk %' },
                { name: 'responseTime', color: 'red.6', label: 'Response Time (ms)' },
              ]}
              curveType="linear"
              withLegend
              legendProps={{ verticalAlign: 'bottom', height: 50 }}
              valueFormatter={(value) => 
                typeof value === 'number' ? value.toFixed(1) : value
              }
            />
          ) : (
            <Paper p="xl" h={300} style={{ display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
              <Stack align="center" gap="md">
                <ThemeIcon size={48} variant="light" color="gray">
                  <IconActivity size={24} />
                </ThemeIcon>
                <Text size="lg" fw={500} ta="center">No Performance History Available</Text>
                <Text size="sm" c="dimmed" ta="center" maw={400}>
                  Performance metrics history is not available for the selected time range. 
                  This may be due to monitoring service configuration or recent system startup.
                </Text>
              </Stack>
            </Paper>
          )}
        </Card.Section>
      </Card>

      {/* Service Status */}
      <Card withBorder>
        <Card.Section withBorder inheritPadding py="xs">
          <Group justify="space-between">
            <Text fw={500}>Service Status</Text>
            <Group gap="xs">
              <Badge color="green" variant="light">
                {services.filter(s => s.status === 'healthy').length} Healthy
              </Badge>
              <Badge color="yellow" variant="light">
                {services.filter(s => s.status === 'degraded').length} Degraded
              </Badge>
              <Badge color="red" variant="light">
                {services.filter(s => s.status === 'down').length} Down
              </Badge>
            </Group>
          </Group>
        </Card.Section>
        <Card.Section inheritPadding py="md">
          <ScrollArea>
            <Table>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Service</Table.Th>
                  <Table.Th>Status</Table.Th>
                  <Table.Th>Uptime</Table.Th>
                  <Table.Th>CPU</Table.Th>
                  <Table.Th>Memory</Table.Th>
                  <Table.Th>Last Check</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {isLoading ? (
                  <Table.Tr>
                    <Table.Td colSpan={6}>
                      <Skeleton height={200} />
                    </Table.Td>
                  </Table.Tr>
                ) : services.length > 0 ? (
                  services.map((service) => (
                    <Table.Tr key={service.name}>
                      <Table.Td>
                        <Group gap="xs">
                          <ThemeIcon 
                            size="xs" 
                            color={getServiceStatusColor(service.status)} 
                            variant="light"
                          >
                            {service.status === 'healthy' ? <IconCircleCheck size={14} /> : <IconAlertTriangle size={14} />}
                          </ThemeIcon>
                          <Text size="sm" fw={500}>{service.name}</Text>
                        </Group>
                      </Table.Td>
                      <Table.Td>
                        <Badge color={getServiceStatusColor(service.status)} variant="light">
                          {service.status}
                        </Badge>
                      </Table.Td>
                      <Table.Td>
                        <Text size="sm">{formatters.duration(service.uptime, { format: 'compact' })}</Text>
                      </Table.Td>
                      <Table.Td>
                        <Text size="sm">{service.cpu}%</Text>
                      </Table.Td>
                      <Table.Td>
                        <Text size="sm">{formatters.fileSize(service.memory)}</Text>
                      </Table.Td>
                      <Table.Td>
                        <Text size="sm">{formatters.date(service.lastCheck, { includeTime: true })}</Text>
                      </Table.Td>
                    </Table.Tr>
                  ))
                ) : (
                  <Table.Tr>
                    <Table.Td colSpan={6}>
                      <Stack align="center" gap="md" py="xl">
                        <ThemeIcon size={32} variant="light" color="gray">
                          <IconServer size={18} />
                        </ThemeIcon>
                        <Text size="sm" c="dimmed" ta="center">
                          No service information available from monitoring system
                        </Text>
                      </Stack>
                    </Table.Td>
                  </Table.Tr>
                )}
              </Table.Tbody>
            </Table>
          </ScrollArea>
        </Card.Section>
      </Card>
    </Stack>
  );
}