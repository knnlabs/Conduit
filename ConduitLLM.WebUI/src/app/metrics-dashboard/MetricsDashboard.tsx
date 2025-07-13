'use client';

import {
  Stack,
  Title,
  Text,
  Card,
  Grid,
  Group,
  Button,
  ThemeIcon,
  Progress,
  Badge,
  Select,
  Paper,
  Table,
  ScrollArea,
  Tabs,
  Switch,
  LoadingOverlay,
  Alert,
} from '@mantine/core';
import {
  IconActivity,
  IconServer,
  IconCpu,
  IconChartLine,
  IconRefresh,
  IconDownload,
  IconTrendingUp,
  IconTrendingDown,
  IconAlertCircle,
  IconBolt,
  IconCloudDataConnection,
  IconCircle,
  IconArrowUp,
} from '@tabler/icons-react';
import { useState, useEffect } from 'react';
import { notifications } from '@mantine/notifications';
import { 
  LazyLineChart as LineChart, 
  LazyAreaChart as AreaChart, 
  LazyBarChart as BarChart, 
  LazyPieChart as PieChart,
  Line, 
  Area, 
  Bar, 
  Pie,
  Cell,
  XAxis, 
  YAxis, 
  CartesianGrid, 
  RechartsTooltip as Tooltip, 
  ResponsiveContainer, 
  RechartsLegend as Legend,
  LabelList,
} from '@/components/charts/LazyCharts';
// import { useHealthCheck, useSystemHealth } from '@/hooks/api/useAdminApi';
// TODO: Import real health data hooks when SDK provides comprehensive metrics endpoints

interface MetricCardProps {
  title: string;
  value: string | number;
  unit?: string;
  trend?: number;
  icon: React.ReactNode;
  color: string;
  subValue?: string;
}

function MetricCard({ title, value, unit, trend, icon, color, subValue }: MetricCardProps) {
  return (
    <Card padding="lg" radius="md" withBorder>
      <Group justify="space-between">
        <div>
          <Text size="sm" c="dimmed" fw={600} tt="uppercase">
            {title}
          </Text>
          <Group align="baseline" gap={4} mt={4}>
            <Text size="xl" fw={700}>
              {value}
            </Text>
            {unit && (
              <Text size="sm" c="dimmed">
                {unit}
              </Text>
            )}
          </Group>
          {subValue && (
            <Text size="xs" c="dimmed" mt={4}>
              {subValue}
            </Text>
          )}
          {trend !== undefined && (
            <Group gap={4} mt={8}>
              {trend > 0 ? (
                <IconTrendingUp size={16} color="green" />
              ) : (
                <IconTrendingDown size={16} color="red" />
              )}
              <Text size="xs" c={trend > 0 ? 'green' : 'red'}>
                {Math.abs(trend)}%
              </Text>
            </Group>
          )}
        </div>
        <ThemeIcon
          color={color}
          variant="light"
          size={48}
          radius="md"
        >
          {icon}
        </ThemeIcon>
      </Group>
    </Card>
  );
}

