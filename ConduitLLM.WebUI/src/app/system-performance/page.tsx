'use client';

import {
  Stack,
  Title,
  Text,
  Group,
  Button,
  Card,
  SimpleGrid,
  ThemeIcon,
  Select,
  Tabs,
  LoadingOverlay,
  Alert,
  Badge,
  Table,
  Progress,
  ActionIcon,
  Tooltip,
  Modal,
  RingProgress,
  Center,
  Divider,
  Code,
} from '@mantine/core';
import {
  IconCpu,
  IconRefresh,
  IconAlertCircle,
  IconCircleCheck,
  IconTrash,
  IconEye,
  IconServer as IconHardDrive,
  IconCpu as IconMemory,
  IconCheck,
  IconNetwork,
} from '@tabler/icons-react';
import { useState } from 'react';
import { useDisclosure } from '@mantine/hooks';
import { 
  useSystemMetrics,
  useResourceUsageHistory,
  useSystemProcesses,
  useServiceStatus,
  useDatabaseMetrics,
  useCacheMetrics,
  useSystemHealth,
  useRestartService,
  useClearCache,
  ProcessInfo,
  ServiceStatus
} from '@/hooks/api/useSystemPerformanceApi';
import { notifications } from '@mantine/notifications';
import { CostChart } from '@/components/charts/CostChart';

