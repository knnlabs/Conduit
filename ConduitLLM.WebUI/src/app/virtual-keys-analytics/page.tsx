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
  Center,
  ActionIcon,
  Tooltip,
  Modal,
  RingProgress,
  Timeline,
  NumberInput,
  Divider,
  Code,
} from '@mantine/core';
import {
  IconKey,
  IconDownload,
  IconRefresh,
  IconAlertCircle,
  IconTrendingUp,
  IconTrendingDown,
  IconClock,
  IconShield,
  IconActivity,
  IconChartLine,
  IconCreditCard,
  IconEye,
  IconSettings,
  IconFilter,
  IconSearch,
  IconBan,
  IconCheck,
  IconX,
  IconTarget,
  IconBolt,
  IconUsers,
  IconServer,
  IconGlobe,
  IconLock,
  IconBell,
  IconTrophy,
  IconFlame,
  IconHeartHandshake,
} from '@tabler/icons-react';
import { useState } from 'react';
import { useDisclosure } from '@mantine/hooks';
import { 
  useVirtualKeysOverview,
  useVirtualKeyUsageMetrics,
  useVirtualKeyBudgetAnalytics,
  useVirtualKeyPerformanceMetrics,
  useVirtualKeySecurityMetrics,
  useVirtualKeyTrends,
  useVirtualKeysLeaderboard,
  useExportVirtualKeysData,
  VirtualKeyOverview,
  TimeRangeFilter 
} from '@/hooks/api/useVirtualKeysAnalyticsApi';
import { notifications } from '@mantine/notifications';
import { CostChart } from '@/components/charts/CostChart';

