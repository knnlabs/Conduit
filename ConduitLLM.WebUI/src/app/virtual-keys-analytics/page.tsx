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
  IconBan,
  IconCheck,
  IconX,
  IconTarget,
  IconBolt,
  IconTrophy,
  IconFlame,
  IconHeartHandshake,
} from '@tabler/icons-react';
import { useState } from 'react';
import { useDisclosure } from '@mantine/hooks';
import { 
  useVirtualKeys,
  useKeyUsage,
  useCostByKey,
  useExportCostAnalytics
} from '@/hooks/useConduitAdmin';
import type { DateRange, VirtualKeyDto } from '@knn_labs/conduit-admin-client';
import { notifications } from '@mantine/notifications';
import { CostChart } from '@/components/charts/CostChart';

export default function VirtualKeysAnalyticsPage() {
  const [timeRangeValue, setTimeRangeValue] = useState('24h');
  const [selectedTab, setSelectedTab] = useState('overview');
  const [selectedKey, setSelectedKey] = useState<VirtualKeyDto | null>(null);
  const [detailsOpened, { open: openDetails, close: closeDetails }] = useDisclosure(false);
  const [budgetOpened, { open: openBudget, close: closeBudget }] = useDisclosure(false);
  const [performanceOpened, { open: openPerformance, close: closePerformance }] = useDisclosure(false);
  const [securityOpened, { open: openSecurity, close: closeSecurity }] = useDisclosure(false);
  
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
  
  const { data: virtualKeys, isLoading: keysLoading } = useVirtualKeys();
  const { data: selectedKeyUsage } = useKeyUsage(selectedKey?.id || 0, dateRange);
  const { data: costByKey } = useCostByKey(dateRange);
  const exportData = useExportCostAnalytics();

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
        type: 'cost',
        filters: {
          startDate: dateRange.startDate,
          endDate: dateRange.endDate,
          virtualKeyIds: selectedKey ? [selectedKey.id] : undefined
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

  const getStatusColor = (isEnabled: boolean) => {
    return isEnabled ? 'green' : 'red';
  };

  const getStatusIcon = (isEnabled: boolean) => {
    return isEnabled ? IconCheck : IconBan;
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

  const openKeyDetails = (key: VirtualKeyDto, tab: 'usage' | 'budget' | 'performance' | 'security' = 'usage') => {
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
  const activeKeys = virtualKeys?.filter((k: VirtualKeyDto) => k.isEnabled).length || 0;
  const totalRequests = costByKey?.costByKey?.reduce((sum: number, k: any) => sum + k.requestCount, 0) || 0;
  const totalCost = costByKey?.costByKey?.reduce((sum: number, k: any) => sum + k.cost, 0) || 0;
  const averageLatency = 0; // Not available in SDK
  const averageErrorRate = 0; // Not available in SDK

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
                {virtualKeys?.map((key: VirtualKeyDto) => {
                  const keyStats = costByKey?.costByKey?.find((k: any) => k.keyId === key.id);
                  const StatusIcon = getStatusIcon(key.isEnabled);
                  
                  return (
                    <Card key={key.id} withBorder p="md">
                      <Group justify="space-between" mb="md">
                        <Group gap="md">
                          <ThemeIcon size="lg" color={getStatusColor(key.isEnabled)} variant="light">
                            <StatusIcon size={20} />
                          </ThemeIcon>
                          <div>
                            <Text fw={600} size="lg">{key.keyName}</Text>
                            <Group gap="xs">
                              <Badge color={getStatusColor(key.isEnabled)} variant="light">
                                {key.isEnabled ? 'active' : 'inactive'}
                              </Badge>
                              <Code>{key.keyPrefix || 'N/A'}...</Code>
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
              ) : selectedKeyUsage ? (
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
                      <Text fw={600} size="xl">{formatCurrency(selectedKeyUsage.budget.limit)}</Text>
                    </Card>
                    <Card withBorder>
                      <Text size="sm" c="dimmed" mb="xs">Spent</Text>
                      <Text fw={600} size="xl">{formatCurrency(selectedKeyUsage.budget.spent)}</Text>
                    </Card>
                    <Card withBorder>
                      <Text size="sm" c="dimmed" mb="xs">Remaining</Text>
                      <Text fw={600} size="xl">{formatCurrency(selectedKeyUsage.budget.remaining)}</Text>
                    </Card>
                    <Card withBorder>
                      <Text size="sm" c="dimmed" mb="xs">Burn Rate</Text>
                      <Text fw={600} size="xl">{formatCurrency(selectedKeyUsage.budget.burnRate)}/day</Text>
                    </Card>
                  </SimpleGrid>

                  {selectedKeyUsage.alerts.length > 0 && (
                    <Alert icon={<IconAlertCircle size={16} />} color="orange" title="Budget Alerts">
                      <Stack gap="xs">
                        {selectedKeyUsage.alerts.map((alert: any) => (
                          <Text key={alert.id} size="sm">{alert.message}</Text>
                        ))}
                      </Stack>
                    </Alert>
                  )}

                  <SimpleGrid cols={{ base: 1, md: 2 }} spacing="lg">
                    <CostChart
                      data={selectedKeyUsage.spendHistory}
                      title="Daily Spend History"
                      type="line"
                      valueKey="dailySpend"
                      nameKey="date"
                      timeKey="date"
                    />

                    <CostChart
                      data={selectedKeyUsage.spendByModel}
                      title="Spend by Model"
                      type="pie"
                      valueKey="cost"
                      nameKey="modelName"
                    />
                  </SimpleGrid>

                  {selectedKeyUsage.recommendations.length > 0 && (
                    <Card withBorder>
                      <Card.Section p="md" withBorder>
                        <Text fw={600}>Cost Optimization Recommendations</Text>
                      </Card.Section>
                      <Card.Section p="md">
                        <Stack gap="md">
                          {selectedKeyUsage.recommendations.map((rec: any, index: number) => (
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
              ) : (
                <Center py="xl">
                  <Stack align="center" gap="md">
                    <IconBolt size={48} color="var(--mantine-color-gray-5)" />
                    <Text c="dimmed">Performance metrics not available in SDK</Text>
                  </Stack>
                </Center>
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
              ) : (
                <Center py="xl">
                  <Stack align="center" gap="md">
                    <IconShield size={48} color="var(--mantine-color-gray-5)" />
                    <Text c="dimmed">Security metrics not available in SDK</Text>
                  </Stack>
                </Center>
              )}
            </div>
          </Tabs.Panel>

          <Tabs.Panel value="leaderboard" pt="md">
            <div style={{ position: 'relative' }}>
              <LoadingOverlay visible={!costByKey} overlayProps={{ radius: 'sm', blur: 2 }} />
              
              {costByKey && (
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

                  <SimpleGrid cols={{ base: 1, md: 2 }} spacing="lg">
                    <Card withBorder>
                      <Card.Section p="md" withBorder>
                        <Group gap="xs">
                          <IconTrophy size={20} color="var(--mantine-color-yellow-6)" />
                          <Text fw={600}>Top by Cost</Text>
                        </Group>
                      </Card.Section>
                      <Card.Section p="md">
                        <Stack gap="sm">
                          {costByKey.costByKey
                            ?.sort((a: any, b: any) => b.cost - a.cost)
                            ?.slice(0, 5)
                            ?.map((key: any, index: number) => (
                              <Group key={key.keyId} justify="space-between">
                                <Group gap="xs">
                                  <Text fw={500} c={index === 0 ? 'yellow' : index === 1 ? 'gray' : index === 2 ? 'orange' : undefined}>
                                    #{index + 1}
                                  </Text>
                                  <Text size="sm">{key.keyName}</Text>
                                </Group>
                                <Stack gap={0} align="flex-end">
                                  <Text size="sm" fw={500}>{formatCurrency(key.cost)}</Text>
                                  <Text size="xs" c="dimmed">
                                    {formatNumber(key.requestCount)} requests
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
                          <IconActivity size={20} color="var(--mantine-color-blue-6)" />
                          <Text fw={600}>Top by Requests</Text>
                        </Group>
                      </Card.Section>
                      <Card.Section p="md">
                        <Stack gap="sm">
                          {costByKey.costByKey
                            ?.sort((a: any, b: any) => b.requestCount - a.requestCount)
                            ?.slice(0, 5)
                            ?.map((key: any, index: number) => (
                              <Group key={key.keyId} justify="space-between">
                                <Group gap="xs">
                                  <Text fw={500} c={index === 0 ? 'yellow' : index === 1 ? 'gray' : index === 2 ? 'orange' : undefined}>
                                    #{index + 1}
                                  </Text>
                                  <Text size="sm">{key.keyName}</Text>
                                </Group>
                                <Stack gap={0} align="flex-end">
                                  <Text size="sm" fw={500}>{formatNumber(key.requestCount)}</Text>
                                  <Text size="xs" c="dimmed">
                                    {formatCurrency(key.cost)}
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
                <Code>{selectedKey.keyPrefix || 'N/A'}...</Code>
              </div>
              <Badge color={getStatusColor(selectedKey.isEnabled)} variant="light">
                {selectedKey.isEnabled ? 'Active' : 'Inactive'}
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
        {selectedKey && selectedKeyUsage && (
          <Stack gap="md">
            <Group justify="space-between">
              <div>
                <Text fw={600} size="lg">{selectedKey.keyName}</Text>
                <Code>{selectedKey.keyPrefix || 'N/A'}...</Code>
              </div>
              <Badge 
                color={selectedKeyUsage.budgetUsed > 90 ? 'red' : 'green'} 
                variant="light"
              >
                {selectedKeyUsage.budgetUsed.toFixed(1)}% used
              </Badge>
            </Group>
            
            <SimpleGrid cols={2} spacing="lg">
              <Card withBorder>
                <Text size="sm" c="dimmed" mb="xs">Budget Remaining</Text>
                <Text fw={600} size="xl">{formatCurrency(selectedKeyUsage.budgetRemaining)}</Text>
              </Card>
              
              <Card withBorder>
                <Text size="sm" c="dimmed" mb="xs">Average Cost per Request</Text>
                <Text fw={600} size="xl">{formatCurrency(selectedKeyUsage.averageCostPerRequest)}</Text>
              </Card>
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
        {selectedKey && (
          <Stack gap="md">
            <Group justify="space-between">
              <div>
                <Text fw={600} size="lg">{selectedKey.keyName}</Text>
                <Code>{selectedKey.keyPrefix || 'N/A'}...</Code>
              </div>
              <Badge color="purple" variant="light">
                Performance metrics not available
              </Badge>
            </Group>
            
            <Center py="xl">
              <Stack align="center" gap="md">
                <IconBolt size={48} color="var(--mantine-color-gray-5)" />
                <Text c="dimmed">Performance metrics not available in SDK</Text>
              </Stack>
            </Center>
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
        {selectedKey && (
          <Stack gap="md">
            <Group justify="space-between">
              <div>
                <Text fw={600} size="lg">{selectedKey.keyName}</Text>
                <Code>{selectedKey.keyPrefix || 'N/A'}...</Code>
              </div>
              <Badge 
                color="gray" 
                variant="light"
              >
                Security metrics not available
              </Badge>
            </Group>
            
            <Center py="xl">
              <Stack align="center" gap="md">
                <IconShield size={48} color="var(--mantine-color-gray-5)" />
                <Text c="dimmed">Security metrics not available in SDK</Text>
              </Stack>
            </Center>
          </Stack>
        )}
      </Modal>
    </Stack>
  );
}