export default function SystemPerformancePage() {
  const [timeRangeValue, setTimeRangeValue] = useState('24h');
  const [selectedTab, setSelectedTab] = useState('overview');
  const [selectedProcess, setSelectedProcess] = useState<ProcessInfo | null>(null);
  const [selectedService, setSelectedService] = useState<ServiceStatus | null>(null);
  const [processOpened, { open: openProcess, close: closeProcess }] = useDisclosure(false);
  const [serviceOpened, { open: openService, close: closeService }] = useDisclosure(false);
  
  const { data: systemMetrics, isLoading: metricsLoading } = useSystemMetrics();
  const { data: resourceHistory, isLoading: historyLoading } = useResourceUsageHistory(timeRangeValue);
  const { data: processes, isLoading: processesLoading } = useSystemProcesses();
  const { data: services, isLoading: servicesLoading } = useServiceStatus();
  const { data: databaseMetrics, isLoading: dbLoading } = useDatabaseMetrics();
  const { data: cacheMetrics, isLoading: cacheLoading } = useCacheMetrics();
  const { data: systemHealth, isLoading: healthLoading } = useSystemHealth();
  
  const restartService = useRestartService();
  const clearCache = useClearCache();

  const isLoading = metricsLoading || healthLoading;

  const handleRefresh = () => {
    notifications.show({
      title: 'Refreshing Data',
      message: 'Updating system performance metrics...',
      color: 'blue',
    });
  };

  const handleRestartService = async (serviceName: string) => {
    try {
      await restartService.mutateAsync(serviceName);
      notifications.show({
        title: 'Service Restarted',
        message: `${serviceName} has been restarted successfully`,
        color: 'green',
      });
    } catch (error: unknown) {
      notifications.show({
        title: 'Restart Failed',
        message: error instanceof Error ? error.message : 'Failed to restart service',
        color: 'red',
      });
    }
  };

  const handleClearCache = async (cacheType: 'redis' | 'application' | 'all') => {
    try {
      await clearCache.mutateAsync(cacheType);
      notifications.show({
        title: 'Cache Cleared',
        message: `${cacheType} cache has been cleared successfully`,
        color: 'green',
      });
    } catch (error: unknown) {
      notifications.show({
        title: 'Clear Failed',
        message: error instanceof Error ? error.message : 'Failed to clear cache',
        color: 'red',
      });
    }
  };

  const formatBytes = (bytes: number) => {
    const units = ['B', 'KB', 'MB', 'GB', 'TB'];
    let size = bytes;
    let unitIndex = 0;
    
    while (size >= 1024 && unitIndex < units.length - 1) {
      size /= 1024;
      unitIndex++;
    }
    
    return `${size.toFixed(1)} ${units[unitIndex]}`;
  };

  const formatPercentage = (value: number) => {
    return `${value.toFixed(1)}%`;
  };

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'healthy':
      case 'running':
      case 'connected':
        return 'green';
      case 'warning':
      case 'degraded':
        return 'orange';
      case 'critical':
      case 'error':
      case 'stopped':
      case 'disconnected':
        return 'red';
      default:
        return 'gray';
    }
  };

  const getUsageColor = (usage: number) => {
    if (usage >= 90) return 'red';
    if (usage >= 75) return 'orange';
    if (usage >= 50) return 'yellow';
    return 'green';
  };

  const openProcessDetails = (process: ProcessInfo) => {
    setSelectedProcess(process);
    openProcess();
  };

  const openServiceDetails = (service: ServiceStatus) => {
    setSelectedService(service);
    openService();
  };

  const healthAlerts = systemHealth?.alerts?.filter(alert => !alert.acknowledged) || [];
  const criticalAlerts = healthAlerts.filter(alert => alert.severity === 'critical' || alert.severity === 'high');

  const systemOverviewCards = systemMetrics ? [
    {
      title: 'CPU Usage',
      value: formatPercentage(systemMetrics.cpu.usage),
      icon: IconCpu,
      color: getUsageColor(systemMetrics.cpu.usage),
      description: `${systemMetrics.cpu.cores} cores @ ${systemMetrics.cpu.frequency.toFixed(1)} GHz`,
    },
    {
      title: 'Memory Usage',
      value: formatPercentage(systemMetrics.memory.usage),
      icon: IconMemory,
      color: getUsageColor(systemMetrics.memory.usage),
      description: `${formatBytes(systemMetrics.memory.used)} / ${formatBytes(systemMetrics.memory.total)}`,
    },
    {
      title: 'Disk Usage',
      value: formatPercentage(systemMetrics.disk.usage),
      icon: IconHardDrive,
      color: getUsageColor(systemMetrics.disk.usage),
      description: `${formatBytes(systemMetrics.disk.used)} / ${formatBytes(systemMetrics.disk.total)}`,
    },
    {
      title: 'Network',
      value: formatBytes(systemMetrics.network.bandwidth),
      icon: IconNetwork,
      color: 'blue',
      description: `${systemMetrics.network.connectionsActive} active connections`,
    },
  ] : [];

  return (
    <Stack gap="xl">
      <Group justify="space-between">
        <div>
          <Title order={1}>System Performance</Title>
          <Text c="dimmed">Monitor system resources, services, and performance metrics</Text>
        </div>

        <Group>
          <Select
            value={timeRangeValue}
            onChange={(value) => setTimeRangeValue(value || '24h')}
            data={[
              { value: '1h', label: 'Last Hour' },
              { value: '24h', label: 'Last 24 Hours' },
              { value: '7d', label: 'Last 7 Days' },
              { value: '30d', label: 'Last 30 Days' },
            ]}
            w={180}
          />
          <Button
            variant="light"
            leftSection={<IconRefresh size={16} />}
            onClick={handleRefresh}
            loading={isLoading}
          >
            Refresh
          </Button>
        </Group>
      </Group>

      {/* Critical Alerts */}
      {criticalAlerts.length > 0 && (
        <Alert icon={<IconAlertCircle size={16} />} color="red" title="System Alerts">
          <Stack gap="xs">
            {criticalAlerts.slice(0, 3).map((alert) => (
              <Group key={alert.id} justify="space-between">
                <Text size="sm">
                  <Text span fw={500}>{alert.component}:</Text> {alert.message}
                </Text>
                <Badge color={alert.severity === 'critical' ? 'red' : 'orange'} variant="light">
                  {alert.severity}
                </Badge>
              </Group>
            ))}
            {criticalAlerts.length > 3 && (
              <Text size="sm" c="dimmed">
                +{criticalAlerts.length - 3} more alerts
              </Text>
            )}
          </Stack>
        </Alert>
      )}

      {/* System Overview Cards */}
      <SimpleGrid cols={{ base: 1, sm: 2, md: 4 }} spacing="lg">
        {systemOverviewCards.map((stat) => (
          <Card key={stat.title} p="md" withBorder>
            <Group justify="space-between" mb="xs">
              <Text size="xs" tt="uppercase" fw={700} c="dimmed">
                {stat.title}
              </Text>
              <ThemeIcon size="sm" variant="light" color={stat.color}>
                <stat.icon size={16} />
              </ThemeIcon>
            </Group>
            <Text fw={700} size="xl">
              {stat.value}
            </Text>
            <Text size="xs" c="dimmed" mt={4}>
              {stat.description}
            </Text>
          </Card>
        ))}
      </SimpleGrid>

      {/* Performance Dashboard */}
      <Card>
        <Tabs value={selectedTab} onChange={(value) => setSelectedTab(value || 'overview')}>
          <Tabs.List>
            <Tabs.Tab value="overview">System Overview</Tabs.Tab>
            <Tabs.Tab value="resources">Resource Usage</Tabs.Tab>
            <Tabs.Tab value="processes">Processes</Tabs.Tab>
            <Tabs.Tab value="services">Services</Tabs.Tab>
            <Tabs.Tab value="database">Database</Tabs.Tab>
            <Tabs.Tab value="cache">Cache</Tabs.Tab>
          </Tabs.List>

          <Tabs.Panel value="overview" pt="md">
            <div style={{ position: 'relative' }}>
              <LoadingOverlay visible={metricsLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
              
              <SimpleGrid cols={{ base: 1, md: 2 }} spacing="lg">
                {/* System Resource Rings */}
                <Card withBorder>
                  <Card.Section p="md" withBorder>
                    <Text fw={600}>System Resources</Text>
                  </Card.Section>
                  <Card.Section p="md">
                    <SimpleGrid cols={2} spacing="md">
                      {systemMetrics && (
                        <>
                          <Center>
                            <RingProgress
                              size={120}
                              thickness={8}
                              sections={[
                                { value: systemMetrics.cpu.usage, color: getUsageColor(systemMetrics.cpu.usage) }
                              ]}
                              label={
                                <Text size="xs" ta="center">
                                  CPU<br />
                                  {formatPercentage(systemMetrics.cpu.usage)}
                                </Text>
                              }
                            />
                          </Center>
                          <Center>
                            <RingProgress
                              size={120}
                              thickness={8}
                              sections={[
                                { value: systemMetrics.memory.usage, color: getUsageColor(systemMetrics.memory.usage) }
                              ]}
                              label={
                                <Text size="xs" ta="center">
                                  Memory<br />
                                  {formatPercentage(systemMetrics.memory.usage)}
                                </Text>
                              }
                            />
                          </Center>
                          <Center>
                            <RingProgress
                              size={120}
                              thickness={8}
                              sections={[
                                { value: systemMetrics.disk.usage, color: getUsageColor(systemMetrics.disk.usage) }
                              ]}
                              label={
                                <Text size="xs" ta="center">
                                  Disk<br />
                                  {formatPercentage(systemMetrics.disk.usage)}
                                </Text>
                              }
                            />
                          </Center>
                          <Center>
                            <RingProgress
                              size={120}
                              thickness={8}
                              sections={[
                                { value: Math.min((systemMetrics.network.connectionsActive / 500) * 100, 100), color: 'blue' }
                              ]}
                              label={
                                <Text size="xs" ta="center">
                                  Network<br />
                                  {systemMetrics.network.connectionsActive}
                                </Text>
                              }
                            />
                          </Center>
                        </>
                      )}
                    </SimpleGrid>
                  </Card.Section>
                </Card>

                {/* System Health */}
                <Card withBorder>
                  <Card.Section p="md" withBorder>
                    <Group justify="space-between">
                      <Text fw={600}>System Health</Text>
                      <Badge color={getStatusColor(systemHealth?.overall || 'unknown')} variant="light">
                        {systemHealth?.overall || 'Unknown'}
                      </Badge>
                    </Group>
                  </Card.Section>
                  <Card.Section p="md">
                    <Stack gap="sm">
                      {systemHealth?.components.map((component) => (
                        <Group key={component.name} justify="space-between">
                          <Group gap="xs">
                            <ThemeIcon size="sm" color={getStatusColor(component.status)} variant="light">
                              {component.status === 'healthy' ? 
                                <IconCheck size={12} /> : 
                                <IconAlertCircle size={12} />
                              }
                            </ThemeIcon>
                            <Text size="sm">{component.name}</Text>
                          </Group>
                          <Group gap="xs">
                            {component.responseTime && (
                              <Text size="xs" c="dimmed">
                                {component.responseTime}ms
                              </Text>
                            )}
                            <Badge size="xs" color={getStatusColor(component.status)} variant="light">
                              {component.status}
                            </Badge>
                          </Group>
                        </Group>
                      ))}
                    </Stack>
                  </Card.Section>
                </Card>
              </SimpleGrid>
            </div>
          </Tabs.Panel>

          <Tabs.Panel value="resources" pt="md">
            <div style={{ position: 'relative' }}>
              <LoadingOverlay visible={historyLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
              
              <SimpleGrid cols={{ base: 1, md: 2 }} spacing="lg">
                {resourceHistory && (
                  <>
                    <CostChart
                      data={(resourceHistory || []) as any[]}
                      title="CPU & Memory Usage"
                      type="line"
                      valueKey="cpuUsage"
                      nameKey="timestamp"
                      timeKey="timestamp"
                      onRefresh={handleRefresh}
                    />
                    
                    <CostChart
                      data={(resourceHistory || []) as any[]}
                      title="Network & Disk Usage"
                      type="line"
                      valueKey="networkUsage"
                      nameKey="timestamp"
                      timeKey="timestamp"
                      onRefresh={handleRefresh}
                    />
                    
                    <CostChart
                      data={(resourceHistory || []) as any[]}
                      title="Response Time"
                      type="line"
                      valueKey="responseTime"
                      nameKey="timestamp"
                      timeKey="timestamp"
                      onRefresh={handleRefresh}
                    />
                    
                    <CostChart
                      data={(resourceHistory || []) as any[]}
                      title="Requests per Second"
                      type="bar"
                      valueKey="requestsPerSecond"
                      nameKey="timestamp"
                      timeKey="timestamp"
                      onRefresh={handleRefresh}
                    />
                  </>
                )}
              </SimpleGrid>
            </div>
          </Tabs.Panel>

          <Tabs.Panel value="processes" pt="md">
            <div style={{ position: 'relative' }}>
              <LoadingOverlay visible={processesLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
              
              <Card withBorder>
                <Card.Section p="md" withBorder>
                  <Group justify="space-between">
                    <Text fw={600}>System Processes</Text>
                    <Badge variant="light">{processes?.length || 0} processes</Badge>
                  </Group>
                </Card.Section>
                <Card.Section>
                  <Table>
                    <Table.Thead>
                      <Table.Tr>
                        <Table.Th>PID</Table.Th>
                        <Table.Th>Name</Table.Th>
                        <Table.Th>Status</Table.Th>
                        <Table.Th>CPU</Table.Th>
                        <Table.Th>Memory</Table.Th>
                        <Table.Th>User</Table.Th>
                        <Table.Th>Actions</Table.Th>
                      </Table.Tr>
                    </Table.Thead>
                    <Table.Tbody>
                      {processes?.map((process) => (
                        <Table.Tr key={process.pid}>
                          <Table.Td>
                            <Code>{process.pid}</Code>
                          </Table.Td>
                          <Table.Td>
                            <Text fw={500}>{process.name}</Text>
                          </Table.Td>
                          <Table.Td>
                            <Badge color={getStatusColor(process.status)} variant="light" size="sm">
                              {process.status}
                            </Badge>
                          </Table.Td>
                          <Table.Td>{process.cpuUsage.toFixed(1)}%</Table.Td>
                          <Table.Td>{formatBytes(process.memoryUsage)}</Table.Td>
                          <Table.Td>
                            <Text size="sm" c="dimmed">{process.user}</Text>
                          </Table.Td>
                          <Table.Td>
                            <Tooltip label="View details">
                              <ActionIcon
                                size="sm"
                                variant="light"
                                onClick={() => openProcessDetails(process)}
                              >
                                <IconEye size={14} />
                              </ActionIcon>
                            </Tooltip>
                          </Table.Td>
                        </Table.Tr>
                      ))}
                    </Table.Tbody>
                  </Table>
                </Card.Section>
              </Card>
            </div>
          </Tabs.Panel>

          <Tabs.Panel value="services" pt="md">
            <div style={{ position: 'relative' }}>
              <LoadingOverlay visible={servicesLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
              
              <Stack gap="md">
                {services?.map((service) => (
                  <Card key={service.name} withBorder p="md">
                    <Group justify="space-between" mb="md">
                      <Group gap="md">
                        <ThemeIcon color={getStatusColor(service.status)} variant="light">
                          {service.status === 'running' ? 
                            <IconCircleCheck size={20} /> : 
                            <IconAlertCircle size={20} />
                          }
                        </ThemeIcon>
                        <div>
                          <Text fw={600} size="lg">{service.name}</Text>
                          <Group gap="xs">
                            <Badge color={getStatusColor(service.status)} variant="light">
                              {service.status}
                            </Badge>
                            {service.port && (
                              <Badge variant="light" color="gray">
                                Port {service.port}
                              </Badge>
                            )}
                          </Group>
                        </div>
                      </Group>
                      
                      <Group gap="xs">
                        <Tooltip label="View details">
                          <ActionIcon
                            variant="light"
                            onClick={() => openServiceDetails(service)}
                          >
                            <IconEye size={16} />
                          </ActionIcon>
                        </Tooltip>
                        <Tooltip label="Restart service">
                          <ActionIcon
                            variant="light"
                            color="orange"
                            onClick={() => handleRestartService(service.name)}
                            loading={restartService.isPending}
                          >
                            <IconRefresh size={16} />
                          </ActionIcon>
                        </Tooltip>
                      </Group>
                    </Group>

                    <SimpleGrid cols={{ base: 2, sm: 4 }} spacing="md">
                      <div>
                        <Text size="xs" c="dimmed" mb={4}>Uptime</Text>
                        <Text fw={500} size="sm">{service.uptime}</Text>
                      </div>
                      <div>
                        <Text size="xs" c="dimmed" mb={4}>Memory</Text>
                        <Text fw={500} size="sm">{formatBytes(service.memoryUsage)}</Text>
                      </div>
                      <div>
                        <Text size="xs" c="dimmed" mb={4}>CPU</Text>
                        <Text fw={500} size="sm">{service.cpuUsage.toFixed(1)}%</Text>
                      </div>
                      <div>
                        <Text size="xs" c="dimmed" mb={4}>Health</Text>
                        <Group gap="xs">
                          <Badge color={getStatusColor(service.healthCheck.status)} variant="light" size="sm">
                            {service.healthCheck.status}
                          </Badge>
                          {service.healthCheck.responseTime && (
                            <Text size="xs" c="dimmed">
                              {service.healthCheck.responseTime}ms
                            </Text>
                          )}
                        </Group>
                      </div>
                    </SimpleGrid>
                  </Card>
                ))}
              </Stack>
            </div>
          </Tabs.Panel>

          <Tabs.Panel value="database" pt="md">
            <div style={{ position: 'relative' }}>
              <LoadingOverlay visible={dbLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
              
              <SimpleGrid cols={{ base: 1, md: 2 }} spacing="lg">
                {databaseMetrics && (
                  <>
                    <Card withBorder>
                      <Card.Section p="md" withBorder>
                        <Text fw={600}>Connection Pool</Text>
                      </Card.Section>
                      <Card.Section p="md">
                        <Stack gap="md">
                          <div>
                            <Group justify="space-between" mb="xs">
                              <Text size="sm">Pool Usage</Text>
                              <Text size="sm">{formatPercentage(databaseMetrics.connectionPool.usage)}</Text>
                            </Group>
                            <Progress
                              value={databaseMetrics.connectionPool.usage}
                              color={getUsageColor(databaseMetrics.connectionPool.usage)}
                              size="lg"
                            />
                          </div>
                          
                          <SimpleGrid cols={3} spacing="md">
                            <div>
                              <Text size="xs" c="dimmed" mb={4}>Active</Text>
                              <Text fw={500}>{databaseMetrics.connectionPool.active}</Text>
                            </div>
                            <div>
                              <Text size="xs" c="dimmed" mb={4}>Idle</Text>
                              <Text fw={500}>{databaseMetrics.connectionPool.idle}</Text>
                            </div>
                            <div>
                              <Text size="xs" c="dimmed" mb={4}>Max</Text>
                              <Text fw={500}>{databaseMetrics.connectionPool.max}</Text>
                            </div>
                          </SimpleGrid>
                        </Stack>
                      </Card.Section>
                    </Card>

                    <Card withBorder>
                      <Card.Section p="md" withBorder>
                        <Text fw={600}>Query Performance</Text>
                      </Card.Section>
                      <Card.Section p="md">
                        <SimpleGrid cols={2} spacing="md">
                          <div>
                            <Text size="xs" c="dimmed" mb={4}>Total Queries</Text>
                            <Text fw={500} size="lg">{databaseMetrics.queries.total.toLocaleString()}</Text>
                          </div>
                          <div>
                            <Text size="xs" c="dimmed" mb={4}>Average Time</Text>
                            <Text fw={500} size="lg">{databaseMetrics.queries.averageTime.toFixed(1)}ms</Text>
                          </div>
                          <div>
                            <Text size="xs" c="dimmed" mb={4}>Success Rate</Text>
                            <Text fw={500} size="lg">
                              {((databaseMetrics.queries.successful / databaseMetrics.queries.total) * 100).toFixed(1)}%
                            </Text>
                          </div>
                          <div>
                            <Text size="xs" c="dimmed" mb={4}>Slow Queries</Text>
                            <Text fw={500} size="lg">{databaseMetrics.queries.slowQueries}</Text>
                          </div>
                        </SimpleGrid>
                      </Card.Section>
                    </Card>

                    <Card withBorder>
                      <Card.Section p="md" withBorder>
                        <Text fw={600}>Storage</Text>
                      </Card.Section>
                      <Card.Section p="md">
                        <SimpleGrid cols={2} spacing="md">
                          <div>
                            <Text size="xs" c="dimmed" mb={4}>Database Size</Text>
                            <Text fw={500}>{formatBytes(databaseMetrics.storage.size)}</Text>
                          </div>
                          <div>
                            <Text size="xs" c="dimmed" mb={4}>Used Space</Text>
                            <Text fw={500}>{formatBytes(databaseMetrics.storage.used)}</Text>
                          </div>
                          <div>
                            <Text size="xs" c="dimmed" mb={4}>Index Size</Text>
                            <Text fw={500}>{formatBytes(databaseMetrics.storage.indexSize)}</Text>
                          </div>
                          <div>
                            <Text size="xs" c="dimmed" mb={4}>Growth Rate</Text>
                            <Text fw={500}>{formatBytes(databaseMetrics.storage.growth)}/day</Text>
                          </div>
                        </SimpleGrid>
                      </Card.Section>
                    </Card>

                    <Card withBorder>
                      <Card.Section p="md" withBorder>
                        <Text fw={600}>Locks & Replication</Text>
                      </Card.Section>
                      <Card.Section p="md">
                        <SimpleGrid cols={2} spacing="md">
                          <div>
                            <Text size="xs" c="dimmed" mb={4}>Active Locks</Text>
                            <Text fw={500}>{databaseMetrics.locks.active}</Text>
                          </div>
                          <div>
                            <Text size="xs" c="dimmed" mb={4}>Waiting Locks</Text>
                            <Text fw={500}>{databaseMetrics.locks.waiting}</Text>
                          </div>
                          <div>
                            <Text size="xs" c="dimmed" mb={4}>Replication Status</Text>
                            <Badge color={getStatusColor(databaseMetrics.replication.status)} variant="light">
                              {databaseMetrics.replication.status}
                            </Badge>
                          </div>
                          <div>
                            <Text size="xs" c="dimmed" mb={4}>Replication Lag</Text>
                            <Text fw={500}>{databaseMetrics.replication.lag.toFixed(1)}ms</Text>
                          </div>
                        </SimpleGrid>
                      </Card.Section>
                    </Card>
                  </>
                )}
              </SimpleGrid>
            </div>
          </Tabs.Panel>

          <Tabs.Panel value="cache" pt="md">
            <div style={{ position: 'relative' }}>
              <LoadingOverlay visible={cacheLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
              
              <SimpleGrid cols={{ base: 1, md: 2 }} spacing="lg">
                {cacheMetrics && (
                  <>
                    <Card withBorder>
                      <Card.Section p="md" withBorder>
                        <Group justify="space-between">
                          <Text fw={600}>Redis Cache</Text>
                          <Group gap="xs">
                            <Badge color={getStatusColor(cacheMetrics.redis.status)} variant="light">
                              {cacheMetrics.redis.status}
                            </Badge>
                            <Button
                              size="xs"
                              variant="light"
                              leftSection={<IconTrash size={14} />}
                              onClick={() => handleClearCache('redis')}
                              loading={clearCache.isPending}
                            >
                              Clear
                            </Button>
                          </Group>
                        </Group>
                      </Card.Section>
                      <Card.Section p="md">
                        <Stack gap="md">
                          <div>
                            <Group justify="space-between" mb="xs">
                              <Text size="sm">Memory Usage</Text>
                              <Text size="sm">{formatPercentage(cacheMetrics.redis.memory.usage)}</Text>
                            </Group>
                            <Progress
                              value={cacheMetrics.redis.memory.usage}
                              color={getUsageColor(cacheMetrics.redis.memory.usage)}
                              size="lg"
                            />
                            <Text size="xs" c="dimmed" mt={4}>
                              {formatBytes(cacheMetrics.redis.memory.used)} / {formatBytes(cacheMetrics.redis.memory.max)}
                            </Text>
                          </div>
                          
                          <SimpleGrid cols={2} spacing="md">
                            <div>
                              <Text size="xs" c="dimmed" mb={4}>Hit Rate</Text>
                              <Text fw={500} size="lg">{formatPercentage(cacheMetrics.redis.operations.hitRate)}</Text>
                            </div>
                            <div>
                              <Text size="xs" c="dimmed" mb={4}>Keys</Text>
                              <Text fw={500} size="lg">{cacheMetrics.redis.keyspace.keys.toLocaleString()}</Text>
                            </div>
                            <div>
                              <Text size="xs" c="dimmed" mb={4}>Connections</Text>
                              <Text fw={500} size="lg">{cacheMetrics.redis.connections.active}</Text>
                            </div>
                            <div>
                              <Text size="xs" c="dimmed" mb={4}>Commands</Text>
                              <Text fw={500} size="lg">{cacheMetrics.redis.operations.commands.toLocaleString()}</Text>
                            </div>
                          </SimpleGrid>
                        </Stack>
                      </Card.Section>
                    </Card>

                    <Card withBorder>
                      <Card.Section p="md" withBorder>
                        <Group justify="space-between">
                          <Text fw={600}>Application Cache</Text>
                          <Button
                            size="xs"
                            variant="light"
                            leftSection={<IconTrash size={14} />}
                            onClick={() => handleClearCache('application')}
                            loading={clearCache.isPending}
                          >
                            Clear
                          </Button>
                        </Group>
                      </Card.Section>
                      <Card.Section p="md">
                        <SimpleGrid cols={2} spacing="md">
                          <div>
                            <Text size="xs" c="dimmed" mb={4}>Cache Size</Text>
                            <Text fw={500} size="lg">{formatBytes(cacheMetrics.applicationCache.size)}</Text>
                          </div>
                          <div>
                            <Text size="xs" c="dimmed" mb={4}>Entries</Text>
                            <Text fw={500} size="lg">{cacheMetrics.applicationCache.entries.toLocaleString()}</Text>
                          </div>
                          <div>
                            <Text size="xs" c="dimmed" mb={4}>Hit Rate</Text>
                            <Text fw={500} size="lg">{formatPercentage(cacheMetrics.applicationCache.hitRate)}</Text>
                          </div>
                          <div>
                            <Text size="xs" c="dimmed" mb={4}>Evictions</Text>
                            <Text fw={500} size="lg">{cacheMetrics.applicationCache.evictions}</Text>
                          </div>
                        </SimpleGrid>
                      </Card.Section>
                    </Card>
                  </>
                )}
              </SimpleGrid>
            </div>
          </Tabs.Panel>
        </Tabs>
      </Card>

      {/* Process Details Modal */}
      <Modal
        opened={processOpened}
        onClose={closeProcess}
        title="Process Details"
        size="md"
      >
        {selectedProcess && (
          <Stack gap="md">
            <Group justify="space-between">
              <div>
                <Text fw={600} size="lg">{selectedProcess.name}</Text>
                <Badge color={getStatusColor(selectedProcess.status)} variant="light">
                  {selectedProcess.status}
                </Badge>
              </div>
              <Code>{selectedProcess.pid}</Code>
            </Group>
            
            <SimpleGrid cols={2} spacing="lg">
              <div>
                <Text size="sm" c="dimmed" mb="xs">CPU Usage</Text>
                <Text fw={600}>{selectedProcess.cpuUsage.toFixed(1)}%</Text>
              </div>
              <div>
                <Text size="sm" c="dimmed" mb="xs">Memory Usage</Text>
                <Text fw={600}>{formatBytes(selectedProcess.memoryUsage)}</Text>
              </div>
              <div>
                <Text size="sm" c="dimmed" mb="xs">User</Text>
                <Text fw={600}>{selectedProcess.user}</Text>
              </div>
              <div>
                <Text size="sm" c="dimmed" mb="xs">Start Time</Text>
                <Text fw={600}>{new Date(selectedProcess.startTime).toLocaleString()}</Text>
              </div>
            </SimpleGrid>
            
            <div>
              <Text size="sm" c="dimmed" mb="xs">Command</Text>
              <Code block>{selectedProcess.command}</Code>
            </div>
          </Stack>
        )}
      </Modal>

      {/* Service Details Modal */}
      <Modal
        opened={serviceOpened}
        onClose={closeService}
        title="Service Details"
        size="lg"
      >
        {selectedService && (
          <Stack gap="md">
            <Group justify="space-between">
              <div>
                <Text fw={600} size="lg">{selectedService.name}</Text>
                <Group gap="xs">
                  <Badge color={getStatusColor(selectedService.status)} variant="light">
                    {selectedService.status}
                  </Badge>
                  {selectedService.port && (
                    <Badge variant="light" color="gray">
                      Port {selectedService.port}
                    </Badge>
                  )}
                </Group>
              </div>
              
              <Group gap="xs">
                <Button
                  size="xs"
                  variant="light"
                  color="orange"
                  leftSection={<IconRefresh size={14} />}
                  onClick={() => handleRestartService(selectedService.name)}
                  loading={restartService.isPending}
                >
                  Restart
                </Button>
              </Group>
            </Group>
            
            <SimpleGrid cols={2} spacing="lg">
              <div>
                <Text size="sm" c="dimmed" mb="xs">Status</Text>
                <Badge color={getStatusColor(selectedService.status)} variant="light">
                  {selectedService.status}
                </Badge>
              </div>
              <div>
                <Text size="sm" c="dimmed" mb="xs">Uptime</Text>
                <Text fw={600}>{selectedService.uptime}</Text>
              </div>
              <div>
                <Text size="sm" c="dimmed" mb="xs">Memory Usage</Text>
                <Text fw={600}>{formatBytes(selectedService.memoryUsage)}</Text>
              </div>
              <div>
                <Text size="sm" c="dimmed" mb="xs">CPU Usage</Text>
                <Text fw={600}>{selectedService.cpuUsage.toFixed(1)}%</Text>
              </div>
              <div>
                <Text size="sm" c="dimmed" mb="xs">Restart Count</Text>
                <Text fw={600}>{selectedService.restartCount}</Text>
              </div>
              {selectedService.lastRestart && (
                <div>
                  <Text size="sm" c="dimmed" mb="xs">Last Restart</Text>
                  <Text fw={600}>{new Date(selectedService.lastRestart).toLocaleString()}</Text>
                </div>
              )}
            </SimpleGrid>
            
            <Divider />
            
            <div>
              <Text fw={500} mb="md">Health Check</Text>
              <Group justify="space-between">
                <Group gap="xs">
                  <Badge color={getStatusColor(selectedService.healthCheck.status)} variant="light">
                    {selectedService.healthCheck.status}
                  </Badge>
                  {selectedService.healthCheck.responseTime && (
                    <Text size="sm" c="dimmed">
                      {selectedService.healthCheck.responseTime}ms response time
                    </Text>
                  )}
                </Group>
                <Text size="sm" c="dimmed">
                  Last check: {new Date(selectedService.healthCheck.lastCheck).toLocaleString()}
                </Text>
              </Group>
            </div>
          </Stack>
        )}
      </Modal>
    </Stack>
  );
}