export default function VirtualKeysAnalyticsPage() {
  const [timeRangeValue, setTimeRangeValue] = useState('24h');
  const [selectedTab, setSelectedTab] = useState('overview');
  const [selectedKey, setSelectedKey] = useState<VirtualKeyOverview | null>(null);
  const [detailsOpened, { open: openDetails, close: closeDetails }] = useDisclosure(false);
  const [budgetOpened, { open: openBudget, close: closeBudget }] = useDisclosure(false);
  const [performanceOpened, { open: openPerformance, close: closePerformance }] = useDisclosure(false);
  const [securityOpened, { open: openSecurity, close: closeSecurity }] = useDisclosure(false);
  
  const timeRange: TimeRangeFilter = { range: timeRangeValue as any };
  
  const { data: virtualKeys, isLoading: keysLoading } = useVirtualKeysOverview();
  const { data: selectedKeyUsage } = useVirtualKeyUsageMetrics(selectedKey?.keyId || '', timeRange);
  const { data: selectedKeyBudget } = useVirtualKeyBudgetAnalytics(selectedKey?.keyId || '', timeRangeValue);
  const { data: selectedKeyPerformance } = useVirtualKeyPerformanceMetrics(selectedKey?.keyId || '', timeRange);
  const { data: selectedKeySecurity } = useVirtualKeySecurityMetrics(selectedKey?.keyId || '', timeRange);
  const { data: selectedKeyTrends } = useVirtualKeyTrends(selectedKey?.keyId || '', timeRange);
  const { data: leaderboard } = useVirtualKeysLeaderboard(timeRangeValue);
  const exportData = useExportVirtualKeysData();

  const isLoading = keysLoading;

  const handleExport = async (type: 'overview' | 'usage' | 'budget' | 'performance' | 'security' | 'trends' | 'leaderboard') => {
    try {
      notifications.show({
        id: 'export-start',
        title: 'Export Started',
        message: `Preparing ${type} data for export...`,
        color: 'blue',
        loading: true,
        autoClose: false,
      });
      
      const result = await exportData.mutateAsync({ 
        type, 
        keyId: selectedKey?.keyId,
        timeRange,
        format: 'csv'
      });
      
      notifications.update({
        id: 'export-start',
        title: 'Export Complete',
        message: `${type} data exported as ${result.filename}`,
        color: 'green',
        loading: false,
        autoClose: 5000,
      });
      
      console.log('Download URL:', result.url);
    } catch (error: any) {
      notifications.update({
        id: 'export-start',
        title: 'Export Failed',
        message: error.message || 'Failed to export data',
        color: 'red',
        loading: false,
        autoClose: 5000,
      });
    }
  };

  const handleRefresh = () => {
    notifications.show({
      title: 'Refreshing Data',
      message: 'Updating virtual keys analytics...',
      color: 'blue',
    });
  };

  const formatNumber = (num: number) => {
    return new Intl.NumberFormat('en-US').format(num);
  };

  const formatCurrency = (amount: number) => {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: 'USD',
    }).format(amount);
  };

  const formatLatency = (ms: number) => {
    if (ms < 1000) return `${Math.round(ms)}ms`;
    return `${(ms / 1000).toFixed(1)}s`;
  };

  const getStatusColor = (status: VirtualKeyOverview['status']) => {
    switch (status) {
      case 'active': return 'green';
      case 'suspended': return 'red';
      case 'expired': return 'gray';
      case 'rate_limited': return 'orange';
      default: return 'gray';
    }
  };

  const getStatusIcon = (status: VirtualKeyOverview['status']) => {
    switch (status) {
      case 'active': return IconCheck;
      case 'suspended': return IconBan;
      case 'expired': return IconClock;
      case 'rate_limited': return IconBolt;
      default: return IconX;
    }
  };

  const getTrendIcon = (trend: number) => {
    return trend > 0 ? IconTrendingUp : IconTrendingDown;
  };

  const getTrendColor = (trend: number, inverse = false) => {
    if (inverse) {
      return trend > 0 ? 'red' : 'green';
    }
    return trend > 0 ? 'green' : 'red';
  };

  const openKeyDetails = (key: VirtualKeyOverview, tab: 'usage' | 'budget' | 'performance' | 'security' = 'usage') => {
    setSelectedKey(key);
    switch (tab) {
      case 'usage':
        openDetails();
        break;
      case 'budget':
        openBudget();
        break;
      case 'performance':
        openPerformance();
        break;
      case 'security':
        openSecurity();
        break;
    }
  };

  const totalKeys = virtualKeys?.length || 0;
  const activeKeys = virtualKeys?.filter(k => k.status === 'active').length || 0;
  const totalRequests = virtualKeys?.reduce((sum, k) => sum + k.totalRequests, 0) || 0;
  const totalCost = virtualKeys?.reduce((sum, k) => sum + k.totalCost, 0) || 0;
  const averageLatency = (virtualKeys?.reduce((sum, k) => sum + k.averageLatency, 0) || 0) / (virtualKeys?.length || 1) || 0;
  const averageErrorRate = (virtualKeys?.reduce((sum, k) => sum + k.errorRate, 0) || 0) / (virtualKeys?.length || 1) || 0;

  const overviewCards = [
    {
      title: 'Total Virtual Keys',
      value: totalKeys,
      icon: IconKey,
      color: 'blue',
    },
    {
      title: 'Active Keys',
      value: `${activeKeys}/${totalKeys}`,
      icon: IconCheck,
      color: 'green',
    },
    {
      title: 'Total Requests',
      value: formatNumber(totalRequests),
      icon: IconActivity,
      color: 'purple',
    },
    {
      title: 'Total Cost',
      value: formatCurrency(totalCost),
      icon: IconCreditCard,
      color: 'orange',
    },
    {
      title: 'Avg Latency',
      value: formatLatency(averageLatency),
      icon: IconClock,
      color: 'teal',
    },
    {
      title: 'Avg Error Rate',
      value: `${averageErrorRate.toFixed(1)}%`,
      icon: IconAlertCircle,
      color: averageErrorRate > 2 ? 'red' : 'green',
    },
  ];

  return (
    <Stack gap="xl">
      <Group justify="space-between">
        <div>
          <Title order={1}>Virtual Keys Analytics</Title>
          <Text c="dimmed">Comprehensive analytics and monitoring for all virtual keys</Text>
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
            onClick={() => handleExport('overview')}
          >
            Export Report
          </Button>
        </Group>
      </Group>

      {/* Overview Statistics */}
      <SimpleGrid cols={{ base: 1, sm: 2, md: 3, lg: 6 }} spacing="lg">
        {overviewCards.map((stat) => (
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

      {/* Main Content Tabs */}
      <Card>
        <Tabs value={selectedTab} onChange={(value) => setSelectedTab(value || 'overview')}>
          <Tabs.List>
            <Tabs.Tab value="overview">Overview</Tabs.Tab>
            <Tabs.Tab value="usage">Usage Patterns</Tabs.Tab>
            <Tabs.Tab value="budget">Budget Analysis</Tabs.Tab>
            <Tabs.Tab value="performance">Performance</Tabs.Tab>
            <Tabs.Tab value="security">Security</Tabs.Tab>
            <Tabs.Tab value="leaderboard">Leaderboard</Tabs.Tab>
          </Tabs.List>

          <Tabs.Panel value="overview" pt="md">
            <div style={{ position: 'relative' }}>
              <LoadingOverlay visible={keysLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
              
              <Stack gap="md">
                {virtualKeys?.map((key) => {
                  const StatusIcon = getStatusIcon(key.status);
                  const RequestsTrendIcon = getTrendIcon(key.trends.requests);
                  const CostTrendIcon = getTrendIcon(key.trends.cost);
                  const LatencyTrendIcon = getTrendIcon(key.trends.latency);
                  
                  return (
                    <Card key={key.keyId} withBorder p="md">
                      <Group justify="space-between" mb="md">
                        <Group gap="md">
                          <ThemeIcon size="lg" color={getStatusColor(key.status)} variant="light">
                            <StatusIcon size={20} />
                          </ThemeIcon>
                          <div>
                            <Text fw={600} size="lg">{key.keyName}</Text>
                            <Group gap="xs">
                              <Badge color={getStatusColor(key.status)} variant="light">
                                {key.status}
                              </Badge>
                              <Code>{key.keyHash}</Code>
                              <Text size="sm" c="dimmed">
                                Created: {new Date(key.createdAt).toLocaleDateString()}
                              </Text>
                            </Group>
                          </div>
                        </Group>
                        
                        <Group gap="xs">
                          <Tooltip label="View usage analytics">
                            <ActionIcon
                              variant="light"
                              onClick={() => openKeyDetails(key, 'usage')}
                            >
                              <IconChartLine size={16} />
                            </ActionIcon>
                          </Tooltip>
                          <Tooltip label="Budget analysis">
                            <ActionIcon
                              variant="light"
                              color="orange"
                              onClick={() => openKeyDetails(key, 'budget')}
                            >
                              <IconCreditCard size={16} />
                            </ActionIcon>
                          </Tooltip>
                          <Tooltip label="Performance metrics">
                            <ActionIcon
                              variant="light"
                              color="purple"
                              onClick={() => openKeyDetails(key, 'performance')}
                            >
                              <IconBolt size={16} />
                            </ActionIcon>
                          </Tooltip>
                          <Tooltip label="Security analysis">
                            <ActionIcon
                              variant="light"
                              color="red"
                              onClick={() => openKeyDetails(key, 'security')}
                            >
                              <IconShield size={16} />
                            </ActionIcon>
                          </Tooltip>
                        </Group>
                      </Group>

                      <SimpleGrid cols={{ base: 2, sm: 4, md: 6 }} spacing="md" mb="md">
                        <div>
                          <Text size="xs" c="dimmed" mb={4}>Total Requests</Text>
                          <Group gap="xs">
                            <Text fw={500}>{formatNumber(key.totalRequests)}</Text>
                            <Group gap={2}>
                              <RequestsTrendIcon 
                                size={12} 
                                color={getTrendColor(key.trends.requests)} 
                              />
                              <Text 
                                size="xs" 
                                c={getTrendColor(key.trends.requests)}
                              >
                                {key.trends.requests > 0 ? '+' : ''}{key.trends.requests.toFixed(1)}%
                              </Text>
                            </Group>
                          </Group>
                        </div>
                        
                        <div>
                          <Text size="xs" c="dimmed" mb={4}>Total Cost</Text>
                          <Group gap="xs">
                            <Text fw={500}>{formatCurrency(key.totalCost)}</Text>
                            <Group gap={2}>
                              <CostTrendIcon 
                                size={12} 
                                color={getTrendColor(key.trends.cost, true)} 
                              />
                              <Text 
                                size="xs" 
                                c={getTrendColor(key.trends.cost, true)}
                              >
                                {key.trends.cost > 0 ? '+' : ''}{key.trends.cost.toFixed(1)}%
                              </Text>
                            </Group>
                          </Group>
                        </div>
                        
                        <div>
                          <Text size="xs" c="dimmed" mb={4}>Avg Latency</Text>
                          <Group gap="xs">
                            <Text fw={500}>{formatLatency(key.averageLatency)}</Text>
                            <Group gap={2}>
                              <LatencyTrendIcon 
                                size={12} 
                                color={getTrendColor(key.trends.latency, true)} 
                              />
                              <Text 
                                size="xs" 
                                c={getTrendColor(key.trends.latency, true)}
                              >
                                {key.trends.latency > 0 ? '+' : ''}{key.trends.latency.toFixed(1)}%
                              </Text>
                            </Group>
                          </Group>
                        </div>
                        
                        <div>
                          <Text size="xs" c="dimmed" mb={4}>Error Rate</Text>
                          <Text fw={500} c={key.errorRate > 2 ? 'red' : 'green'}>
                            {key.errorRate.toFixed(1)}%
                          </Text>
                        </div>
                        
                        <div>
                          <Text size="xs" c="dimmed" mb={4}>Today</Text>
                          <Stack gap={2}>
                            <Text size="sm">{formatNumber(key.requestsToday)} requests</Text>
                            <Text size="sm">{formatCurrency(key.costToday)}</Text>
                          </Stack>
                        </div>
                        
                        <div>
                          <Text size="xs" c="dimmed" mb={4}>Last Used</Text>
                          <Text size="sm">
                            {new Date(key.lastUsed).toLocaleString()}
                          </Text>
                        </div>
                      </SimpleGrid>

                      <Group justify="space-between" align="flex-end">
                        <div style={{ flex: 1 }}>
                          <Group justify="space-between" mb="xs">
                            <Text size="sm">Budget Usage</Text>
                            <Text size="sm">
                              {formatCurrency(key.budget.used)} / {formatCurrency(key.budget.limit)}
                            </Text>
                          </Group>
                          <Progress
                            value={key.budget.percentage}
                            color={
                              key.budget.percentage >= 90 ? 'red' :
                              key.budget.percentage >= 75 ? 'orange' : 'green'
                            }
                            size="lg"
                          />
                        </div>
                        
                        <Group gap="xs" ml="md">
                          <Text size="xs" c="dimmed">Top Models:</Text>
                          {key.models.slice(0, 3).map((model) => (
                            <Badge key={model.name} size="xs" variant="light">
                              {model.name}
                            </Badge>
                          ))}
                          {key.models.length > 3 && (
                            <Badge size="xs" variant="light" color="gray">
                              +{key.models.length - 3}
                            </Badge>
                          )}
                        </Group>
                      </Group>
                    </Card>
                  );
                })}
              </Stack>
            </div>
          </Tabs.Panel>

          <Tabs.Panel value="usage" pt="md">
            <div style={{ position: 'relative' }}>
              {!selectedKey ? (
                <Center py="xl">
                  <Stack align="center" gap="md">
                    <IconChartLine size={48} color="var(--mantine-color-gray-5)" />
                    <Text c="dimmed">Select a virtual key from the overview tab to view usage patterns</Text>
                  </Stack>
                </Center>
              ) : selectedKeyUsage ? (
                <Stack gap="lg">
                  <Group justify="space-between">
                    <div>
                      <Text fw={600} size="lg">{selectedKey.keyName} - Usage Analytics</Text>
                      <Text size="sm" c="dimmed">Detailed usage patterns and metrics</Text>
                    </div>
                    <Button
                      size="sm"
                      variant="light"
                      leftSection={<IconDownload size={14} />}
                      onClick={() => handleExport('usage')}
                    >
                      Export Usage Data
                    </Button>
                  </Group>

                  <SimpleGrid cols={{ base: 1, md: 2 }} spacing="lg">
                    <CostChart
                      data={selectedKeyUsage.usageData}
                      title="Requests Over Time"
                      type="line"
                      valueKey="requests"
                      nameKey="timestamp"
                      timeKey="timestamp"
                    />
                    
                    <CostChart
                      data={selectedKeyUsage.usageData}
                      title="Cost Over Time"
                      type="line"
                      valueKey="cost"
                      nameKey="timestamp"
                      timeKey="timestamp"
                    />

                    <CostChart
                      data={selectedKeyUsage.modelBreakdown}
                      title="Requests by Model"
                      type="pie"
                      valueKey="requests"
                      nameKey="modelName"
                    />

                    <CostChart
                      data={selectedKeyUsage.geographicDistribution}
                      title="Geographic Distribution"
                      type="bar"
                      valueKey="requests"
                      nameKey="region"
                    />
                  </SimpleGrid>
                </Stack>
              ) : (
                <LoadingOverlay visible overlayProps={{ radius: 'sm', blur: 2 }} />
              )}
            </div>
          </Tabs.Panel>

          <Tabs.Panel value="budget" pt="md">
            <div style={{ position: 'relative' }}>
              {!selectedKey ? (
                <Center py="xl">
                  <Stack align="center" gap="md">
                    <IconCreditCard size={48} color="var(--mantine-color-gray-5)" />
                    <Text c="dimmed">Select a virtual key from the overview tab to view budget analysis</Text>
                  </Stack>
                </Center>
              ) : selectedKeyBudget ? (
                <Stack gap="lg">
                  <Group justify="space-between">
                    <div>
                      <Text fw={600} size="lg">{selectedKey.keyName} - Budget Analysis</Text>
                      <Text size="sm" c="dimmed">Spending patterns and budget optimization</Text>
                    </div>
                    <Button
                      size="sm"
                      variant="light"
                      leftSection={<IconDownload size={14} />}
                      onClick={() => handleExport('budget')}
                    >
                      Export Budget Data
                    </Button>
                  </Group>

                  <SimpleGrid cols={{ base: 1, sm: 2, md: 4 }} spacing="lg">
                    <Card withBorder>
                      <Text size="sm" c="dimmed" mb="xs">Budget Limit</Text>
                      <Text fw={600} size="xl">{formatCurrency(selectedKeyBudget.budget.limit)}</Text>
                    </Card>
                    <Card withBorder>
                      <Text size="sm" c="dimmed" mb="xs">Spent</Text>
                      <Text fw={600} size="xl">{formatCurrency(selectedKeyBudget.budget.spent)}</Text>
                    </Card>
                    <Card withBorder>
                      <Text size="sm" c="dimmed" mb="xs">Remaining</Text>
                      <Text fw={600} size="xl">{formatCurrency(selectedKeyBudget.budget.remaining)}</Text>
                    </Card>
                    <Card withBorder>
                      <Text size="sm" c="dimmed" mb="xs">Burn Rate</Text>
                      <Text fw={600} size="xl">{formatCurrency(selectedKeyBudget.budget.burnRate)}/day</Text>
                    </Card>
                  </SimpleGrid>

                  {selectedKeyBudget.alerts.length > 0 && (
                    <Alert icon={<IconAlertCircle size={16} />} color="orange" title="Budget Alerts">
                      <Stack gap="xs">
                        {selectedKeyBudget.alerts.map((alert) => (
                          <Text key={alert.id} size="sm">{alert.message}</Text>
                        ))}
                      </Stack>
                    </Alert>
                  )}

                  <SimpleGrid cols={{ base: 1, md: 2 }} spacing="lg">
                    <CostChart
                      data={selectedKeyBudget.spendHistory}
                      title="Daily Spend History"
                      type="line"
                      valueKey="dailySpend"
                      nameKey="date"
                      timeKey="date"
                    />

                    <CostChart
                      data={selectedKeyBudget.spendByModel}
                      title="Spend by Model"
                      type="pie"
                      valueKey="cost"
                      nameKey="modelName"
                    />
                  </SimpleGrid>

                  {selectedKeyBudget.recommendations.length > 0 && (
                    <Card withBorder>
                      <Card.Section p="md" withBorder>
                        <Text fw={600}>Cost Optimization Recommendations</Text>
                      </Card.Section>
                      <Card.Section p="md">
                        <Stack gap="md">
                          {selectedKeyBudget.recommendations.map((rec, index) => (
                            <Alert
                              key={index}
                              icon={<IconTarget size={16} />}
                              color="blue"
                              variant="light"
                            >
                              <Stack gap="xs">
                                <Group justify="space-between">
                                  <Text fw={500}>{rec.title}</Text>
                                  {rec.potentialSavings && (
                                    <Badge color="green" variant="light">
                                      Save {formatCurrency(rec.potentialSavings)}
                                    </Badge>
                                  )}
                                </Group>
                                <Text size="sm">{rec.description}</Text>
                              </Stack>
                            </Alert>
                          ))}
                        </Stack>
                      </Card.Section>
                    </Card>
                  )}
                </Stack>
              ) : (
                <LoadingOverlay visible overlayProps={{ radius: 'sm', blur: 2 }} />
              )}
            </div>
          </Tabs.Panel>

          <Tabs.Panel value="performance" pt="md">
            <div style={{ position: 'relative' }}>
              {!selectedKey ? (
                <Center py="xl">
                  <Stack align="center" gap="md">
                    <IconBolt size={48} color="var(--mantine-color-gray-5)" />
                    <Text c="dimmed">Select a virtual key from the overview tab to view performance metrics</Text>
                  </Stack>
                </Center>
              ) : selectedKeyPerformance ? (
                <Stack gap="lg">
                  <Group justify="space-between">
                    <div>
                      <Text fw={600} size="lg">{selectedKey.keyName} - Performance Metrics</Text>
                      <Text size="sm" c="dimmed">Latency, throughput, and reliability analysis</Text>
                    </div>
                    <Button
                      size="sm"
                      variant="light"
                      leftSection={<IconDownload size={14} />}
                      onClick={() => handleExport('performance')}
                    >
                      Export Performance Data
                    </Button>
                  </Group>

                  <SimpleGrid cols={{ base: 1, sm: 2, md: 4 }} spacing="lg">
                    <Card withBorder>
                      <Text size="sm" c="dimmed" mb="xs">Avg Latency</Text>
                      <Text fw={600} size="xl">{formatLatency(selectedKeyPerformance.latency.average)}</Text>
                      <Group gap={4} mt={4}>
                        <ThemeIcon
                          size="xs"
                          variant="light"
                          color={getTrendColor(selectedKeyPerformance.latency.trend, true)}
                        >
                          {selectedKeyPerformance.latency.trend > 0 ? 
                            <IconTrendingUp size={12} /> : <IconTrendingDown size={12} />}
                        </ThemeIcon>
                        <Text size="xs" c={getTrendColor(selectedKeyPerformance.latency.trend, true)}>
                          {selectedKeyPerformance.latency.trend > 0 ? '+' : ''}{selectedKeyPerformance.latency.trend.toFixed(1)}%
                        </Text>
                      </Group>
                    </Card>
                    
                    <Card withBorder>
                      <Text size="sm" c="dimmed" mb="xs">Throughput</Text>
                      <Text fw={600} size="xl">{selectedKeyPerformance.throughput.requestsPerSecond.toFixed(1)} RPS</Text>
                      <Group gap={4} mt={4}>
                        <ThemeIcon
                          size="xs"
                          variant="light"
                          color={getTrendColor(selectedKeyPerformance.throughput.trend)}
                        >
                          {selectedKeyPerformance.throughput.trend > 0 ? 
                            <IconTrendingUp size={12} /> : <IconTrendingDown size={12} />}
                        </ThemeIcon>
                        <Text size="xs" c={getTrendColor(selectedKeyPerformance.throughput.trend)}>
                          {selectedKeyPerformance.throughput.trend > 0 ? '+' : ''}{selectedKeyPerformance.throughput.trend.toFixed(1)}%
                        </Text>
                      </Group>
                    </Card>
                    
                    <Card withBorder>
                      <Text size="sm" c="dimmed" mb="xs">Success Rate</Text>
                      <Text fw={600} size="xl">{selectedKeyPerformance.reliability.successRate.toFixed(1)}%</Text>
                    </Card>
                    
                    <Card withBorder>
                      <Text size="sm" c="dimmed" mb="xs">Uptime</Text>
                      <Text fw={600} size="xl">{selectedKeyPerformance.reliability.uptime.toFixed(1)}%</Text>
                    </Card>
                  </SimpleGrid>

                  <SimpleGrid cols={{ base: 1, md: 2 }} spacing="lg">
                    <Card withBorder>
                      <Card.Section p="md" withBorder>
                        <Text fw={600}>Request Quota Usage</Text>
                      </Card.Section>
                      <Card.Section p="md">
                        <Center>
                          <RingProgress
                            size={180}
                            thickness={12}
                            sections={[
                              { 
                                value: selectedKeyPerformance.quotaUsage.requestQuota.percentage, 
                                color: selectedKeyPerformance.quotaUsage.requestQuota.percentage > 80 ? 'red' : 'blue' 
                              }
                            ]}
                            label={
                              <Text size="xs" ta="center">
                                <Text size="lg" fw={700}>
                                  {selectedKeyPerformance.quotaUsage.requestQuota.percentage.toFixed(1)}%
                                </Text>
                                <br />
                                {formatNumber(selectedKeyPerformance.quotaUsage.requestQuota.used)} / {formatNumber(selectedKeyPerformance.quotaUsage.requestQuota.limit)}
                              </Text>
                            }
                          />
                        </Center>
                        <Text size="xs" c="dimmed" ta="center" mt="md">
                          Resets: {new Date(selectedKeyPerformance.quotaUsage.requestQuota.resetsAt).toLocaleString()}
                        </Text>
                      </Card.Section>
                    </Card>

                    <Card withBorder>
                      <Card.Section p="md" withBorder>
                        <Text fw={600}>Token Quota Usage</Text>
                      </Card.Section>
                      <Card.Section p="md">
                        <Center>
                          <RingProgress
                            size={180}
                            thickness={12}
                            sections={[
                              { 
                                value: selectedKeyPerformance.quotaUsage.tokenQuota.percentage, 
                                color: selectedKeyPerformance.quotaUsage.tokenQuota.percentage > 80 ? 'red' : 'green' 
                              }
                            ]}
                            label={
                              <Text size="xs" ta="center">
                                <Text size="lg" fw={700}>
                                  {selectedKeyPerformance.quotaUsage.tokenQuota.percentage.toFixed(1)}%
                                </Text>
                                <br />
                                {formatNumber(selectedKeyPerformance.quotaUsage.tokenQuota.used)} / {formatNumber(selectedKeyPerformance.quotaUsage.tokenQuota.limit)}
                              </Text>
                            }
                          />
                        </Center>
                        <Text size="xs" c="dimmed" ta="center" mt="md">
                          Resets: {new Date(selectedKeyPerformance.quotaUsage.tokenQuota.resetsAt).toLocaleString()}
                        </Text>
                      </Card.Section>
                    </Card>
                  </SimpleGrid>

                  <CostChart
                    data={selectedKeyPerformance.performanceHistory}
                    title="Performance Over Time"
                    type="line"
                    valueKey="avgLatency"
                    nameKey="timestamp"
                    timeKey="timestamp"
                  />
                </Stack>
              ) : (
                <LoadingOverlay visible overlayProps={{ radius: 'sm', blur: 2 }} />
              )}
            </div>
          </Tabs.Panel>

          <Tabs.Panel value="security" pt="md">
            <div style={{ position: 'relative' }}>
              {!selectedKey ? (
                <Center py="xl">
                  <Stack align="center" gap="md">
                    <IconShield size={48} color="var(--mantine-color-gray-5)" />
                    <Text c="dimmed">Select a virtual key from the overview tab to view security analysis</Text>
                  </Stack>
                </Center>
              ) : selectedKeySecurity ? (
                <Stack gap="lg">
                  <Group justify="space-between">
                    <div>
                      <Text fw={600} size="lg">{selectedKey.keyName} - Security Analysis</Text>
                      <Text size="sm" c="dimmed">Access patterns, security metrics, and compliance status</Text>
                    </div>
                    <Button
                      size="sm"
                      variant="light"
                      leftSection={<IconDownload size={14} />}
                      onClick={() => handleExport('security')}
                    >
                      Export Security Data
                    </Button>
                  </Group>

                  {selectedKeySecurity.accessPatterns.suspiciousActivity.length > 0 && (
                    <Alert icon={<IconAlertCircle size={16} />} color="red" title="Security Alerts">
                      <Stack gap="xs">
                        {selectedKeySecurity.accessPatterns.suspiciousActivity.map((activity) => (
                          <Group key={activity.id} justify="space-between">
                            <Text size="sm">{activity.description}</Text>
                            <Badge color="red" variant="light">
                              {activity.severity}
                            </Badge>
                          </Group>
                        ))}
                      </Stack>
                    </Alert>
                  )}

                  <SimpleGrid cols={{ base: 1, sm: 2, md: 4 }} spacing="lg">
                    <Card withBorder>
                      <Text size="sm" c="dimmed" mb="xs">Unique IPs</Text>
                      <Text fw={600} size="xl">{selectedKeySecurity.accessPatterns.uniqueIPs}</Text>
                    </Card>
                    
                    <Card withBorder>
                      <Text size="sm" c="dimmed" mb="xs">Valid Requests</Text>
                      <Text fw={600} size="xl">{formatNumber(selectedKeySecurity.authentication.validRequests)}</Text>
                    </Card>
                    
                    <Card withBorder>
                      <Text size="sm" c="dimmed" mb="xs">Rate Limit Violations</Text>
                      <Text fw={600} size="xl" c={selectedKeySecurity.rateLimiting.violations > 0 ? 'red' : 'green'}>
                        {selectedKeySecurity.rateLimiting.violations}
                      </Text>
                    </Card>
                    
                    <Card withBorder>
                      <Text size="sm" c="dimmed" mb="xs">Invalid Attempts</Text>
                      <Text fw={600} size="xl" c={selectedKeySecurity.authentication.invalidRequests > 0 ? 'red' : 'green'}>
                        {selectedKeySecurity.authentication.invalidRequests}
                      </Text>
                    </Card>
                  </SimpleGrid>

                  <Card withBorder>
                    <Card.Section p="md" withBorder>
                      <Text fw={600}>Access by IP Address</Text>
                    </Card.Section>
                    <Card.Section>
                      <Table>
                        <Table.Thead>
                          <Table.Tr>
                            <Table.Th>IP Address</Table.Th>
                            <Table.Th>Country</Table.Th>
                            <Table.Th>Requests</Table.Th>
                            <Table.Th>Last Seen</Table.Th>
                            <Table.Th>Status</Table.Th>
                          </Table.Tr>
                        </Table.Thead>
                        <Table.Tbody>
                          {selectedKeySecurity.accessPatterns.requestsByIP.map((ip) => (
                            <Table.Tr key={ip.ip}>
                              <Table.Td>
                                <Code>{ip.ip}</Code>
                              </Table.Td>
                              <Table.Td>{ip.country || 'Unknown'}</Table.Td>
                              <Table.Td>{formatNumber(ip.requests)}</Table.Td>
                              <Table.Td>
                                <Text size="sm" c="dimmed">
                                  {new Date(ip.lastSeen).toLocaleString()}
                                </Text>
                              </Table.Td>
                              <Table.Td>
                                <Badge color={ip.flagged ? 'red' : 'green'} variant="light">
                                  {ip.flagged ? 'Flagged' : 'Normal'}
                                </Badge>
                              </Table.Td>
                            </Table.Tr>
                          ))}
                        </Table.Tbody>
                      </Table>
                    </Card.Section>
                  </Card>

                  <Card withBorder>
                    <Card.Section p="md" withBorder>
                      <Text fw={600}>Compliance Status</Text>
                    </Card.Section>
                    <Card.Section p="md">
                      <SimpleGrid cols={{ base: 1, sm: 2 }} spacing="lg">
                        <div>
                          <Text size="sm" c="dimmed" mb="xs">Data Regions</Text>
                          <Group gap="xs">
                            {selectedKeySecurity.compliance.dataRegions.map((region) => (
                              <Badge key={region} variant="light">
                                {region}
                              </Badge>
                            ))}
                          </Group>
                        </div>
                        
                        <div>
                          <Text size="sm" c="dimmed" mb="xs">Retention Policy</Text>
                          <Text fw={500}>{selectedKeySecurity.compliance.retentionPolicy}</Text>
                        </div>
                        
                        <div>
                          <Text size="sm" c="dimmed" mb="xs">Encryption</Text>
                          <Badge 
                            color={selectedKeySecurity.compliance.encryptionStatus === 'enabled' ? 'green' : 'red'} 
                            variant="light"
                          >
                            {selectedKeySecurity.compliance.encryptionStatus}
                          </Badge>
                        </div>
                        
                        <div>
                          <Text size="sm" c="dimmed" mb="xs">Audit Logs</Text>
                          <Badge 
                            color={selectedKeySecurity.compliance.auditLogsEnabled ? 'green' : 'red'} 
                            variant="light"
                          >
                            {selectedKeySecurity.compliance.auditLogsEnabled ? 'Enabled' : 'Disabled'}
                          </Badge>
                        </div>
                      </SimpleGrid>
                    </Card.Section>
                  </Card>
                </Stack>
              ) : (
                <LoadingOverlay visible overlayProps={{ radius: 'sm', blur: 2 }} />
              )}
            </div>
          </Tabs.Panel>

          <Tabs.Panel value="leaderboard" pt="md">
            <div style={{ position: 'relative' }}>
              <LoadingOverlay visible={!leaderboard} overlayProps={{ radius: 'sm', blur: 2 }} />
              
              {leaderboard && (
                <Stack gap="lg">
                  <Group justify="space-between">
                    <div>
                      <Text fw={600} size="lg">Virtual Keys Leaderboard</Text>
                      <Text size="sm" c="dimmed">Top performing virtual keys across different metrics</Text>
                    </div>
                    <Button
                      size="sm"
                      variant="light"
                      leftSection={<IconDownload size={14} />}
                      onClick={() => handleExport('leaderboard')}
                    >
                      Export Leaderboard
                    </Button>
                  </Group>

                  <SimpleGrid cols={{ base: 1, md: 2, lg: 3 }} spacing="lg">
                    <Card withBorder>
                      <Card.Section p="md" withBorder>
                        <Group gap="xs">
                          <IconTrophy size={20} color="var(--mantine-color-yellow-6)" />
                          <Text fw={600}>Top by Requests</Text>
                        </Group>
                      </Card.Section>
                      <Card.Section p="md">
                        <Stack gap="sm">
                          {leaderboard.categories.topByRequests.map((key, index) => (
                            <Group key={key.keyId} justify="space-between">
                              <Group gap="xs">
                                <Text fw={500} c={index === 0 ? 'yellow' : index === 1 ? 'gray' : index === 2 ? 'orange' : undefined}>
                                  #{index + 1}
                                </Text>
                                <Text size="sm">{key.keyName}</Text>
                              </Group>
                              <Stack gap={0} align="flex-end">
                                <Text size="sm" fw={500}>{formatNumber(key.requests)}</Text>
                                <Text 
                                  size="xs" 
                                  c={getTrendColor(key.change)}
                                >
                                  {key.change > 0 ? '+' : ''}{key.change.toFixed(1)}%
                                </Text>
                              </Stack>
                            </Group>
                          ))}
                        </Stack>
                      </Card.Section>
                    </Card>

                    <Card withBorder>
                      <Card.Section p="md" withBorder>
                        <Group gap="xs">
                          <IconCreditCard size={20} color="var(--mantine-color-green-6)" />
                          <Text fw={600}>Top by Cost</Text>
                        </Group>
                      </Card.Section>
                      <Card.Section p="md">
                        <Stack gap="sm">
                          {leaderboard.categories.topByCost.map((key, index) => (
                            <Group key={key.keyId} justify="space-between">
                              <Group gap="xs">
                                <Text fw={500} c={index === 0 ? 'yellow' : index === 1 ? 'gray' : index === 2 ? 'orange' : undefined}>
                                  #{index + 1}
                                </Text>
                                <Text size="sm">{key.keyName}</Text>
                              </Group>
                              <Stack gap={0} align="flex-end">
                                <Text size="sm" fw={500}>{formatCurrency(key.cost)}</Text>
                                <Text 
                                  size="xs" 
                                  c={getTrendColor(key.change, true)}
                                >
                                  {key.change > 0 ? '+' : ''}{key.change.toFixed(1)}%
                                </Text>
                              </Stack>
                            </Group>
                          ))}
                        </Stack>
                      </Card.Section>
                    </Card>

                    <Card withBorder>
                      <Card.Section p="md" withBorder>
                        <Group gap="xs">
                          <IconFlame size={20} color="var(--mantine-color-red-6)" />
                          <Text fw={600}>Most Efficient</Text>
                        </Group>
                      </Card.Section>
                      <Card.Section p="md">
                        <Stack gap="sm">
                          {leaderboard.categories.mostEfficient.map((key, index) => (
                            <Group key={key.keyId} justify="space-between">
                              <Group gap="xs">
                                <Text fw={500} c={index === 0 ? 'yellow' : index === 1 ? 'gray' : index === 2 ? 'orange' : undefined}>
                                  #{index + 1}
                                </Text>
                                <Text size="sm">{key.keyName}</Text>
                              </Group>
                              <Stack gap={0} align="flex-end">
                                <Text size="sm" fw={500}>{formatCurrency(key.costPerRequest)}</Text>
                                <Text size="xs" c="blue">
                                  {key.efficiency.toFixed(1)}% efficient
                                </Text>
                              </Stack>
                            </Group>
                          ))}
                        </Stack>
                      </Card.Section>
                    </Card>

                    <Card withBorder>
                      <Card.Section p="md" withBorder>
                        <Group gap="xs">
                          <IconBolt size={20} color="var(--mantine-color-purple-6)" />
                          <Text fw={600}>Fastest Response</Text>
                        </Group>
                      </Card.Section>
                      <Card.Section p="md">
                        <Stack gap="sm">
                          {leaderboard.categories.fastestResponse.map((key, index) => (
                            <Group key={key.keyId} justify="space-between">
                              <Group gap="xs">
                                <Text fw={500} c={index === 0 ? 'yellow' : index === 1 ? 'gray' : index === 2 ? 'orange' : undefined}>
                                  #{index + 1}
                                </Text>
                                <Text size="sm">{key.keyName}</Text>
                              </Group>
                              <Stack gap={0} align="flex-end">
                                <Text size="sm" fw={500}>{formatLatency(key.avgLatency)}</Text>
                                <Text 
                                  size="xs" 
                                  c={getTrendColor(key.improvement, true)}
                                >
                                  {key.improvement > 0 ? '+' : ''}{key.improvement.toFixed(1)}%
                                </Text>
                              </Stack>
                            </Group>
                          ))}
                        </Stack>
                      </Card.Section>
                    </Card>

                    <Card withBorder>
                      <Card.Section p="md" withBorder>
                        <Group gap="xs">
                          <IconHeartHandshake size={20} color="var(--mantine-color-teal-6)" />
                          <Text fw={600}>Most Reliable</Text>
                        </Group>
                      </Card.Section>
                      <Card.Section p="md">
                        <Stack gap="sm">
                          {leaderboard.categories.mostReliable.map((key, index) => (
                            <Group key={key.keyId} justify="space-between">
                              <Group gap="xs">
                                <Text fw={500} c={index === 0 ? 'yellow' : index === 1 ? 'gray' : index === 2 ? 'orange' : undefined}>
                                  #{index + 1}
                                </Text>
                                <Text size="sm">{key.keyName}</Text>
                              </Group>
                              <Stack gap={0} align="flex-end">
                                <Text size="sm" fw={500}>{key.successRate.toFixed(1)}%</Text>
                                <Text size="xs" c="teal">
                                  {key.uptime.toFixed(1)}% uptime
                                </Text>
                              </Stack>
                            </Group>
                          ))}
                        </Stack>
                      </Card.Section>
                    </Card>

                    <Card withBorder>
                      <Card.Section p="md" withBorder>
                        <Group gap="xs">
                          <IconChartLine size={20} color="var(--mantine-color-blue-6)" />
                          <Text fw={600}>Top by Tokens</Text>
                        </Group>
                      </Card.Section>
                      <Card.Section p="md">
                        <Stack gap="sm">
                          {leaderboard.categories.topByTokens.map((key, index) => (
                            <Group key={key.keyId} justify="space-between">
                              <Group gap="xs">
                                <Text fw={500} c={index === 0 ? 'yellow' : index === 1 ? 'gray' : index === 2 ? 'orange' : undefined}>
                                  #{index + 1}
                                </Text>
                                <Text size="sm">{key.keyName}</Text>
                              </Group>
                              <Stack gap={0} align="flex-end">
                                <Text size="sm" fw={500}>{formatNumber(key.tokens)}</Text>
                                <Text 
                                  size="xs" 
                                  c={getTrendColor(key.change)}
                                >
                                  {key.change > 0 ? '+' : ''}{key.change.toFixed(1)}%
                                </Text>
                              </Stack>
                            </Group>
                          ))}
                        </Stack>
                      </Card.Section>
                    </Card>
                  </SimpleGrid>
                </Stack>
              )}
            </div>
          </Tabs.Panel>
        </Tabs>
      </Card>

      {/* Key Details Modal */}
      <Modal
        opened={detailsOpened}
        onClose={closeDetails}
        title="Usage Analytics"
        size="xl"
      >
        {selectedKey && selectedKeyUsage && (
          <Stack gap="md">
            <Group justify="space-between">
              <div>
                <Text fw={600} size="lg">{selectedKey.keyName}</Text>
                <Code>{selectedKey.keyHash}</Code>
              </div>
              <Badge color={getStatusColor(selectedKey.status)} variant="light">
                {selectedKey.status}
              </Badge>
            </Group>
            
            <SimpleGrid cols={2} spacing="lg">
              <CostChart
                data={selectedKeyUsage.usageData}
                title="Requests Over Time"
                type="line"
                valueKey="requests"
                nameKey="timestamp"
                timeKey="timestamp"
              />
              
              <CostChart
                data={selectedKeyUsage.modelBreakdown}
                title="Model Usage"
                type="pie"
                valueKey="requests"
                nameKey="modelName"
              />
            </SimpleGrid>
          </Stack>
        )}
      </Modal>

      {/* Budget Modal */}
      <Modal
        opened={budgetOpened}
        onClose={closeBudget}
        title="Budget Analysis"
        size="xl"
      >
        {selectedKey && selectedKeyBudget && (
          <Stack gap="md">
            <Group justify="space-between">
              <div>
                <Text fw={600} size="lg">{selectedKey.keyName}</Text>
                <Code>{selectedKey.keyHash}</Code>
              </div>
              <Badge 
                color={selectedKeyBudget.budget.spent / selectedKeyBudget.budget.limit > 0.9 ? 'red' : 'green'} 
                variant="light"
              >
                {((selectedKeyBudget.budget.spent / selectedKeyBudget.budget.limit) * 100).toFixed(1)}% used
              </Badge>
            </Group>
            
            <SimpleGrid cols={2} spacing="lg">
              <CostChart
                data={selectedKeyBudget.spendHistory}
                title="Daily Spend"
                type="line"
                valueKey="dailySpend"
                nameKey="date"
                timeKey="date"
              />
              
              <CostChart
                data={selectedKeyBudget.spendByModel}
                title="Spend by Model"
                type="pie"
                valueKey="cost"
                nameKey="modelName"
              />
            </SimpleGrid>
          </Stack>
        )}
      </Modal>

      {/* Performance Modal */}
      <Modal
        opened={performanceOpened}
        onClose={closePerformance}
        title="Performance Metrics"
        size="xl"
      >
        {selectedKey && selectedKeyPerformance && (
          <Stack gap="md">
            <Group justify="space-between">
              <div>
                <Text fw={600} size="lg">{selectedKey.keyName}</Text>
                <Code>{selectedKey.keyHash}</Code>
              </div>
              <Badge color="purple" variant="light">
                {selectedKeyPerformance.reliability.successRate.toFixed(1)}% success rate
              </Badge>
            </Group>
            
            <SimpleGrid cols={2} spacing="lg">
              <Card withBorder>
                <Text size="sm" c="dimmed" mb="xs">Request Quota</Text>
                <Center>
                  <RingProgress
                    size={120}
                    thickness={8}
                    sections={[
                      { 
                        value: selectedKeyPerformance.quotaUsage.requestQuota.percentage, 
                        color: selectedKeyPerformance.quotaUsage.requestQuota.percentage > 80 ? 'red' : 'blue' 
                      }
                    ]}
                    label={
                      <Text size="xs" ta="center">
                        {selectedKeyPerformance.quotaUsage.requestQuota.percentage.toFixed(1)}%
                      </Text>
                    }
                  />
                </Center>
              </Card>
              
              <Card withBorder>
                <Text size="sm" c="dimmed" mb="xs">Token Quota</Text>
                <Center>
                  <RingProgress
                    size={120}
                    thickness={8}
                    sections={[
                      { 
                        value: selectedKeyPerformance.quotaUsage.tokenQuota.percentage, 
                        color: selectedKeyPerformance.quotaUsage.tokenQuota.percentage > 80 ? 'red' : 'green' 
                      }
                    ]}
                    label={
                      <Text size="xs" ta="center">
                        {selectedKeyPerformance.quotaUsage.tokenQuota.percentage.toFixed(1)}%
                      </Text>
                    }
                  />
                </Center>
              </Card>
            </SimpleGrid>
          </Stack>
        )}
      </Modal>

      {/* Security Modal */}
      <Modal
        opened={securityOpened}
        onClose={closeSecurity}
        title="Security Analysis"
        size="xl"
      >
        {selectedKey && selectedKeySecurity && (
          <Stack gap="md">
            <Group justify="space-between">
              <div>
                <Text fw={600} size="lg">{selectedKey.keyName}</Text>
                <Code>{selectedKey.keyHash}</Code>
              </div>
              <Badge 
                color={selectedKeySecurity.accessPatterns.suspiciousActivity.length > 0 ? 'red' : 'green'} 
                variant="light"
              >
                {selectedKeySecurity.accessPatterns.suspiciousActivity.length} alerts
              </Badge>
            </Group>
            
            <SimpleGrid cols={2} spacing="lg">
              <Card withBorder>
                <Text size="sm" c="dimmed" mb="xs">Unique IPs</Text>
                <Text fw={600} size="xl">{selectedKeySecurity.accessPatterns.uniqueIPs}</Text>
              </Card>
              
              <Card withBorder>
                <Text size="sm" c="dimmed" mb="xs">Rate Limit Violations</Text>
                <Text fw={600} size="xl" c={selectedKeySecurity.rateLimiting.violations > 0 ? 'red' : 'green'}>
                  {selectedKeySecurity.rateLimiting.violations}
                </Text>
              </Card>
            </SimpleGrid>
          </Stack>
        )}
      </Modal>
    </Stack>
  );
}