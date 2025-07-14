'use client';

import {
  Stack,
  Title,
  Text,
  Card,
  Group,
  Button,
  Badge,
  ThemeIcon,
  Paper,
  SimpleGrid,
  Progress,
  Switch,
  NumberInput,
  Table,
  ScrollArea,
  LoadingOverlay,
  Alert,
  Timeline,
  ActionIcon,
  Tooltip,
  Code,
  Tabs,
  RingProgress,
  Center,
} from '@mantine/core';
import {
  IconAlertCircle,
  IconCircleCheck,
  IconBell,
  IconBellOff,
  IconRefresh,
  IconSettings,
  IconActivity,
  IconHistory,
  IconHeartRateMonitor,
  IconClock,
  IconDatabase,
  IconServer,
  IconBolt,
  IconAlertTriangle,
  IconInfoCircle,
  IconX,
  IconTrash,
} from '@tabler/icons-react';
import { useState, useEffect, useCallback } from 'react';
import { notifications } from '@mantine/notifications';
import { formatters } from '@/lib/utils/formatters';

interface MonitoringStatus {
  lastCheck: string;
  isHealthy: boolean;
  currentHitRate: number;
  currentMemoryUsagePercent: number;
  currentEvictionRate: number;
  currentResponseTimeMs: number;
  activeAlerts: number;
  details: Record<string, any>;
}

interface AlertThresholds {
  minHitRate: number;
  maxMemoryUsage: number;
  maxEvictionRate: number;
  maxResponseTimeMs: number;
  minRequestsForHitRateAlert: number;
}

interface CacheAlert {
  alertType: string;
  message: string;
  severity: 'info' | 'warning' | 'error' | 'critical';
  region?: string;
  details?: Record<string, any>;
  timestamp: string;
}

interface AlertDefinition {
  type: string;
  name: string;
  defaultSeverity: string;
  description: string;
  recommendedActions: string[];
  notificationEnabled: boolean;
  cooldownPeriodMinutes: number;
}

interface HealthSummary {
  overallHealth: string;
  hitRate: number;
  memoryUsagePercent: number;
  responseTimeMs: number;
  evictionRate: number;
  activeAlerts: number;
  totalCacheSize: number;
  totalEntries: number;
  lastCheck: string;
  recentAlerts: CacheAlert[];
}

