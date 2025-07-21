'use client';

import {
  Stack,
  Title,
  Text,
  Card,
  Group,
  Button,
  Badge,
  SimpleGrid,
  ThemeIcon,
  Progress,
  Paper,
  Select,
  Table,
  ScrollArea,
  LoadingOverlay,
  Alert,
  Tabs,
  Code,
  RingProgress,
  Grid,
} from '@mantine/core';
import { AreaChart, LineChart } from '@mantine/charts';
import {
  IconMicrophone,
  IconVolume,
  IconClock,
  IconCurrencyDollar,
  IconRefresh,
  IconDownload,
  IconTrendingUp,
  IconTrendingDown,
  IconFileMusic,
  IconActivity,
  IconCalendar,
  IconFilter,
  IconInfoCircle,
} from '@tabler/icons-react';
import { useState, useEffect } from 'react';
import { notifications } from '@mantine/notifications';
import { formatters } from '@/lib/utils/formatters';

interface AudioUsageStats {
  totalMinutes: number;
  totalRequests: number;
  totalCost: number;
  avgRequestDuration: number;
  transcriptionMinutes: number;
  synthesisMinutes: number;
  successRate: number;
  errorRate: number;
}

interface AudioUsageByProvider {
  provider: string;
  transcriptionMinutes: number;
  synthesisMinutes: number;
  totalMinutes: number;
  requests: number;
  cost: number;
  avgLatency: number;
}

interface AudioUsageByModel {
  model: string;
  provider: string;
  type: 'transcription' | 'synthesis';
  minutes: number;
  requests: number;
  cost: number;
  costPerMinute: number;
}

interface DailyUsage {
  date: string;
  transcriptionMinutes: number;
  synthesisMinutes: number;
  requests: number;
  cost: number;
  errors: number;
}

interface VirtualKeyUsage {
  keyName: string;
  keyId: string;
  transcriptionMinutes: number;
  synthesisMinutes: number;
  totalMinutes: number;
  requests: number;
  cost: number;
  lastUsed: string;
}

