'use client';

import {
  Stack,
  Title,
  Text,
  Card,
  Group,
  Button,
  Table,
  Badge,
  ThemeIcon,
  Paper,
  SimpleGrid,
  Progress,
  Code,
  LoadingOverlay,
  Alert,
  Tabs,
  ScrollArea,
} from '@mantine/core';
import { StatusIndicator } from '@/components/common/StatusIndicator';
import {
  IconServer,
  IconCpu,
  IconDatabase,
  IconBrandDocker,
  IconRefresh,
  IconDownload,
  IconCircleCheck,
  IconAlertTriangle,
  IconClock,
  IconCpu2,
  IconDeviceFloppy,
  IconNetwork,
  IconLock,
  IconPackage,
  IconBolt,
} from '@tabler/icons-react';
import { useState, useEffect } from 'react';
import { notifications } from '@mantine/notifications';
import { formatters } from '@/lib/utils/formatters';

interface SystemMetric {
  name: string;
  value: string | number;
  unit?: string;
  status: 'healthy' | 'warning' | 'critical';
  description?: string;
}

interface ServiceInfo {
  name: string;
  version: string;
  status: 'running' | 'stopped' | 'degraded';
  uptime?: string;
  port?: number;
  memory?: string;
  cpu?: string;
}

interface EnvironmentVariable {
  key: string;
  value: string;
  source: 'env' | 'config' | 'default';
  sensitive?: boolean;
}

