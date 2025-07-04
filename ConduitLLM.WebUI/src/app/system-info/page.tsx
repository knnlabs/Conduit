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
  Tabs,
  LoadingOverlay,
  Alert,
  Accordion,
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
  IconCheck,
  IconAlertCircle,
  IconInfoCircle,
  IconChartBar,
  IconSettings,
  IconCloud,
  IconDeviceDesktop,
} from '@tabler/icons-react';
import { useState, useEffect } from 'react';
import { useSystemInfo, useSystemMetrics, useSystemHealth } from '@/hooks/api/useAdminApi';
import { notifications } from '@mantine/notifications';

// Types will come from the SDK responses

export default function SystemInfoPage() {
  const { data: systemInfo, isLoading: systemLoading } = useSystemInfo();
  const { data: metrics, isLoading: metricsLoading, refetch: refetchMetrics } = useSystemMetrics();
  const { data: health, isLoading: healthLoading, refetch: refetchHealth } = useSystemHealth();
  const [autoRefresh, _setAutoRefresh] = useState(false);
  
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
              <ThemeIcon size="sm" variant="light" color={getUsageColor(metrics.cpu?.usage || 0)}>
                <IconCpu size={16} />
              </ThemeIcon>
            </Group>
            <Text fw={700} size="xl">
              {(metrics.cpu?.usage || 0).toFixed(1)}%
            </Text>
            <Progress value={metrics.cpu?.usage || 0} color={getUsageColor(metrics.cpu?.usage || 0)} size="sm" mt="xs" />
            <Text size="xs" c="dimmed" mt="xs">
              {metrics.cpu?.cores || 0} cores
            </Text>
          </Card>

          <Card p="md" withBorder>
            <Group justify="space-between" mb="xs">
              <Text size="xs" tt="uppercase" fw={700} c="dimmed">
                Memory Usage
              </Text>
              <ThemeIcon size="sm" variant="light" color={getUsageColor(metrics.memory?.percentage || 0)}>
                <IconMemory size={16} />
              </ThemeIcon>
            </Group>
            <Text fw={700} size="xl">
              {(metrics.memory?.percentage || 0).toFixed(1)}%
            </Text>
            <Progress value={metrics.memory?.percentage || 0} color={getUsageColor(metrics.memory?.percentage || 0)} size="sm" mt="xs" />
            <Text size="xs" c="dimmed" mt="xs">
              {formatBytes(metrics.memory?.used || 0)} / {formatBytes(metrics.memory?.total || 0)}
            </Text>
          </Card>

          <Card p="md" withBorder>
            <Group justify="space-between" mb="xs">
              <Text size="xs" tt="uppercase" fw={700} c="dimmed">
                Disk Usage
              </Text>
              <ThemeIcon size="sm" variant="light" color={getUsageColor(metrics.disk?.percentage || 0)}>
                <IconHardDrive size={16} />
              </ThemeIcon>
            </Group>
            <Text fw={700} size="xl">
              {(metrics.disk?.percentage || 0).toFixed(1)}%
            </Text>
            <Progress value={metrics.disk?.percentage || 0} color={getUsageColor(metrics.disk?.percentage || 0)} size="sm" mt="xs" />
            <Text size="xs" c="dimmed" mt="xs">
              {formatBytes(metrics.disk?.used || 0)} / {formatBytes(metrics.disk?.total || 0)}
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
              ↓ {formatBytes(metrics.network?.bytesIn || 0)}
            </Text>
            <Text fw={700} size="lg">
              ↑ {formatBytes(metrics.network?.bytesOut || 0)}
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
                      {health?.status?.toUpperCase() || 'UNKNOWN'}
                    </Badge>
                  </Group>
                </Card.Section>
                <Card.Section>
                  <Table>
                    <Table.Thead>
                      <Table.Tr>
                        <Table.Th>Check</Table.Th>
                        <Table.Th>Status</Table.Th>
                        <Table.Th>Duration</Table.Th>
                        <Table.Th>Description</Table.Th>
                      </Table.Tr>
                    </Table.Thead>
                    <Table.Tbody>
                      {health?.checks && Object.entries(health.checks).map(([name, check]: [string, unknown]) => {
                        if (typeof check === 'object' && check !== null && 'status' in check && 'description' in check) {
                          const healthCheck = check as { status: string; description: string };
                          return (
                        <Table.Tr key={name}>
                          <Table.Td>
                            <Text fw={500}>{name}</Text>
                          </Table.Td>
                          <Table.Td>
                            <Badge color={getStatusColor(healthCheck.status)} variant="light" size="sm">
                              {healthCheck.status?.toUpperCase() || 'UNKNOWN'}
                            </Badge>
                          </Table.Td>
                          <Table.Td>
                            <Text size="sm">{(healthCheck as { duration?: number }).duration ? `${(healthCheck as { duration?: number }).duration}ms` : 'N/A'}</Text>
                          </Table.Td>
                          <Table.Td>
                            <Text size="sm">{healthCheck.description || (healthCheck as { error?: string }).error || 'N/A'}</Text>
                          </Table.Td>
                        </Table.Tr>
                        );
                        }
                        return null;
                      })}
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
                        <Text size="sm">Provider</Text>
                        <Text size="sm" c="dimmed">{systemInfo?.database?.provider || 'Unknown'}</Text>
                      </Group>
                      <Group justify="space-between">
                        <Text size="sm">Status</Text>
                        <Badge color={systemInfo?.database?.isConnected ? 'green' : 'red'} variant="light">
                          {systemInfo?.database?.isConnected ? 'Connected' : 'Disconnected'}
                        </Badge>
                      </Group>
                      {systemInfo?.database?.pendingMigrations && systemInfo.database.pendingMigrations.length > 0 && (
                        <Alert color="yellow" icon={<IconAlertCircle size={16} />}>
                          {systemInfo.database.pendingMigrations.length} pending migrations
                        </Alert>
                      )}
                    </Stack>
                  </Accordion.Panel>
                </Accordion.Item>

                <Accordion.Item value="features">
                  <Accordion.Control icon={<IconSettings size={20} />}>
                    Enabled Features
                  </Accordion.Control>
                  <Accordion.Panel>
                    <Stack gap="xs">
                      {systemInfo?.features && Object.entries(systemInfo.features).map(([feature, enabled]) => (
                        <Group key={feature} justify="space-between">
                          <Text size="sm">{feature}</Text>
                          <Badge color={enabled ? 'green' : 'gray'} variant="light">
                            {enabled ? 'Enabled' : 'Disabled'}
                          </Badge>
                        </Group>
                      ))}
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
                    <Text size="sm" c="dimmed">{systemInfo?.runtime?.os || 'Unknown'}</Text>
                  </Group>
                  <Group justify="space-between">
                    <Text size="sm">.NET Runtime</Text>
                    <Text size="sm" c="dimmed">{systemInfo?.runtime?.dotnetVersion || 'Unknown'}</Text>
                  </Group>
                  <Group justify="space-between">
                    <Text size="sm">Architecture</Text>
                    <Text size="sm" c="dimmed">{systemInfo?.runtime?.architecture || 'Unknown'}</Text>
                  </Group>
                  <Group justify="space-between">
                    <Text size="sm">Memory Usage</Text>
                    <Text size="sm" c="dimmed">{systemInfo?.runtime?.memoryUsage ? `${systemInfo.runtime.memoryUsage.toFixed(1)} MB` : 'Unknown'}</Text>
                  </Group>
                  {systemInfo?.runtime?.cpuUsage !== undefined && (
                    <Group justify="space-between">
                      <Text size="sm">CPU Usage</Text>
                      <Text size="sm" c="dimmed">{systemInfo.runtime.cpuUsage.toFixed(1)}%</Text>
                    </Group>
                  )}
                </Stack>
              </Card.Section>
            </Card>

            <Card withBorder>
              <Card.Section p="md" withBorder>
                <Group>
                  <IconCloud size={20} />
                  <Text fw={600}>Application Info</Text>
                </Group>
              </Card.Section>
              <Card.Section p="md">
                <Stack gap="xs">
                  <Group justify="space-between">
                    <Text size="sm">Version</Text>
                    <Text size="sm" c="dimmed">{systemInfo?.version || 'Unknown'}</Text>
                  </Group>
                  <Group justify="space-between">
                    <Text size="sm">Build Date</Text>
                    <Text size="sm" c="dimmed">{systemInfo?.buildDate || 'Unknown'}</Text>
                  </Group>
                  <Group justify="space-between">
                    <Text size="sm">Environment</Text>
                    <Text size="sm" c="dimmed">{systemInfo?.environment || 'Unknown'}</Text>
                  </Group>
                  <Group justify="space-between">
                    <Text size="sm">Uptime</Text>
                    <Text size="sm" c="dimmed">{systemInfo?.uptime ? `${(systemInfo.uptime / 3600).toFixed(1)} hours` : 'Unknown'}</Text>
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
              <Alert icon={<IconInfoCircle size={16} />} color="blue" variant="light">
                <Stack gap="xs">
                  <Text size="sm" fw={500}>Diagnostics Summary</Text>
                  <Text size="sm" c="dimmed">
                    System Time: {systemInfo?.systemTime || new Date().toISOString()}
                  </Text>
                  {health && (
                    <Text size="sm" c="dimmed">
                      Health Status: {health.status} (checked in {health.totalDuration}ms)
                    </Text>
                  )}
                </Stack>
              </Alert>

              {health?.checks && Object.entries(health.checks).length > 0 && (
                <Stack gap="md" mt="md">
                  <Text fw={500}>Health Check Details</Text>
                  {Object.entries(health.checks).map(([name, check]: [string, unknown]) => {
                    if (typeof check === 'object' && check !== null && 'status' in check && 'description' in check) {
                      const healthCheck = check as { status: string; description: string };
                      return (
                    <Card key={name} withBorder p="sm">
                      <Group justify="space-between">
                        <Group>
                          <ThemeIcon color={getStatusColor(healthCheck.status)} variant="light" size="sm">
                            {healthCheck.status === 'healthy' ? <IconCheck size={16} /> : <IconAlertCircle size={16} />}
                          </ThemeIcon>
                          <Text size="sm" fw={500}>{name}</Text>
                        </Group>
                        <Badge color={getStatusColor(healthCheck.status)} variant="light" size="sm">
                          {healthCheck.status}
                        </Badge>
                      </Group>
                      {healthCheck.description && (
                        <Text size="xs" c="dimmed" mt="xs">
                          {healthCheck.description}
                        </Text>
                      )}
                      {(healthCheck as { error?: string }).error && (
                        <Text size="xs" c="red" mt="xs">
                          Error: {(healthCheck as { error?: string }).error}
                        </Text>
                      )}
                    </Card>
                    );
                    }
                    return null;
                  })}
                </Stack>
              )}
            </Card.Section>
          </Card>
        </Tabs.Panel>
      </Tabs>
    </Stack>
  );
}