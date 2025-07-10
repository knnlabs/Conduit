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
  Badge,
  Table,
  Progress,
  ActionIcon,
  Tooltip,
  Modal,
} from '@mantine/core';
import {
  IconActivity,
  IconDownload,
  IconRefresh,
  IconTrendingUp,
  IconTrendingDown,
  IconClock,
  IconUsers,
  IconServer,
  IconAlertCircle as IconBugReport,
  IconChartLine,
  IconEye,
} from '@tabler/icons-react';
import { useState } from 'react';
import { useDisclosure } from '@mantine/hooks';
import { 
  useUsageMetrics,
  useModelUsage,
  useKeyUsage,
  useExportUsageAnalytics,
  useCostByKey
} from '@/hooks/useConduitAdmin';
import type { DateRange } from '@knn_labs/conduit-admin-client';
import { notifications } from '@mantine/notifications';
import { CostChart, type ChartDataItem } from '@/components/charts/CostChart';
import { formatters } from '@/lib/utils/formatters';
import { badgeHelpers } from '@/lib/utils/ui-helpers';
import { BaseTable, type ColumnDef } from '@/components/common/BaseTable';

export default function UsageAnalyticsPage() {
  const [timeRangeValue, setTimeRangeValue] = useState('24h');
  const [selectedTab, setSelectedTab] = useState('overview');
  const [detailsOpened, { open: openDetails, close: closeDetails }] = useDisclosure(false);
  interface EndpointDetails {
    endpoint: string;
    requests: number;
    avgLatency: number;
    errorRate: number;
    popularModels: string[];
    requestsOverTime?: ChartDataItem[];
  }
  
  const [selectedEndpoint, setSelectedEndpoint] = useState<EndpointDetails | null>(null);
  
  // Convert time range to DateRange for SDK
  const getDateRange = (): DateRange => {
    const now = new Date();
    const start = new Date();
    
    switch (timeRangeValue) {
      case '1h':
        start.setHours(now.getHours() - 1);
        break;
      case '24h':
        start.setDate(now.getDate() - 1);
        break;
      case '7d':
        start.setDate(now.getDate() - 7);
        break;
      case '30d':
        start.setDate(now.getDate() - 30);
        break;
      case '90d':
        start.setDate(now.getDate() - 90);
        break;
    }
    
    return {
      startDate: start.toISOString(),
      endDate: now.toISOString()
    };
  };
  
  const dateRange = getDateRange();
  
  const { data: usageMetrics, isLoading: metricsLoading } = useUsageMetrics(dateRange);
  const { data: costByKey, isLoading: keyUsageLoading } = useCostByKey(dateRange);
  const exportUsageData = useExportUsageAnalytics();
  
  // Derive additional data from the main metrics
  const requestVolume = usageMetrics ? {
    totalRequests: usageMetrics.totalRequests,
    successfulRequests: usageMetrics.successfulRequests,
    failedRequests: usageMetrics.failedRequests,
    requestsPerMinute: usageMetrics.requestsPerMinute
  } : null;
  
  const errorAnalytics = usageMetrics ? {
    errorRate: usageMetrics.errorRate,
    totalErrors: usageMetrics.failedRequests
  } : null;
  
  const latencyMetrics = usageMetrics ? {
    averageLatency: usageMetrics.averageLatency,
    p95Latency: usageMetrics.p95Latency,
    p99Latency: usageMetrics.p99Latency
  } : null;
  
  const userAnalytics = costByKey?.costByKey;
  
  const isLoading = metricsLoading || keyUsageLoading;


  const handleExport = async (type: 'metrics' | 'requests' | 'tokens' | 'errors' | 'latency' | 'users' | 'endpoints') => {
    try {
      notifications.show({
        id: 'export-start',
        title: 'Export Started',
        message: `Preparing ${type} data for export...`,
        color: 'blue',
        loading: true,
        autoClose: false,
      });
      
      const result = await exportUsageData.mutateAsync({ 
        type: 'usage', 
        filters: { 
          startDate: dateRange.startDate, 
          endDate: dateRange.endDate 
        }
      });
      
      notifications.update({
        id: 'export-start',
        title: 'Export Complete',
        message: `${type} data exported as ${result.filename}`,
        color: 'green',
        loading: false,
        autoClose: 5000,
      });
      
      // Download URL available at: result.url
    } catch (error: unknown) {
      notifications.update({
        id: 'export-start',
        title: 'Export Failed',
        message: (error as Error).message || 'Failed to export data',
        color: 'red',
        loading: false,
        autoClose: 5000,
      });
    }
  };

  const handleRefresh = () => {
    notifications.show({
      title: 'Refreshing Data',
      message: 'Updating usage analytics...',
      color: 'blue',
    });
  };


  const getStatusColor = (rate: number, type: 'error' | 'success') => {
    if (type === 'error') {
      return badgeHelpers.getPercentageColor(rate, { danger: 5, warning: 2, good: 0 });
    } else {
      // For success rate, reverse the logic
      return badgeHelpers.getPercentageColor(100 - rate, { danger: 5, warning: 2, good: 0 });
    }
  };

  const openEndpointDetails = (endpoint: unknown) => {
    // Convert from table data to detail format
    const endpointData = endpoint as { endpoint: string; requests: number; averageLatency: number; errorRate: number };
    const details: EndpointDetails = {
      endpoint: endpointData.endpoint,
      requests: endpointData.requests,
      avgLatency: endpointData.averageLatency,
      errorRate: endpointData.errorRate,
      popularModels: ['gpt-4', 'gpt-3.5-turbo'], // Mock data
      requestsOverTime: requestVolume as unknown as ChartDataItem[],
    };
    setSelectedEndpoint(details);
    openDetails();
  };

  const metricCards = usageMetrics ? [
    {
      title: 'Total Requests',
      value: formatters.number(usageMetrics.totalRequests),
      icon: IconActivity,
      color: 'blue',
    },
    {
      title: 'Requests/Min',
      value: formatters.number(usageMetrics.requestsPerMinute),
      icon: IconChartLine,
      color: 'green',
    },
    {
      title: 'Active Keys',
      value: usageMetrics.uniqueKeys,
      icon: IconUsers,
      color: 'purple',
    },
    {
      title: 'Avg Latency',
      value: formatters.responseTime(usageMetrics.averageLatency),
      icon: IconClock,
      color: 'orange',
    },
    {
      title: 'Error Rate',
      value: `${usageMetrics.errorRate.toFixed(1)}%`,
      icon: IconBugReport,
      color: getStatusColor(usageMetrics.errorRate, 'error'),
    },
    {
      title: 'Success Rate',
      value: `${((1 - usageMetrics.errorRate / 100) * 100).toFixed(1)}%`,
      icon: IconServer,
      color: getStatusColor((1 - usageMetrics.errorRate / 100) * 100, 'success'),
    },
  ] : [];

  return (
    <Stack gap="xl">
      <Group justify="space-between">
        <div>
          <Title order={1}>Usage Analytics</Title>
          <Text c="dimmed">Monitor API usage, performance, and user behavior</Text>
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
              { value: '90d', label: 'Last 3 Months' },
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
          <Button
            leftSection={<IconDownload size={16} />}
            onClick={() => handleExport('metrics')}
          >
            Export Report
          </Button>
        </Group>
      </Group>

      {/* Statistics Cards */}
      <SimpleGrid cols={{ base: 1, sm: 2, md: 3, lg: 6 }} spacing="lg">
        {metricCards.map((stat) => (
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
          </Card>
        ))}
      </SimpleGrid>

      {/* Analytics Tabs */}
      <Card>
        <Tabs value={selectedTab} onChange={(value) => setSelectedTab(value || 'overview')}>
          <Tabs.List>
            <Tabs.Tab value="overview">Overview</Tabs.Tab>
            <Tabs.Tab value="requests">Request Volume</Tabs.Tab>
            <Tabs.Tab value="tokens">Token Usage</Tabs.Tab>
            <Tabs.Tab value="errors">Error Analysis</Tabs.Tab>
            <Tabs.Tab value="latency">Latency Metrics</Tabs.Tab>
            <Tabs.Tab value="users">User Analytics</Tabs.Tab>
            <Tabs.Tab value="endpoints">Endpoint Usage</Tabs.Tab>
          </Tabs.List>

          <Tabs.Panel value="overview" pt="md">
            <div style={{ position: 'relative' }}>
              <LoadingOverlay visible={isLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
              
              <SimpleGrid cols={{ base: 1, md: 2 }} spacing="lg">
                <CostChart
                  data={(requestVolume || []) as unknown as ChartDataItem[]}
                  title="Request Volume Over Time"
                  type="line"
                  valueKey="requests"
                  nameKey="timestamp"
                  timeKey="timestamp"
                  onRefresh={handleRefresh}
                  onExport={() => handleExport('requests')}
                />
                
                <CostChart
                  data={[]}
                  title="Token Usage Over Time"
                  type="line"
                  valueKey="totalTokens"
                  nameKey="timestamp"
                  timeKey="timestamp"
                  onRefresh={handleRefresh}
                  onExport={() => handleExport('tokens')}
                />
              </SimpleGrid>
            </div>
          </Tabs.Panel>

          <Tabs.Panel value="requests" pt="md">
            <div style={{ position: 'relative' }}>
              <LoadingOverlay visible={isLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
              
              <SimpleGrid cols={{ base: 1, md: 2 }} spacing="lg">
                <CostChart
                  data={(requestVolume || []) as unknown as ChartDataItem[]}
                  title="Request Volume"
                  type="bar"
                  valueKey="requests"
                  nameKey="timestamp"
                  timeKey="timestamp"
                  onRefresh={handleRefresh}
                  onExport={() => handleExport('requests')}
                />
                
                <CostChart
                  data={(requestVolume || []) as unknown as ChartDataItem[]}
                  title="Success vs Failed Requests"
                  type="line"
                  valueKey="successfulRequests"
                  nameKey="timestamp"
                  timeKey="timestamp"
                  onRefresh={handleRefresh}
                  onExport={() => handleExport('requests')}
                />
              </SimpleGrid>
            </div>
          </Tabs.Panel>

          <Tabs.Panel value="tokens" pt="md">
            <div style={{ position: 'relative' }}>
              <LoadingOverlay visible={isLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
              
              <SimpleGrid cols={{ base: 1, md: 2 }} spacing="lg">
                <CostChart
                  data={[]}
                  title="Input vs Output Tokens"
                  type="line"
                  valueKey="inputTokens"
                  nameKey="timestamp"
                  timeKey="timestamp"
                  onRefresh={handleRefresh}
                  onExport={() => handleExport('tokens')}
                />
                
                <CostChart
                  data={[]}
                  title="Tokens per Request"
                  type="line"
                  valueKey="averageTokensPerRequest"
                  nameKey="timestamp"
                  timeKey="timestamp"
                  onRefresh={handleRefresh}
                  onExport={() => handleExport('tokens')}
                />
              </SimpleGrid>
            </div>
          </Tabs.Panel>

          <Tabs.Panel value="errors" pt="md">
            <div style={{ position: 'relative' }}>
              <LoadingOverlay visible={isLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
              
              <SimpleGrid cols={{ base: 1, md: 2 }} spacing="lg">
                <CostChart
                  data={(errorAnalytics || []) as unknown as ChartDataItem[]}
                  title="Error Distribution"
                  type="pie"
                  valueKey="count"
                  nameKey="errorType"
                  onRefresh={handleRefresh}
                  onExport={() => handleExport('errors')}
                />
                
                <Card withBorder>
                  <Card.Section p="md" withBorder>
                    <Group justify="space-between">
                      <Text fw={600}>Error Details</Text>
                      <Button
                        size="xs"
                        variant="light"
                        leftSection={<IconDownload size={14} />}
                        onClick={() => handleExport('errors')}
                      >
                        Export
                      </Button>
                    </Group>
                  </Card.Section>
                  <Card.Section>
                    <Table>
                      <Table.Thead>
                        <Table.Tr>
                          <Table.Th>Error Type</Table.Th>
                          <Table.Th>Count</Table.Th>
                          <Table.Th>Percentage</Table.Th>
                          <Table.Th>Last Seen</Table.Th>
                        </Table.Tr>
                      </Table.Thead>
                      <Table.Tbody>
                        {errorAnalytics && (
                          <Table.Tr>
                            <Table.Td>
                              <Text fw={500}>Total Errors</Text>
                            </Table.Td>
                            <Table.Td>
                              <Badge variant="light" color="red">
                                {formatters.number(errorAnalytics.totalErrors)}
                              </Badge>
                            </Table.Td>
                            <Table.Td>{errorAnalytics.errorRate.toFixed(1)}%</Table.Td>
                            <Table.Td>
                              <Text size="sm" c="dimmed">
                                Current period
                              </Text>
                            </Table.Td>
                          </Table.Tr>
                        )}
                      </Table.Tbody>
                    </Table>
                  </Card.Section>
                </Card>
              </SimpleGrid>
            </div>
          </Tabs.Panel>

          <Tabs.Panel value="latency" pt="md">
            <div style={{ position: 'relative' }}>
              <LoadingOverlay visible={isLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
              
              <Card withBorder>
                <Card.Section p="md" withBorder>
                  <Group justify="space-between">
                    <Text fw={600}>Latency Metrics by Endpoint</Text>
                    <Button
                      size="xs"
                      variant="light"
                      leftSection={<IconDownload size={14} />}
                      onClick={() => handleExport('latency')}
                    >
                      Export
                    </Button>
                  </Group>
                </Card.Section>
                <Card.Section>
                  <Table>
                    <Table.Thead>
                      <Table.Tr>
                        <Table.Th>Endpoint</Table.Th>
                        <Table.Th>Avg Latency</Table.Th>
                        <Table.Th>P50</Table.Th>
                        <Table.Th>P90</Table.Th>
                        <Table.Th>P95</Table.Th>
                        <Table.Th>P99</Table.Th>
                        <Table.Th>Requests</Table.Th>
                      </Table.Tr>
                    </Table.Thead>
                    <Table.Tbody>
                      {latencyMetrics && (
                        <Table.Tr>
                          <Table.Td>
                            <Text fw={500} size="sm">Overall</Text>
                          </Table.Td>
                          <Table.Td>{formatters.responseTime(latencyMetrics.averageLatency)}</Table.Td>
                          <Table.Td>-</Table.Td>
                          <Table.Td>-</Table.Td>
                          <Table.Td>{formatters.responseTime(latencyMetrics.p95Latency)}</Table.Td>
                          <Table.Td>{formatters.responseTime(latencyMetrics.p99Latency)}</Table.Td>
                          <Table.Td>
                            <Badge variant="light">
                              {formatters.number(usageMetrics?.totalRequests || 0)}
                            </Badge>
                          </Table.Td>
                        </Table.Tr>
                      )}
                    </Table.Tbody>
                  </Table>
                </Card.Section>
              </Card>
            </div>
          </Tabs.Panel>

          <Tabs.Panel value="users" pt="md">
            <div style={{ position: 'relative' }}>
              <LoadingOverlay visible={keyUsageLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
              
              <BaseTable
                data={userAnalytics || []}
                isLoading={keyUsageLoading}
                searchable
                searchPlaceholder="Search virtual keys..."
                onRefresh={handleRefresh}
                emptyMessage="No user analytics data available"
                columns={[
                  {
                    key: 'virtualKeyName',
                    label: 'Virtual Key',
                    sortable: true,
                    filterable: true,
                    render: (user) => <Text fw={500}>{user.virtualKeyName}</Text>
                  },
                  {
                    key: 'totalRequests',
                    label: 'Requests',
                    sortable: true,
                    sortType: 'number',
                    render: (user) => (
                      <Badge variant="light">
                        {formatters.number(user.totalRequests)}
                      </Badge>
                    )
                  },
                  {
                    key: 'totalTokens',
                    label: 'Tokens',
                    sortable: true,
                    sortType: 'number',
                    render: (user) => (
                      <Badge variant="light">
                        {formatters.number(user.totalTokens)}
                      </Badge>
                    )
                  },
                  {
                    key: 'averageLatency',
                    label: 'Avg Latency',
                    sortable: true,
                    sortType: 'number',
                    render: (user) => formatters.responseTime(user.averageLatency)
                  },
                  {
                    key: 'errorRate',
                    label: 'Error Rate',
                    sortable: true,
                    sortType: 'number',
                    render: (user) => (
                      <Badge color={getStatusColor(user.errorRate, 'error')} variant="light">
                        {user.errorRate.toFixed(1)}%
                      </Badge>
                    )
                  },
                  {
                    key: 'topModels',
                    label: 'Top Models',
                    render: (user) => (
                      <Group gap="xs">
                        {user.topModels.slice(0, 2).map((model: any) => (
                          <Badge key={model} size="xs" variant="light">
                            {model}
                          </Badge>
                        ))}
                        {user.topModels.length > 2 && (
                          <Badge size="xs" variant="light" color="gray">
                            +{user.topModels.length - 2}
                          </Badge>
                        )}
                      </Group>
                    )
                  },
                  {
                    key: 'lastActivity',
                    label: 'Last Activity',
                    sortable: true,
                    sortType: 'date',
                    render: (user) => (
                      <Text size="sm" c="dimmed">
                        {formatters.date(user.lastActivity)}
                      </Text>
                    )
                  }
                ] as ColumnDef<any>[]}
                customActions={[
                  {
                    label: 'Export',
                    icon: IconDownload,
                    onClick: () => handleExport('users'),
                    tooltip: 'Export user analytics'
                  }
                ]}
              />
            </div>
          </Tabs.Panel>

          <Tabs.Panel value="endpoints" pt="md">
            <div style={{ position: 'relative' }}>
              <LoadingOverlay visible={isLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
              
              <Card withBorder>
                <Card.Section p="md" withBorder>
                  <Group justify="space-between">
                    <Text fw={600}>Endpoint Usage Analytics</Text>
                    <Button
                      size="xs"
                      variant="light"
                      leftSection={<IconDownload size={14} />}
                      onClick={() => handleExport('endpoints')}
                    >
                      Export
                    </Button>
                  </Group>
                </Card.Section>
                <Card.Section>
                  <Table>
                    <Table.Thead>
                      <Table.Tr>
                        <Table.Th>Endpoint</Table.Th>
                        <Table.Th>Requests</Table.Th>
                        <Table.Th>Avg Latency</Table.Th>
                        <Table.Th>Success Rate</Table.Th>
                        <Table.Th>Cost/Request</Table.Th>
                        <Table.Th>Popular Models</Table.Th>
                        <Table.Th>Actions</Table.Th>
                      </Table.Tr>
                    </Table.Thead>
                    <Table.Tbody>
                      <Table.Tr>
                        <Table.Td colSpan={7}>
                          <Text size="sm" c="dimmed" ta="center">
                            Endpoint-specific analytics not available in SDK
                          </Text>
                        </Table.Td>
                      </Table.Tr>
                    </Table.Tbody>
                  </Table>
                </Card.Section>
              </Card>
            </div>
          </Tabs.Panel>
        </Tabs>
      </Card>

      {/* Endpoint Details Modal */}
      <Modal
        opened={detailsOpened}
        onClose={closeDetails}
        title="Endpoint Details"
        size="lg"
      >
        {selectedEndpoint && (
          <Stack gap="md">
            <Group justify="space-between">
              <div>
                <Text fw={600} size="lg">{selectedEndpoint.endpoint}</Text>
                <Badge color="gray" variant="light">
                  HTTP
                </Badge>
              </div>
              <Group gap="xs">
                <Badge variant="light">
                  {formatters.number(selectedEndpoint.requests)} requests
                </Badge>
                <Badge color={getStatusColor((1 - selectedEndpoint.errorRate) * 100, 'success')} variant="light">
                  {((1 - selectedEndpoint.errorRate) * 100).toFixed(1)}% success
                </Badge>
              </Group>
            </Group>
            
            <SimpleGrid cols={2} spacing="lg">
              <Card withBorder>
                <Text size="sm" c="dimmed" mb="xs">Average Latency</Text>
                <Text fw={600} size="xl">{formatters.responseTime(selectedEndpoint.avgLatency)}</Text>
              </Card>
              <Card withBorder>
                <Text size="sm" c="dimmed" mb="xs">Cost per Request</Text>
                <Text fw={600} size="xl">{formatters.currency(0.0042, { precision: 4 })}</Text>
              </Card>
            </SimpleGrid>
            
            <div>
              <Text fw={500} mb="xs">Popular Models</Text>
              <Group gap="xs">
                {selectedEndpoint.popularModels.map((model) => (
                  <Badge key={model} variant="light">
                    {model}
                  </Badge>
                ))}
              </Group>
            </div>
            
            <CostChart
              data={selectedEndpoint.requestsOverTime || []}
              title="Request Volume Over Time"
              type="line"
              valueKey="requests"
              nameKey="timestamp"
              timeKey="timestamp"
            />
          </Stack>
        )}
      </Modal>
    </Stack>
  );
}