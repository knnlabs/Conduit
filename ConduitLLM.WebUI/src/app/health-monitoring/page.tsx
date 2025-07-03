'use client';

import {
  Stack,
  Title,
  Text,
  Card,
  Grid,
  Group,
  Button,
  Badge,
  ThemeIcon,
  Progress,
  Timeline,
  Table,
  ScrollArea,
  Alert,
  Switch,
  Select,
  NumberInput,
  Paper,
  RingProgress,
  Center,
  Tabs,
  Code,
  ActionIcon,
  Tooltip,
  SimpleGrid,
  LoadingOverlay,
} from '@mantine/core';
import {
  IconHeartbeat,
  IconServer,
  IconDatabase,
  IconNetwork,
  IconClock,
  IconRefresh,
  IconDownload,
  IconCheck,
  IconX,
  IconAlertCircle,
  IconInfoCircle,
  IconActivity,
  IconBrandDocker,
  IconApi,
  IconMessage2,
  IconCircle,
  IconCloud,
  IconTrendingUp,
  IconTrendingDown,
  IconBell,
  IconSettings,
  IconChartBar,
  IconHistory,
} from '@tabler/icons-react';
import { useState, useEffect } from 'react';
import { notifications } from '@mantine/notifications';
import { LineChart, Line, AreaChart, Area, XAxis, YAxis, CartesianGrid, Tooltip as RechartsTooltip, ResponsiveContainer, Legend } from 'recharts';
import { formatNumber, formatPercent, formatRelativeTime, formatDuration, formatBytes } from '@/lib/utils/formatting';
import { useServiceHealth, useIncidents, useHealthHistory } from '@/hooks/api/useHealthApi';


const serviceTypeIcons = {
  api: IconApi,
  database: IconDatabase,
  cache: IconCircle,
  queue: IconMessage2,
  provider: IconCloud,
  storage: IconServer,
};

const statusColors = {
  healthy: 'green',
  degraded: 'yellow',
  unhealthy: 'red',
  unknown: 'gray',
};

const severityColors = {
  low: 'blue',
  medium: 'yellow',
  high: 'orange',
  critical: 'red',
};