export default function AudioUsagePage() {
  const [isLoading, setIsLoading] = useState(true);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [activeTab, setActiveTab] = useState<string | null>('overview');
  const [timeRange, setTimeRange] = useState('7d');
  const [selectedProvider, setSelectedProvider] = useState<string | null>('all');
  const [stats, setStats] = useState<AudioUsageStats | null>(null);
  const [providerUsage, setProviderUsage] = useState<AudioUsageByProvider[]>([]);
  const [modelUsage, setModelUsage] = useState<AudioUsageByModel[]>([]);
  const [dailyUsage, setDailyUsage] = useState<DailyUsage[]>([]);
  const [virtualKeyUsage, setVirtualKeyUsage] = useState<VirtualKeyUsage[]>([]);

  useEffect(() => {
    fetchUsageData();
  }, [timeRange, selectedProvider]);

  const fetchUsageData = async () => {
    try {
      // Mock data for development
      const mockStats: AudioUsageStats = {
        totalMinutes: 18432,
        totalRequests: 34567,
        totalCost: 1234.56,
        avgRequestDuration: 32.5,
        transcriptionMinutes: 12456,
        synthesisMinutes: 5976,
        successRate: 98.5,
        errorRate: 1.5,
      };

      const mockProviderUsage: AudioUsageByProvider[] = [
        {
          provider: 'OpenAI',
          transcriptionMinutes: 8234,
          synthesisMinutes: 1567,
          totalMinutes: 9801,
          requests: 15678,
          cost: 456.78,
          avgLatency: 450,
        },
        {
          provider: 'ElevenLabs',
          transcriptionMinutes: 0,
          synthesisMinutes: 3456,
          totalMinutes: 3456,
          requests: 6789,
          cost: 678.90,
          avgLatency: 380,
        },
        {
          provider: 'Azure',
          transcriptionMinutes: 2345,
          synthesisMinutes: 876,
          totalMinutes: 3221,
          requests: 8765,
          cost: 89.12,
          avgLatency: 320,
        },
        {
          provider: 'Google',
          transcriptionMinutes: 1877,
          synthesisMinutes: 77,
          totalMinutes: 1954,
          requests: 3335,
          cost: 9.76,
          avgLatency: 410,
        },
      ];

      const mockModelUsage: AudioUsageByModel[] = [
        { model: 'whisper-1', provider: 'OpenAI', type: 'transcription', minutes: 8234, requests: 12456, cost: 49.40, costPerMinute: 0.006 },
        { model: 'tts-1', provider: 'OpenAI', type: 'synthesis', minutes: 987, requests: 2345, cost: 14.81, costPerMinute: 0.015 },
        { model: 'tts-1-hd', provider: 'OpenAI', type: 'synthesis', minutes: 580, requests: 890, cost: 17.40, costPerMinute: 0.030 },
        { model: 'eleven-multilingual-v2', provider: 'ElevenLabs', type: 'synthesis', minutes: 3456, requests: 6789, cost: 1658.88, costPerMinute: 0.48 },
        { model: 'azure-speech-to-text', provider: 'Azure', type: 'transcription', minutes: 2345, requests: 5678, cost: 37.52, costPerMinute: 0.016 },
        { model: 'azure-neural-tts', provider: 'Azure', type: 'synthesis', minutes: 876, requests: 3087, cost: 14.02, costPerMinute: 0.016 },
        { model: 'google-standard', provider: 'Google', type: 'transcription', minutes: 1877, requests: 3245, cost: 45.05, costPerMinute: 0.024 },
      ];

      // Generate daily usage data
      const days = timeRange === '24h' ? 1 : timeRange === '7d' ? 7 : timeRange === '30d' ? 30 : 90;
      const mockDailyUsage: DailyUsage[] = Array.from({ length: days }, (_, i) => {
        const date = new Date();
        date.setDate(date.getDate() - (days - 1 - i));
        const baseRequests = 800 + Math.random() * 400;
        const transcriptionRatio = 0.6 + Math.random() * 0.2;
        
        return {
          date: date.toISOString().split('T')[0],
          transcriptionMinutes: Math.floor(baseRequests * transcriptionRatio * 0.5),
          synthesisMinutes: Math.floor(baseRequests * (1 - transcriptionRatio) * 0.5),
          requests: Math.floor(baseRequests),
          cost: baseRequests * 0.045 + Math.random() * 10,
          errors: Math.floor(Math.random() * 10),
        };
      });

      const mockVirtualKeyUsage: VirtualKeyUsage[] = [
        {
          keyName: 'Production API',
          keyId: 'vk_prod_123',
          transcriptionMinutes: 5678,
          synthesisMinutes: 2345,
          totalMinutes: 8023,
          requests: 12456,
          cost: 567.89,
          lastUsed: '2024-01-10T12:30:00Z',
        },
        {
          keyName: 'Customer A',
          keyId: 'vk_cust_a_456',
          transcriptionMinutes: 3456,
          synthesisMinutes: 1234,
          totalMinutes: 4690,
          requests: 8765,
          cost: 345.67,
          lastUsed: '2024-01-10T12:25:00Z',
        },
        {
          keyName: 'Development API',
          keyId: 'vk_dev_789',
          transcriptionMinutes: 2345,
          synthesisMinutes: 876,
          totalMinutes: 3221,
          requests: 6543,
          cost: 234.56,
          lastUsed: '2024-01-10T11:45:00Z',
        },
        {
          keyName: 'Mobile App',
          keyId: 'vk_mobile_012',
          transcriptionMinutes: 977,
          synthesisMinutes: 1521,
          totalMinutes: 2498,
          requests: 4567,
          cost: 87.54,
          lastUsed: '2024-01-10T12:15:00Z',
        },
      ];

      setStats(mockStats);
      setProviderUsage(mockProviderUsage);
      setModelUsage(mockModelUsage);
      setDailyUsage(mockDailyUsage);
      setVirtualKeyUsage(mockVirtualKeyUsage);
    } catch (error) {
      console.error('Error fetching audio usage data:', error);
      notifications.show({
        title: 'Error',
        message: 'Failed to load audio usage data',
        color: 'red',
      });
    } finally {
      setIsLoading(false);
    }
  };

  const handleRefresh = async () => {
    setIsRefreshing(true);
    await fetchUsageData();
    setIsRefreshing(false);
    notifications.show({
      title: 'Refreshed',
      message: 'Audio usage data updated',
      color: 'green',
    });
  };

  const handleExport = () => {
    // In production, this would generate a CSV or PDF report
    notifications.show({
      title: 'Export Started',
      message: 'Your audio usage report is being generated',
      color: 'green',
    });
  };

  const transcriptionPercentage = stats ? (stats.transcriptionMinutes / stats.totalMinutes) * 100 : 0;
  const synthesisPercentage = stats ? (stats.synthesisMinutes / stats.totalMinutes) * 100 : 0;

  const chartData = dailyUsage.map(day => ({
    date: formatters.date(day.date, { month: 'short', day: 'numeric' }),
    transcription: day.transcriptionMinutes,
    synthesis: day.synthesisMinutes,
    cost: day.cost,
  }));

  if (isLoading) {
    return (
      <Stack>
        <Card shadow="sm" p="md" radius="md" pos="relative" mih={200}>
          <LoadingOverlay visible={true} />
        </Card>
      </Stack>
    );
  }

  return (
    <Stack gap="xl">
      <Card shadow="sm" p="md" radius="md">
        <Group justify="space-between" align="center">
          <div>
            <Title order={2}>Audio Usage Analytics</Title>
            <Text size="sm" c="dimmed" mt={4}>
              Track audio processing usage and costs
            </Text>
          </div>
          <Group>
            <Select
              value={timeRange}
              onChange={(value) => setTimeRange(value || '7d')}
              data={[
                { value: '24h', label: 'Last 24 Hours' },
                { value: '7d', label: 'Last 7 Days' },
                { value: '30d', label: 'Last 30 Days' },
                { value: '90d', label: 'Last 90 Days' },
              ]}
              w={150}
            />
            <Button
              variant="light"
              leftSection={<IconRefresh size={16} />}
              onClick={handleRefresh}
              loading={isRefreshing}
            >
              Refresh
            </Button>
            <Button
              variant="filled"
              leftSection={<IconDownload size={16} />}
              onClick={handleExport}
            >
              Export
            </Button>
          </Group>
        </Group>
      </Card>

      <SimpleGrid cols={{ base: 1, sm: 2, lg: 4 }} spacing="md">
        <Card padding="lg" radius="md" withBorder>
          <Group justify="space-between">
            <div>
              <Text size="sm" c="dimmed" fw={600} tt="uppercase">
                Total Usage
              </Text>
              <Text size="xl" fw={700} mt={4}>
                {formatters.duration((stats?.totalMinutes || 0) * 60000)}
              </Text>
              <Text size="xs" c="dimmed" mt={4}>
                {stats?.totalMinutes.toLocaleString()} minutes
              </Text>
            </div>
            <ThemeIcon color="blue" variant="light" size={48} radius="md">
              <IconClock size={24} />
            </ThemeIcon>
          </Group>
        </Card>

        <Card padding="lg" radius="md" withBorder>
          <Group justify="space-between">
            <div>
              <Text size="sm" c="dimmed" fw={600} tt="uppercase">
                Total Requests
              </Text>
              <Text size="xl" fw={700} mt={4}>
                {stats?.totalRequests.toLocaleString() || 0}
              </Text>
              <Text size="xs" c="dimmed" mt={4}>
                Avg {stats?.avgRequestDuration.toFixed(1)}s
              </Text>
            </div>
            <ThemeIcon color="green" variant="light" size={48} radius="md">
              <IconActivity size={24} />
            </ThemeIcon>
          </Group>
        </Card>

        <Card padding="lg" radius="md" withBorder>
          <Group justify="space-between">
            <div>
              <Text size="sm" c="dimmed" fw={600} tt="uppercase">
                Total Cost
              </Text>
              <Text size="xl" fw={700} mt={4}>
                ${stats?.totalCost.toFixed(2) || '0.00'}
              </Text>
              <Text size="xs" c="dimmed" mt={4}>
                {timeRange} period
              </Text>
            </div>
            <ThemeIcon color="orange" variant="light" size={48} radius="md">
              <IconCurrencyDollar size={24} />
            </ThemeIcon>
          </Group>
        </Card>

        <Card padding="lg" radius="md" withBorder>
          <Group justify="space-between">
            <div>
              <Text size="sm" c="dimmed" fw={600} tt="uppercase">
                Success Rate
              </Text>
              <Text size="xl" fw={700} mt={4}>
                {stats?.successRate || 0}%
              </Text>
              <Text size="xs" c="dimmed" mt={4}>
                {stats?.errorRate || 0}% errors
              </Text>
            </div>
            <ThemeIcon color="teal" variant="light" size={48} radius="md">
              <IconTrendingUp size={24} />
            </ThemeIcon>
          </Group>
        </Card>
      </SimpleGrid>

      <Tabs value={activeTab} onChange={setActiveTab}>
        <Tabs.List>
          <Tabs.Tab value="overview" leftSection={<IconActivity size={16} />}>
            Overview
          </Tabs.Tab>
          <Tabs.Tab value="providers" leftSection={<IconMicrophone size={16} />}>
            By Provider
          </Tabs.Tab>
          <Tabs.Tab value="models" leftSection={<IconVolume size={16} />}>
            By Model
          </Tabs.Tab>
          <Tabs.Tab value="virtualkeys" leftSection={<IconFileMusic size={16} />}>
            By Virtual Key
          </Tabs.Tab>
        </Tabs.List>

        <Tabs.Panel value="overview" pt="md">
          <Grid>
            <Grid.Col span={{ base: 12, md: 8 }}>
              <Card shadow="sm" p="md" radius="md" withBorder>
                <Title order={4} mb="md">Usage Trends</Title>
                <AreaChart
                  h={300}
                  data={chartData}
                  dataKey="date"
                  series={[
                    { name: 'transcription', label: 'Transcription (min)', color: 'blue.6' },
                    { name: 'synthesis', label: 'Synthesis (min)', color: 'green.6' },
                  ]}
                  curveType="linear"
                  strokeWidth={2}
                  fillOpacity={0.4}
                />
              </Card>
            </Grid.Col>

            <Grid.Col span={{ base: 12, md: 4 }}>
              <Card shadow="sm" p="md" radius="md" withBorder h="100%">
                <Title order={4} mb="md">Usage Distribution</Title>
                <Stack align="center" gap="md">
                  <RingProgress
                    size={180}
                    thickness={30}
                    sections={[
                      { value: transcriptionPercentage, color: 'blue' },
                      { value: synthesisPercentage, color: 'green' },
                    ]}
                    label={
                      <div style={{ textAlign: 'center' }}>
                        <Text size="lg" fw={700}>{stats?.totalMinutes.toLocaleString()}</Text>
                        <Text size="xs" c="dimmed">Total Minutes</Text>
                      </div>
                    }
                  />
                  <Stack gap="xs" w="100%">
                    <Group justify="space-between">
                      <Group gap="xs">
                        <div style={{ width: 12, height: 12, backgroundColor: 'var(--mantine-color-blue-6)', borderRadius: 2 }} />
                        <Text size="sm">Transcription</Text>
                      </Group>
                      <Text size="sm" fw={500}>{transcriptionPercentage.toFixed(1)}%</Text>
                    </Group>
                    <Group justify="space-between">
                      <Group gap="xs">
                        <div style={{ width: 12, height: 12, backgroundColor: 'var(--mantine-color-green-6)', borderRadius: 2 }} />
                        <Text size="sm">Synthesis</Text>
                      </Group>
                      <Text size="sm" fw={500}>{synthesisPercentage.toFixed(1)}%</Text>
                    </Group>
                  </Stack>
                </Stack>
              </Card>
            </Grid.Col>

            <Grid.Col span={12}>
              <Card shadow="sm" p="md" radius="md" withBorder>
                <Title order={4} mb="md">Cost Trends</Title>
                <LineChart
                  h={250}
                  data={chartData}
                  dataKey="date"
                  series={[
                    { name: 'cost', label: 'Cost ($)', color: 'orange.6' },
                  ]}
                  curveType="monotone"
                  strokeWidth={2}
                  valueFormatter={(value) => `$${value.toFixed(2)}`}
                />
              </Card>
            </Grid.Col>
          </Grid>
        </Tabs.Panel>

        <Tabs.Panel value="providers" pt="md">
          <Card shadow="sm" p="md" radius="md" withBorder>
            <ScrollArea>
              <Table>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Provider</Table.Th>
                    <Table.Th>Transcription</Table.Th>
                    <Table.Th>Synthesis</Table.Th>
                    <Table.Th>Total Minutes</Table.Th>
                    <Table.Th>Requests</Table.Th>
                    <Table.Th>Cost</Table.Th>
                    <Table.Th>Avg Latency</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {providerUsage.map((provider) => (
                    <Table.Tr key={provider.provider}>
                      <Table.Td>
                        <Text fw={500}>{provider.provider}</Text>
                      </Table.Td>
                      <Table.Td>{provider.transcriptionMinutes.toLocaleString()} min</Table.Td>
                      <Table.Td>{provider.synthesisMinutes.toLocaleString()} min</Table.Td>
                      <Table.Td>
                        <Group gap="xs">
                          <Text fw={500}>{provider.totalMinutes.toLocaleString()} min</Text>
                          <Progress
                            value={(provider.totalMinutes / (stats?.totalMinutes || 1)) * 100}
                            size="sm"
                            w={60}
                          />
                        </Group>
                      </Table.Td>
                      <Table.Td>{provider.requests.toLocaleString()}</Table.Td>
                      <Table.Td>
                        <Text fw={600}>${provider.cost.toFixed(2)}</Text>
                      </Table.Td>
                      <Table.Td>
                        <Badge
                          color={provider.avgLatency < 400 ? 'green' : provider.avgLatency < 500 ? 'yellow' : 'red'}
                          variant="light"
                        >
                          {provider.avgLatency}ms
                        </Badge>
                      </Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </ScrollArea>
          </Card>
        </Tabs.Panel>

        <Tabs.Panel value="models" pt="md">
          <Card shadow="sm" p="md" radius="md" withBorder>
            <ScrollArea>
              <Table>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Model</Table.Th>
                    <Table.Th>Provider</Table.Th>
                    <Table.Th>Type</Table.Th>
                    <Table.Th>Minutes</Table.Th>
                    <Table.Th>Requests</Table.Th>
                    <Table.Th>Cost/Min</Table.Th>
                    <Table.Th>Total Cost</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {modelUsage.map((model) => (
                    <Table.Tr key={`${model.provider}-${model.model}`}>
                      <Table.Td>
                        <Text fw={500}>{model.model}</Text>
                      </Table.Td>
                      <Table.Td>{model.provider}</Table.Td>
                      <Table.Td>
                        <Badge
                          color={model.type === 'transcription' ? 'blue' : 'green'}
                          variant="light"
                        >
                          {model.type === 'transcription' ? 'STT' : 'TTS'}
                        </Badge>
                      </Table.Td>
                      <Table.Td>{model.minutes.toLocaleString()} min</Table.Td>
                      <Table.Td>{model.requests.toLocaleString()}</Table.Td>
                      <Table.Td>
                        <Code>${model.costPerMinute.toFixed(3)}</Code>
                      </Table.Td>
                      <Table.Td>
                        <Text fw={600}>${model.cost.toFixed(2)}</Text>
                      </Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </ScrollArea>
          </Card>
        </Tabs.Panel>

        <Tabs.Panel value="virtualkeys" pt="md">
          <Card shadow="sm" p="md" radius="md" withBorder>
            <ScrollArea>
              <Table>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Virtual Key</Table.Th>
                    <Table.Th>Transcription</Table.Th>
                    <Table.Th>Synthesis</Table.Th>
                    <Table.Th>Total Minutes</Table.Th>
                    <Table.Th>Requests</Table.Th>
                    <Table.Th>Cost</Table.Th>
                    <Table.Th>Last Used</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {virtualKeyUsage.map((key) => (
                    <Table.Tr key={key.keyId}>
                      <Table.Td>
                        <div>
                          <Text fw={500}>{key.keyName}</Text>
                          <Text size="xs" c="dimmed">{key.keyId}</Text>
                        </div>
                      </Table.Td>
                      <Table.Td>{key.transcriptionMinutes.toLocaleString()} min</Table.Td>
                      <Table.Td>{key.synthesisMinutes.toLocaleString()} min</Table.Td>
                      <Table.Td>
                        <Group gap="xs">
                          <Text fw={500}>{key.totalMinutes.toLocaleString()} min</Text>
                          <Progress
                            value={(key.totalMinutes / (stats?.totalMinutes || 1)) * 100}
                            size="sm"
                            w={60}
                          />
                        </Group>
                      </Table.Td>
                      <Table.Td>{key.requests.toLocaleString()}</Table.Td>
                      <Table.Td>
                        <Text fw={600}>${key.cost.toFixed(2)}</Text>
                      </Table.Td>
                      <Table.Td>
                        <Text size="sm">{formatters.date(key.lastUsed, { relativeDays: 7 })}</Text>
                      </Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </ScrollArea>
          </Card>
        </Tabs.Panel>
      </Tabs>

      <Alert
        icon={<IconInfoCircle size={16} />}
        title="Audio Usage Information"
        color="blue"
      >
        <Text size="sm">
          Audio usage is calculated based on the duration of processed audio files. Transcription is billed per minute of audio input, 
          while synthesis is billed per minute of generated audio. Costs vary by provider and model. 
          Usage data is updated in real-time and aggregated hourly.
        </Text>
      </Alert>

      <LoadingOverlay visible={isRefreshing} />
    </Stack>
  );
}