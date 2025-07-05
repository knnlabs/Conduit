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
  Alert,
  Badge,
  Table,
  Progress,
} from '@mantine/core';
import {
  IconChartBar,
  IconDownload,
  IconRefresh,
  IconAlertCircle,
  IconTrendingUp,
  IconTrendingDown,
  IconCreditCard,
  IconActivity,
  IconCalendar,
} from '@tabler/icons-react';
import { useState } from 'react';
import { CostChart, type ChartDataItem } from '@/components/charts/CostChart';
import { useVirtualKeys } from '@/hooks/api/useAdminApi';
import { 
  useCostSummary, 
  useCostTrends, 
  useProviderCosts, 
  useModelCosts, 
  useVirtualKeyCosts, 
  useCostAlerts,
  useExportCostData,
  TimeRangeFilter 
} from '@/hooks/api/useAnalyticsApi';
import { notifications } from '@mantine/notifications';
import GlobalTaskMonitor from '@/components/realtime/GlobalTaskMonitor';
import { formatters } from '@/lib/utils/formatters';
import { badgeHelpers } from '@/lib/utils/badge-helpers';

export default function CostDashboardPage() {
  const [timeRangeValue, setTimeRangeValue] = useState('7d');
  const [selectedTab, setSelectedTab] = useState('overview');
  const [selectedVirtualKey, setSelectedVirtualKey] = useState('');
  
  const timeRange: TimeRangeFilter = { range: timeRangeValue as '7d' | '30d' | '90d' | 'custom' };
  
  const { data: _virtualKeys, isLoading: keysLoading, error } = useVirtualKeys();
  const { data: costSummary, isLoading: summaryLoading } = useCostSummary(timeRange);
  const { data: costTrends, isLoading: trendsLoading } = useCostTrends(timeRange);
  const { data: providerCosts, isLoading: providersLoading } = useProviderCosts(timeRange);
  const { data: modelCosts, isLoading: modelsLoading } = useModelCosts(timeRange);
  const { data: virtualKeyCosts, isLoading: keysCostsLoading } = useVirtualKeyCosts(timeRange);
  const { data: costAlerts } = useCostAlerts();
  const exportCostData = useExportCostData();
  
  const isLoading = keysLoading || summaryLoading;


  // Use real cost summary data
  const stats = costSummary;

  const handleExport = async (type: 'summary' | 'trends' | 'providers' | 'models' | 'virtual-keys') => {
    try {
      notifications.show({
        id: 'export-start',
        title: 'Export Started',
        message: `Preparing ${type} data for export...`,
        color: 'blue',
        loading: true,
        autoClose: false,
      });
      
      const result = await exportCostData.mutateAsync({ type, timeRange });
      
      notifications.update({
        id: 'export-start',
        title: 'Export Complete',
        message: `${type} data exported as ${result.filename}`,
        color: 'green',
        loading: false,
        autoClose: 5000,
      });
      
      // In a real implementation, this would trigger a download
      console.log('Download URL:', result.url);
    } catch (error: unknown) {
      notifications.update({
        id: 'export-start',
        title: 'Export Failed',
        message: error instanceof Error ? error.message : 'Failed to export data',
        color: 'red',
        loading: false,
        autoClose: 5000,
      });
    }
  };

  const handleRefresh = () => {
    notifications.show({
      title: 'Refreshing Data',
      message: 'Updating cost analytics...',
      color: 'blue',
    });
  };

  const getBudgetUsagePercentage = () => {
    if (!stats || !stats.totalBudget) return 0;
    return Math.min((stats.totalSpend / stats.totalBudget) * 100, 100);
  };
  
  const unacknowledgedAlerts = costAlerts?.filter(alert => !alert.acknowledged) || [];
  const highPriorityAlerts = unacknowledgedAlerts.filter(alert => alert.severity === 'high');

  const getBudgetUsageColor = () => {
    const percentage = getBudgetUsagePercentage();
    return badgeHelpers.getPercentageColor(percentage, { danger: 90, warning: 75, good: 0 });
  };

  const statCards = stats ? [
    {
      title: 'Total Spend',
      value: formatters.currency(stats.totalSpend),
      icon: IconCreditCard,
      color: 'blue',
      trend: stats.spendTrend > 0 ? `+${stats.spendTrend.toFixed(1)}%` : `${stats.spendTrend.toFixed(1)}%`,
      trendUp: stats.spendTrend > 0,
    },
    {
      title: 'Total Budget',
      value: formatters.currency(stats.totalBudget),
      icon: IconChartBar,
      color: 'green',
    },
    {
      title: 'Active Keys',
      value: stats.activeVirtualKeys,
      icon: IconActivity,
      color: 'purple',
    },
    {
      title: 'Total Requests',
      value: formatters.number(stats.totalRequests),
      icon: IconTrendingUp,
      color: 'orange',
      trend: stats.requestTrend > 0 ? `+${stats.requestTrend.toFixed(1)}%` : `${stats.requestTrend.toFixed(1)}%`,
      trendUp: stats.requestTrend > 0,
    },
    {
      title: 'Avg Cost/Request',
      value: formatters.currency(stats.averageCostPerRequest),
      icon: IconCalendar,
      color: 'teal',
    },
  ] : [];

  if (error) {
    return (
      <Stack gap="xl">
        <div>
          <Title order={1}>Cost Analytics</Title>
          <Text c="dimmed">Monitor spending and usage analytics</Text>
        </div>
        
        <Alert 
          icon={<IconAlertCircle size={16} />} 
          title="Error loading cost data"
          color="red"
        >
          {error instanceof Error ? error.message : 'Failed to load cost analytics. Please try again.'}
        </Alert>
      </Stack>
    );
  }

  return (
    <Stack gap="xl">
      <Group justify="space-between">
        <div>
          <Title order={1}>Cost Analytics</Title>
          <Text c="dimmed">Monitor spending and usage analytics</Text>
        </div>

        <Group>
          <Select
            value={timeRangeValue}
            onChange={(value) => setTimeRangeValue(value || '7d')}
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
            onClick={() => handleExport('summary')}
          >
            Export Report
          </Button>
        </Group>
      </Group>

      {/* Statistics Cards */}
      {/* Cost Alerts */}
      {highPriorityAlerts.length > 0 && (
        <Alert icon={<IconAlertCircle size={16} />} color="red" title="Budget Alerts">
          <Stack gap="xs">
            {highPriorityAlerts.slice(0, 3).map((alert) => (
              <Text key={alert.id} size="sm">
                {alert.message}
              </Text>
            ))}
            {highPriorityAlerts.length > 3 && (
              <Text size="sm" c="dimmed">
                +{highPriorityAlerts.length - 3} more alerts
              </Text>
            )}
          </Stack>
        </Alert>
      )}
      
      {/* Statistics Cards */}
      <SimpleGrid cols={{ base: 1, sm: 2, md: 5 }} spacing="lg">
        {statCards.map((stat) => (
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
            {stat.trend && (
              <Group gap={4} mt={4}>
                <ThemeIcon
                  size="xs"
                  variant="light"
                  color={stat.trendUp ? 'green' : 'red'}
                >
                  {stat.trendUp ? <IconTrendingUp size={12} /> : <IconTrendingDown size={12} />}
                </ThemeIcon>
                <Text size="xs" c={stat.trendUp ? 'green' : 'red'}>
                  {stat.trend} from last period
                </Text>
              </Group>
            )}
          </Card>
        ))}
      </SimpleGrid>

      {/* Budget Usage Overview */}
      {stats && stats.totalBudget > 0 && (
        <Card>
          <Card.Section p="md" withBorder>
            <Group justify="space-between">
              <Text fw={600}>Budget Usage</Text>
              <Badge color={getBudgetUsageColor()} variant="light">
                {getBudgetUsagePercentage().toFixed(1)}% Used
              </Badge>
            </Group>
          </Card.Section>
          <Card.Section p="md">
            <Stack gap="xs">
              <Group justify="space-between">
                <Text size="sm">
                  {formatters.currency(stats.totalSpend)} of {formatters.currency(stats.totalBudget)}
                </Text>
                <Text size="sm" c="dimmed">
                  {formatters.currency(stats.totalBudget - stats.totalSpend)} remaining
                </Text>
              </Group>
              <Progress
                value={getBudgetUsagePercentage()}
                color={getBudgetUsageColor()}
                size="lg"
                radius="md"
              />
            </Stack>
          </Card.Section>
        </Card>
      )}

      {/* Analytics Tabs */}
      <Card>
        <Tabs value={selectedTab} onChange={(value) => setSelectedTab(value || 'overview')}>
          <Tabs.List>
            <Tabs.Tab value="overview">Overview</Tabs.Tab>
            <Tabs.Tab value="providers">By Provider</Tabs.Tab>
            <Tabs.Tab value="models">By Model</Tabs.Tab>
            <Tabs.Tab value="keys">By Virtual Key</Tabs.Tab>
          </Tabs.List>

          <Tabs.Panel value="overview" pt="md">
            <div style={{ position: 'relative' }}>
              <LoadingOverlay visible={trendsLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
              
              <SimpleGrid cols={{ base: 1, md: 2 }} spacing="lg">
                <CostChart
                  data={(costTrends || []) as unknown as ChartDataItem[]}
                  title="Spending Over Time"
                  type="line"
                  valueKey="spend"
                  nameKey="date"
                  timeKey="date"
                  onRefresh={handleRefresh}
                  onExport={() => handleExport('trends')}
                />
                
                <CostChart
                  data={(costTrends || []) as unknown as ChartDataItem[]}
                  title="Request Volume"
                  type="bar"
                  valueKey="requests"
                  nameKey="date"
                  timeKey="date"
                  onRefresh={handleRefresh}
                  onExport={() => handleExport('trends')}
                />
              </SimpleGrid>
              
              {/* Global Task Monitor for real-time monitoring */}
              {selectedVirtualKey && (
                <div style={{ marginTop: '1rem' }}>
                  <GlobalTaskMonitor virtualKey={selectedVirtualKey} />
                </div>
              )}
            </div>
          </Tabs.Panel>

          <Tabs.Panel value="providers" pt="md">
            <div style={{ position: 'relative' }}>
              <LoadingOverlay visible={providersLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
              
              <SimpleGrid cols={{ base: 1, md: 2 }} spacing="lg">
                <CostChart
                  data={(providerCosts || []) as unknown as ChartDataItem[]}
                  title="Spend by Provider"
                  type="pie"
                  valueKey="spend"
                  nameKey="provider"
                  onRefresh={handleRefresh}
                  onExport={() => handleExport('providers')}
                />
                
                <Card withBorder>
                  <Card.Section p="md" withBorder>
                    <Group justify="space-between">
                      <Text fw={600}>Provider Breakdown</Text>
                      <Button
                        size="xs"
                        variant="light"
                        leftSection={<IconDownload size={14} />}
                        onClick={() => handleExport('providers')}
                      >
                        Export
                      </Button>
                    </Group>
                  </Card.Section>
                  <Card.Section>
                    <Table>
                      <Table.Thead>
                        <Table.Tr>
                          <Table.Th>Provider</Table.Th>
                          <Table.Th>Spend</Table.Th>
                          <Table.Th>Requests</Table.Th>
                          <Table.Th>Avg Cost</Table.Th>
                          <Table.Th>Share</Table.Th>
                        </Table.Tr>
                      </Table.Thead>
                      <Table.Tbody>
                        {(providerCosts || []).map((provider) => (
                          <Table.Tr key={provider.provider}>
                            <Table.Td>
                              <Stack gap="xs">
                                <Text fw={500}>{provider.provider}</Text>
                                <Group gap="xs">
                                  {provider.models.slice(0, 2).map((model) => (
                                    <Badge key={model} size="xs" variant="light">
                                      {model}
                                    </Badge>
                                  ))}
                                  {provider.models.length > 2 && (
                                    <Badge size="xs" variant="light" color="gray">
                                      +{provider.models.length - 2}
                                    </Badge>
                                  )}
                                </Group>
                              </Stack>
                            </Table.Td>
                            <Table.Td>{formatters.currency(provider.spend)}</Table.Td>
                            <Table.Td>{formatters.number(provider.requests)}</Table.Td>
                            <Table.Td>{formatters.currency(provider.averageCost)}</Table.Td>
                            <Table.Td>
                              <Group gap="xs">
                                <Text size="sm">{formatters.percentage(provider.percentage / 100, undefined, { decimals: 1 })}</Text>
                                <Progress
                                  value={provider.percentage}
                                  size="sm"
                                  w={100}
                                  color="blue"
                                />
                              </Group>
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

          <Tabs.Panel value="models" pt="md">
            <div style={{ position: 'relative' }}>
              <LoadingOverlay visible={modelsLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
              
              <Card withBorder>
                <Card.Section p="md" withBorder>
                  <Group justify="space-between">
                    <Text fw={600}>Model Usage & Costs</Text>
                    <Button
                      size="xs"
                      variant="light"
                      leftSection={<IconDownload size={14} />}
                      onClick={() => handleExport('models')}
                    >
                      Export
                    </Button>
                  </Group>
                </Card.Section>
                <Card.Section>
                  <Table>
                    <Table.Thead>
                      <Table.Tr>
                        <Table.Th>Model</Table.Th>
                        <Table.Th>Provider</Table.Th>
                        <Table.Th>Total Spend</Table.Th>
                        <Table.Th>Requests</Table.Th>
                        <Table.Th>Tokens</Table.Th>
                        <Table.Th>Cost/Request</Table.Th>
                        <Table.Th>Cost/Token</Table.Th>
                      </Table.Tr>
                    </Table.Thead>
                    <Table.Tbody>
                      {(modelCosts || []).map((model) => (
                        <Table.Tr key={`${model.provider}-${model.model}`}>
                          <Table.Td>
                            <Text fw={500}>{model.model}</Text>
                          </Table.Td>
                          <Table.Td>
                            <Badge variant="light" size="sm">
                              {model.provider}
                            </Badge>
                          </Table.Td>
                          <Table.Td>{formatters.currency(model.spend)}</Table.Td>
                          <Table.Td>{formatters.number(model.requests)}</Table.Td>
                          <Table.Td>
                            {model.tokens > 0 ? formatters.number(model.tokens) : 'N/A'}
                          </Table.Td>
                          <Table.Td>{formatters.currency(model.averageCostPerRequest)}</Table.Td>
                          <Table.Td>
                            {model.averageCostPerToken > 0 
                              ? formatters.currency(model.averageCostPerToken)
                              : 'N/A'
                            }
                          </Table.Td>
                        </Table.Tr>
                      ))}
                    </Table.Tbody>
                  </Table>
                </Card.Section>
              </Card>
            </div>
          </Tabs.Panel>

          <Tabs.Panel value="keys" pt="md">
            <div style={{ position: 'relative' }}>
              <LoadingOverlay visible={keysCostsLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
              
              {virtualKeyCosts && virtualKeyCosts.length > 0 ? (
                <Card withBorder>
                  <Card.Section p="md" withBorder>
                    <Group justify="space-between">
                      <Text fw={600}>Virtual Key Analytics</Text>
                      <Group gap="xs">
                        <Select
                          placeholder="Filter by key"
                          data={[
                            { value: '', label: 'All Keys' },
                            ...(virtualKeyCosts || []).map(key => ({
                              value: key.keyId,
                              label: key.keyName,
                            }))
                          ]}
                          value={selectedVirtualKey}
                          onChange={(value) => setSelectedVirtualKey(value || '')}
                          w={200}
                          clearable
                        />
                        <Button
                          size="xs"
                          variant="light"
                          leftSection={<IconDownload size={14} />}
                          onClick={() => handleExport('virtual-keys')}
                        >
                          Export
                        </Button>
                      </Group>
                    </Group>
                  </Card.Section>
                  <Card.Section>
                    <Table>
                      <Table.Thead>
                        <Table.Tr>
                          <Table.Th>Key Name</Table.Th>
                          <Table.Th>Current Spend</Table.Th>
                          <Table.Th>Budget</Table.Th>
                          <Table.Th>Usage</Table.Th>
                          <Table.Th>Requests</Table.Th>
                          <Table.Th>Top Models</Table.Th>
                          <Table.Th>Last Activity</Table.Th>
                        </Table.Tr>
                      </Table.Thead>
                      <Table.Tbody>
                        {virtualKeyCosts
                          .filter(key => !selectedVirtualKey || key.keyId === selectedVirtualKey)
                          .map((key) => {
                            const usageColor = key.isOverBudget ? 'red' : badgeHelpers.getPercentageColor(key.usagePercentage, { danger: 90, warning: 75, good: 0 });
                            
                            return (
                              <Table.Tr key={key.keyId}>
                                <Table.Td>
                                  <Group gap="xs">
                                    <Text fw={500}>{key.keyName}</Text>
                                    {key.isOverBudget && (
                                      <Badge size="xs" color="red" variant="filled">
                                        Over Budget
                                      </Badge>
                                    )}
                                  </Group>
                                </Table.Td>
                                <Table.Td>{formatters.currency(key.spend)}</Table.Td>
                                <Table.Td>
                                  {key.budget > 0 ? formatters.currency(key.budget) : 'No limit'}
                                </Table.Td>
                                <Table.Td>
                                  {key.budget > 0 ? (
                                    <Group gap="xs">
                                      <Text size="sm">{formatters.percentage(key.usagePercentage / 100, undefined, { decimals: 1 })}</Text>
                                      <Progress
                                        value={Math.min(key.usagePercentage, 100)}
                                        size="sm"
                                        w={80}
                                        color={usageColor}
                                      />
                                    </Group>
                                  ) : (
                                    <Text size="sm" c="dimmed">No limit</Text>
                                  )}
                                </Table.Td>
                                <Table.Td>{formatters.number(key.requests)}</Table.Td>
                                <Table.Td>
                                  <Group gap="xs">
                                    {key.topModels.slice(0, 2).map((model) => (
                                      <Badge key={model} size="xs" variant="light">
                                        {model}
                                      </Badge>
                                    ))}
                                    {key.topModels.length > 2 && (
                                      <Badge size="xs" variant="light" color="gray">
                                        +{key.topModels.length - 2}
                                      </Badge>
                                    )}
                                  </Group>
                                </Table.Td>
                                <Table.Td>
                                  <Text size="sm" c="dimmed">
                                    {formatters.date(key.lastActivity)}
                                  </Text>
                                </Table.Td>
                              </Table.Tr>
                            );
                          })}
                      </Table.Tbody>
                    </Table>
                  </Card.Section>
                </Card>
              ) : (
                <Text c="dimmed" ta="center" py="xl">
                  No virtual key cost data available. Analytics will appear as keys are used.
                </Text>
              )}
            </div>
          </Tabs.Panel>
        </Tabs>
      </Card>
    </Stack>
  );
}