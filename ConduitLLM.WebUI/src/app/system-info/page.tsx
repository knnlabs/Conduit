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
import {
  IconServer,
  IconDatabase,
  IconBrandDocker,
  IconRefresh,
  IconDownload,
  IconCircleCheck,
  IconAlertTriangle,
  IconClock,
  IconLock,
  IconPackage,
  IconBolt,
} from '@tabler/icons-react';
import { useState, useEffect } from 'react';
import { notifications } from '@mantine/notifications';
import { SystemInfoDto } from '@knn_labs/conduit-admin-client';
import { withAdminClient } from '@/lib/client/adminClient';

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




export default function SystemInfoPage() {
  const [isLoading, setIsLoading] = useState(true);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [systemInfo, setSystemInfo] = useState<SystemInfoDto | null>(null);
  const [activeTab, setActiveTab] = useState<string | null>('overview');
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    void fetchSystemInfo();
  }, []);

  const fetchSystemInfo = async () => {
    try {
      setError(null);
      
      const data = await withAdminClient(client => 
        client.system.getSystemInfo()
      );
      
      setSystemInfo(data);
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Unknown error';
      console.error('Error fetching system info:', errorMessage);
      setError(errorMessage);
      notifications.show({
        title: 'Error',
        message: errorMessage,
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

  // Helper function to format uptime from seconds
  const formatUptime = (uptimeSeconds: number): string => {
    if (!uptimeSeconds) return 'Unknown';
    
    const days = Math.floor(uptimeSeconds / 86400);
    const hours = Math.floor((uptimeSeconds % 86400) / 3600);
    const minutes = Math.floor((uptimeSeconds % 3600) / 60);
    
    if (days > 0) {
      return `${days}d ${hours}h ${minutes}m`;
    } else if (hours > 0) {
      return `${hours}h ${minutes}m`;
    } else {
      return `${minutes}m`;
    }
  };

  // Generate system metrics from real data
  const systemMetrics: SystemMetric[] = [];
  
  // Note: Memory and CPU usage are not provided by the backend API
  // Only show metrics that are actually available

  if (systemInfo?.database?.isConnected !== undefined) {
    systemMetrics.push({
      name: 'Database Status',
      value: systemInfo.database.isConnected ? 'Connected' : 'Disconnected',
      status: systemInfo.database.isConnected ? 'healthy' : 'critical',
      description: `Provider: ${systemInfo.database.provider ?? 'Unknown'}`
    });
  }

  // Service information from real data
  const services: ServiceInfo[] = [];
  if (systemInfo) {
    services.push({
      name: 'Conduit Core API',
      version: systemInfo.version ?? 'Unknown',
      status: 'running',
      uptime: formatUptime(systemInfo.uptime ?? 0)
    });
    
    if (systemInfo.database?.isConnected) {
      services.push({
        name: systemInfo.database.provider ?? 'Database',
        version: 'Unknown',
        status: systemInfo.database.isConnected ? 'running' : 'stopped'
      });
    }
  }


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

  const getStatusColor = (status: string): string => {
    switch (status) {
      case 'running':
      case 'healthy':
      case 'latest':
        return 'green';
      case 'degraded':
      case 'warning':
      case 'outdated':
        return 'orange';
      case 'stopped':
      case 'unhealthy':
      case 'error':
        return 'red';
      default:
        return 'gray';
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

  if (error && !systemInfo) {
    return (
      <Stack gap="xl">
        <Card shadow="sm" p="md" radius="md">
          <Alert
            icon={<IconAlertTriangle size={16} />}
            title="Failed to load system information"
            color="red"
          >
            {error}
          </Alert>
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
              onClick={() => void handleRefresh()}
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
                {systemInfo?.runtime?.os ?? 'Unknown'}
              </Text>
              <Text size="xs" c="dimmed" mt={4}>
                {systemInfo?.runtime?.architecture ?? 'Unknown Architecture'}
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
                .NET Runtime
              </Text>
              <Text size="xl" fw={700} mt={4}>
                {systemInfo?.runtime?.dotnetVersion ?? 'Unknown'}
              </Text>
              <Text size="xs" c="dimmed" mt={4}>
                Environment: {systemInfo?.environment ?? 'Unknown'}
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
                {systemInfo?.uptime ? formatUptime(systemInfo.uptime) : 'Unknown'}
              </Text>
              <Text size="xs" c="dimmed" mt={4}>
                Version: {systemInfo?.version ?? 'Unknown'}
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
                Database
              </Text>
              <Text size="xl" fw={700} mt={4}>
                {systemInfo?.database?.isConnected ? 'Connected' : 'Disconnected'}
              </Text>
              <Text size="xs" c="dimmed" mt={4}>
                {systemInfo?.database?.provider ?? 'Unknown Provider'}
              </Text>
            </div>
            <ThemeIcon 
              color={systemInfo?.database?.isConnected ? 'green' : 'red'} 
              variant="light" 
              size={48} 
              radius="md"
            >
              <IconDatabase size={24} />
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
                        {String(metric.value)}{metric.unit ?? ''}
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
                      <Table.Td>{service.uptime ?? '-'}</Table.Td>
                      <Table.Td>{service.port ?? '-'}</Table.Td>
                      <Table.Td>
                        <Text size="sm">
                          CPU: {service.cpu ?? '-'}, Mem: {service.memory ?? '-'}
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
            <Title order={4} mb="md">System Configuration</Title>
            <Alert
              icon={<IconLock size={16} />}
              title="Security Notice"
              color="blue"
              mb="md"
            >
              Environment variables are not exposed via the API for security reasons. Configuration values are shown below where available.
            </Alert>
            <ScrollArea>
              <Table>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Setting</Table.Th>
                    <Table.Th>Value</Table.Th>
                    <Table.Th>Status</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  <Table.Tr>
                    <Table.Td>
                      <Code>Environment</Code>
                    </Table.Td>
                    <Table.Td>
                      <Code>{systemInfo?.environment ?? 'Unknown'}</Code>
                    </Table.Td>
                    <Table.Td>
                      <Badge variant="light" size="sm" color="blue">
                        System
                      </Badge>
                    </Table.Td>
                  </Table.Tr>
                  <Table.Tr>
                    <Table.Td>
                      <Code>Build Date</Code>
                    </Table.Td>
                    <Table.Td>
                      <Code>{systemInfo?.buildDate ? new Date(systemInfo.buildDate).toLocaleDateString() : 'Unknown'}</Code>
                    </Table.Td>
                    <Table.Td>
                      <Badge variant="light" size="sm" color="blue">
                        System
                      </Badge>
                    </Table.Td>
                  </Table.Tr>
                  <Table.Tr>
                    <Table.Td>
                      <Code>IP Filtering</Code>
                    </Table.Td>
                    <Table.Td>
                      <Code>{systemInfo?.features?.ipFiltering ? 'Enabled' : 'Disabled'}</Code>
                    </Table.Td>
                    <Table.Td>
                      <Badge 
                        variant="light" 
                        size="sm" 
                        color={systemInfo?.features?.ipFiltering ? 'green' : 'gray'}
                      >
                        Feature
                      </Badge>
                    </Table.Td>
                  </Table.Tr>
                  <Table.Tr>
                    <Table.Td>
                      <Code>Provider Health</Code>
                    </Table.Td>
                    <Table.Td>
                      <Code>{systemInfo?.features?.providerHealth ? 'Enabled' : 'Disabled'}</Code>
                    </Table.Td>
                    <Table.Td>
                      <Badge 
                        variant="light" 
                        size="sm" 
                        color={systemInfo?.features?.providerHealth ? 'green' : 'gray'}
                      >
                        Feature
                      </Badge>
                    </Table.Td>
                  </Table.Tr>
                  <Table.Tr>
                    <Table.Td>
                      <Code>Cost Tracking</Code>
                    </Table.Td>
                    <Table.Td>
                      <Code>{systemInfo?.features?.costTracking ? 'Enabled' : 'Disabled'}</Code>
                    </Table.Td>
                    <Table.Td>
                      <Badge 
                        variant="light" 
                        size="sm" 
                        color={systemInfo?.features?.costTracking ? 'green' : 'gray'}
                      >
                        Feature
                      </Badge>
                    </Table.Td>
                  </Table.Tr>
                  <Table.Tr>
                    <Table.Td>
                      <Code>Audio Support</Code>
                    </Table.Td>
                    <Table.Td>
                      <Code>{systemInfo?.features?.audioSupport ? 'Enabled' : 'Disabled'}</Code>
                    </Table.Td>
                    <Table.Td>
                      <Badge 
                        variant="light" 
                        size="sm" 
                        color={systemInfo?.features?.audioSupport ? 'green' : 'gray'}
                      >
                        Feature
                      </Badge>
                    </Table.Td>
                  </Table.Tr>
                  {systemInfo?.database?.pendingMigrations && Array.isArray(systemInfo.database.pendingMigrations) && systemInfo.database.pendingMigrations.length > 0 && (
                    <Table.Tr>
                      <Table.Td>
                        <Code>Pending Migrations</Code>
                      </Table.Td>
                      <Table.Td>
                        <Stack gap="xs">
                          {systemInfo.database.pendingMigrations.map((migration) => (
                            <Code key={migration}>{String(migration)}</Code>
                          ))}
                        </Stack>
                      </Table.Td>
                      <Table.Td>
                        <Badge variant="light" size="sm" color="orange">
                          Database
                        </Badge>
                      </Table.Td>
                    </Table.Tr>
                  )}
                </Table.Tbody>
              </Table>
            </ScrollArea>
          </Card>
        </Tabs.Panel>

        <Tabs.Panel value="dependencies" pt="md">
          <Card shadow="sm" p="md" radius="md" withBorder>
            <Title order={4} mb="md">System Dependencies</Title>
            <Alert
              icon={<IconPackage size={16} />}
              title="Information"
              color="blue"
              mb="md"
            >
              Package dependency information is not available via the system API. Check package.json files directly for detailed dependency information.
            </Alert>
            <ScrollArea>
              <Table>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Component</Table.Th>
                    <Table.Th>Version</Table.Th>
                    <Table.Th>Status</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  <Table.Tr>
                    <Table.Td>
                      <Code>Conduit Core</Code>
                    </Table.Td>
                    <Table.Td>{systemInfo?.version ?? 'Unknown'}</Table.Td>
                    <Table.Td>
                      <Badge variant="light" color="green">
                        Current
                      </Badge>
                    </Table.Td>
                  </Table.Tr>
                  <Table.Tr>
                    <Table.Td>
                      <Code>.NET Runtime</Code>
                    </Table.Td>
                    <Table.Td>{systemInfo?.runtime?.dotnetVersion ?? 'Unknown'}</Table.Td>
                    <Table.Td>
                      <Badge variant="light" color="green">
                        Runtime
                      </Badge>
                    </Table.Td>
                  </Table.Tr>
                  <Table.Tr>
                    <Table.Td>
                      <Code>Database Provider</Code>
                    </Table.Td>
                    <Table.Td>{systemInfo?.database?.provider ?? 'Unknown'}</Table.Td>
                    <Table.Td>
                      <Badge 
                        variant="light" 
                        color={systemInfo?.database?.isConnected ? 'green' : 'red'}
                      >
                        {systemInfo?.database?.isConnected ? 'Connected' : 'Disconnected'}
                      </Badge>
                    </Table.Td>
                  </Table.Tr>
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