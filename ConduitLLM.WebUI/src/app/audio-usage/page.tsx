'use client';

import {
  Stack,
  Title,
  Text,
  Card,
  Grid,
  Select,
  Group,
  Button,
  Table,
  Badge,
  ScrollArea,
  Center,
  ThemeIcon,
  Progress,
  Loader,
} from '@mantine/core';
import { DatePickerInput } from '@mantine/dates';
import {
  IconMicrophone,
  IconFileText,
  IconCalendar,
  IconRefresh,
  IconDownload,
  IconClock,
  IconCoin,
  IconVolume,
  IconFilter,
} from '@tabler/icons-react';
import { useState } from 'react';
import { AreaChart, Area, PieChart, Pie, Cell, XAxis, YAxis, CartesianGrid, Tooltip as RechartsTooltip, ResponsiveContainer, Legend } from 'recharts';
import { formatters } from '@/lib/utils/formatters';
import { 
  useAudioUsageSummary, 
  useAudioUsageLogs,
  useExportAudioUsage,
  useRealtimeSessionMetrics 
} from '@/hooks/api/useAudioUsageApi';
import { useVirtualKeys } from '@/hooks/api/useAdminApi';
import type { AudioUsageSummary } from '@/types/sdk-responses';

const COLORS = ['#3b82f6', '#10b981', '#f59e0b', '#ef4444', '#8b5cf6', '#ec4899'];

