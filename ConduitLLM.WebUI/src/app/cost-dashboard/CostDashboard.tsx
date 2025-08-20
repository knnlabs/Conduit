'use client';

import {
  Stack,
  Title,
  Text,
  Group,
  Button,
  Card,
  Grid,
  Select,
  Badge,
  Progress,
  ThemeIcon,
  Paper,
  Table,
  ScrollArea,
  Alert,
} from '@mantine/core';
import {
  IconCurrencyDollar,
  IconTrendingUp,
  IconTrendingDown,
  IconRefresh,
  IconDownload,
  IconAlertCircle,
  IconChartBar,
  IconCalendar,
  IconFilter,
} from '@tabler/icons-react';
import { useState } from 'react';
import { notifications } from '@mantine/notifications';
import { CostChart } from '@/components/charts/CostChart';
import { ErrorDisplay } from '@/components/common/ErrorDisplay';
import { safeLog } from '@/lib/utils/logging';
import { useQuery } from '@tanstack/react-query';
import { withAdminClient } from '@/lib/client/adminClient';
// Define types locally since SDK exports may not be fully available
interface DetailedCostDataDto {
  name: string;
  cost: number;
  percentage: number;
  requestCount: number;
}

interface CostTrendDataDto {
  date: string;
  cost: number;
  requestCount: number;
}

interface CostDashboardDto {
  timeFrame: string;
  startDate: string;
  endDate: string;
  last24HoursCost: number;
  last7DaysCost: number;
  last30DaysCost: number;
  totalCost: number;
  topModelsBySpend: DetailedCostDataDto[];
  topProvidersBySpend: DetailedCostDataDto[];
  topVirtualKeysBySpend: DetailedCostDataDto[];
}

interface CostTrendDto {
  period: string;
  startDate: string;
  endDate: string;
  data: CostTrendDataDto[];
}

// Local types for transformed data
interface ProviderCost {
  provider: string;
  cost: number;
  usage: number;
  trend: number;
}

interface ModelUsage {
  model: string;
  provider: string;
  requests: number;
  tokensIn: number;
  tokensOut: number;
  cost: number;
}

interface DailyCost {
  date: string;
  cost: number;
  [providerName: string]: string | number;
}

