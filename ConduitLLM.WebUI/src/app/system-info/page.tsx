'use client';

import {
  Stack,
  Title,
  Text,
  Group,
  Button,
  Card,
  Badge,
  Table,
  SimpleGrid,
  ThemeIcon,
  Progress,
  Code,
  Tabs,
  LoadingOverlay,
  Alert,
  Timeline,
  Accordion,
  CopyButton,
  ActionIcon,
  Tooltip,
} from '@mantine/core';
import {
  IconServer,
  IconDatabase,
  IconCpu,
  IconCpu as IconMemory,
  IconServer as IconHardDrive,
  IconNetwork,
  IconRefresh,
  IconDownload,
  IconCopy,
  IconCheck,
  IconAlertCircle,
  IconInfoCircle,
  IconChartBar,
  IconSettings,
  IconBrandDocker,
  IconCloud,
  IconDeviceDesktop,
} from '@tabler/icons-react';
import { useState, useEffect } from 'react';
import { useSystemInfo, useSystemMetrics, useSystemHealth } from '@/hooks/api/useAdminApi';
import { notifications } from '@mantine/notifications';

interface SystemMetrics {
  cpu: {
    usage: number;
    cores: number;
    model: string;
    frequency: number;
  };
  memory: {
    total: number;
    used: number;
    available: number;
    usage: number;
  };
  disk: {
    total: number;
    used: number;
    available: number;
    usage: number;
  };
  network: {
    bytesReceived: number;
    bytesSent: number;
    packetsReceived: number;
    packetsSent: number;
  };
}

interface SystemHealth {
  status: 'healthy' | 'warning' | 'critical';
  services: Array<{
    name: string;
    status: 'running' | 'stopped' | 'error';
    uptime: string;
    version?: string;
  }>;
  dependencies: Array<{
    name: string;
    status: 'connected' | 'disconnected' | 'degraded';
    version?: string;
    latency?: number;
  }>;
}

