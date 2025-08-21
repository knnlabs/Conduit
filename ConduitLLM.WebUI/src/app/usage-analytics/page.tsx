'use client';

import {
  Stack,
  Title,
  Text,
  Group,
  Select,
  Card,
  Button,
  Badge,
  Table,
  ScrollArea,
  Code,
  Skeleton,
} from '@mantine/core';
import {
  IconDownload,
  IconRefresh,
} from '@tabler/icons-react';
import { useState, useEffect, useCallback } from 'react';
import { formatters } from '@/lib/utils/formatters';
import { AnalyticsCharts } from './AnalyticsCharts';
import { 
  useFetchAnalytics, 
  useExportAnalytics,
  type UsageMetrics,
  type TimeSeriesData,
  type ProviderUsage,
  type ModelUsage,
  type VirtualKeyUsage,
  type EndpointUsage,
} from './AnalyticsUtils';

export default function UsageAnalyticsPage() {
  const [timeRange, setTimeRange] = useState('7d');
  const [isLoading, setIsLoading] = useState(true);
  const [metrics, setMetrics] = useState<UsageMetrics | null>(null);
  const [timeSeriesData, setTimeSeriesData] = useState<TimeSeriesData[]>([]);
  const [providerUsage, setProviderUsage] = useState<ProviderUsage[]>([]);
  const [modelUsage, setModelUsage] = useState<ModelUsage[]>([]);
  const [virtualKeyUsage, setVirtualKeyUsage] = useState<VirtualKeyUsage[]>([]);
  const [endpointUsage, setEndpointUsage] = useState<EndpointUsage[]>([]);

  // Use utility hooks
  const { fetchAnalytics: fetchAnalyticsData } = useFetchAnalytics();
  const { handleExport } = useExportAnalytics();

  const fetchAnalytics = useCallback(async () => {
    try {
      setIsLoading(true);
      const data = await fetchAnalyticsData(timeRange);
      
      setMetrics(data.metrics);
      setTimeSeriesData(data.timeSeries ?? []);
      setProviderUsage(data.providerUsage ?? []);
      setModelUsage(data.modelUsage ?? []);
      setVirtualKeyUsage(data.virtualKeyUsage ?? []);
      setEndpointUsage(data.endpointUsage ?? []);
    } catch (error) {
      console.error('Error fetching analytics:', error);
    } finally {
      setIsLoading(false);
    }
  }, [timeRange, fetchAnalyticsData]);

  useEffect(() => {
    void fetchAnalytics();
  }, [fetchAnalytics]);

  return (
    <Stack gap="xl">
      <Group justify="space-between">
        <div>
          <Title order={1}>Usage Analytics</Title>
          <Text c="dimmed">Comprehensive API usage statistics and trends</Text>
        </div>
        <Group>
          <Select
            value={timeRange}
            onChange={(value) => setTimeRange(value ?? '7d')}
            data={[
              { value: '24h', label: 'Last 24 Hours' },
              { value: '7d', label: 'Last 7 Days' },
              { value: '30d', label: 'Last 30 Days' },
              { value: '90d', label: 'Last 90 Days' },
            ]}
          />
          <Button
            variant="light"
            leftSection={<IconDownload size={16} />}
            onClick={() => void handleExport(timeRange)}
          >
            Export
          </Button>
          <Button
            variant="light"
            leftSection={<IconRefresh size={16} />}
            onClick={() => void fetchAnalytics()}
            loading={isLoading}
          >
            Refresh
          </Button>
        </Group>
      </Group>

      {/* Analytics Charts */}
      <AnalyticsCharts
        isLoading={isLoading}
        metrics={metrics}
        timeSeriesData={timeSeriesData}
        providerUsage={providerUsage}
        modelUsage={modelUsage}
        virtualKeyUsage={virtualKeyUsage}
      />

      {/* Endpoint Performance */}
      <Card withBorder>
        <Card.Section withBorder inheritPadding py="xs">
          <Text fw={500}>Endpoint Performance</Text>
        </Card.Section>
        <Card.Section inheritPadding py="md">
          <ScrollArea>
            <Table>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Endpoint</Table.Th>
                  <Table.Th>Requests</Table.Th>
                  <Table.Th>Avg Duration</Table.Th>
                  <Table.Th>Error Rate</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {isLoading ? (
                  <Table.Tr>
                    <Table.Td colSpan={4}>
                      <Skeleton height={200} />
                    </Table.Td>
                  </Table.Tr>
                ) : (
                  endpointUsage.map((endpoint) => (
                    <Table.Tr key={endpoint.endpoint}>
                      <Table.Td>
                        <Code>{endpoint.endpoint}</Code>
                      </Table.Td>
                      <Table.Td>{formatters.number(endpoint.requests)}</Table.Td>
                      <Table.Td>{endpoint.avgDuration}ms</Table.Td>
                      <Table.Td>
                        <Badge 
                          color={(() => {
                            if (endpoint.errorRate > 5) return 'red';
                            if (endpoint.errorRate > 1) return 'orange';
                            return 'green';
                          })()}
                          variant="light"
                        >
                          {endpoint.errorRate.toFixed(1)}%
                        </Badge>
                      </Table.Td>
                    </Table.Tr>
                  ))
                )}
              </Table.Tbody>
            </Table>
          </ScrollArea>
        </Card.Section>
      </Card>
    </Stack>
  );
}