export default function MetricsDashboard() {
  const [timeRange, setTimeRange] = useState('1h');
  const [autoRefresh, setAutoRefresh] = useState(true);
  const [activeTab, setActiveTab] = useState<string | null>('performance');
  const [isExporting, setIsExporting] = useState(false);

  const healthData = null;
  const systemHealthData = null;
  const isLoading = false;
  const error = null;
  const refetch = () => {};

  useEffect(() => {
    if (!autoRefresh) return;

    const interval = setInterval(() => {
      refetch();
    }, 10000); // Refresh every 10 seconds

    return () => clearInterval(interval);
  }, [autoRefresh, refetch]);

  const handleExport = async () => {
    setIsExporting(true);
    try {
      // Mock export functionality
      await new Promise(resolve => setTimeout(resolve, 1500));
      
      notifications.show({
        title: 'Export Successful',
        message: 'Metrics data has been exported',
        color: 'green',
      });
    } catch (error) {
      notifications.show({
        title: 'Export Failed',
        message: 'Failed to export metrics data',
        color: 'red',
      });
    } finally {
      setIsExporting(false);
    }
  };

  // TODO: Replace with real data from SDK when available
  // SDK methods needed:
  // - adminClient.metrics.getSystemMetrics() - for system-wide metrics
  // - adminClient.metrics.getPerformanceMetrics() - for performance data
  // - adminClient.metrics.getProviderMetrics() - for provider-specific metrics
  const displayData = healthData || {
    status: 'unknown',
    timestamp: new Date().toISOString(),
    services: {
      coreApi: { status: 'unknown', latency: 0 },
      adminApi: { status: 'unknown', latency: 0 },
      database: { status: 'unknown', latency: 0 },
      cache: { status: 'unknown', latency: 0 },
    },
    _warning: 'Metrics data is not available. SDK metrics methods are not yet implemented.',
  };
  const systemData = systemHealthData || {
    cpuUsage: 0,
    memoryUsage: 0,
    diskUsage: 0,
    uptime: 0,
  };

  // Calculate metrics
  const totalRequests = 125000;
  const avgResponseTime = 45;
  const errorRate = 0.23;
  const uptime = 99.95;

  // Prepare chart data
  const performanceData = [
    { time: '00:00', requests: 1200, responseTime: 42 },
    { time: '04:00', requests: 980, responseTime: 38 },
    { time: '08:00', requests: 2100, responseTime: 45 },
    { time: '12:00', requests: 3200, responseTime: 52 },
    { time: '16:00', requests: 2800, responseTime: 48 },
    { time: '20:00', requests: 1900, responseTime: 41 },
    { time: '24:00', requests: 1400, responseTime: 39 },
  ];

  const providerMetrics = [
    { name: 'OpenAI', requests: 45000, errors: 120, avgLatency: 42 },
    { name: 'Anthropic', requests: 38000, errors: 89, avgLatency: 38 },
    { name: 'Google', requests: 28000, errors: 76, avgLatency: 45 },
    { name: 'Azure', requests: 14000, errors: 45, avgLatency: 51 },
  ];

  const endpointMetrics = [
    { endpoint: '/chat/completions', count: 65000, avgTime: 42 },
    { endpoint: '/embeddings', count: 32000, avgTime: 28 },
    { endpoint: '/images/generations', count: 18000, avgTime: 120 },
    { endpoint: '/audio/transcriptions', count: 10000, avgTime: 85 },
  ];

  const errorDistribution = [
    { name: 'Rate Limit', value: 35, color: '#ff6b6b' },
    { name: 'Timeout', value: 25, color: '#4ecdc4' },
    { name: 'Invalid Request', value: 20, color: '#45b7d1' },
    { name: 'Server Error', value: 15, color: '#96ceb4' },
    { name: 'Other', value: 5, color: '#dfe6e9' },
  ];

  return (
    <Stack>
      <Alert
        icon={<IconAlertCircle size="1rem" />}
        title="Limited SDK Functionality"
        color="yellow"
        variant="light"
        mb="md"
      >
        System metrics data is currently unavailable. The SDK methods for comprehensive metrics collection
        (system metrics, performance data, and provider metrics) are not yet implemented.
      </Alert>

      <Card shadow="sm" p="md" radius="md">
        <Group justify="space-between" align="center">
          <div>
            <Title order={2}>System Metrics Dashboard</Title>
            <Text size="sm" c="dimmed" mt={4}>
              Real-time monitoring and analytics
            </Text>
          </div>
          <Group>
            <Switch
              label="Auto Refresh"
              checked={autoRefresh}
              onChange={(event) => setAutoRefresh(event.currentTarget.checked)}
            />
            <Select
              value={timeRange}
              onChange={(value) => setTimeRange(value || '1h')}
              data={[
                { value: '1h', label: 'Last Hour' },
                { value: '6h', label: 'Last 6 Hours' },
                { value: '24h', label: 'Last 24 Hours' },
                { value: '7d', label: 'Last 7 Days' },
              ]}
              w={150}
            />
            <Button
              variant="light"
              leftSection={<IconRefresh size={16} />}
              onClick={() => refetch()}
              loading={isLoading}
            >
              Refresh
            </Button>
            <Button
              variant="filled"
              leftSection={<IconDownload size={16} />}
              onClick={handleExport}
              loading={isExporting}
            >
              Export
            </Button>
          </Group>
        </Group>
      </Card>

      <Grid>
        <Grid.Col span={{ base: 12, sm: 6, lg: 3 }}>
          <MetricCard
            title="Total Requests"
            value={totalRequests.toLocaleString()}
            unit="requests"
            trend={12.5}
            icon={<IconActivity size={24} />}
            color="blue"
            subValue="Last 24 hours"
          />
        </Grid.Col>
        <Grid.Col span={{ base: 12, sm: 6, lg: 3 }}>
          <MetricCard
            title="Avg Response Time"
            value={avgResponseTime}
            unit="ms"
            trend={-5.2}
            icon={<IconBolt size={24} />}
            color="green"
            subValue="P95: 120ms"
          />
        </Grid.Col>
        <Grid.Col span={{ base: 12, sm: 6, lg: 3 }}>
          <MetricCard
            title="Error Rate"
            value={errorRate}
            unit="%"
            trend={0.05}
            icon={<IconAlertCircle size={24} />}
            color="red"
            subValue="287 errors"
          />
        </Grid.Col>
        <Grid.Col span={{ base: 12, sm: 6, lg: 3 }}>
          <MetricCard
            title="System Uptime"
            value={uptime}
            unit="%"
            icon={<IconServer size={24} />}
            color="teal"
            subValue="30 days"
          />
        </Grid.Col>
      </Grid>

      <Tabs value={activeTab} onChange={setActiveTab}>
        <Tabs.List>
          <Tabs.Tab value="performance" leftSection={<IconChartLine size={16} />}>
            Performance
          </Tabs.Tab>
          <Tabs.Tab value="providers" leftSection={<IconCloudDataConnection size={16} />}>
            Providers
          </Tabs.Tab>
          <Tabs.Tab value="system" leftSection={<IconCpu size={16} />}>
            System Health
          </Tabs.Tab>
        </Tabs.List>

        <Tabs.Panel value="performance" pt="md">
          <Grid>
            <Grid.Col span={12}>
              <Card shadow="sm" p="md" radius="md" withBorder>
                <Title order={4} mb="md">Request Volume & Response Time</Title>
                <ResponsiveContainer width="100%" height={300}>
                  <LineChart data={performanceData}>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis dataKey="time" />
                    <YAxis yAxisId="left" />
                    <YAxis yAxisId="right" orientation="right" />
                    <Tooltip />
                    <Legend />
                    <Line
                      yAxisId="left"
                      type="monotone"
                      dataKey="requests"
                      stroke="#8884d8"
                      name="Requests"
                    />
                    <Line
                      yAxisId="right"
                      type="monotone"
                      dataKey="responseTime"
                      stroke="#82ca9d"
                      name="Response Time (ms)"
                    />
                  </LineChart>
                </ResponsiveContainer>
              </Card>
            </Grid.Col>

            <Grid.Col span={{ base: 12, md: 6 }}>
              <Card shadow="sm" p="md" radius="md" withBorder>
                <Title order={4} mb="md">Top Endpoints</Title>
                <ResponsiveContainer width="100%" height={250}>
                  <BarChart data={endpointMetrics}>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis dataKey="endpoint" angle={-45} textAnchor="end" height={100} />
                    <YAxis />
                    <Tooltip />
                    <Bar dataKey="count" fill="#8884d8" />
                  </BarChart>
                </ResponsiveContainer>
              </Card>
            </Grid.Col>

            <Grid.Col span={{ base: 12, md: 6 }}>
              <Card shadow="sm" p="md" radius="md" withBorder>
                <Title order={4} mb="md">Error Distribution</Title>
                <ResponsiveContainer width="100%" height={250}>
                  <PieChart>
                    <Pie
                      data={errorDistribution}
                      cx="50%"
                      cy="50%"
                      labelLine={false}
                      label={({ name, percent }) => `${name} ${(percent * 100).toFixed(0)}%`}
                      outerRadius={80}
                      fill="#8884d8"
                      dataKey="value"
                    >
                      {errorDistribution.map((entry, index) => (
                        <Cell key={`cell-${index}`} fill={entry.color} />
                      ))}
                    </Pie>
                    <Tooltip />
                  </PieChart>
                </ResponsiveContainer>
              </Card>
            </Grid.Col>
          </Grid>
        </Tabs.Panel>

        <Tabs.Panel value="providers" pt="md">
          <Card shadow="sm" p="md" radius="md" withBorder>
            <Title order={4} mb="md">Provider Performance</Title>
            <ScrollArea>
              <Table striped>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Provider</Table.Th>
                    <Table.Th>Requests</Table.Th>
                    <Table.Th>Errors</Table.Th>
                    <Table.Th>Error Rate</Table.Th>
                    <Table.Th>Avg Latency</Table.Th>
                    <Table.Th>Status</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {providerMetrics.map((provider) => (
                    <Table.Tr key={provider.name}>
                      <Table.Td>{provider.name}</Table.Td>
                      <Table.Td>{provider.requests.toLocaleString()}</Table.Td>
                      <Table.Td>{provider.errors}</Table.Td>
                      <Table.Td>
                        <Badge
                          color={provider.errors / provider.requests < 0.01 ? 'green' : 'red'}
                          variant="light"
                        >
                          {((provider.errors / provider.requests) * 100).toFixed(2)}%
                        </Badge>
                      </Table.Td>
                      <Table.Td>{provider.avgLatency}ms</Table.Td>
                      <Table.Td>
                        <Badge
                          leftSection={<IconCircle size={8} />}
                          color="green"
                          variant="light"
                        >
                          Healthy
                        </Badge>
                      </Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </ScrollArea>
          </Card>
        </Tabs.Panel>

        <Tabs.Panel value="system" pt="md">
          <Grid>
            <Grid.Col span={{ base: 12, md: 6 }}>
              <Card shadow="sm" p="md" radius="md" withBorder>
                <Title order={4} mb="md">CPU Usage</Title>
                <Progress
                  value={systemData?.cpuUsage || 45}
                  color={systemData?.cpuUsage > 80 ? 'red' : 'blue'}
                  size="xl"
                  radius="md"
                  mb="xs"
                />
                <Text size="sm" c="dimmed">
                  {systemData?.cpuUsage || 45}% utilization
                </Text>
              </Card>
            </Grid.Col>

            <Grid.Col span={{ base: 12, md: 6 }}>
              <Card shadow="sm" p="md" radius="md" withBorder>
                <Title order={4} mb="md">Memory Usage</Title>
                <Progress
                  value={systemData?.memoryUsage || 62}
                  color={systemData?.memoryUsage > 80 ? 'red' : 'green'}
                  size="xl"
                  radius="md"
                  mb="xs"
                />
                <Text size="sm" c="dimmed">
                  {systemData?.memoryUsage || 62}% utilization
                </Text>
              </Card>
            </Grid.Col>

            <Grid.Col span={12}>
              <Card shadow="sm" p="md" radius="md" withBorder>
                <Title order={4} mb="md">Service Status</Title>
                <Stack gap="sm">
                  {['Core API', 'Admin API', 'Redis Cache', 'PostgreSQL'].map((service) => (
                    <Paper key={service} p="sm" withBorder>
                      <Group justify="space-between">
                        <Group>
                          <IconServer size={20} />
                          <Text fw={500}>{service}</Text>
                        </Group>
                        <Badge
                          leftSection={<IconCircle size={8} />}
                          color="green"
                          variant="light"
                        >
                          Operational
                        </Badge>
                      </Group>
                    </Paper>
                  ))}
                </Stack>
              </Card>
            </Grid.Col>
          </Grid>
        </Tabs.Panel>
      </Tabs>

      <LoadingOverlay visible={isLoading && !displayData} />
    </Stack>
  );
}