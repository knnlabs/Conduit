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
  Paper,
  Table,
  ScrollArea,
  Alert,
  Progress,
} from '@mantine/core';
import {
  IconRefresh,
  IconDownload,
  IconAlertCircle,
  IconCalendar,
  IconFilter,
} from '@tabler/icons-react';
import { useState } from 'react';
import { CostChart } from '@/components/charts/CostChart';
import { ErrorDisplay } from '@/components/common/ErrorDisplay';
import { useCostData, useTransformedData } from './hooks';
import { useCostDashboardHandlers } from './handlers';
import { CostMetricsCards } from './CostMetricsCards';
import type { ProviderCost, ModelUsage } from './types';

export default function CostDashboard() {
  const [timeRange, setTimeRange] = useState('30d');
  const [selectedProvider, setSelectedProvider] = useState<string | null>('all');
  const [isExporting, setIsExporting] = useState(false);

  // Use custom hooks for data and handlers
  const {
    costSummary,
    costTrends,
    isLoading,
    error,
    refetchAll
  } = useCostData(timeRange);

  const {
    totalSpend,
    averageDailyCost,
    projectedMonthlySpend,
    projectedTrend,
    providerCosts,
    modelUsage,
    dailyCosts,
    monthlyBudget,
    budgetUtilization,
    isOverBudget
  } = useTransformedData(costSummary, costTrends, timeRange);

  const {
    handleRefresh,
    handleExport
  } = useCostDashboardHandlers(refetchAll, timeRange, setIsExporting);

  const filteredProviderCosts = selectedProvider === 'all' 
    ? providerCosts 
    : providerCosts.filter((p: ProviderCost) => p.provider === selectedProvider);

  const filteredModelUsage = selectedProvider === 'all'
    ? modelUsage
    : modelUsage.filter((m: ModelUsage) => m.provider === selectedProvider);

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

      <CostMetricsCards
        totalSpend={totalSpend}
        projectedTrend={projectedTrend}
        averageDailyCost={averageDailyCost}
        projectedMonthlySpend={projectedMonthlySpend}
        monthlyBudget={monthlyBudget}
        budgetUtilization={budgetUtilization}
        isOverBudget={isOverBudget}
        timeRange={timeRange}
      />

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