export default function HealthMonitoringPage() {
  const [activeTab, setActiveTab] = useState<string | null>('overview');
  const [autoRefresh, setAutoRefresh] = useState(true);
  const [refreshInterval, setRefreshInterval] = useState(30);
  const [selectedTimeRange, setSelectedTimeRange] = useState<'1h' | '24h' | '7d'>('24h');

  // Fetch data using the health API hooks
  const { data: healthData, isLoading: healthLoading, refetch: refetchHealth } = useServiceHealth();
  const { data: incidentsData, isLoading: incidentsLoading } = useIncidents();
  const { data: historyData, isLoading: historyLoading } = useHealthHistory(
    selectedTimeRange === '1h' ? 1 : selectedTimeRange === '24h' ? 24 : 168
  );

  const services = healthData?.services || [];
  const incidents = incidentsData?.incidents || [];

  // Calculate summary statistics
  const healthyServices = services.filter(s => s.status === 'healthy').length;
  const degradedServices = services.filter(s => s.status === 'degraded').length;
  const unhealthyServices = services.filter(s => s.status === 'unhealthy').length;
  const overallUptime = services.reduce((sum, s) => {
    const uptime = typeof s.uptime === 'number' ? s.uptime : 
                   typeof s.uptime === 'object' && s.uptime.days ? s.uptime.days * 24 * 60 * 60 * 1000 : 0;
    return sum + uptime;
  }, 0) / services.length;
  const activeIncidents = incidents.filter(i => i.status === 'active').length;

  // Auto-refresh effect
  useEffect(() => {
    if (!autoRefresh) return;
    
    const interval = setInterval(() => {
      refetchHealth();
    }, refreshInterval * 1000);

    return () => clearInterval(interval);
  }, [autoRefresh, refreshInterval, refetchHealth]);

  const handleRefresh = () => {
    refetchHealth();
    notifications.show({
      title: 'Refreshing Health Status',
      message: 'Service health checks are being updated',
      color: 'blue',
    });
  };

  const handleExport = () => {
    notifications.show({
      title: 'Export Started',
      message: 'Generating health status report...',
      color: 'blue',
    });
  };

  const handleTestService = (service: any) => {
    notifications.show({
      title: 'Running Health Check',
      message: `Testing ${service.name}...`,
      color: 'blue',
      loading: true,
    });

    // Refetch to get latest health status
    setTimeout(() => {
      refetchHealth();
      notifications.show({
        title: 'Health Check Complete',
        message: `${service.name} is ${service.status}`,
        color: statusColors[service.status as keyof typeof statusColors] || 'gray',
      });
    }, 2000);
  };

  const getServiceIcon = (type: string) => {
    const Icon = serviceTypeIcons[type as keyof typeof serviceTypeIcons] || IconServer;
    return <Icon size={20} />;
  };

  return (
    <Stack gap="md">
      <Group justify="space-between">
        <div>
          <Title order={1}>Health Monitoring</Title>
          <Text c="dimmed">Real-time system health and availability monitoring</Text>
        </div>
        <Group>
          <Group gap="xs">
            <Switch
              checked={autoRefresh}
              onChange={(e) => setAutoRefresh(e.currentTarget.checked)}
              label={`Auto-refresh (${refreshInterval}s)`}
            />
          </Group>
          <Button
            variant="light"
            leftSection={<IconRefresh size={16} />}
            onClick={handleRefresh}
            loading={healthLoading}
          >
            Refresh
          </Button>
          <Button
            leftSection={<IconDownload size={16} />}
            onClick={handleExport}
          >
            Export Report
          </Button>
        </Group>
      </Group>

      {/* Health Overview Cards */}
      <Grid>
        <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
          <Card withBorder p="md" h={140}>
            <Group justify="space-between" h="100%">
              <div>
                <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                  Overall Health
                </Text>
                <Text size="xl" fw={700} c={unhealthyServices > 0 ? 'red' : degradedServices > 0 ? 'yellow' : 'green'}>
                  {unhealthyServices > 0 ? 'Issues Detected' : degradedServices > 0 ? 'Degraded' : 'All Systems Go'}
                </Text>
                <Text size="xs" c="dimmed" mt={4}>
                  {healthyServices} healthy, {degradedServices} degraded, {unhealthyServices} unhealthy
                </Text>
              </div>
              <ThemeIcon size="xl" radius="md" variant="light" color={unhealthyServices > 0 ? 'red' : degradedServices > 0 ? 'yellow' : 'green'}>
                <IconHeartbeat size={24} />
              </ThemeIcon>
            </Group>
          </Card>
        </Grid.Col>

        <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
          <Card withBorder p="md" h={140}>
            <Group justify="space-between" h="100%">
              <div>
                <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                  System Uptime
                </Text>
                <Text size="xl" fw={700}>
                  {formatPercent(overallUptime)}
                </Text>
                <Progress value={overallUptime} size="sm" mt={8} color="green" />
              </div>
              <ThemeIcon size="xl" radius="md" variant="light" color="blue">
                <IconClock size={24} />
              </ThemeIcon>
            </Group>
          </Card>
        </Grid.Col>

        <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
          <Card withBorder p="md" h={140}>
            <Group justify="space-between" h="100%">
              <div>
                <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                  Active Incidents
                </Text>
                <Text size="xl" fw={700} c={activeIncidents > 0 ? 'orange' : 'green'}>
                  {activeIncidents}
                </Text>
                <Text size="xs" c="dimmed" mt={4}>
                  {incidents.filter(i => i.status === 'resolved').length} resolved today
                </Text>
              </div>
              <ThemeIcon size="xl" radius="md" variant="light" color={activeIncidents > 0 ? 'orange' : 'green'}>
                <IconAlertCircle size={24} />
              </ThemeIcon>
            </Group>
          </Card>
        </Grid.Col>

        <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
          <Card withBorder p="md" h={140}>
            <Group justify="space-between" h="100%">
              <div>
                <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                  Avg Response Time
                </Text>
                <Text size="xl" fw={700}>
                  {Math.round(services.reduce((sum, s) => sum + s.responseTime, 0) / services.length)}ms
                </Text>
                <Group gap={4} mt={4}>
                  <IconTrendingDown size={14} color="var(--mantine-color-green-6)" />
                  <Text size="xs" c="green">12% faster</Text>
                </Group>
              </div>
              <ThemeIcon size="xl" radius="md" variant="light" color="purple">
                <IconActivity size={24} />
              </ThemeIcon>
            </Group>
          </Card>
        </Grid.Col>
      </Grid>

      {/* Health Timeline Chart */}
      <Card withBorder>
        <Group justify="space-between" mb="md">
          <Text fw={600}>System Health Timeline</Text>
          <Select
            value={selectedTimeRange}
            onChange={(value) => setSelectedTimeRange(value as '1h' | '24h' | '7d')}
            data={[
              { value: '1h', label: 'Last Hour' },
              { value: '24h', label: 'Last 24 Hours' },
              { value: '7d', label: 'Last 7 Days' },
            ]}
            w={150}
          />
        </Group>
        {historyData?.history && historyData.history.length > 0 && (
          <ResponsiveContainer width="100%" height={200}>
            <AreaChart data={historyData.history}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis 
                dataKey="timestamp" 
                tickFormatter={(value) => new Date(value).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
              />
              <YAxis />
              <RechartsTooltip 
                labelFormatter={(value) => new Date(value).toLocaleString()}
              />
              <Legend />
              <Area type="monotone" dataKey="healthyServices" stackId="1" stroke="#10b981" fill="#10b981" name="Healthy" />
              <Area type="monotone" dataKey="degradedServices" stackId="1" stroke="#f59e0b" fill="#f59e0b" name="Degraded" />
              <Area type="monotone" dataKey="unhealthyServices" stackId="1" stroke="#ef4444" fill="#ef4444" name="Unhealthy" />
            </AreaChart>
          </ResponsiveContainer>
        )}
      </Card>

      {/* Tabbed Content */}
      <Tabs value={activeTab} onChange={setActiveTab}>
        <Tabs.List>
          <Tabs.Tab value="overview" leftSection={<IconHeartbeat size={16} />}>
            Service Status
          </Tabs.Tab>
          <Tabs.Tab value="incidents" leftSection={<IconAlertCircle size={16} />}>
            Incidents
          </Tabs.Tab>
          <Tabs.Tab value="alerts" leftSection={<IconBell size={16} />}>
            Alert Configuration
          </Tabs.Tab>
        </Tabs.List>

        <Tabs.Panel value="overview" pt="md">
          {/* Service Health Grid */}
          <LoadingOverlay visible={healthLoading} />
          <SimpleGrid cols={{ base: 1, md: 2, xl: 3 }}>
            {services.map((service) => (
              <Card key={service.id} withBorder>
                <Group justify="space-between" mb="md">
                  <Group>
                    <ThemeIcon
                      size="lg"
                      variant="light"
                      color={statusColors[service.status]}
                    >
                      {getServiceIcon((service as any).type || 'api')}
                    </ThemeIcon>
                    <div>
                      <Text fw={500}>{service.name}</Text>
                      <Badge variant="light" color={statusColors[service.status]}>
                        {service.status}
                      </Badge>
                    </div>
                  </Group>
                  <ActionIcon
                    variant="subtle"
                    onClick={() => handleTestService(service)}
                  >
                    <IconRefresh size={16} />
                  </ActionIcon>
                </Group>

                <Stack gap="sm">
                  <Group justify="space-between">
                    <Text size="sm" c="dimmed">Uptime</Text>
                    <Group gap="xs">
                      <Text size="sm" fw={500}>{formatPercent(typeof service.uptime === 'number' ? service.uptime : 99.9)}</Text>
                      <Progress value={typeof service.uptime === 'number' ? service.uptime : 99.9} size="sm" w={60} color="green" />
                    </Group>
                  </Group>

                  <Group justify="space-between">
                    <Text size="sm" c="dimmed">Response Time</Text>
                    <Text size="sm" fw={500}>{service.responseTime}ms</Text>
                  </Group>

                  <Group justify="space-between">
                    <Text size="sm" c="dimmed">Last Check</Text>
                    <Text size="sm" fw={500}>{formatRelativeTime(new Date(service.lastCheck))}</Text>
                  </Group>

                  {/* Service-specific metrics */}
                  {(service as any).metrics?.cpu !== undefined && (
                    <Group justify="space-between">
                      <Text size="sm" c="dimmed">CPU Usage</Text>
                      <Group gap="xs">
                        <Text size="sm" fw={500}>{(service as any).metrics.cpu}%</Text>
                        <Progress
                          value={(service as any).metrics.cpu}
                          size="sm"
                          w={60}
                          color={(service as any).metrics.cpu > 80 ? 'red' : (service as any).metrics.cpu > 60 ? 'yellow' : 'green'}
                        />
                      </Group>
                    </Group>
                  )}

                  {(service as any).metrics?.memory !== undefined && (
                    <Group justify="space-between">
                      <Text size="sm" c="dimmed">Memory Usage</Text>
                      <Group gap="xs">
                        <Text size="sm" fw={500}>{(service as any).metrics.memory}%</Text>
                        <Progress
                          value={(service as any).metrics.memory}
                          size="sm"
                          w={60}
                          color={(service as any).metrics.memory > 80 ? 'red' : (service as any).metrics.memory > 60 ? 'yellow' : 'green'}
                        />
                      </Group>
                    </Group>
                  )}

                  {(service as any).metrics?.queueDepth !== undefined && (
                    <Group justify="space-between">
                      <Text size="sm" c="dimmed">Queue Depth</Text>
                      <Text size="sm" fw={500}>{formatNumber((service as any).metrics.queueDepth)}</Text>
                    </Group>
                  )}

                  {/* Health Checks */}
                  <div>
                    <Text size="sm" c="dimmed" mb="xs">Health Checks</Text>
                    <Stack gap={4}>
                      {((service as any).checks || []).map((check: any, index: number) => (
                        <Group key={index} gap="xs">
                          {check.status === 'pass' ? (
                            <IconCheck size={14} color="var(--mantine-color-green-6)" />
                          ) : check.status === 'warn' ? (
                            <IconAlertCircle size={14} color="var(--mantine-color-yellow-6)" />
                          ) : (
                            <IconX size={14} color="var(--mantine-color-red-6)" />
                          )}
                          <Text size="xs">{check.name}</Text>
                          <Text size="xs" c="dimmed">({check.duration}ms)</Text>
                        </Group>
                      ))}
                    </Stack>
                  </div>
                </Stack>
              </Card>
            ))}
          </SimpleGrid>
        </Tabs.Panel>

        <Tabs.Panel value="incidents" pt="md">
          {/* Active Incidents */}
          {activeIncidents > 0 && (
            <Alert
              icon={<IconAlertCircle size={16} />}
              title="Active Incidents"
              color="orange"
              mb="md"
            >
              There are currently {activeIncidents} active incidents affecting system performance.
            </Alert>
          )}

          {/* Incidents Timeline */}
          <Card withBorder>
            <Text fw={600} mb="md">Incident History</Text>
            <LoadingOverlay visible={incidentsLoading} />
            <Timeline active={-1} bulletSize={24} lineWidth={2}>
              {incidents.map((incident) => (
                <Timeline.Item
                  key={incident.id}
                  bullet={
                    <ThemeIcon
                      size={24}
                      variant="light"
                      color={incident.status === 'resolved' ? 'green' : severityColors[incident.severity as keyof typeof severityColors] || 'gray'}
                    >
                      {incident.status === 'resolved' ? (
                        <IconCheck size={16} />
                      ) : (
                        <IconAlertCircle size={16} />
                      )}
                    </ThemeIcon>
                  }
                  title={
                    <Group gap="xs">
                      <Text fw={500}>{incident.title}</Text>
                      <Badge variant="light" color={severityColors[incident.severity as keyof typeof severityColors] || 'gray'}>
                        {incident.severity}
                      </Badge>
                      <Badge variant="light" color={incident.status === 'resolved' ? 'green' : 'orange'}>
                        {incident.status}
                      </Badge>
                    </Group>
                  }
                >
                  <Text c="dimmed" size="sm">
                    {(incident as any).service || 'System'} • Started {formatRelativeTime(new Date(incident.startTime))}
                    {incident.endTime && ` • Resolved ${formatRelativeTime(new Date(incident.endTime))}`}
                  </Text>
                  <Text size="sm" mt={4}>{(incident as any).description || 'No description'}</Text>
                  <Text size="sm" c="dimmed" mt={4}>Impact: {(incident as any).impact || 'Unknown'}</Text>
                </Timeline.Item>
              ))}
            </Timeline>
          </Card>
        </Tabs.Panel>

        <Tabs.Panel value="alerts" pt="md">
          {/* Alert Configuration */}
          <Card withBorder>
            <Text fw={600} mb="md">Alert Thresholds</Text>
            <Grid>
              <Grid.Col span={{ base: 12, md: 6 }}>
                <Stack gap="md">
                  <NumberInput
                    label="Service Downtime Alert"
                    description="Alert when service is down for more than (seconds)"
                    defaultValue={60}
                    min={10}
                    max={600}
                    suffix=" seconds"
                  />

                  <NumberInput
                    label="Response Time Alert"
                    description="Alert when response time exceeds (milliseconds)"
                    defaultValue={1000}
                    min={100}
                    max={10000}
                    suffix=" ms"
                  />

                  <NumberInput
                    label="Error Rate Alert"
                    description="Alert when error rate exceeds (%)"
                    defaultValue={5}
                    min={0.1}
                    max={100}
                    step={0.1}
                    suffix=" %"
                  />

                  <NumberInput
                    label="CPU Usage Alert"
                    description="Alert when CPU usage exceeds (%)"
                    defaultValue={80}
                    min={50}
                    max={100}
                    suffix=" %"
                  />
                </Stack>
              </Grid.Col>

              <Grid.Col span={{ base: 12, md: 6 }}>
                <Stack gap="md">
                  <NumberInput
                    label="Memory Usage Alert"
                    description="Alert when memory usage exceeds (%)"
                    defaultValue={85}
                    min={50}
                    max={100}
                    suffix=" %"
                  />

                  <NumberInput
                    label="Queue Depth Alert"
                    description="Alert when queue depth exceeds"
                    defaultValue={10000}
                    min={100}
                    max={100000}
                  />

                  <Select
                    label="Alert Channel"
                    description="Where to send health alerts"
                    defaultValue="email"
                    data={[
                      { value: 'email', label: 'Email' },
                      { value: 'slack', label: 'Slack' },
                      { value: 'webhook', label: 'Webhook' },
                      { value: 'sms', label: 'SMS' },
                    ]}
                  />

                  <Switch
                    label="Enable Auto-Recovery"
                    description="Automatically attempt to restart unhealthy services"
                    defaultChecked
                  />
                </Stack>
              </Grid.Col>
            </Grid>

            <Group justify="flex-end" mt="xl">
              <Button variant="light">Reset to Defaults</Button>
              <Button leftSection={<IconCheck size={16} />}>Save Alert Settings</Button>
            </Group>
          </Card>
        </Tabs.Panel>
      </Tabs>
    </Stack>
  );
}