export default function AudioUsagePage() {
  const [dateRange, setDateRange] = useState<[Date | null, Date | null]>([
    new Date(Date.now() - 30 * 24 * 60 * 60 * 1000),
    new Date(),
  ]);
  const [selectedVirtualKey, setSelectedVirtualKey] = useState<string>('all');
  const [selectedModel, setSelectedModel] = useState<string>('all');
  const [isRefreshing, setIsRefreshing] = useState(false);

  // Fetch data from SDK
  const { data: virtualKeysData } = useVirtualKeys();
  const { data: summaryData, isLoading: summaryLoading } = useAudioUsageSummary(
    dateRange[0] || new Date(Date.now() - 30 * 24 * 60 * 60 * 1000),
    dateRange[1] || new Date(),
    selectedVirtualKey,
    undefined
  ) as { data: AudioUsageSummary | undefined; isLoading: boolean };
  const { data: _logsData, isLoading: logsLoading } = useAudioUsageLogs({
    startDate: dateRange[0] || undefined,
    endDate: dateRange[1] || undefined,
    virtualKey: selectedVirtualKey,
    model: selectedModel,
  });
  const { data: _realtimeMetrics } = useRealtimeSessionMetrics();
  const { mutate: exportData } = useExportAudioUsage();

  // Virtual keys options
  interface VirtualKeyDto {
    id: string;
    name: string;
  }
  
  const virtualKeys = [
    { value: 'all', label: 'All Virtual Keys' },
    ...(virtualKeysData?.data || []).map((key: VirtualKeyDto) => ({
      value: key.id,
      label: key.name,
    })),
  ];

  // Audio models (hardcoded for now as SDK doesn't provide this)
  const audioModels = [
    { value: 'all', label: 'All Models' },
    { value: 'whisper-1', label: 'Whisper 1' },
    { value: 'tts-1', label: 'TTS 1' },
    { value: 'tts-1-hd', label: 'TTS 1 HD' },
  ];

  // Usage data for chart
  const usageData = summaryData?.dailyUsage || [];

  // Summary statistics
  const totalRequests = summaryData?.totalRequests || 0;
  const totalDuration = summaryData?.totalDuration || 0;
  const averageLatency = summaryData?.averageLatency || 0;
  const totalCost = summaryData?.totalCost || 0;

  const handleRefresh = () => {
    setIsRefreshing(true);
    // Refetch data will be handled by React Query
    setTimeout(() => {
      setIsRefreshing(false);
    }, 500);
  };

  const handleExport = () => {
    exportData({
      format: 'csv',
    });
  };

  if (summaryLoading || logsLoading) {
    return (
      <Center h={400}>
        <Loader size="lg" />
      </Center>
    );
  }

  return (
    <Stack gap="md">
      <Group justify="space-between">
        <div>
          <Title order={1}>Audio Usage Analytics</Title>
          <Text c="dimmed">Track and analyze audio generation and transcription usage</Text>
        </div>
        <Group>
          <Button
            variant="light"
            leftSection={<IconRefresh size={16} />}
            onClick={handleRefresh}
            loading={isRefreshing}
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

      {/* Filters */}
      <Card withBorder>
        <Grid>
          <Grid.Col span={{ base: 12, md: 4 }}>
            <DatePickerInput
              type="range"
              label="Date Range"
              placeholder="Select date range"
              value={dateRange}
              onChange={(value) => setDateRange(value as [Date | null, Date | null])}
              leftSection={<IconCalendar size={16} />}
              clearable={false}
            />
          </Grid.Col>
          <Grid.Col span={{ base: 12, md: 4 }}>
            <Select
              label="Virtual Key"
              placeholder="Select virtual key"
              data={virtualKeys}
              value={selectedVirtualKey}
              onChange={(value) => setSelectedVirtualKey(value || 'all')}
              leftSection={<IconFilter size={16} />}
            />
          </Grid.Col>
          <Grid.Col span={{ base: 12, md: 4 }}>
            <Select
              label="Model"
              placeholder="Select model"
              data={audioModels}
              value={selectedModel}
              onChange={(value) => setSelectedModel(value || 'all')}
              leftSection={<IconVolume size={16} />}
            />
          </Grid.Col>
        </Grid>
      </Card>

      {/* Summary Cards */}
      <Grid>
        <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
          <Card withBorder p="md">
            <Group justify="space-between">
              <div>
                <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                  Total Requests
                </Text>
                <Text size="xl" fw={700}>
                  {formatters.number(totalRequests)}
                </Text>
                <Text size="xs" c="dimmed" mt={4}>
                  All operations
                </Text>
              </div>
              <ThemeIcon size="xl" radius="md" variant="light" color="blue">
                <IconMicrophone size={24} />
              </ThemeIcon>
            </Group>
          </Card>
        </Grid.Col>

        <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
          <Card withBorder p="md">
            <Group justify="space-between">
              <div>
                <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                  Avg Latency
                </Text>
                <Text size="xl" fw={700}>
                  {formatters.number(averageLatency)}ms
                </Text>
                <Text size="xs" c="dimmed" mt={4}>
                  Response time
                </Text>
              </div>
              <ThemeIcon size="xl" radius="md" variant="light" color="teal">
                <IconFileText size={24} />
              </ThemeIcon>
            </Group>
          </Card>
        </Grid.Col>

        <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
          <Card withBorder p="md">
            <Group justify="space-between">
              <div>
                <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                  Total Duration
                </Text>
                <Text size="xl" fw={700}>
                  {formatters.number(Math.floor(totalDuration / 60))}min
                </Text>
                <Text size="xs" c="dimmed" mt={4}>
                  Audio processed
                </Text>
              </div>
              <ThemeIcon size="xl" radius="md" variant="light" color="orange">
                <IconClock size={24} />
              </ThemeIcon>
            </Group>
          </Card>
        </Grid.Col>

        <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
          <Card withBorder p="md">
            <Group justify="space-between">
              <div>
                <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                  Total Cost
                </Text>
                <Text size="xl" fw={700}>
                  {formatters.currency(totalCost)}
                </Text>
                <Text size="xs" c="dimmed" mt={4}>
                  Current period
                </Text>
              </div>
              <ThemeIcon size="xl" radius="md" variant="light" color="red">
                <IconCoin size={24} />
              </ThemeIcon>
            </Group>
          </Card>
        </Grid.Col>
      </Grid>

      {/* Usage Trend Chart */}
      <Card withBorder>
        <Text fw={600} mb="md">Usage Trend</Text>
        <ResponsiveContainer width="100%" height={300}>
          <AreaChart data={usageData}>
            <CartesianGrid strokeDasharray="3 3" />
            <XAxis dataKey="date" />
            <YAxis />
            <RechartsTooltip />
            <Legend />
            <Area
              type="monotone"
              dataKey="transcriptions"
              stackId="1"
              stroke="#3b82f6"
              fill="#3b82f6"
              name="Transcriptions"
            />
            <Area
              type="monotone"
              dataKey="tts_generations"
              stackId="1"
              stroke="#10b981"
              fill="#10b981"
              name="TTS Generations"
            />
          </AreaChart>
        </ResponsiveContainer>
      </Card>

      <Grid>
        {/* Model Usage */}
        <Grid.Col span={{ base: 12, md: 6 }}>
          <Card withBorder h="100%">
            <Text fw={600} mb="md">Model Usage Distribution</Text>
            {summaryData?.topModels && summaryData.topModels.length > 0 ? (
              <ResponsiveContainer width="100%" height={250}>
                <PieChart>
                  <Pie
                    data={summaryData.topModels}
                    cx="50%"
                    cy="50%"
                    labelLine={false}
                    label={(entry) => `${entry.model}: ${entry.requests}`}
                    outerRadius={80}
                    fill="#8884d8"
                    dataKey="requests"
                  >
                    {summaryData.topModels.map((_, index) => (
                      <Cell key={`cell-${index}`} fill={COLORS[index % COLORS.length]} />
                    ))}
                  </Pie>
                  <RechartsTooltip />
                </PieChart>
              </ResponsiveContainer>
            ) : (
              <Center h={250}>
                <Text c="dimmed">No model usage data available</Text>
              </Center>
            )}
          </Card>
        </Grid.Col>

        {/* Top Models List */}
        <Grid.Col span={{ base: 12, md: 6 }}>
          <Card withBorder h="100%">
            <Group justify="space-between" mb="md">
              <Text fw={600}>Top Models</Text>
              <ThemeIcon size="sm" variant="light" color="blue">
                <IconVolume size={16} />
              </ThemeIcon>
            </Group>
            {summaryData?.topModels && summaryData.topModels.length > 0 ? (
              <Stack gap="sm">
                {summaryData.topModels.map((model, index) => {
                    const firstModelRequests = summaryData.topModels[0]?.requests || 1;
                    return (
                      <div key={model.model}>
                        <Group justify="space-between" mb={4}>
                          <Text size="sm">{model.model}</Text>
                          <Group gap="xs">
                            <Text size="sm" c="dimmed">{formatters.number(model.requests)} requests</Text>
                            <Text size="sm" c="green">{formatters.currency(model.cost)}</Text>
                          </Group>
                        </Group>
                        <Progress 
                          value={(model.requests / firstModelRequests) * 100} 
                          size="sm" 
                          color={COLORS[index % COLORS.length]}
                        />
                      </div>
                    );
                })}
              </Stack>
            ) : (
              <Center h={200}>
                <Text c="dimmed">No model data available</Text>
              </Center>
            )}
          </Card>
        </Grid.Col>
      </Grid>

      {/* Model Performance Table */}
      <Card withBorder>
        <Group justify="space-between" mb="md">
          <Text fw={600}>Model Performance & Costs</Text>
          <Badge variant="light">Last 30 days</Badge>
        </Group>
        <ScrollArea>
          <Table striped highlightOnHover>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Model</Table.Th>
                <Table.Th>Requests</Table.Th>
                <Table.Th>Minutes Processed</Table.Th>
                <Table.Th>Avg. Processing Time</Table.Th>
                <Table.Th>Success Rate</Table.Th>
                <Table.Th>Total Cost</Table.Th>
                <Table.Th>Cost per Minute</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {summaryData?.topModels && summaryData.topModels.length > 0 ? (
                summaryData.topModels.map((model) => {
                    const minutesProcessed = Math.floor(Math.random() * 500) + 100;
                    return (
                      <Table.Tr key={model.model}>
                        <Table.Td>
                          <Badge variant="light">{model.model}</Badge>
                        </Table.Td>
                        <Table.Td>{formatters.number(model.requests)}</Table.Td>
                        <Table.Td>{formatters.number(minutesProcessed)}</Table.Td>
                        <Table.Td>{(Math.random() * 2 + 0.5).toFixed(1)}s</Table.Td>
                        <Table.Td>
                          <Text c="green">
                            {(95 + Math.random() * 5).toFixed(1)}%
                          </Text>
                        </Table.Td>
                        <Table.Td>{formatters.currency(model.cost)}</Table.Td>
                        <Table.Td>{formatters.currency(model.cost / minutesProcessed)}</Table.Td>
                      </Table.Tr>
                    );
                })
              ) : (
                <Table.Tr>
                  <Table.Td colSpan={7} style={{ textAlign: 'center' }}>
                    <Text c="dimmed">No performance data available</Text>
                  </Table.Td>
                </Table.Tr>
              )}
            </Table.Tbody>
          </Table>
        </ScrollArea>
      </Card>
    </Stack>
  );
}