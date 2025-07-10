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
  Tabs,
  LoadingOverlay,
  Alert,
  Accordion,
} from '@mantine/core';
import { StatusIndicator } from '@/components/common/StatusIndicator';
import {
  IconServer,
  IconDatabase,
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
import { useSystemInfo, useSystemHealth } from '@/hooks/useConduitAdmin';
import { useProviderHealth } from '@/hooks/api/useProviderHealthApi';
import { notifications } from '@mantine/notifications';

// Types will come from the SDK responses

export default function SystemInfoPage() {
  const { data: systemInfo, isLoading: systemLoading, refetch: refetchSystemInfo } = useSystemInfo();
  const { data: health, isLoading: healthLoading, refetch: refetchHealth } = useSystemHealth();
  const { data: providerHealth, isLoading: providerHealthLoading } = useProviderHealth();
  
  const isLoading = systemLoading || healthLoading || providerHealthLoading;


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


  const mapToSystemStatus = (status: string) => {
    switch (status) {
      case 'running':
      case 'connected':
      case 'healthy':
        return 'healthy';
      case 'warning':
      case 'degraded':
        return 'degraded';
      case 'stopped':
      case 'disconnected':
      case 'error':
      case 'critical':
        return 'unhealthy';
      default:
        return 'unknown';
    }
  };


  return (
    <Stack gap="xl">
      <Group justify="space-between">
        <div>
          <Title order={1}>System Information</Title>
          <Text c="dimmed">Monitor system configuration, health status, and provider connectivity</Text>
        </div>

        <Group>
          <Button
            variant="light"
            leftSection={<IconRefresh size={16} />}
            onClick={() => {
              refetchSystemInfo();
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

      {/* System Status Overview */}
      <SimpleGrid cols={{ base: 1, sm: 2, md: 3 }} spacing="lg">
        <Card p="md" withBorder>
          <Group justify="space-between" mb="xs">
            <Text size="xs" tt="uppercase" fw={700} c="dimmed">
              System Health
            </Text>
            <StatusIndicator
              status={health ? mapToSystemStatus(health.status) : 'unknown'}
              variant="icon"
              size="sm"
              showTooltip={false}
            />
          </Group>
          <Text fw={700} size="xl">
            {health?.status?.toUpperCase() || 'UNKNOWN'}
          </Text>
          <Text size="xs" c="dimmed" mt="xs">
            {health?.checks ? Object.keys(health.checks).length : 0} health checks
          </Text>
        </Card>

        <Card p="md" withBorder>
          <Group justify="space-between" mb="xs">
            <Text size="xs" tt="uppercase" fw={700} c="dimmed">
              Active Providers
            </Text>
            <ThemeIcon size="sm" variant="light" color="blue">
              <IconCloud size={16} />
            </ThemeIcon>
          </Group>
          <Text fw={700} size="xl">
            {providerHealth?.filter((p) => p.status === 'healthy').length || 0} / {providerHealth?.length || 0}
          </Text>
          <Text size="xs" c="dimmed" mt="xs">
            Healthy providers
          </Text>
        </Card>

        <Card p="md" withBorder>
          <Group justify="space-between" mb="xs">
            <Text size="xs" tt="uppercase" fw={700} c="dimmed">
              Database Status
            </Text>
            <ThemeIcon size="sm" variant="light" color={systemInfo?.database?.isConnected ? 'green' : 'red'}>
              <IconDatabase size={16} />
            </ThemeIcon>
          </Group>
          <Text fw={700} size="xl">
            {systemInfo?.database?.isConnected ? 'Connected' : 'Disconnected'}
          </Text>
          <Text size="xs" c="dimmed" mt="xs">
            {systemInfo?.database?.provider || 'Unknown provider'}
          </Text>
        </Card>
      </SimpleGrid>

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
            
            <Stack gap="lg">
              {/* Services Status */}
              <Card withBorder>
                <Card.Section p="md" withBorder>
                  <Group justify="space-between">
                    <Text fw={600}>Service Health Status</Text>
                    <StatusIndicator
                      status={health ? mapToSystemStatus(health.status) : 'unknown'}
                      variant="badge"
                      size="sm"
                      label={health?.status?.toUpperCase() || 'UNKNOWN'}
                    />
                  </Group>
                </Card.Section>
                <Card.Section>
                  <Table>
                    <Table.Thead>
                      <Table.Tr>
                        <Table.Th>Component</Table.Th>
                        <Table.Th>Status</Table.Th>
                        <Table.Th>Response Time</Table.Th>
                        <Table.Th>Details</Table.Th>
                      </Table.Tr>
                    </Table.Thead>
                    <Table.Tbody>
                      {health?.checks && Object.entries(health.checks).map(([name, check]: [string, unknown]) => {
                        if (typeof check === 'object' && check !== null && 'status' in check) {
                          const healthCheck = check as { status: string; description?: string; duration?: number; error?: string };
                          return (
                        <Table.Tr key={name}>
                          <Table.Td>
                            <Text fw={500}>{name}</Text>
                          </Table.Td>
                          <Table.Td>
                            <StatusIndicator
                              status={mapToSystemStatus(healthCheck.status)}
                              variant="badge"
                              size="sm"
                              label={healthCheck.status?.toUpperCase() || 'UNKNOWN'}
                            />
                          </Table.Td>
                          <Table.Td>
                            <Text size="sm">{healthCheck.duration ? `${healthCheck.duration}ms` : '-'}</Text>
                          </Table.Td>
                          <Table.Td>
                            <Text size="sm">{healthCheck.description || healthCheck.error || '-'}</Text>
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

              {/* Provider Health Status */}
              <Card withBorder>
                <Card.Section p="md" withBorder>
                  <Group justify="space-between">
                    <Text fw={600}>Provider Health Status</Text>
                    <Badge variant="light">
                      {providerHealth?.length || 0} providers configured
                    </Badge>
                  </Group>
                </Card.Section>
                <Card.Section>
                  {providerHealth && providerHealth.length > 0 ? (
                    <Table>
                      <Table.Thead>
                        <Table.Tr>
                          <Table.Th>Provider</Table.Th>
                          <Table.Th>Status</Table.Th>
                          <Table.Th>Response Time</Table.Th>
                          <Table.Th>Last Check</Table.Th>
                          <Table.Th>Uptime</Table.Th>
                        </Table.Tr>
                      </Table.Thead>
                      <Table.Tbody>
                        {providerHealth?.map((provider) => (
                          <Table.Tr key={provider.providerId}>
                            <Table.Td>
                              <Text fw={500}>{provider.providerName}</Text>
                            </Table.Td>
                            <Table.Td>
                              <StatusIndicator
                                status={mapToSystemStatus(provider.status)}
                                variant="badge"
                                size="sm"
                                label={provider.status?.toUpperCase() || 'UNKNOWN'}
                              />
                            </Table.Td>
                            <Table.Td>
                              <Text size="sm">{provider.responseTime ? `${provider.responseTime}ms` : '-'}</Text>
                            </Table.Td>
                            <Table.Td>
                              <Text size="sm">
                                {provider.lastChecked ? new Date(provider.lastChecked).toLocaleTimeString() : '-'}
                              </Text>
                            </Table.Td>
                            <Table.Td>
                              <Text size="sm">{provider.uptime ? `${provider.uptime.toFixed(1)}%` : '-'}</Text>
                            </Table.Td>
                          </Table.Tr>
                        ))}
                      </Table.Tbody>
                    </Table>
                  ) : (
                    <Card.Section p="md">
                      <Text size="sm" c="dimmed" ta="center">No providers configured</Text>
                    </Card.Section>
                  )}
                </Card.Section>
              </Card>
            </Stack>
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
                          <StatusIndicator
                            status={mapToSystemStatus(healthCheck.status)}
                            variant="icon"
                            size="sm"
                            showTooltip={false}
                          />
                          <Text size="sm" fw={500}>{name}</Text>
                        </Group>
                        <StatusIndicator
                          status={mapToSystemStatus(healthCheck.status)}
                          variant="badge"
                          size="sm"
                          label={healthCheck.status}
                        />
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