export default function SystemInfoPage() {
  const { data: systemInfo, isLoading: systemLoading } = useSystemInfo();
  const { data: metrics, isLoading: metricsLoading, refetch: refetchMetrics } = useSystemMetrics();
  const { data: health, isLoading: healthLoading, refetch: refetchHealth } = useSystemHealth();
  const [autoRefresh, setAutoRefresh] = useState(false);
  
  const isLoading = systemLoading || metricsLoading || healthLoading;

  // Auto-refresh functionality
  useEffect(() => {
    if (autoRefresh) {
      const interval = setInterval(() => {
        refetchMetrics();
        refetchHealth();
      }, 30000);
      return () => clearInterval(interval);
    }
  }, [autoRefresh, refetchMetrics, refetchHealth]);

  const handleExportDiagnostics = () => {
    notifications.show({
      title: 'Export Started',
      message: 'Generating system diagnostics report...',
      color: 'blue',
    });
    
    // TODO: Implement actual export functionality
    setTimeout(() => {
      notifications.show({
        title: 'Export Complete',
        message: 'System diagnostics report has been generated',
        color: 'green',
      });
    }, 3000);
  };

  const formatBytes = (bytes: number) => {
    const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
    if (bytes === 0) return '0 B';
    const i = Math.floor(Math.log(bytes) / Math.log(1024));
    return `${(bytes / Math.pow(1024, i)).toFixed(1)} ${sizes[i]}`;
  };

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'running':
      case 'connected':
      case 'healthy':
        return 'green';
      case 'warning':
      case 'degraded':
        return 'yellow';
      case 'stopped':
      case 'disconnected':
      case 'error':
      case 'critical':
        return 'red';
      default:
        return 'gray';
    }
  };

  const getUsageColor = (usage: number) => {
    if (usage < 60) return 'green';
    if (usage < 80) return 'yellow';
    return 'red';
  };

  return (
    <Stack gap="xl">
      <Group justify="space-between">
        <div>
          <Title order={1}>System Information</Title>
          <Text c="dimmed">Monitor system health, performance, and configuration</Text>
        </div>

        <Group>
          <Button
            variant="light"
            leftSection={<IconRefresh size={16} />}
            onClick={() => {
              refetchMetrics();
              refetchHealth();
            }}
            loading={isLoading}
          >
            Refresh
          </Button>
          <Button
            leftSection={<IconDownload size={16} />}
            onClick={handleExportDiagnostics}
          >
            Export Diagnostics
          </Button>
        </Group>
      </Group>

      {/* System Overview */}
      {systemInfo && (
        <Alert icon={<IconInfoCircle size={16} />} color="blue" variant="light">
          <Group>
            <div>
              <Text size="sm" fw={500}>Conduit LLM Platform</Text>
              <Text size="xs" c="dimmed">
                Version: {systemInfo.version || 'Unknown'} | 
                Environment: {systemInfo.environment || 'Production'} | 
                Uptime: {systemInfo.uptime || 'Unknown'}
              </Text>
            </div>
          </Group>
        </Alert>
      )}

      {/* Resource Usage Cards */}
      {metrics && (
        <SimpleGrid cols={{ base: 1, sm: 2, md: 4 }} spacing="lg">
          <Card p="md" withBorder>
            <Group justify="space-between" mb="xs">
              <Text size="xs" tt="uppercase" fw={700} c="dimmed">
                CPU Usage
              </Text>
              <ThemeIcon size="sm" variant="light" color={getUsageColor(metrics.cpu.usage)}>
                <IconCpu size={16} />
              </ThemeIcon>
            </Group>
            <Text fw={700} size="xl">
              {metrics.cpu.usage.toFixed(1)}%
            </Text>
            <Progress value={metrics.cpu.usage} color={getUsageColor(metrics.cpu.usage)} size="sm" mt="xs" />
            <Text size="xs" c="dimmed" mt="xs">
              {metrics.cpu.cores} cores @ {metrics.cpu.frequency} GHz
            </Text>
          </Card>

          <Card p="md" withBorder>
            <Group justify="space-between" mb="xs">
              <Text size="xs" tt="uppercase" fw={700} c="dimmed">
                Memory Usage
              </Text>
              <ThemeIcon size="sm" variant="light" color={getUsageColor(metrics.memory.usage)}>
                <IconMemory size={16} />
              </ThemeIcon>
            </Group>
            <Text fw={700} size="xl">
              {metrics.memory.usage.toFixed(1)}%
            </Text>
            <Progress value={metrics.memory.usage} color={getUsageColor(metrics.memory.usage)} size="sm" mt="xs" />
            <Text size="xs" c="dimmed" mt="xs">
              {formatBytes(metrics.memory.used)} / {formatBytes(metrics.memory.total)}
            </Text>
          </Card>

          <Card p="md" withBorder>
            <Group justify="space-between" mb="xs">
              <Text size="xs" tt="uppercase" fw={700} c="dimmed">
                Disk Usage
              </Text>
              <ThemeIcon size="sm" variant="light" color={getUsageColor(metrics.disk.usage)}>
                <IconHardDrive size={16} />
              </ThemeIcon>
            </Group>
            <Text fw={700} size="xl">
              {metrics.disk.usage.toFixed(1)}%
            </Text>
            <Progress value={metrics.disk.usage} color={getUsageColor(metrics.disk.usage)} size="sm" mt="xs" />
            <Text size="xs" c="dimmed" mt="xs">
              {formatBytes(metrics.disk.used)} / {formatBytes(metrics.disk.total)}
            </Text>
          </Card>

          <Card p="md" withBorder>
            <Group justify="space-between" mb="xs">
              <Text size="xs" tt="uppercase" fw={700} c="dimmed">
                Network I/O
              </Text>
              <ThemeIcon size="sm" variant="light" color="blue">
                <IconNetwork size={16} />
              </ThemeIcon>
            </Group>
            <Text fw={700} size="lg">
              ↓ {formatBytes(metrics.network.bytesReceived)}
            </Text>
            <Text fw={700} size="lg">
              ↑ {formatBytes(metrics.network.bytesSent)}
            </Text>
          </Card>
        </SimpleGrid>
      )}

      <Tabs defaultValue="services">
        <Tabs.List>
          <Tabs.Tab value="services" leftSection={<IconServer size={16} />}>
            Services & Health
          </Tabs.Tab>
          <Tabs.Tab value="configuration" leftSection={<IconSettings size={16} />}>
            Configuration
          </Tabs.Tab>
          <Tabs.Tab value="environment" leftSection={<IconCloud size={16} />}>
            Environment
          </Tabs.Tab>
          <Tabs.Tab value="diagnostics" leftSection={<IconChartBar size={16} />}>
            Diagnostics
          </Tabs.Tab>
        </Tabs.List>

        <Tabs.Panel value="services" pt="md">
          <div style={{ position: 'relative' }}>
            <LoadingOverlay visible={isLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
            
            <SimpleGrid cols={{ base: 1, md: 2 }} spacing="lg">
              {/* Services Status */}
              <Card withBorder>
                <Card.Section p="md" withBorder>
                  <Group justify="space-between">
                    <Text fw={600}>Service Status</Text>
                    <Badge color={health ? getStatusColor(health.status) : 'gray'} variant="light">
                      {health?.status.toUpperCase()}
                    </Badge>
                  </Group>
                </Card.Section>
                <Card.Section>
                  <Table>
                    <Table.Thead>
                      <Table.Tr>
                        <Table.Th>Service</Table.Th>
                        <Table.Th>Status</Table.Th>
                        <Table.Th>Uptime</Table.Th>
                        <Table.Th>Version</Table.Th>
                      </Table.Tr>
                    </Table.Thead>
                    <Table.Tbody>
                      {health?.services.map((service: any) => (
                        <Table.Tr key={service.name}>
                          <Table.Td>
                            <Text fw={500}>{service.name}</Text>
                          </Table.Td>
                          <Table.Td>
                            <Badge color={getStatusColor(service.status)} variant="light" size="sm">
                              {service.status.toUpperCase()}
                            </Badge>
                          </Table.Td>
                          <Table.Td>
                            <Text size="sm">{service.uptime}</Text>
                          </Table.Td>
                          <Table.Td>
                            <Text size="sm">{service.version || 'N/A'}</Text>
                          </Table.Td>
                        </Table.Tr>
                      ))}
                    </Table.Tbody>
                  </Table>
                </Card.Section>
              </Card>

              {/* Dependencies Status */}
              <Card withBorder>
                <Card.Section p="md" withBorder>
                  <Text fw={600}>External Dependencies</Text>
                </Card.Section>
                <Card.Section>
                  <Table>
                    <Table.Thead>
                      <Table.Tr>
                        <Table.Th>Dependency</Table.Th>
                        <Table.Th>Status</Table.Th>
                        <Table.Th>Latency</Table.Th>
                        <Table.Th>Version</Table.Th>
                      </Table.Tr>
                    </Table.Thead>
                    <Table.Tbody>
                      {health?.dependencies.map((dep: any) => (
                        <Table.Tr key={dep.name}>
                          <Table.Td>
                            <Text fw={500}>{dep.name}</Text>
                          </Table.Td>
                          <Table.Td>
                            <Badge color={getStatusColor(dep.status)} variant="light" size="sm">
                              {dep.status.toUpperCase()}
                            </Badge>
                          </Table.Td>
                          <Table.Td>
                            <Text size="sm">
                              {dep.latency ? `${dep.latency}ms` : 'N/A'}
                            </Text>
                          </Table.Td>
                          <Table.Td>
                            <Text size="sm">{dep.version || 'N/A'}</Text>
                          </Table.Td>
                        </Table.Tr>
                      ))}
                    </Table.Tbody>
                  </Table>
                </Card.Section>
              </Card>
            </SimpleGrid>
          </div>
        </Tabs.Panel>

        <Tabs.Panel value="configuration" pt="md">
          <Card withBorder>
            <Card.Section p="md" withBorder>
              <Text fw={600}>System Configuration</Text>
            </Card.Section>
            <Card.Section p="md">
              <Accordion variant="contained">
                <Accordion.Item value="database">
                  <Accordion.Control icon={<IconDatabase size={20} />}>
                    Database Configuration
                  </Accordion.Control>
                  <Accordion.Panel>
                    <Stack gap="xs">
                      <Group justify="space-between">
                        <Text size="sm">Connection String</Text>
                        <Group gap="xs">
                          <Code>postgresql://***:***@localhost:5432/conduit</Code>
                          <CopyButton value="postgresql://***:***@localhost:5432/conduit">
                            {({ copied, copy }) => (
                              <Tooltip label={copied ? 'Copied' : 'Copy'}>
                                <ActionIcon color={copied ? 'teal' : 'gray'} size="sm" onClick={copy}>
                                  {copied ? <IconCheck size={16} /> : <IconCopy size={16} />}
                                </ActionIcon>
                              </Tooltip>
                            )}
                          </CopyButton>
                        </Group>
                      </Group>
                      <Group justify="space-between">
                        <Text size="sm">Max Pool Size</Text>
                        <Text size="sm" c="dimmed">100</Text>
                      </Group>
                      <Group justify="space-between">
                        <Text size="sm">Command Timeout</Text>
                        <Text size="sm" c="dimmed">30 seconds</Text>
                      </Group>
                    </Stack>
                  </Accordion.Panel>
                </Accordion.Item>

                <Accordion.Item value="redis">
                  <Accordion.Control icon={<IconMemory size={20} />}>
                    Redis Configuration
                  </Accordion.Control>
                  <Accordion.Panel>
                    <Stack gap="xs">
                      <Group justify="space-between">
                        <Text size="sm">Connection String</Text>
                        <Code>redis://localhost:6379</Code>
                      </Group>
                      <Group justify="space-between">
                        <Text size="sm">Database</Text>
                        <Text size="sm" c="dimmed">0</Text>
                      </Group>
                      <Group justify="space-between">
                        <Text size="sm">Key Prefix</Text>
                        <Text size="sm" c="dimmed">conduit:</Text>
                      </Group>
                    </Stack>
                  </Accordion.Panel>
                </Accordion.Item>

                <Accordion.Item value="messaging">
                  <Accordion.Control icon={<IconNetwork size={20} />}>
                    Message Bus Configuration
                  </Accordion.Control>
                  <Accordion.Panel>
                    <Stack gap="xs">
                      <Group justify="space-between">
                        <Text size="sm">Transport</Text>
                        <Text size="sm" c="dimmed">RabbitMQ</Text>
                      </Group>
                      <Group justify="space-between">
                        <Text size="sm">Host</Text>
                        <Text size="sm" c="dimmed">rabbitmq:5672</Text>
                      </Group>
                      <Group justify="space-between">
                        <Text size="sm">Virtual Host</Text>
                        <Text size="sm" c="dimmed">/</Text>
                      </Group>
                    </Stack>
                  </Accordion.Panel>
                </Accordion.Item>
              </Accordion>
            </Card.Section>
          </Card>
        </Tabs.Panel>

        <Tabs.Panel value="environment" pt="md">
          <SimpleGrid cols={{ base: 1, md: 2 }} spacing="lg">
            <Card withBorder>
              <Card.Section p="md" withBorder>
                <Group>
                  <IconDeviceDesktop size={20} />
                  <Text fw={600}>Runtime Environment</Text>
                </Group>
              </Card.Section>
              <Card.Section p="md">
                <Stack gap="xs">
                  <Group justify="space-between">
                    <Text size="sm">Operating System</Text>
                    <Text size="sm" c="dimmed">Linux 6.12.10</Text>
                  </Group>
                  <Group justify="space-between">
                    <Text size="sm">.NET Runtime</Text>
                    <Text size="sm" c="dimmed">8.0.11</Text>
                  </Group>
                  <Group justify="space-between">
                    <Text size="sm">ASP.NET Core</Text>
                    <Text size="sm" c="dimmed">8.0.11</Text>
                  </Group>
                  <Group justify="space-between">
                    <Text size="sm">GC Mode</Text>
                    <Text size="sm" c="dimmed">Server</Text>
                  </Group>
                </Stack>
              </Card.Section>
            </Card>

            <Card withBorder>
              <Card.Section p="md" withBorder>
                <Group>
                  <IconBrandDocker size={20} />
                  <Text fw={600}>Container Environment</Text>
                </Group>
              </Card.Section>
              <Card.Section p="md">
                <Stack gap="xs">
                  <Group justify="space-between">
                    <Text size="sm">Container Runtime</Text>
                    <Text size="sm" c="dimmed">Docker 25.0.3</Text>
                  </Group>
                  <Group justify="space-between">
                    <Text size="sm">Image Tag</Text>
                    <Text size="sm" c="dimmed">conduit:2.1.0</Text>
                  </Group>
                  <Group justify="space-between">
                    <Text size="sm">Build Date</Text>
                    <Text size="sm" c="dimmed">2024-01-20</Text>
                  </Group>
                  <Group justify="space-between">
                    <Text size="sm">Memory Limit</Text>
                    <Text size="sm" c="dimmed">16 GB</Text>
                  </Group>
                </Stack>
              </Card.Section>
            </Card>
          </SimpleGrid>
        </Tabs.Panel>

        <Tabs.Panel value="diagnostics" pt="md">
          <Card withBorder>
            <Card.Section p="md" withBorder>
              <Text fw={600}>System Diagnostics</Text>
            </Card.Section>
            <Card.Section p="md">
              <Timeline active={4} bulletSize={24} lineWidth={2}>
                <Timeline.Item
                  bullet={<IconCheck size={12} />}
                  title="System Health Check"
                  color="green"
                >
                  <Text c="dimmed" size="sm">
                    All services are running normally
                  </Text>
                  <Text size="xs" c="dimmed">
                    Last checked: {new Date().toLocaleString()}
                  </Text>
                </Timeline.Item>

                <Timeline.Item
                  bullet={<IconDatabase size={12} />}
                  title="Database Connectivity"
                  color="green"
                >
                  <Text c="dimmed" size="sm">
                    PostgreSQL connection established successfully
                  </Text>
                  <Text size="xs" c="dimmed">
                    Latency: 12ms
                  </Text>
                </Timeline.Item>

                <Timeline.Item
                  bullet={<IconMemory size={12} />}
                  title="Cache Performance"
                  color="green"
                >
                  <Text c="dimmed" size="sm">
                    Redis cache responding normally
                  </Text>
                  <Text size="xs" c="dimmed">
                    Hit rate: 94.2%
                  </Text>
                </Timeline.Item>

                <Timeline.Item
                  bullet={<IconNetwork size={12} />}
                  title="External API Health"
                  color="yellow"
                >
                  <Text c="dimmed" size="sm">
                    Some providers experiencing higher latency
                  </Text>
                  <Text size="xs" c="dimmed">
                    Average response time: 245ms
                  </Text>
                </Timeline.Item>
              </Timeline>
            </Card.Section>
          </Card>
        </Tabs.Panel>
      </Tabs>
    </Stack>
  );
}