export default function SystemInfoPage() {
  const [isLoading, setIsLoading] = useState(true);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [systemInfo, setSystemInfo] = useState<any>(null);
  const [activeTab, setActiveTab] = useState<string | null>('overview');

  useEffect(() => {
    fetchSystemInfo();
  }, []);

  const fetchSystemInfo = async () => {
    try {
      const response = await fetch('/api/settings/system-info', {
        headers: {
          'X-Admin-Auth-Key': localStorage.getItem('adminAuthKey') || '',
        },
      });

      if (!response.ok) {
        throw new Error('Failed to fetch system information');
      }

      const data = await response.json();
      setSystemInfo(data);
    } catch (error) {
      console.error('Error fetching system info:', error);
      notifications.show({
        title: 'Error',
        message: 'Failed to load system information',
        color: 'red',
      });
    } finally {
      setIsLoading(false);
    }
  };

  const handleRefresh = async () => {
    setIsRefreshing(true);
    await fetchSystemInfo();
    setIsRefreshing(false);
    notifications.show({
      title: 'Refreshed',
      message: 'System information updated',
      color: 'green',
    });
  };

  const handleExport = () => {
    const exportData = {
      timestamp: new Date().toISOString(),
      system: systemInfo,
    };
    
    const blob = new Blob([JSON.stringify(exportData, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `system-info-${new Date().toISOString()}.json`;
    a.click();
    URL.revokeObjectURL(url);
    
    notifications.show({
      title: 'Exported',
      message: 'System information exported successfully',
      color: 'green',
    });
  };

  // Mock data for development
  const services: ServiceInfo[] = [
    { name: 'Conduit Core API', version: '1.2.0', status: 'running', uptime: '30d 14h 23m', port: 8080, memory: '512MB', cpu: '2.3%' },
    { name: 'Admin API', version: '1.2.0', status: 'running', uptime: '30d 14h 23m', port: 8081, memory: '256MB', cpu: '1.1%' },
    { name: 'Redis Cache', version: '7.2.4', status: 'running', uptime: '45d 2h 15m', port: 6379, memory: '128MB', cpu: '0.5%' },
    { name: 'PostgreSQL', version: '16.1', status: 'running', uptime: '45d 2h 15m', port: 5432, memory: '1.2GB', cpu: '3.7%' },
    { name: 'RabbitMQ', version: '3.13.0', status: 'running', uptime: '30d 14h 23m', port: 5672, memory: '384MB', cpu: '1.8%' },
  ];

  const systemMetrics: SystemMetric[] = [
    { name: 'CPU Usage', value: 45, unit: '%', status: 'healthy', description: '8 cores available' },
    { name: 'Memory Usage', value: 62, unit: '%', status: 'warning', description: '19.8GB / 32GB used' },
    { name: 'Disk Usage', value: 38, unit: '%', status: 'healthy', description: '152GB / 400GB used' },
    { name: 'Network I/O', value: '125MB/s', status: 'healthy', description: 'Inbound: 75MB/s, Outbound: 50MB/s' },
    { name: 'Open Connections', value: 2847, status: 'healthy', description: 'Max: 10,000' },
    { name: 'Thread Pool', value: '85/100', status: 'warning', description: 'Active threads' },
  ];

  const environmentVars: EnvironmentVariable[] = [
    { key: 'NODE_ENV', value: 'production', source: 'env' },
    { key: 'CONDUIT_MASTER_KEY', value: '••••••••', source: 'env', sensitive: true },
    { key: 'CONDUIT_WEBUI_AUTH_KEY', value: '••••••••', source: 'env', sensitive: true },
    { key: 'DATABASE_URL', value: 'postgresql://...', source: 'env', sensitive: true },
    { key: 'REDIS_URL', value: 'redis://localhost:6379', source: 'env' },
    { key: 'RABBITMQ_URL', value: 'amqp://localhost:5672', source: 'env' },
    { key: 'LOG_LEVEL', value: 'info', source: 'config' },
    { key: 'MAX_REQUEST_SIZE', value: '10MB', source: 'config' },
    { key: 'REQUEST_TIMEOUT', value: '30000', source: 'config' },
    { key: 'RATE_LIMIT_WINDOW', value: '60000', source: 'default' },
    { key: 'RATE_LIMIT_MAX_REQUESTS', value: '100', source: 'default' },
  ];

  const dependencies = [
    { name: '@knn_labs/conduit-admin-client', version: '1.0.1-dev.20250709095716', status: 'latest' },
    { name: '@mantine/core', version: '7.3.2', status: 'latest' },
    { name: 'next', version: '14.0.4', status: 'outdated', latest: '14.1.0' },
    { name: 'react', version: '18.2.0', status: 'latest' },
    { name: 'typescript', version: '5.3.3', status: 'latest' },
  ];

  const mapToSystemStatus = (status: string) => {
    switch (status) {
      case 'running':
      case 'healthy':
      case 'latest':
        return 'green';
      case 'degraded':
      case 'warning':
      case 'outdated':
        return 'yellow';
      case 'stopped':
      case 'critical':
        return 'unhealthy';
      default:
        return 'unknown';
    }
  };

  const getStatusIcon = (status: string) => {
    switch (status) {
      case 'running':
      case 'healthy':
      case 'latest':
        return <IconCircleCheck size={16} />;
      case 'degraded':
      case 'warning':
      case 'outdated':
        return <IconAlertTriangle size={16} />;
      default:
        return <IconAlertTriangle size={16} />;
    }
  };

  if (isLoading) {
    return (
      <Stack>
        <Card shadow="sm" p="md" radius="md" pos="relative" mih={200}>
          <LoadingOverlay visible={true} />
        </Card>
      </Stack>
    );
  }

  return (
    <Stack gap="xl">
      <Card shadow="sm" p="md" radius="md">
        <Group justify="space-between" align="center">
          <div>
            <Title order={2}>System Information</Title>
            <Text size="sm" c="dimmed" mt={4}>
              Runtime environment and service configuration
            </Text>
          </div>
          <Group>
            <Button
              variant="light"
              leftSection={<IconRefresh size={16} />}
              onClick={handleRefresh}
              loading={isRefreshing}
            >
              Refresh
            </Button>
            <Button
              variant="filled"
              leftSection={<IconDownload size={16} />}
              onClick={handleExport}
            >
              Export
            </Button>
          </Group>
        </Group>
      </Card>

      <SimpleGrid cols={{ base: 1, sm: 2, lg: 4 }} spacing="md">
        <Card padding="lg" radius="md" withBorder>
          <Group justify="space-between">
            <div>
              <Text size="sm" c="dimmed" fw={600} tt="uppercase">
                Platform
              </Text>
              <Text size="xl" fw={700} mt={4}>
                Docker
              </Text>
              <Text size="xs" c="dimmed" mt={4}>
                Linux 5.15.0-91-generic
              </Text>
            </div>
            <ThemeIcon color="blue" variant="light" size={48} radius="md">
              <IconBrandDocker size={24} />
            </ThemeIcon>
          </Group>
        </Card>

        <Card padding="lg" radius="md" withBorder>
          <Group justify="space-between">
            <div>
              <Text size="sm" c="dimmed" fw={600} tt="uppercase">
                Node Version
              </Text>
              <Text size="xl" fw={700} mt={4}>
                v20.11.0
              </Text>
              <Text size="xs" c="dimmed" mt={4}>
                NPM 10.2.4
              </Text>
            </div>
            <ThemeIcon color="green" variant="light" size={48} radius="md">
              <IconPackage size={24} />
            </ThemeIcon>
          </Group>
        </Card>

        <Card padding="lg" radius="md" withBorder>
          <Group justify="space-between">
            <div>
              <Text size="sm" c="dimmed" fw={600} tt="uppercase">
                System Uptime
              </Text>
              <Text size="xl" fw={700} mt={4}>
                30d 14h
              </Text>
              <Text size="xs" c="dimmed" mt={4}>
                Since Dec 11, 2024
              </Text>
            </div>
            <ThemeIcon color="teal" variant="light" size={48} radius="md">
              <IconClock size={24} />
            </ThemeIcon>
          </Group>
        </Card>

        <Card padding="lg" radius="md" withBorder>
          <Group justify="space-between">
            <div>
              <Text size="sm" c="dimmed" fw={600} tt="uppercase">
                Total Services
              </Text>
              <Text size="xl" fw={700} mt={4}>
                5 / 5
              </Text>
              <Text size="xs" c="dimmed" mt={4}>
                All services healthy
              </Text>
            </div>
            <ThemeIcon color="green" variant="light" size={48} radius="md">
              <IconServer size={24} />
            </ThemeIcon>
          </Group>
        </Card>
      </SimpleGrid>

      <Tabs value={activeTab} onChange={setActiveTab}>
        <Tabs.List>
          <Tabs.Tab value="overview" leftSection={<IconServer size={16} />}>
            Overview
          </Tabs.Tab>
          <Tabs.Tab value="services" leftSection={<IconBolt size={16} />}>
            Services
          </Tabs.Tab>
          <Tabs.Tab value="environment" leftSection={<IconLock size={16} />}>
            Environment
          </Tabs.Tab>
          <Tabs.Tab value="dependencies" leftSection={<IconPackage size={16} />}>
            Dependencies
          </Tabs.Tab>
        </Tabs.List>

        <Tabs.Panel value="overview" pt="md">
          <Card shadow="sm" p="md" radius="md" withBorder>
            <Title order={4} mb="md">System Resources</Title>
            <Stack gap="md">
              {systemMetrics.map((metric) => (
                <Paper key={metric.name} p="md" withBorder>
                  <Group justify="space-between" mb="xs">
                    <div>
                      <Text fw={500}>{metric.name}</Text>
                      {metric.description && (
                        <Text size="xs" c="dimmed">{metric.description}</Text>
                      )}
                    </div>
                    <Group gap="xs">
                      <Badge
                        leftSection={getStatusIcon(metric.status)}
                        color={getStatusColor(metric.status)}
                        variant="light"
                      >
                        {metric.status}
                      </Badge>
                      <Text fw={600}>
                        {metric.value}{metric.unit}
                      </Text>
                    </Group>
                  </Group>
                  {typeof metric.value === 'number' && metric.unit === '%' && (
                    <Progress
                      value={metric.value}
                      color={getStatusColor(metric.status)}
                      size="sm"
                      radius="md"
                    />
                  )}
                </Paper>
              ))}
            </Stack>
          </Card>
        </Tabs.Panel>

        <Tabs.Panel value="services" pt="md">
          <Card shadow="sm" p="md" radius="md" withBorder>
            <Title order={4} mb="md">Running Services</Title>
            <ScrollArea>
              <Table>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Service</Table.Th>
                    <Table.Th>Version</Table.Th>
                    <Table.Th>Status</Table.Th>
                    <Table.Th>Uptime</Table.Th>
                    <Table.Th>Port</Table.Th>
                    <Table.Th>Resources</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {services.map((service) => (
                    <Table.Tr key={service.name}>
                      <Table.Td>
                        <Text fw={500}>{service.name}</Text>
                      </Table.Td>
                      <Table.Td>
                        <Code>{service.version}</Code>
                      </Table.Td>
                      <Table.Td>
                        <Badge
                          leftSection={getStatusIcon(service.status)}
                          color={getStatusColor(service.status)}
                          variant="light"
                        >
                          {service.status}
                        </Badge>
                      </Table.Td>
                      <Table.Td>{service.uptime || '-'}</Table.Td>
                      <Table.Td>{service.port || '-'}</Table.Td>
                      <Table.Td>
                        <Text size="sm">
                          CPU: {service.cpu || '-'}, Mem: {service.memory || '-'}
                        </Text>
                      </Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </ScrollArea>
          </Card>
        </Tabs.Panel>

        <Tabs.Panel value="environment" pt="md">
          <Card shadow="sm" p="md" radius="md" withBorder>
            <Title order={4} mb="md">Environment Variables</Title>
            <Alert
              icon={<IconLock size={16} />}
              title="Security Notice"
              color="blue"
              mb="md"
            >
              Sensitive values are masked for security. Edit configuration files directly to modify these values.
            </Alert>
            <ScrollArea>
              <Table>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Variable</Table.Th>
                    <Table.Th>Value</Table.Th>
                    <Table.Th>Source</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {environmentVars.map((env) => (
                    <Table.Tr key={env.key}>
                      <Table.Td>
                        <Code>{env.key}</Code>
                      </Table.Td>
                      <Table.Td>
                        <Code c={env.sensitive ? 'dimmed' : undefined}>
                          {env.value}
                        </Code>
                      </Table.Td>
                      <Table.Td>
                        <Badge variant="light" size="sm">
                          {env.source}
                        </Badge>
                      </Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </ScrollArea>
          </Card>
        </Tabs.Panel>

        <Tabs.Panel value="dependencies" pt="md">
          <Card shadow="sm" p="md" radius="md" withBorder>
            <Title order={4} mb="md">Package Dependencies</Title>
            <ScrollArea>
              <Table>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Package</Table.Th>
                    <Table.Th>Current Version</Table.Th>
                    <Table.Th>Status</Table.Th>
                    <Table.Th>Latest Version</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {dependencies.map((dep) => (
                    <Table.Tr key={dep.name}>
                      <Table.Td>
                        <Code>{dep.name}</Code>
                      </Table.Td>
                      <Table.Td>{dep.version}</Table.Td>
                      <Table.Td>
                        <Badge
                          color={dep.status === 'latest' ? 'green' : 'yellow'}
                          variant="light"
                        >
                          {dep.status}
                        </Badge>
                      </Table.Td>
                      <Table.Td>
                        {dep.latest || dep.version}
                      </Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </ScrollArea>
          </Card>
        </Tabs.Panel>
      </Tabs>

      <LoadingOverlay visible={isRefreshing} />
    </Stack>
  );
}