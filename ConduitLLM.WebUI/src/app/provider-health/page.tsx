'use client';

import {
  Stack,
  Title,
  Text,
  Group,
  Grid,
  Card,
  Badge,
  ThemeIcon,
  Table,
  ScrollArea,
  Button,
  Select,
  Switch,
  Progress,
  Skeleton,
  Timeline,
  RingProgress,
  Tooltip,
  ActionIcon,
  Modal,
  Code,
  Tabs,
  Alert,
  LoadingOverlay,
  Center,
  Paper,
} from '@mantine/core';
import {
  LineChart,
  AreaChart,
} from '@mantine/charts';
import {
  IconCircleCheck,
  IconCircleX,
  IconAlertTriangle,
  IconRefresh,
  IconDownload,
  IconClock,
  IconActivity,
  IconApi,
  IconBolt,
  IconWifi,
  IconInfoCircle,
  IconChartLine,
  IconServer,
  IconAlertCircle,
} from '@tabler/icons-react';
import { useState, useEffect, useCallback } from 'react';
import { useDisclosure } from '@mantine/hooks';
import { notifications } from '@mantine/notifications';
import { CardSkeleton } from '@/components/common/LoadingState';
import { formatters } from '@/lib/utils/formatters';
import { StatusIndicator } from '@/components/common/StatusIndicator';

interface ProviderHealthData {
  id: string;
  name: string;
  status: 'healthy' | 'degraded' | 'down';
  uptime: number;
  responseTime: number;
  lastCheck: string;
  errorRate: number;
  requestCount: number;
  models: {
    name: string;
    status: 'available' | 'unavailable' | 'maintenance';
    performance: number;
  }[];
}

interface HealthHistory {
  timestamp: string;
  status: 'healthy' | 'degraded' | 'down';
  responseTime: number;
  errorRate: number;
}

interface HealthIncident {
  id: string;
  provider: string;
  type: 'outage' | 'degradation' | 'rate_limit';
  startTime: string;
  endTime?: string;
  severity: 'low' | 'medium' | 'high';
  message: string;
  affectedModels: string[];
  impact: {
    requestsFailed: number;
    usersAffected: number;
  };
}

interface ProviderMetrics {
  requestVolume: number[];
  errorRate: number[];
  responseTime: number[];
  timestamps: string[];
  availability: number;
  avgResponseTime: number;
  p95ResponseTime: number;
  p99ResponseTime: number;
}

