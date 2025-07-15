'use client';

import {
  Stack,
  Title,
  Text,
  Group,
  Button,
  Card,
  Grid,
  SimpleGrid,
  Select,
  Badge,
  Progress,
  ThemeIcon,
  Paper,
  Table,
  ScrollArea,
  LoadingOverlay,
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
import { useState, useEffect } from 'react';
import { notifications } from '@mantine/notifications';
import { CostChart } from '@/components/charts/CostChart';
import { ErrorDisplay } from '@/components/common/ErrorDisplay';
import { safeLog } from '@/lib/utils/logging';
import { useQuery } from '@tanstack/react-query';

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
  providers: Record<string, number>;
}

export default function CostDashboard() {
  const [timeRange, setTimeRange] = useState('30d');
  const [selectedProvider, setSelectedProvider] = useState<string | null>('all');
  const [isExporting, setIsExporting] = useState(false);

  // Fetch real cost analytics data from the API
  const { data: costData, isLoading, error, refetch } = useQuery({
    queryKey: ['cost-analytics', timeRange],
    queryFn: async () => {
      const response = await fetch(`/api/cost-analytics?timeRange=${timeRange}`);
      if (!response.ok) {
        throw new Error('Failed to fetch cost data');
      }
      return response.json();
    },
    refetchInterval: 300000, // Refresh every 5 minutes
  });

  // Use real data from API or fallback values
  const totalSpend = costData?.totalSpend || 0;
  const monthlyBudget = costData?.monthlyBudget || null;
  const projectedSpend = costData?.projectedMonthlySpend || 0;
  const averageDailyCost = costData?.averageDailyCost || 0;
  const providerCosts: ProviderCost[] = costData?.providerCosts || [];
  const modelUsage: ModelUsage[] = costData?.modelUsage || [];
  const dailyCosts: DailyCost[] = costData?.dailyCosts || [];

  const handleRefresh = async () => {
    try {
      await refetch();
      notifications.show({
        title: 'Data Refreshed',
        message: 'Cost data has been updated',
        color: 'green',
      });
    } catch (error) {
      safeLog('error', 'Failed to refresh cost data', error);
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
      const response = await fetch(`/api/cost-analytics/export?timeRange=${timeRange}`, {
        method: 'POST',
      });
      
      if (!response.ok) {
        throw new Error('Export failed');
      }
      
      const blob = await response.blob();
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `cost-report-${timeRange}-${new Date().toISOString().split('T')[0]}.csv`;
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      document.body.removeChild(a);
      
      notifications.show({
        title: 'Export Successful',
        message: 'Cost report has been exported',
        color: 'green',
      });
    } catch (error) {
      safeLog('error', 'Failed to export cost report', error);
      notifications.show({
        title: 'Export Failed',
        message: 'Failed to export cost report',
        color: 'red',
      });
    } finally {
      setIsExporting(false);
    }
  };

  const filteredData = selectedProvider === 'all' 
    ? dailyCosts 
    : dailyCosts.map(day => ({
        ...day,
        cost: selectedProvider ? (day.providers[selectedProvider] || 0) : 0,
      }));

  const budgetUsagePercent = monthlyBudget ? (totalSpend / monthlyBudget) * 100 : 0;
  const budgetStatusColor = budgetUsagePercent > 90 ? 'red' : budgetUsagePercent > 70 ? 'yellow' : 'green';

  if (error) {
    return <ErrorDisplay error={error} title="Failed to load cost data" variant="card" onRetry={() => window.location.reload()} />;
  }

  return (
    <Stack>
      {!costData && !isLoading && (
        <Alert
          icon={<IconAlertCircle size="1rem" />}
          title="Cost Tracking Initializing"
          color="blue"
          variant="light"
          mb="md"
        >
          Cost tracking data is being collected. Initial data may take a few moments to appear.
        </Alert>
      )}

      <Card shadow="sm" p="md" radius="md">
        <Group justify="space-between" align="center">
          <div>
            <Title order={2}>Cost Dashboard</Title>
            <Text size="sm" c="dimmed" mt={4}>
              Monitor and analyze your API usage costs
            </Text>
          </div>
          <Group>
            <Select
              value={timeRange}
              onChange={(value) => setTimeRange(value || '30d')}
              data={[
                { value: '7d', label: 'Last 7 Days' },
                { value: '30d', label: 'Last 30 Days' },
                { value: '90d', label: 'Last 90 Days' },
                { value: 'ytd', label: 'Year to Date' },
              ]}
              w={150}
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
          <Card padding="lg" radius="md" withBorder>
            <Group justify="space-between">
              <div>
                <Text size="sm" c="dimmed" fw={600} tt="uppercase">
                  Total Spend
                </Text>
                <Text size="xl" fw={700} mt={4}>
                  ${totalSpend.toFixed(2)}
                </Text>
                <Text size="xs" c="dimmed" mt={4}>
                  This month
                </Text>
              </div>
              <ThemeIcon
                color="blue"
                variant="light"
                size={48}
                radius="md"
              >
                <IconCurrencyDollar size={24} />
              </ThemeIcon>
            </Group>
          </Card>
        </Grid.Col>

        <Grid.Col span={{ base: 12, sm: 6, lg: 3 }}>
          <Card padding="lg" radius="md" withBorder>
            <Group justify="space-between">
              <div>
                <Text size="sm" c="dimmed" fw={600} tt="uppercase">
                  Budget Usage
                </Text>
                {monthlyBudget ? (
                  <>
                    <Text size="xl" fw={700} mt={4}>
                      {budgetUsagePercent.toFixed(1)}%
                    </Text>
                    <Progress
                      value={budgetUsagePercent}
                      color={budgetStatusColor}
                      size="sm"
                      radius="md"
                      mt={8}
                    />
                  </>
                ) : (
                  <Text size="xl" fw={700} mt={4} c="dimmed">
                    No budget set
                  </Text>
                )}
              </div>
              <ThemeIcon
                color={monthlyBudget ? budgetStatusColor : 'gray'}
                variant="light"
                size={48}
                radius="md"
              >
                <IconChartBar size={24} />
              </ThemeIcon>
            </Group>
          </Card>
        </Grid.Col>

        <Grid.Col span={{ base: 12, sm: 6, lg: 3 }}>
          <Card padding="lg" radius="md" withBorder>
            <Group justify="space-between">
              <div>
                <Text size="sm" c="dimmed" fw={600} tt="uppercase">
                  Projected Spend
                </Text>
                <Text size="xl" fw={700} mt={4}>
                  ${projectedSpend.toFixed(2)}
                </Text>
                {costData?.projectedTrend && (
                  <Group gap={4} mt={8}>
                    {costData.projectedTrend > 0 ? (
                      <IconTrendingUp size={16} color="var(--mantine-color-red-6)" />
                    ) : (
                      <IconTrendingDown size={16} color="var(--mantine-color-green-6)" />
                    )}
                    <Text size="xs" c={costData.projectedTrend > 0 ? "red" : "green"}>
                      {costData.projectedTrend > 0 ? '+' : ''}{costData.projectedTrend.toFixed(1)}% from last month
                    </Text>
                  </Group>
                )}
              </div>
              <ThemeIcon
                color="yellow"
                variant="light"
                size={48}
                radius="md"
              >
                <IconTrendingUp size={24} />
              </ThemeIcon>
            </Group>
          </Card>
        </Grid.Col>

        <Grid.Col span={{ base: 12, sm: 6, lg: 3 }}>
          <Card padding="lg" radius="md" withBorder>
            <Group justify="space-between">
              <div>
                <Text size="sm" c="dimmed" fw={600} tt="uppercase">
                  Avg Daily Cost
                </Text>
                <Text size="xl" fw={700} mt={4}>
                  ${averageDailyCost.toFixed(2)}
                </Text>
                <Text size="xs" c="dimmed" mt={4}>
                  Last 30 days
                </Text>
              </div>
              <ThemeIcon
                color="green"
                variant="light"
                size={48}
                radius="md"
              >
                <IconCalendar size={24} />
              </ThemeIcon>
            </Group>
          </Card>
        </Grid.Col>
      </Grid>

      <Card shadow="sm" p="md" radius="md" withBorder>
        <Group justify="space-between" mb="md">
          <Title order={4}>Cost Trends</Title>
          <Select
            value={selectedProvider}
            onChange={setSelectedProvider}
            data={[
              { value: 'all', label: 'All Providers' },
              ...providerCosts.map(p => ({ value: p.provider, label: p.provider })),
            ]}
            w={180}
            leftSection={<IconFilter size={16} />}
          />
        </Group>
        <CostChart 
          data={filteredData.map(day => ({
            date: day.date,
            cost: day.cost,
            ...day.providers
          }))} 
          height={300} 
          title="Daily Cost Trends"
          type="line"
          valueKey="cost"
          nameKey="date"
        />
      </Card>

      <Grid>
        <Grid.Col span={{ base: 12, md: 6 }}>
          <Card shadow="sm" p="md" radius="md" withBorder h="100%">
            <Title order={4} mb="md">Provider Breakdown</Title>
            <Stack gap="sm">
              {providerCosts.map((provider) => (
                <Paper key={provider.provider} p="sm" withBorder>
                  <Group justify="space-between" mb="xs">
                    <Group>
                      <Text fw={500}>{provider.provider}</Text>
                      <Badge
                        variant="light"
                        color={provider.trend > 0 ? 'red' : 'green'}
                        leftSection={provider.trend > 0 ? <IconTrendingUp size={12} /> : <IconTrendingDown size={12} />}
                      >
                        {Math.abs(provider.trend)}%
                      </Badge>
                    </Group>
                    <Text fw={600}>${provider.cost.toFixed(2)}</Text>
                  </Group>
                  <Progress
                    value={provider.usage}
                    color="blue"
                    size="sm"
                    radius="md"
                  />
                  <Text size="xs" c="dimmed" mt={4}>
                    {provider.usage}% of total usage
                  </Text>
                </Paper>
              ))}
            </Stack>
          </Card>
        </Grid.Col>

        <Grid.Col span={{ base: 12, md: 6 }}>
          <Card shadow="sm" p="md" radius="md" withBorder h="100%">
            <Title order={4} mb="md">Top Models by Cost</Title>
            <ScrollArea h={300}>
              <Table striped>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Model</Table.Th>
                    <Table.Th>Requests</Table.Th>
                    <Table.Th>Cost</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {modelUsage
                    .sort((a, b) => b.cost - a.cost)
                    .slice(0, 6)
                    .map((model) => (
                      <Table.Tr key={model.model}>
                        <Table.Td>
                          <div>
                            <Text size="sm" fw={500}>{model.model}</Text>
                            <Text size="xs" c="dimmed">{model.provider}</Text>
                          </div>
                        </Table.Td>
                        <Table.Td>
                          <Text size="sm">{model.requests.toLocaleString()}</Text>
                        </Table.Td>
                        <Table.Td>
                          <Text size="sm" fw={600}>${model.cost.toFixed(2)}</Text>
                        </Table.Td>
                      </Table.Tr>
                    ))}
                </Table.Tbody>
              </Table>
            </ScrollArea>
          </Card>
        </Grid.Col>
      </Grid>

      {monthlyBudget && projectedSpend > monthlyBudget && (
        <Alert
          icon={<IconAlertCircle size={16} />}
          title="Budget Alert"
          color="red"
          variant="light"
        >
          Your projected spend (${projectedSpend.toFixed(2)}) exceeds your monthly budget (${monthlyBudget.toFixed(2)}). 
          Consider reviewing your usage patterns to avoid overage charges.
        </Alert>
      )}

      <LoadingOverlay visible={isLoading} />
    </Stack>
  );
}