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
  LineChart, 
  Line, 
  AreaChart, 
  Area, 
  BarChart, 
  Bar, 
  XAxis, 
  YAxis, 
  CartesianGrid, 
  Tooltip as RechartsTooltip, 
  ResponsiveContainer, 
  Legend, 
  PieChart, 
  Pie, 
  Cell 
} from 'recharts';
import { formatters } from '@/lib/utils/formatters';
import { useRealtimeMetrics, useTimeSeriesMetrics, useProviderMetrics } from '@/hooks/api/useDashboardApi';

const COLORS = ['#3b82f6', '#10b981', '#f59e0b', '#ef4444', '#8b5cf6', '#ec4899'];

export default function MetricsDashboardPage() {
  const [selectedTimeRange, setSelectedTimeRange] = useState<'hour' | 'day' | 'week' | 'month'>('hour');
  const [autoRefresh, setAutoRefresh] = useState(true);
  const [_selectedProvider, _setSelectedProvider] = useState<string | null>(null);

  // Fetch data using the dashboard API hooks
  const { data: realtimeData, isLoading: realtimeLoading, refetch: refetchRealtime } = useRealtimeMetrics();
  const { data: timeSeriesData, isLoading: timeSeriesLoading } = useTimeSeriesMetrics(selectedTimeRange);
  const { data: providerData, isLoading: providerLoading } = useProviderMetrics();

  // Auto-refresh effect
  useEffect(() => {
    if (!autoRefresh) return;
    
    const interval = setInterval(() => {
      refetchRealtime();
    }, (realtimeData?.refreshIntervalSeconds || 10) * 1000);

    return () => clearInterval(interval);
  }, [autoRefresh, refetchRealtime, realtimeData?.refreshIntervalSeconds]);

  const handleRefresh = () => {
    refetchRealtime();
    notifications.show({
      title: 'Refreshing Metrics',
      message: 'Dashboard metrics are being updated',
      color: 'blue',
    });
  };

  const handleExport = () => {
    notifications.show({
      title: 'Export Started',
      message: 'Preparing metrics report for download',
      color: 'blue',
    });
  };

  // Calculate trend for a metric
  const _calculateTrend = (current: number, previous: number) => {
    if (!previous) return { value: 0, direction: 'stable' as const };
    const change = ((current - previous) / previous) * 100;
    return {
      value: Math.abs(change),
      direction: change > 0 ? 'up' as const : change < 0 ? 'down' as const : 'stable' as const
    };
  };

  const _getTrendIcon = (direction: 'up' | 'down' | 'stable') => {
    switch (direction) {
      case 'up':
        return <IconTrendingUp size={16} />;
      case 'down':
        return <IconTrendingDown size={16} />;
      default:
        return <IconArrowUp size={16} style={{ transform: 'rotate(90deg)' }} />;
    }
  };

  const _getTrendColor = (direction: 'up' | 'down' | 'stable', inverse = false) => {
    if (direction === 'stable') return 'gray';
    if (inverse) {
      return direction === 'up' ? 'red' : 'green';
    }
    return direction === 'up' ? 'green' : 'red';
  };

  return (
    <Stack gap="md">
      <Group justify="space-between">
        <div>
          <Title order={1}>Metrics Dashboard</Title>
          <Text c="dimmed">Real-time system performance and usage metrics</Text>
        </div>
        <Group>
          <Switch
            label="Auto-refresh"
            checked={autoRefresh}
            onChange={(event) => setAutoRefresh(event.currentTarget.checked)}
          />
          <Select
            value={selectedTimeRange}
            onChange={(value) => setSelectedTimeRange(value as typeof selectedTimeRange)}
            data={[
              { value: 'hour', label: 'Last Hour' },
              { value: 'day', label: 'Last 24 Hours' },
              { value: 'week', label: 'Last 7 Days' },
              { value: 'month', label: 'Last 30 Days' },
            ]}
            w={150}
          />
          <Button
            variant="light"
            leftSection={<IconRefresh size={16} />}
            onClick={handleRefresh}
          >
            Refresh
          </Button>
          <Button
            leftSection={<IconDownload size={16} />}
            onClick={handleExport}
          >
            Export
          </Button>
        </Group>
      </Group>

      {/* Key Metrics Overview */}
      <Grid>
        <Grid.Col span={{ base: 12, md: 3 }}>
          <Card>
            <Group justify="space-between" mb="xs">
              <Text size="sm" c="dimmed">Total Requests</Text>
              <ThemeIcon color="blue" variant="light" size="sm">
                <IconActivity size={16} />
              </ThemeIcon>
            </Group>
            <Group align="baseline" gap="xs">
              <Text size="xl" fw={700}>
                {formatters.number(realtimeData?.system.totalRequestsDay || 0)}
              </Text>
              <Badge color="gray" variant="light" size="sm">
                /day
              </Badge>
            </Group>
            <Text size="xs" c="dimmed" mt="xs">
              {formatters.number(realtimeData?.system.totalRequestsHour || 0)} in last hour
            </Text>
          </Card>
        </Grid.Col>

        <Grid.Col span={{ base: 12, md: 3 }}>
          <Card>
            <Group justify="space-between" mb="xs">
              <Text size="sm" c="dimmed">Average Latency</Text>
              <ThemeIcon color="green" variant="light" size="sm">
                <IconBolt size={16} />
              </ThemeIcon>
            </Group>
            <Group align="baseline" gap="xs">
              <Text size="xl" fw={700}>
                {formatters.responseTime(realtimeData?.system.avgLatencyHour || 0)}
              </Text>
            </Group>
            <Progress
              value={Math.min((realtimeData?.system.avgLatencyHour || 0) / 500 * 100, 100)}
              color={(realtimeData?.system.avgLatencyHour || 0) < 200 ? 'green' : (realtimeData?.system.avgLatencyHour || 0) < 500 ? 'yellow' : 'red'}
              size="sm"
              mt="xs"
            />
          </Card>
        </Grid.Col>

        <Grid.Col span={{ base: 12, md: 3 }}>
          <Card>
            <Group justify="space-between" mb="xs">
              <Text size="sm" c="dimmed">Error Rate</Text>
              <ThemeIcon color="red" variant="light" size="sm">
                <IconAlertCircle size={16} />
              </ThemeIcon>
            </Group>
            <Group align="baseline" gap="xs">
              <Text size="xl" fw={700}>
                {formatters.percentage(realtimeData?.system.errorRateHour || 0)}
              </Text>
            </Group>
            <Progress
              value={realtimeData?.system.errorRateHour || 0}
              color={(realtimeData?.system.errorRateHour || 0) < 1 ? 'green' : (realtimeData?.system.errorRateHour || 0) < 5 ? 'yellow' : 'red'}
              size="sm"
              mt="xs"
            />
          </Card>
        </Grid.Col>

        <Grid.Col span={{ base: 12, md: 3 }}>
          <Card>
            <Group justify="space-between" mb="xs">
              <Text size="sm" c="dimmed">Active Services</Text>
              <ThemeIcon color="teal" variant="light" size="sm">
                <IconServer size={16} />
              </ThemeIcon>
            </Group>
            <Group align="baseline" gap="xs">
              <Text size="xl" fw={700}>
                {realtimeData?.system.activeProviders || 0} / {realtimeData?.system.activeKeys || 0}
              </Text>
            </Group>
            <Text size="xs" c="dimmed" mt="xs">
              Providers / Virtual Keys
            </Text>
          </Card>
        </Grid.Col>
      </Grid>

      <Tabs defaultValue="overview">
        <Tabs.List>
          <Tabs.Tab value="overview" leftSection={<IconChartLine size={16} />}>
            Overview
          </Tabs.Tab>
          <Tabs.Tab value="providers" leftSection={<IconCloudDataConnection size={16} />}>
            Providers
          </Tabs.Tab>
          <Tabs.Tab value="models" leftSection={<IconCpu size={16} />}>
            Models
          </Tabs.Tab>
          <Tabs.Tab value="realtime" leftSection={<IconActivity size={16} />}>
            Real-time
          </Tabs.Tab>
        </Tabs.List>

        <Tabs.Panel value="overview" pt="md">
          <Stack gap="md">
            {/* Time Series Chart */}
            <Card withBorder>
              <Title order={3} mb="md">Request Volume & Latency</Title>
              <LoadingOverlay visible={timeSeriesLoading} />
              {timeSeriesData?.series && timeSeriesData.series.length > 0 && (
                <ResponsiveContainer width="100%" height={300}>
                  <LineChart data={timeSeriesData.series}>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis 
                      dataKey="timestamp" 
                      tickFormatter={(value) => new Date(value).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                    />
                    <YAxis yAxisId="left" />
                    <YAxis yAxisId="right" orientation="right" />
                    <RechartsTooltip 
                      labelFormatter={(value) => new Date(value).toLocaleString()}
                    />
                    <Legend />
                    <Line 
                      yAxisId="left" 
                      type="monotone" 
                      dataKey="requests" 
                      stroke="#3b82f6" 
                      name="Requests"
                      strokeWidth={2}
                    />
                    <Line 
                      yAxisId="right" 
                      type="monotone" 
                      dataKey="avgLatency" 
                      stroke="#10b981" 
                      name="Avg Latency (ms)"
                      strokeWidth={2}
                    />
                  </LineChart>
                </ResponsiveContainer>
              )}
            </Card>

            {/* Error Rate Chart */}
            <Card withBorder>
              <Title order={3} mb="md">Error Rate & Cost</Title>
              {timeSeriesData?.series && timeSeriesData.series.length > 0 && (
                <ResponsiveContainer width="100%" height={300}>
                  <AreaChart data={timeSeriesData.series}>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis 
                      dataKey="timestamp" 
                      tickFormatter={(value) => new Date(value).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                    />
                    <YAxis yAxisId="left" />
                    <YAxis yAxisId="right" orientation="right" />
                    <RechartsTooltip 
                      labelFormatter={(value) => new Date(value).toLocaleString()}
                    />
                    <Legend />
                    <Area 
                      yAxisId="left"
                      type="monotone" 
                      dataKey="errors" 
                      stroke="#ef4444" 
                      fill="#ef4444" 
                      fillOpacity={0.3}
                      name="Errors"
                    />
                    <Area 
                      yAxisId="right"
                      type="monotone" 
                      dataKey="totalCost" 
                      stroke="#f59e0b" 
                      fill="#f59e0b" 
                      fillOpacity={0.3}
                      name="Cost ($)"
                    />
                  </AreaChart>
                </ResponsiveContainer>
              )}
            </Card>
          </Stack>
        </Tabs.Panel>

        <Tabs.Panel value="providers" pt="md">
          <Stack gap="md">
            {/* Provider Performance Table */}
            <Card withBorder>
              <Title order={3} mb="md">Provider Performance</Title>
              <LoadingOverlay visible={providerLoading} />
              <ScrollArea>
                <Table striped highlightOnHover>
                  <Table.Thead>
                    <Table.Tr>
                      <Table.Th>Provider</Table.Th>
                      <Table.Th>Status</Table.Th>
                      <Table.Th>Requests</Table.Th>
                      <Table.Th>Success Rate</Table.Th>
                      <Table.Th>Avg Latency</Table.Th>
                      <Table.Th>P95 Latency</Table.Th>
                      <Table.Th>Total Cost</Table.Th>
                    </Table.Tr>
                  </Table.Thead>
                  <Table.Tbody>
                    {providerData?.modelMetrics?.map((provider, index) => {
                      const successRate = provider.metrics.totalRequests > 0 
                        ? (provider.metrics.successfulRequests / provider.metrics.totalRequests) * 100 
                        : 0;
                      
                      return (
                        <Table.Tr key={index}>
                          <Table.Td>
                            <Group gap="xs">
                              <ThemeIcon size="xs" variant="light">
                                <IconCircle size={8} fill="currentColor" />
                              </ThemeIcon>
                              <Text size="sm" fw={500}>{provider.model}</Text>
                            </Group>
                          </Table.Td>
                          <Table.Td>
                            <Badge
                              color={successRate > 99 ? 'green' : successRate > 95 ? 'yellow' : 'red'}
                              variant="light"
                            >
                              {successRate > 99 ? 'Healthy' : successRate > 95 ? 'Degraded' : 'Unhealthy'}
                            </Badge>
                          </Table.Td>
                          <Table.Td>{formatters.number(provider.metrics.totalRequests)}</Table.Td>
                          <Table.Td>
                            <Group gap="xs">
                              <Text size="sm">{formatters.percentage(successRate)}</Text>
                              <Progress value={successRate} color={successRate > 99 ? 'green' : successRate > 95 ? 'yellow' : 'red'} size="xs" style={{ width: 60 }} />
                            </Group>
                          </Table.Td>
                          <Table.Td>{formatters.responseTime(provider.metrics.avgLatency)}</Table.Td>
                          <Table.Td>{formatters.responseTime(provider.metrics.p95Latency)}</Table.Td>
                          <Table.Td>${provider.metrics.totalCost.toFixed(2)}</Table.Td>
                        </Table.Tr>
                      );
                    })}
                  </Table.Tbody>
                </Table>
              </ScrollArea>
            </Card>

            {/* Provider Health History */}
            {providerData?.healthHistory && providerData.healthHistory.length > 0 && (
              <Card withBorder>
                <Title order={3} mb="md">Provider Health History</Title>
                <ResponsiveContainer width="100%" height={300}>
                  <BarChart data={providerData.healthHistory}>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis dataKey="provider" />
                    <YAxis />
                    <RechartsTooltip />
                    <Legend />
                    <Bar dataKey="successRate" fill="#10b981" name="Success Rate %" />
                    <Bar dataKey="avgResponseTime" fill="#3b82f6" name="Avg Response Time (ms)" />
                  </BarChart>
                </ResponsiveContainer>
              </Card>
            )}
          </Stack>
        </Tabs.Panel>

        <Tabs.Panel value="models" pt="md">
          <Stack gap="md">
            {/* Model Performance Table */}
            <Card withBorder>
              <Title order={3} mb="md">Model Performance</Title>
              <LoadingOverlay visible={realtimeLoading} />
              <ScrollArea>
                <Table striped highlightOnHover>
                  <Table.Thead>
                    <Table.Tr>
                      <Table.Th>Model</Table.Th>
                      <Table.Th>Requests</Table.Th>
                      <Table.Th>Avg Latency</Table.Th>
                      <Table.Th>Total Tokens</Table.Th>
                      <Table.Th>Total Cost</Table.Th>
                      <Table.Th>Error Rate</Table.Th>
                    </Table.Tr>
                  </Table.Thead>
                  <Table.Tbody>
                    {realtimeData?.modelMetrics?.map((model, index) => (
                      <Table.Tr key={index}>
                        <Table.Td>
                          <Text size="sm" fw={500}>{model.model}</Text>
                        </Table.Td>
                        <Table.Td>{formatters.number(model.requestCount)}</Table.Td>
                        <Table.Td>{formatters.responseTime(model.avgLatency)}</Table.Td>
                        <Table.Td>{formatters.number(model.totalTokens)}</Table.Td>
                        <Table.Td>${model.totalCost.toFixed(2)}</Table.Td>
                        <Table.Td>
                          <Group gap="xs">
                            <Text size="sm">{formatters.percentage(model.errorRate)}</Text>
                            <Progress 
                              value={model.errorRate} 
                              color={model.errorRate < 1 ? 'green' : model.errorRate < 5 ? 'yellow' : 'red'} 
                              size="xs" 
                              style={{ width: 60 }} 
                            />
                          </Group>
                        </Table.Td>
                      </Table.Tr>
                    ))}
                  </Table.Tbody>
                </Table>
              </ScrollArea>
            </Card>

            {/* Model Distribution Chart */}
            {realtimeData?.modelMetrics && realtimeData.modelMetrics.length > 0 && (
              <Card withBorder>
                <Title order={3} mb="md">Request Distribution by Model</Title>
                <ResponsiveContainer width="100%" height={300}>
                  <PieChart>
                    <Pie
                      data={realtimeData.modelMetrics}
                      cx="50%"
                      cy="50%"
                      labelLine={false}
                      label={({ model, percent }) => `${model}: ${(percent * 100).toFixed(0)}%`}
                      outerRadius={80}
                      fill="#8884d8"
                      dataKey="requestCount"
                      nameKey="model"
                    >
                      {realtimeData.modelMetrics.map((entry, index) => (
                        <Cell key={`cell-${index}`} fill={COLORS[index % COLORS.length]} />
                      ))}
                    </Pie>
                    <RechartsTooltip />
                  </PieChart>
                </ResponsiveContainer>
              </Card>
            )}
          </Stack>
        </Tabs.Panel>

        <Tabs.Panel value="realtime" pt="md">
          <Stack gap="md">
            {/* Real-time Provider Status */}
            <Card withBorder>
              <Title order={3} mb="md">Provider Status</Title>
              <LoadingOverlay visible={realtimeLoading} />
              <Grid>
                {realtimeData?.providerStatus?.map((provider, index) => (
                  <Grid.Col key={index} span={{ base: 12, sm: 6, md: 4 }}>
                    <Paper p="md" withBorder>
                      <Group justify="space-between" mb="xs">
                        <Text fw={500}>{provider.providerName}</Text>
                        <Badge
                          color={provider.isEnabled ? (provider.lastHealthCheck?.isHealthy ? 'green' : 'red') : 'gray'}
                          variant="dot"
                        >
                          {provider.isEnabled ? (provider.lastHealthCheck?.isHealthy ? 'Online' : 'Offline') : 'Disabled'}
                        </Badge>
                      </Group>
                      {provider.lastHealthCheck && (
                        <>
                          <Text size="xs" c="dimmed">
                            Response Time: {formatters.responseTime(provider.lastHealthCheck.responseTime)}
                          </Text>
                          <Text size="xs" c="dimmed">
                            Last Check: {formatters.date(provider.lastHealthCheck.checkedAt)}
                          </Text>
                        </>
                      )}
                    </Paper>
                  </Grid.Col>
                ))}
              </Grid>
            </Card>

            {/* Top Virtual Keys */}
            <Card withBorder>
              <Title order={3} mb="md">Top Virtual Keys (Today)</Title>
              <ScrollArea>
                <Table>
                  <Table.Thead>
                    <Table.Tr>
                      <Table.Th>Key Name</Table.Th>
                      <Table.Th>Requests</Table.Th>
                      <Table.Th>Cost</Table.Th>
                      <Table.Th>Budget Used</Table.Th>
                    </Table.Tr>
                  </Table.Thead>
                  <Table.Tbody>
                    {realtimeData?.topKeys?.map((key) => (
                      <Table.Tr key={key.id}>
                        <Table.Td>
                          <Text size="sm" fw={500}>{key.name}</Text>
                        </Table.Td>
                        <Table.Td>{formatters.number(key.requestsToday)}</Table.Td>
                        <Table.Td>${key.costToday.toFixed(2)}</Table.Td>
                        <Table.Td>
                          <Group gap="xs">
                            <Text size="sm">{formatters.percentage(key.budgetUtilization)}</Text>
                            <Progress 
                              value={key.budgetUtilization} 
                              color={key.budgetUtilization < 80 ? 'blue' : key.budgetUtilization < 95 ? 'yellow' : 'red'} 
                              size="xs" 
                              style={{ width: 60 }} 
                            />
                          </Group>
                        </Table.Td>
                      </Table.Tr>
                    ))}
                  </Table.Tbody>
                </Table>
              </ScrollArea>
            </Card>

            {/* System Information */}
            <Card withBorder>
              <Title order={3} mb="md">System Information</Title>
              <Text size="sm" c="dimmed" mb="xs">
                Last Updated: {realtimeData?.timestamp ? new Date(realtimeData.timestamp).toLocaleString() : 'Never'}
              </Text>
              <Text size="sm" c="dimmed">
                Auto-refresh: Every {realtimeData?.refreshIntervalSeconds || 10} seconds
              </Text>
            </Card>
          </Stack>
        </Tabs.Panel>
      </Tabs>
    </Stack>
  );
}