export default function ProviderHealthPage() {
  const [timeRange, setTimeRange] = useState('24h');
  const [autoRefresh, setAutoRefresh] = useState(true);
  const [isLoading, setIsLoading] = useState(true);
  const [providers, setProviders] = useState<ProviderHealthData[]>([]);
  const [selectedProvider, setSelectedProvider] = useState<ProviderHealthData | null>(null);
  const [healthHistory, setHealthHistory] = useState<Record<string, HealthHistory[]>>({});
  const [incidents, setIncidents] = useState<HealthIncident[]>([]);
  const [providerMetrics, setProviderMetrics] = useState<Record<string, ProviderMetrics>>({});
  const [opened, { open, close }] = useDisclosure(false);

  const fetchProviderHealth = useCallback(async () => {
    try {
      setIsLoading(true);
      const response = await fetch(`/api/provider-health?range=${timeRange}`);
      if (!response.ok) {
        throw new Error('Failed to fetch provider health');
      }
      const data = await response.json();
      
      setProviders(data.providers || []);
      setHealthHistory(data.history || {});
      setIncidents(data.incidents || []);
      setProviderMetrics(data.metrics || {});
    } catch (error) {
      console.error('Error fetching provider health:', error);
      notifications.show({
        title: 'Error',
        message: 'Failed to fetch provider health data',
        color: 'red',
      });
    } finally {
      setIsLoading(false);
    }
  }, [timeRange]);

  useEffect(() => {
    fetchProviderHealth();
    
    if (autoRefresh) {
      const interval = setInterval(fetchProviderHealth, 30000);
      return () => clearInterval(interval);
    }
  }, [fetchProviderHealth, autoRefresh]);

  const getStatusIcon = (status: string) => {
    switch (status) {
      case 'healthy': return <IconCircleCheck size={20} />;
      case 'degraded': return <IconAlertTriangle size={20} />;
      case 'down': return <IconCircleX size={20} />;
      default: return <IconAlertCircle size={20} />;
    }
  };

  const getStatusColor = (status: string): string => {
    switch (status) {
      case 'healthy': return 'green';
      case 'degraded': return 'orange';
      case 'down': return 'red';
      default: return 'gray';
    }
  };

  const handleExport = async () => {
    try {
      const response = await fetch(`/api/provider-health/export?range=${timeRange}`);
      if (!response.ok) throw new Error('Export failed');

      const blob = await response.blob();
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `provider-health-${timeRange}-${new Date().toISOString()}.csv`;
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      document.body.removeChild(a);
    } catch (error) {
      console.error('Export failed:', error);
      notifications.show({
        title: 'Export Failed',
        message: 'Could not export provider health data',
        color: 'red',
      });
    }
  };

  const healthyCount = providers.filter(p => p.status === 'healthy').length;
  const degradedCount = providers.filter(p => p.status === 'degraded').length;
  const downCount = providers.filter(p => p.status === 'down').length;
  const overallHealth = providers.length > 0 ? (healthyCount / providers.length) * 100 : 0;

  return (
    <Stack gap="xl">

      <Group justify="space-between">
        <div>
          <Title order={1}>Provider Health</Title>
          <Text c="dimmed">Monitor LLM provider availability and performance</Text>
        </div>
        <Group>
          <Select
            value={timeRange}
            onChange={(value) => setTimeRange(value || '24h')}
            data={[
              { value: '1h', label: 'Last Hour' },
              { value: '24h', label: 'Last 24 Hours' },
              { value: '7d', label: 'Last 7 Days' },
              { value: '30d', label: 'Last 30 Days' },
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
            onClick={handleExport}
          >
            Export
          </Button>
          <Button
            variant="light"
            leftSection={<IconRefresh size={16} />}
            onClick={fetchProviderHealth}
            loading={isLoading}
          >
            Refresh
          </Button>
        </Group>
      </Group>

      {/* Overall Health Summary */}
      <Grid>
        <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
          <Card withBorder>
            <Group justify="space-between" mb="md">
              <div>
                <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                  Overall Health
                </Text>
                <Text size="xl" fw={700}>
                  {overallHealth.toFixed(0)}%
                </Text>
              </div>
              <RingProgress
                size={60}
                thickness={6}
                sections={[
                  { value: overallHealth, color: 'green' },
                ]}
              />
            </Group>
            <Text size="xs" c="dimmed">
              {healthyCount}/{providers.length} providers operational
            </Text>
          </Card>
        </Grid.Col>

        <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
          <Card withBorder>
            <Group justify="space-between" mb="md">
              <div>
                <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                  Healthy Providers
                </Text>
                <Text size="xl" fw={700} c="green">
                  {healthyCount}
                </Text>
              </div>
              <ThemeIcon color="green" variant="light" size="xl">
                <IconCircleCheck size={24} />
              </ThemeIcon>
            </Group>
          </Card>
        </Grid.Col>

        <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
          <Card withBorder>
            <Group justify="space-between" mb="md">
              <div>
                <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                  Degraded
                </Text>
                <Text size="xl" fw={700} c="orange">
                  {degradedCount}
                </Text>
              </div>
              <ThemeIcon color="orange" variant="light" size="xl">
                <IconAlertTriangle size={24} />
              </ThemeIcon>
            </Group>
          </Card>
        </Grid.Col>

        <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
          <Card withBorder>
            <Group justify="space-between" mb="md">
              <div>
                <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                  Down
                </Text>
                <Text size="xl" fw={700} c="red">
                  {downCount}
                </Text>
              </div>
              <ThemeIcon color="red" variant="light" size="xl">
                <IconCircleX size={24} />
              </ThemeIcon>
            </Group>
          </Card>
        </Grid.Col>
      </Grid>

      {/* Provider Status Table */}
      <Card withBorder>
        <Card.Section withBorder inheritPadding py="xs">
          <Text fw={500}>Provider Status</Text>
        </Card.Section>
        <Card.Section inheritPadding py="md">
          {isLoading ? (
            <CardSkeleton height={400} />
          ) : (
            <ScrollArea>
              <Table striped highlightOnHover>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Provider</Table.Th>
                    <Table.Th>Status</Table.Th>
                    <Table.Th>Uptime</Table.Th>
                    <Table.Th>Response Time</Table.Th>
                    <Table.Th>Error Rate</Table.Th>
                    <Table.Th>Requests</Table.Th>
                    <Table.Th>Last Check</Table.Th>
                    <Table.Th>Actions</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {providers.map((provider) => (
                    <Table.Tr key={provider.id}>
                      <Table.Td>
                        <Text fw={500}>{provider.name}</Text>
                      </Table.Td>
                      <Table.Td>
                        <Badge
                          leftSection={getStatusIcon(provider.status)}
                          color={getStatusColor(provider.status)}
                          variant="light"
                        >
                          {provider.status}
                        </Badge>
                      </Table.Td>
                      <Table.Td>
                        <Text size="sm">{provider.uptime.toFixed(2)}%</Text>
                      </Table.Td>
                      <Table.Td>
                        <Text size="sm">{provider.responseTime}ms</Text>
                      </Table.Td>
                      <Table.Td>
                        <Text size="sm" c={provider.errorRate > 5 ? 'red' : 'dimmed'}>
                          {provider.errorRate.toFixed(1)}%
                        </Text>
                      </Table.Td>
                      <Table.Td>
                        <Text size="sm">{formatters.number(provider.requestCount)}</Text>
                      </Table.Td>
                      <Table.Td>
                        <Text size="sm" c="dimmed">
                          {formatters.date(provider.lastCheck)}
                        </Text>
                      </Table.Td>
                      <Table.Td>
                        <Button
                          size="xs"
                          variant="subtle"
                          onClick={() => {
                            setSelectedProvider(provider);
                            open();
                          }}
                        >
                          Details
                        </Button>
                      </Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </ScrollArea>
          )}
        </Card.Section>
      </Card>

      {/* Recent Incidents */}
      {incidents.length > 0 && (
        <Card withBorder>
          <Card.Section withBorder inheritPadding py="xs">
            <Group justify="space-between">
              <Text fw={500}>Recent Incidents</Text>
              <Badge color="red" variant="light">
                {incidents.length} incidents
              </Badge>
            </Group>
          </Card.Section>
          <Card.Section inheritPadding py="md">
            <Timeline active={-1} bulletSize={24} lineWidth={2}>
              {incidents.slice(0, 5).map((incident) => (
                <Timeline.Item
                  key={incident.id}
                  bullet={
                    <ThemeIcon
                      size={24}
                      variant="light"
                      color={incident.severity === 'high' ? 'red' : incident.severity === 'medium' ? 'orange' : 'yellow'}
                    >
                      <IconAlertCircle size={14} />
                    </ThemeIcon>
                  }
                  title={`${incident.provider} - ${incident.type}`}
                >
                  <Text c="dimmed" size="sm">
                    {formatters.date(incident.startTime)}
                    {incident.endTime && ` - ${formatters.date(incident.endTime)}`}
                  </Text>
                  <Text size="sm" mt={4}>
                    {incident.message}
                  </Text>
                  <Group gap="xs" mt="xs">
                    <Badge size="sm" variant="light">
                      {incident.impact.requestsFailed} requests failed
                    </Badge>
                    <Badge size="sm" variant="light">
                      {incident.impact.usersAffected} users affected
                    </Badge>
                  </Group>
                </Timeline.Item>
              ))}
            </Timeline>
          </Card.Section>
        </Card>
      )}

      {/* Provider Details Modal */}
      <Modal
        opened={opened}
        onClose={close}
        title={selectedProvider ? `${selectedProvider.name} Details` : ''}
        size="xl"
      >
        {selectedProvider && (
          <Stack gap="md">
            <Grid>
              <Grid.Col span={6}>
                <Paper withBorder p="md">
                  <Text size="sm" c="dimmed">Current Status</Text>
                  <Group gap="xs" mt="xs">
                    {getStatusIcon(selectedProvider.status)}
                    <Text fw={500} c={getStatusColor(selectedProvider.status)}>
                      {selectedProvider.status.toUpperCase()}
                    </Text>
                  </Group>
                </Paper>
              </Grid.Col>
              <Grid.Col span={6}>
                <Paper withBorder p="md">
                  <Text size="sm" c="dimmed">Uptime (30 days)</Text>
                  <Text size="xl" fw={700} mt="xs">
                    {selectedProvider.uptime.toFixed(2)}%
                  </Text>
                </Paper>
              </Grid.Col>
            </Grid>

            <Card withBorder>
              <Card.Section withBorder inheritPadding py="xs">
                <Text fw={500}>Model Status</Text>
              </Card.Section>
              <Card.Section inheritPadding py="md">
                <Table>
                  <Table.Thead>
                    <Table.Tr>
                      <Table.Th>Model</Table.Th>
                      <Table.Th>Status</Table.Th>
                      <Table.Th>Performance</Table.Th>
                    </Table.Tr>
                  </Table.Thead>
                  <Table.Tbody>
                    {selectedProvider.models.map((model) => (
                      <Table.Tr key={model.name}>
                        <Table.Td>{model.name}</Table.Td>
                        <Table.Td>
                          <Badge
                            color={model.status === 'available' ? 'green' : model.status === 'maintenance' ? 'orange' : 'red'}
                            variant="light"
                          >
                            {model.status}
                          </Badge>
                        </Table.Td>
                        <Table.Td>
                          <Progress value={model.performance} size="sm" />
                        </Table.Td>
                      </Table.Tr>
                    ))}
                  </Table.Tbody>
                </Table>
              </Card.Section>
            </Card>

            {providerMetrics[selectedProvider.id] && (
              <Card withBorder>
                <Card.Section withBorder inheritPadding py="xs">
                  <Text fw={500}>Performance Metrics</Text>
                </Card.Section>
                <Card.Section inheritPadding py="md">
                  <AreaChart
                    h={300}
                    data={providerMetrics[selectedProvider.id].timestamps.map((timestamp, index) => ({
                      timestamp,
                      responseTime: providerMetrics[selectedProvider.id].responseTime[index],
                      errorRate: providerMetrics[selectedProvider.id].errorRate[index],
                    }))}
                    dataKey="timestamp"
                    series={[
                      { name: 'responseTime', label: 'Response Time (ms)', color: 'blue.6' },
                      { name: 'errorRate', label: 'Error Rate (%)', color: 'red.6' },
                    ]}
                    curveType="linear"
                    withLegend
                    legendProps={{ verticalAlign: 'bottom', height: 50 }}
                  />
                </Card.Section>
              </Card>
            )}
          </Stack>
        )}
      </Modal>
    </Stack>
  );
}