export default function CostDashboard() {
  const [timeRange, setTimeRange] = useState('30d');
  const [selectedProvider, setSelectedProvider] = useState<string | null>('all');
  const [isExporting, setIsExporting] = useState(false);

  // Calculate date range based on timeRange
  const getDateRange = () => {
    const now = new Date();
    const startDate = new Date();
    
    switch (timeRange) {
      case '7d':
        startDate.setDate(now.getDate() - 7);
        break;
      case '30d':
        startDate.setDate(now.getDate() - 30);
        break;
      case '90d':
        startDate.setDate(now.getDate() - 90);
        break;
      default:
        startDate.setDate(now.getDate() - 30);
    }
    
    return {
      startDate: startDate.toISOString().split('T')[0],
      endDate: now.toISOString().split('T')[0],
    };
  };

  // Fetch cost summary from Admin SDK
  const { data: costSummary, isLoading: isLoadingSummary, error: summaryError, refetch: refetchSummary } = useQuery<CostDashboardDto>({
    queryKey: ['cost-summary', timeRange],
    queryFn: async () => {
      const { startDate, endDate } = getDateRange();
      return withAdminClient(client => 
        client.analytics.getCostSummary('daily', startDate, endDate)
      );
    },
    refetchInterval: 300000, // Refresh every 5 minutes
  });

  // Fetch cost trends from Admin SDK
  const { data: costTrends, isLoading: isLoadingTrends, error: trendsError, refetch: refetchTrends } = useQuery<CostTrendDto>({
    queryKey: ['cost-trends', timeRange],
    queryFn: async () => {
      const { startDate, endDate } = getDateRange();
      return withAdminClient(client => 
        client.analytics.getCostTrends('daily', startDate, endDate)
      );
    },
    refetchInterval: 300000, // Refresh every 5 minutes
  });

  const isLoading = isLoadingSummary || isLoadingTrends;
  const error = summaryError ?? trendsError;

  // Calculate derived metrics
  const totalSpend = costSummary?.totalCost ?? 0;
  const last7DaysCost = costSummary?.last7DaysCost ?? 0;
  const last30DaysCost = costSummary?.last30DaysCost ?? 0;
  
  // Calculate average daily cost based on the time range
  let daysInRange: number;
  if (timeRange === '7d') {
    daysInRange = 7;
  } else if (timeRange === '30d') {
    daysInRange = 30;
  } else {
    daysInRange = 90;
  }
  const averageDailyCost = totalSpend / daysInRange;
  
  // Calculate projected monthly spend
  const daysInMonth = 30;
  const projectedMonthlySpend = averageDailyCost * daysInMonth;
  
  // Calculate trend (comparing last 7 days to previous 7 days)
  const projectedTrend = last7DaysCost > 0 && last30DaysCost > 0
    ? ((last7DaysCost - (last30DaysCost - last7DaysCost) / 3) / ((last30DaysCost - last7DaysCost) / 3)) * 100
    : 0;

  // Transform provider costs
  const providerCosts: ProviderCost[] = costSummary?.topProvidersBySpend?.map((provider: DetailedCostDataDto) => ({
    provider: provider.name,
    cost: provider.cost,
    usage: provider.percentage,
    trend: 0, // Trend calculation would require historical data
  })) ?? [];

  // Transform model usage
  const modelUsage: ModelUsage[] = costSummary?.topModelsBySpend?.map((model: DetailedCostDataDto) => ({
    model: model.name,
    provider: model.name.includes('/') ? model.name.split('/')[0] : 'unknown',
    requests: model.requestCount,
    tokensIn: 0, // Not available in cost summary
    tokensOut: 0, // Not available in cost summary
    cost: model.cost,
  })) ?? [];

  // Transform daily costs from trends - flatten providers for chart compatibility
  const dailyCosts: DailyCost[] = costTrends?.data?.map((trend: CostTrendDataDto) => {
    const result: DailyCost = {
      date: trend.date,
      cost: trend.cost,
    };
    
    // Add provider costs as separate fields for chart compatibility
    costSummary?.topProvidersBySpend?.forEach((provider: DetailedCostDataDto) => {
      result[provider.name] = (trend.cost * provider.percentage) / 100;
    });

    return result;
  }) ?? [];

  const handleRefresh = async () => {
    try {
      await Promise.all([refetchSummary(), refetchTrends()]);
      notifications.show({
        title: 'Data Refreshed',
        message: 'Cost data has been updated',
        color: 'green',
      });
    } catch (err) {
      safeLog('error', 'Failed to refresh cost data', err);
      notifications.show({
        title: 'Refresh Failed',
        message: 'Failed to refresh cost data',
        color: 'red',
      });
    }
  };

  const handleExport = async () => {
    setIsExporting(true);
    try {
      const { startDate, endDate } = getDateRange();
      
      // Get export data from Admin SDK (returns Uint8Array)
      const exportData = await withAdminClient(client =>
        client.analytics.exportAnalyticsAsync('csv', startDate, endDate)
      );
      
      // Create a blob from the Uint8Array and download
      // Cast to unknown then to BlobPart to avoid TypeScript ArrayBufferLike vs ArrayBuffer issue
      const blob = new Blob([exportData as BlobPart], { type: 'text/csv; charset=utf-8' });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `cost-report-${timeRange}-${new Date().toISOString().split('T')[0]}.csv`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
      
      notifications.show({
        title: 'Export Successful',
        message: 'Cost report has been downloaded',
        color: 'green',
      });
    } catch (err) {
      safeLog('error', 'Failed to export cost data', err);
      notifications.show({
        title: 'Export Failed',
        message: 'Failed to export cost data',
        color: 'red',
      });
    } finally {
      setIsExporting(false);
    }
  };

  const filteredProviderCosts = selectedProvider === 'all' 
    ? providerCosts 
    : providerCosts.filter((p: ProviderCost) => p.provider === selectedProvider);

  const filteredModelUsage = selectedProvider === 'all'
    ? modelUsage
    : modelUsage.filter((m: ModelUsage) => m.provider === selectedProvider);

  // Calculate budget utilization (if we had budget data)
  const monthlyBudget: number | null = null; // Budget feature not yet implemented
  const budgetUtilization = monthlyBudget !== null ? (projectedMonthlySpend / monthlyBudget) * 100 : null;
  const isOverBudget = budgetUtilization !== null ? budgetUtilization > 100 : false;

  if (error) {
    return <ErrorDisplay error={error} />;
  }

  return (
    <Stack gap="lg">
      <Group justify="space-between">
        <div>
          <Title order={2}>Cost Dashboard</Title>
          <Text size="sm" c="dimmed">
            Monitor and analyze your API usage costs
          </Text>
        </div>
        <Group>
          <Select
            value={timeRange}
            onChange={(value) => setTimeRange(value ?? '30d')}
            data={[
              { value: '7d', label: 'Last 7 days' },
              { value: '30d', label: 'Last 30 days' },
              { value: '90d', label: 'Last 90 days' },
            ]}
            leftSection={<IconCalendar size={16} />}
          />
          <Select
            value={selectedProvider}
            onChange={(value) => setSelectedProvider(value ?? 'all')}
            data={[
              { value: 'all', label: 'All Providers' },
              ...providerCosts.map((p: ProviderCost) => ({ value: p.provider, label: p.provider }))
            ]}
            leftSection={<IconFilter size={16} />}
          />
          <Button
            variant="subtle"
            leftSection={<IconRefresh size={16} />}
            onClick={() => void handleRefresh()}
            loading={isLoading}
          >
            Refresh
          </Button>
          <Button
            variant="light"
            leftSection={<IconDownload size={16} />}
            onClick={() => void handleExport()}
            loading={isExporting}
          >
            Export
          </Button>
        </Group>
      </Group>

      {budgetUtilization && isOverBudget && (
        <Alert icon={<IconAlertCircle />} color="red" variant="light">
          <Group justify="space-between">
            <div>
              <Text fw={600}>Budget Alert</Text>
              <Text size="sm">
                You are projected to exceed your monthly budget by{' '}
                {monthlyBudget !== null ? ((projectedMonthlySpend - monthlyBudget) / monthlyBudget * 100).toFixed(1) : 0}%
              </Text>
            </div>
            <Button variant="subtle" color="red" size="xs">
              View Details
            </Button>
          </Group>
        </Alert>
      )}

      <Grid>
        <Grid.Col span={{ base: 12, md: 6, lg: 3 }}>
          <Card padding="lg" radius="md" withBorder>
            <Group justify="space-between" mb="xs">
              <Text size="sm" c="dimmed" tt="uppercase" fw={600}>
                Total Spend
              </Text>
              <ThemeIcon color="blue" variant="light" radius="md" size="md">
                <IconCurrencyDollar size={18} />
              </ThemeIcon>
            </Group>
            <Group align="baseline" gap="xs">
              <Text size="xl" fw={700}>
                ${totalSpend.toFixed(2)}
              </Text>
              {projectedTrend !== 0 && (
                <Badge
                  color={projectedTrend > 0 ? 'red' : 'green'}
                  variant="light"
                  leftSection={
                    projectedTrend > 0 ? <IconTrendingUp size={12} /> : <IconTrendingDown size={12} />
                  }
                >
                  {Math.abs(projectedTrend).toFixed(1)}%
                </Badge>
              )}
            </Group>
            <Text size="xs" c="dimmed" mt="xs">
              {(() => {
                if (timeRange === '7d') return 'Last 7 days';
                if (timeRange === '30d') return 'Last 30 days';
                return 'Last 90 days';
              })()}
            </Text>
          </Card>
        </Grid.Col>

        <Grid.Col span={{ base: 12, md: 6, lg: 3 }}>
          <Card padding="lg" radius="md" withBorder>
            <Group justify="space-between" mb="xs">
              <Text size="sm" c="dimmed" tt="uppercase" fw={600}>
                Daily Average
              </Text>
              <ThemeIcon color="teal" variant="light" radius="md" size="md">
                <IconChartBar size={18} />
              </ThemeIcon>
            </Group>
            <Text size="xl" fw={700}>
              ${averageDailyCost.toFixed(2)}
            </Text>
            <Text size="xs" c="dimmed" mt="xs">
              Per day average
            </Text>
          </Card>
        </Grid.Col>

        <Grid.Col span={{ base: 12, md: 6, lg: 3 }}>
          <Card padding="lg" radius="md" withBorder>
            <Group justify="space-between" mb="xs">
              <Text size="sm" c="dimmed" tt="uppercase" fw={600}>
                Projected Monthly
              </Text>
              <ThemeIcon color="orange" variant="light" radius="md" size="md">
                <IconTrendingUp size={18} />
              </ThemeIcon>
            </Group>
            <Text size="xl" fw={700}>
              ${projectedMonthlySpend.toFixed(2)}
            </Text>
            <Text size="xs" c="dimmed" mt="xs">
              Based on current usage
            </Text>
          </Card>
        </Grid.Col>

        <Grid.Col span={{ base: 12, md: 6, lg: 3 }}>
          <Card padding="lg" radius="md" withBorder>
            <Group justify="space-between" mb="xs">
              <Text size="sm" c="dimmed" tt="uppercase" fw={600}>
                Budget Status
              </Text>
              <ThemeIcon 
                color={isOverBudget ? 'red' : 'green'} 
                variant="light" 
                radius="md" 
                size="md"
              >
                <IconCurrencyDollar size={18} />
              </ThemeIcon>
            </Group>
            {monthlyBudget ? (
              <>
                <Text size="xl" fw={700}>
                  {budgetUtilization?.toFixed(1)}%
                </Text>
                <Progress 
                  value={budgetUtilization ?? 0} 
                  color={isOverBudget ? 'red' : 'green'} 
                  size="sm" 
                  mt="xs" 
                />
              </>
            ) : (
              <>
                <Text size="xl" fw={700} c="dimmed">
                  N/A
                </Text>
                <Text size="xs" c="dimmed" mt="xs">
                  No budget set
                </Text>
              </>
            )}
          </Card>
        </Grid.Col>
      </Grid>

      <Grid>
        <Grid.Col span={{ base: 12, lg: 8 }}>
          <Card padding="lg" radius="md" withBorder>
            <Title order={4} mb="md">Cost Trends</Title>
            {dailyCosts.length > 0 ? (
              <CostChart 
                data={dailyCosts} 
                height={300} 
                title="Cost Trends"
                type="line"
                valueKey="cost"
                nameKey="date"
                showControls={false}
              />
            ) : (
              <Text c="dimmed" ta="center" py="xl">
                No cost data available for the selected period
              </Text>
            )}
          </Card>
        </Grid.Col>

        <Grid.Col span={{ base: 12, lg: 4 }}>
          <Card padding="lg" radius="md" withBorder>
            <Title order={4} mb="md">Provider Breakdown</Title>
            <Stack gap="sm">
              {filteredProviderCosts.length > 0 ? (
                filteredProviderCosts.map((provider: ProviderCost) => (
                  <div key={provider.provider}>
                    <Group justify="space-between" mb={4}>
                      <Text size="sm">{provider.provider}</Text>
                      <Group gap="xs">
                        <Text size="sm" fw={600}>
                          ${provider.cost.toFixed(2)}
                        </Text>
                        <Badge size="sm" variant="light">
                          {provider.usage.toFixed(1)}%
                        </Badge>
                      </Group>
                    </Group>
                    <Progress value={provider.usage} size="xs" />
                  </div>
                ))
              ) : (
                <Text c="dimmed" ta="center" py="md">
                  No provider data available
                </Text>
              )}
            </Stack>
          </Card>
        </Grid.Col>
      </Grid>

      <Card padding="lg" radius="md" withBorder>
        <Title order={4} mb="md">Model Usage</Title>
        <ScrollArea h={400}>
          <Table striped highlightOnHover>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Model</Table.Th>
                <Table.Th>Provider</Table.Th>
                <Table.Th>Requests</Table.Th>
                <Table.Th>Cost</Table.Th>
                <Table.Th>Avg Cost/Request</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {filteredModelUsage.length > 0 ? (
                filteredModelUsage.map((model: ModelUsage) => (
                  <Table.Tr key={model.model}>
                    <Table.Td>
                      <Text size="sm" fw={500}>
                        {model.model}
                      </Text>
                    </Table.Td>
                    <Table.Td>
                      <Badge size="sm" variant="light">
                        {model.provider}
                      </Badge>
                    </Table.Td>
                    <Table.Td>{model.requests.toLocaleString()}</Table.Td>
                    <Table.Td>${model.cost.toFixed(4)}</Table.Td>
                    <Table.Td>
                      ${model.requests > 0 ? (model.cost / model.requests).toFixed(6) : '0.00'}
                    </Table.Td>
                  </Table.Tr>
                ))
              ) : (
                <Table.Tr>
                  <Table.Td colSpan={5}>
                    <Text c="dimmed" ta="center" py="md">
                      No model usage data available
                    </Text>
                  </Table.Td>
                </Table.Tr>
              )}
            </Table.Tbody>
          </Table>
        </ScrollArea>
      </Card>

      <Paper p="xs" withBorder>
        <Group justify="space-between">
          <Text size="xs" c="dimmed">
            Last updated: {costSummary ? new Date().toLocaleString() : 'Never'}
          </Text>
          <Text size="xs" c="dimmed">
            Data source: Admin Analytics API
          </Text>
        </Group>
      </Paper>
    </Stack>
  );
}