export default function CacheMonitoringPage() {
  const [isLoading, setIsLoading] = useState(true);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [activeTab, setActiveTab] = useState<string | null>('overview');
  const [status, setStatus] = useState<MonitoringStatus | null>(null);
  const [thresholds, setThresholds] = useState<AlertThresholds | null>(null);
  const [recentAlerts, setRecentAlerts] = useState<CacheAlert[]>([]);
  const [alertDefinitions, setAlertDefinitions] = useState<AlertDefinition[]>([]);
  const [healthSummary, setHealthSummary] = useState<HealthSummary | null>(null);
  const [notificationsEnabled, setNotificationsEnabled] = useState(true);
  const [lastAlertCount, setLastAlertCount] = useState(0);

  // Poll for new alerts
  useEffect(() => {
    if (!notificationsEnabled) return;

    const checkForNewAlerts = () => {
      if (recentAlerts.length > lastAlertCount && lastAlertCount > 0) {
        // New alert detected
        const newAlert = recentAlerts[0];
        const color = newAlert.severity === 'critical' ? 'red' : 
                     newAlert.severity === 'error' ? 'orange' :
                     newAlert.severity === 'warning' ? 'yellow' : 'blue';
        
        notifications.show({
          title: `Cache Alert: ${newAlert.alertType}`,
          message: newAlert.message,
          color,
          icon: <IconAlertCircle size={16} />,
        });
      }
      setLastAlertCount(recentAlerts.length);
    };

    checkForNewAlerts();
  }, [recentAlerts, notificationsEnabled, lastAlertCount]);

  useEffect(() => {
    fetchMonitoringData();
    // Refresh every 30 seconds
    const interval = setInterval(fetchMonitoringData, 30000);
    return () => clearInterval(interval);
  }, []);

  const fetchMonitoringData = async () => {
    try {
      const [statusRes, thresholdsRes, alertsRes, definitionsRes, healthRes] = await Promise.all([
        fetch('/api/cache/monitoring/status'),
        fetch('/api/cache/monitoring/thresholds'),
        fetch('/api/cache/monitoring/alerts?count=20'),
        fetch('/api/cache/monitoring/alert-definitions'),
        fetch('/api/cache/monitoring/health')
      ]);

      if (!statusRes.ok || !thresholdsRes.ok || !alertsRes.ok || !definitionsRes.ok || !healthRes.ok) {
        throw new Error('Failed to fetch monitoring data');
      }

      const [statusData, thresholdsData, alertsData, definitionsData, healthData] = await Promise.all([
        statusRes.json(),
        thresholdsRes.json(),
        alertsRes.json(),
        definitionsRes.json(),
        healthRes.json()
      ]);

      setStatus(statusData);
      setThresholds(thresholdsData);
      setRecentAlerts(alertsData);
      setAlertDefinitions(definitionsData);
      setHealthSummary(healthData);
    } catch (error) {
      console.error('Error fetching monitoring data:', error);
      notifications.show({
        title: 'Error',
        message: 'Failed to load monitoring data',
        color: 'red',
      });
    } finally {
      setIsLoading(false);
    }
  };

  const handleRefresh = async () => {
    setIsRefreshing(true);
    await fetchMonitoringData();
    setIsRefreshing(false);
    notifications.show({
      title: 'Refreshed',
      message: 'Monitoring data updated',
      color: 'green',
    });
  };

  const handleForceCheck = async () => {
    try {
      const response = await fetch('/api/cache/monitoring/check', {
        method: 'POST',
      });

      if (!response.ok) {
        throw new Error('Failed to force check');
      }

      const data = await response.json();
      setStatus(data);
      
      notifications.show({
        title: 'Check Complete',
        message: 'Cache monitoring check completed',
        color: 'green',
      });
    } catch (error) {
      notifications.show({
        title: 'Error',
        message: 'Failed to perform check',
        color: 'red',
      });
    }
  };

  const handleUpdateThresholds = async (updates: Partial<AlertThresholds>) => {
    try {
      const response = await fetch('/api/cache/monitoring/thresholds', {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(updates),
      });

      if (!response.ok) {
        throw new Error('Failed to update thresholds');
      }

      const data = await response.json();
      setThresholds(data);
      
      notifications.show({
        title: 'Thresholds Updated',
        message: 'Alert thresholds have been updated',
        color: 'green',
      });
    } catch (error) {
      notifications.show({
        title: 'Error',
        message: 'Failed to update thresholds',
        color: 'red',
      });
    }
  };

  const handleClearAlertHistory = async () => {
    try {
      const response = await fetch('/api/cache/monitoring/alerts', {
        method: 'DELETE',
      });

      if (!response.ok) {
        throw new Error('Failed to clear alerts');
      }

      setRecentAlerts([]);
      
      notifications.show({
        title: 'Alerts Cleared',
        message: 'Alert history has been cleared',
        color: 'green',
      });
    } catch (error) {
      notifications.show({
        title: 'Error',
        message: 'Failed to clear alert history',
        color: 'red',
      });
    }
  };

  const getSeverityColor = (severity: string) => {
    switch (severity) {
      case 'critical': return 'red';
      case 'error': return 'orange';
      case 'warning': return 'yellow';
      case 'info': return 'blue';
      default: return 'gray';
    }
  };

  const getHealthColor = (health: string) => {
    switch (health) {
      case 'Healthy': return 'green';
      case 'Degraded': return 'yellow';
      case 'Unhealthy': return 'red';
      default: return 'gray';
    }
  };

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
            <Title order={2}>Cache Monitoring & Alerts</Title>
            <Text size="sm" c="dimmed" mt={4}>
              Real-time cache performance monitoring and alerting
            </Text>
          </div>
          <Group>
            <Tooltip label={`Notifications ${notificationsEnabled ? 'enabled' : 'disabled'}`}>
              <ActionIcon
                variant={notificationsEnabled ? 'filled' : 'light'}
                color={notificationsEnabled ? 'blue' : 'gray'}
                onClick={() => setNotificationsEnabled(!notificationsEnabled)}
              >
                {notificationsEnabled ? <IconBell size={18} /> : <IconBellOff size={18} />}
              </ActionIcon>
            </Tooltip>
            <Button
              variant="light"
              leftSection={<IconHeartRateMonitor size={16} />}
              onClick={handleForceCheck}
            >
              Force Check
            </Button>
            <Button
              variant="light"
              leftSection={<IconRefresh size={16} />}
              onClick={handleRefresh}
              loading={isRefreshing}
            >
              Refresh
            </Button>
          </Group>
        </Group>
      </Card>

      {/* Health Overview Cards */}
      {healthSummary && (
        <SimpleGrid cols={{ base: 1, sm: 2, lg: 4 }} spacing="md">
          <Card padding="lg" radius="md" withBorder>
            <Group justify="space-between">
              <div>
                <Text size="sm" c="dimmed" fw={600} tt="uppercase">
                  Overall Health
                </Text>
                <Badge
                  size="xl"
                  color={getHealthColor(healthSummary.overallHealth)}
                  variant="filled"
                  mt={8}
                >
                  {healthSummary.overallHealth}
                </Badge>
                <Text size="xs" c="dimmed" mt={4}>
                  {healthSummary.activeAlerts} active alert{healthSummary.activeAlerts !== 1 ? 's' : ''}
                </Text>
              </div>
              <ThemeIcon
                color={getHealthColor(healthSummary.overallHealth)}
                variant="light"
                size={48}
                radius="md"
              >
                <IconActivity size={24} />
              </ThemeIcon>
            </Group>
          </Card>

          <Card padding="lg" radius="md" withBorder>
            <Group justify="space-between">
              <div>
                <Text size="sm" c="dimmed" fw={600} tt="uppercase">
                  Hit Rate
                </Text>
                <Group gap={4} align="baseline">
                  <Text size="xl" fw={700} mt={4}>
                    {healthSummary.hitRate.toFixed(1)}%
                  </Text>
                  <Text size="xs" c={healthSummary.hitRate < (thresholds?.minHitRate || 70) ? 'red' : 'dimmed'}>
                    {healthSummary.hitRate < (thresholds?.minHitRate || 70) ? '↓ Below threshold' : ''}
                  </Text>
                </Group>
                <Progress
                  value={healthSummary.hitRate}
                  size="xs"
                  mt={8}
                  color={healthSummary.hitRate > 80 ? 'green' : healthSummary.hitRate > 60 ? 'yellow' : 'red'}
                />
              </div>
              <RingProgress
                sections={[{ value: healthSummary.hitRate, color: 'blue' }]}
                size={48}
                thickness={4}
              />
            </Group>
          </Card>

          <Card padding="lg" radius="md" withBorder>
            <Group justify="space-between">
              <div>
                <Text size="sm" c="dimmed" fw={600} tt="uppercase">
                  Memory Usage
                </Text>
                <Group gap={4} align="baseline">
                  <Text size="xl" fw={700} mt={4}>
                    {healthSummary.memoryUsagePercent.toFixed(1)}%
                  </Text>
                  <Text size="xs" c={healthSummary.memoryUsagePercent > (thresholds?.maxMemoryUsage || 85) * 100 ? 'red' : 'dimmed'}>
                    {healthSummary.memoryUsagePercent > (thresholds?.maxMemoryUsage || 85) * 100 ? '↑ Above threshold' : ''}
                  </Text>
                </Group>
                <Progress
                  value={healthSummary.memoryUsagePercent}
                  size="xs"
                  mt={8}
                  color={healthSummary.memoryUsagePercent > 90 ? 'red' : healthSummary.memoryUsagePercent > 70 ? 'yellow' : 'green'}
                />
              </div>
              <ThemeIcon color="blue" variant="light" size={48} radius="md">
                <IconServer size={24} />
              </ThemeIcon>
            </Group>
          </Card>

          <Card padding="lg" radius="md" withBorder>
            <Group justify="space-between">
              <div>
                <Text size="sm" c="dimmed" fw={600} tt="uppercase">
                  Response Time
                </Text>
                <Group gap={4} align="baseline">
                  <Text size="xl" fw={700} mt={4}>
                    {healthSummary.responseTimeMs.toFixed(2)}ms
                  </Text>
                  <Text size="xs" c={healthSummary.responseTimeMs > (thresholds?.maxResponseTimeMs || 100) ? 'red' : 'dimmed'}>
                    {healthSummary.responseTimeMs > (thresholds?.maxResponseTimeMs || 100) ? '↑ Above threshold' : ''}
                  </Text>
                </Group>
                <Text size="xs" c="dimmed" mt={4}>
                  Last check: {formatters.date(healthSummary.lastCheck, { relativeDays: 0 })}
                </Text>
              </div>
              <ThemeIcon color="teal" variant="light" size={48} radius="md">
                <IconBolt size={24} />
              </ThemeIcon>
            </Group>
          </Card>
        </SimpleGrid>
      )}

      <Tabs value={activeTab} onChange={setActiveTab}>
        <Tabs.List>
          <Tabs.Tab value="overview" leftSection={<IconActivity size={16} />}>
            Overview
          </Tabs.Tab>
          <Tabs.Tab value="alerts" leftSection={<IconAlertCircle size={16} />}>
            Alerts
          </Tabs.Tab>
          <Tabs.Tab value="thresholds" leftSection={<IconSettings size={16} />}>
            Thresholds
          </Tabs.Tab>
          <Tabs.Tab value="definitions" leftSection={<IconInfoCircle size={16} />}>
            Alert Types
          </Tabs.Tab>
        </Tabs.List>

        <Tabs.Panel value="overview" pt="md">
          <Card shadow="sm" p="md" radius="md" withBorder>
            <Title order={4} mb="md">Recent Alerts</Title>
            {recentAlerts.length > 0 ? (
              <Timeline active={-1} bulletSize={24} lineWidth={2}>
                {recentAlerts.slice(0, 10).map((alert, index) => (
                  <Timeline.Item
                    key={index}
                    bullet={
                      <ThemeIcon
                        size={24}
                        variant="filled"
                        color={getSeverityColor(alert.severity)}
                        radius="xl"
                      >
                        {alert.severity === 'critical' ? <IconAlertTriangle size={14} /> : <IconAlertCircle size={14} />}
                      </ThemeIcon>
                    }
                    title={
                      <Group gap="xs">
                        <Text fw={500}>{alert.alertType}</Text>
                        {alert.region && (
                          <Badge size="xs" variant="light">
                            {alert.region}
                          </Badge>
                        )}
                      </Group>
                    }
                  >
                    <Text c="dimmed" size="sm">{alert.message}</Text>
                    <Text size="xs" c="dimmed" mt={4}>
                      {formatters.date(alert.timestamp, { relativeDays: 0 })}
                    </Text>
                  </Timeline.Item>
                ))}
              </Timeline>
            ) : (
              <Alert
                icon={<IconCircleCheck size={16} />}
                title="No recent alerts"
                color="green"
              >
                All cache systems are operating normally
              </Alert>
            )}
          </Card>
        </Tabs.Panel>

        <Tabs.Panel value="alerts" pt="md">
          <Card shadow="sm" p="md" radius="md" withBorder>
            <Group justify="space-between" mb="md">
              <Title order={4}>Alert History</Title>
              <Button
                variant="light"
                color="red"
                size="xs"
                leftSection={<IconTrash size={14} />}
                onClick={handleClearAlertHistory}
              >
                Clear History
              </Button>
            </Group>
            <ScrollArea h={400}>
              <Table>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Time</Table.Th>
                    <Table.Th>Type</Table.Th>
                    <Table.Th>Severity</Table.Th>
                    <Table.Th>Region</Table.Th>
                    <Table.Th>Message</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {recentAlerts.map((alert, index) => (
                    <Table.Tr key={index}>
                      <Table.Td>
                        <Text size="xs">{formatters.date(alert.timestamp, { relativeDays: 0 })}</Text>
                      </Table.Td>
                      <Table.Td>
                        <Code>{alert.alertType}</Code>
                      </Table.Td>
                      <Table.Td>
                        <Badge color={getSeverityColor(alert.severity)} variant="light">
                          {alert.severity}
                        </Badge>
                      </Table.Td>
                      <Table.Td>
                        {alert.region || '-'}
                      </Table.Td>
                      <Table.Td>
                        <Text size="sm" lineClamp={1}>
                          {alert.message}
                        </Text>
                      </Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </ScrollArea>
          </Card>
        </Tabs.Panel>

        <Tabs.Panel value="thresholds" pt="md">
          <Card shadow="sm" p="md" radius="md" withBorder>
            <Title order={4} mb="md">Alert Thresholds</Title>
            {thresholds && (
              <Stack gap="md">
                <Paper p="md" withBorder>
                  <Text fw={500} mb="sm">Performance Thresholds</Text>
                  <SimpleGrid cols={{ base: 1, sm: 2 }} spacing="md">
                    <div>
                      <Text size="sm" c="dimmed">Minimum Hit Rate (%)</Text>
                      <NumberInput
                        value={thresholds.minHitRate * 100}
                        onChange={(value) => handleUpdateThresholds({ minHitRate: Number(value) / 100 })}
                        min={0}
                        max={100}
                        suffix="%"
                        mt={4}
                      />
                    </div>
                    <div>
                      <Text size="sm" c="dimmed">Maximum Memory Usage (%)</Text>
                      <NumberInput
                        value={thresholds.maxMemoryUsage * 100}
                        onChange={(value) => handleUpdateThresholds({ maxMemoryUsage: Number(value) / 100 })}
                        min={0}
                        max={100}
                        suffix="%"
                        mt={4}
                      />
                    </div>
                    <div>
                      <Text size="sm" c="dimmed">Maximum Eviction Rate (per hour)</Text>
                      <NumberInput
                        value={thresholds.maxEvictionRate}
                        onChange={(value) => handleUpdateThresholds({ maxEvictionRate: Number(value) })}
                        min={0}
                        mt={4}
                      />
                    </div>
                    <div>
                      <Text size="sm" c="dimmed">Maximum Response Time (ms)</Text>
                      <NumberInput
                        value={thresholds.maxResponseTimeMs}
                        onChange={(value) => handleUpdateThresholds({ maxResponseTimeMs: Number(value) })}
                        min={0}
                        suffix="ms"
                        mt={4}
                      />
                    </div>
                  </SimpleGrid>
                </Paper>

                <Paper p="md" withBorder>
                  <Text fw={500} mb="sm">Alert Configuration</Text>
                  <div>
                    <Text size="sm" c="dimmed">Minimum Requests for Hit Rate Alert</Text>
                    <NumberInput
                      value={thresholds.minRequestsForHitRateAlert}
                      onChange={(value) => handleUpdateThresholds({ minRequestsForHitRateAlert: Number(value) })}
                      min={0}
                      mt={4}
                      description="Number of requests required before hit rate alerts are triggered"
                    />
                  </div>
                </Paper>
              </Stack>
            )}
          </Card>
        </Tabs.Panel>

        <Tabs.Panel value="definitions" pt="md">
          <Card shadow="sm" p="md" radius="md" withBorder>
            <Title order={4} mb="md">Alert Type Definitions</Title>
            <Stack gap="md">
              {alertDefinitions.map((def) => (
                <Paper key={def.type} p="md" withBorder>
                  <Group justify="space-between" mb="sm">
                    <Group>
                      <Text fw={600}>{def.name}</Text>
                      <Badge color={getSeverityColor(def.defaultSeverity.toLowerCase())} variant="light">
                        {def.defaultSeverity}
                      </Badge>
                      {def.notificationEnabled && (
                        <Badge color="green" variant="light" leftSection={<IconBell size={12} />}>
                          Notifications
                        </Badge>
                      )}
                    </Group>
                    <Text size="xs" c="dimmed">
                      Cooldown: {def.cooldownPeriodMinutes} min
                    </Text>
                  </Group>
                  <Text size="sm" c="dimmed" mb="sm">{def.description}</Text>
                  {def.recommendedActions.length > 0 && (
                    <>
                      <Text size="xs" fw={500} c="dimmed" mb={4}>Recommended Actions:</Text>
                      <Stack gap={4}>
                        {def.recommendedActions.map((action, index) => (
                          <Text key={index} size="xs" c="dimmed" pl="md">
                            • {action}
                          </Text>
                        ))}
                      </Stack>
                    </>
                  )}
                </Paper>
              ))}
            </Stack>
          </Card>
        </Tabs.Panel>
      </Tabs>

      <LoadingOverlay visible={isRefreshing} />
    </Stack>
  );
}