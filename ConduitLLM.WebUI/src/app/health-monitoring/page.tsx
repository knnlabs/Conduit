'use client';

import {
  Stack,
  Title,
  Text,
  Group,
  Button,
  Card,
  Badge,
  Grid,
  Paper,
  ThemeIcon,
  Progress,
  Timeline,
  Alert,
} from '@mantine/core';
import {
  IconRefresh,
  IconCircleCheck,
  IconCircleX,
  IconAlertCircle,
  IconServer,
  IconDatabase,
  IconNetwork,
  IconClock,
  IconActivity,
} from '@tabler/icons-react';
import { useState, useEffect, useCallback } from 'react';
import { notifications } from '@mantine/notifications';

interface ProviderHealth {
  id: string;
  name: string;
  status: 'healthy' | 'degraded' | 'unhealthy' | 'unknown';
  lastChecked: string;
  responseTime: number;
  uptime: number;
  errorRate: number;
  details?: {
    lastError?: string;
    consecutiveFailures?: number;
    lastSuccessfulCheck?: string;
  };
}

interface SystemHealth {
  overall: 'healthy' | 'degraded' | 'unhealthy';
  components: {
    api: ComponentHealth;
    database: ComponentHealth;
    cache: ComponentHealth;
    queue: ComponentHealth;
  };
  metrics: {
    cpu: number;
    memory: number;
    disk: number;
    activeConnections: number;
  };
}

interface ComponentHealth {
  status: 'healthy' | 'degraded' | 'unhealthy';
  message?: string;
  lastChecked: string;
}

interface HealthEvent {
  id: string;
  timestamp: string;
  type: 'provider_down' | 'provider_up' | 'system_issue' | 'system_recovered';
  message: string;
  severity: 'info' | 'warning' | 'error';
}

export default function HealthMonitoringPage() {
  const [providers, setProviders] = useState<ProviderHealth[]>([]);
  const [systemHealth, setSystemHealth] = useState<SystemHealth | null>(null);
  const [healthEvents, setHealthEvents] = useState<HealthEvent[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [autoRefresh, setAutoRefresh] = useState(true);

  const fetchHealthData = useCallback(async () => {
    try {
      // Fetch provider health
      const providersResponse = await fetch('/api/health/providers');
      if (providersResponse.ok) {
        const providersData = await providersResponse.json();
        setProviders(providersData);
      }

      // Fetch system health
      const systemResponse = await fetch('/api/health/system');
      if (systemResponse.ok) {
        const systemData = await systemResponse.json();
        setSystemHealth(systemData);
      }

      // Fetch recent health events
      const eventsResponse = await fetch('/api/health/events?limit=10');
      if (eventsResponse.ok) {
        const eventsData = await eventsResponse.json();
        setHealthEvents(eventsData);
      }
    } catch (error) {
      console.error('Error fetching health data:', error);
      notifications.show({
        title: 'Error',
        message: 'Failed to fetch health data',
        color: 'red',
      });
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchHealthData();

    if (autoRefresh) {
      const interval = setInterval(fetchHealthData, 30000); // Refresh every 30 seconds
      return () => clearInterval(interval);
    }
  }, [fetchHealthData, autoRefresh]);

  const getStatusIcon = (status: string) => {
    switch (status) {
      case 'healthy':
        return <IconCircleCheck size={20} color="var(--mantine-color-green-6)" />;
      case 'degraded':
        return <IconAlertCircle size={20} color="var(--mantine-color-yellow-6)" />;
      case 'unhealthy':
        return <IconCircleX size={20} color="var(--mantine-color-red-6)" />;
      default:
        return <IconAlertCircle size={20} color="var(--mantine-color-gray-6)" />;
    }
  };

  const getStatusColor = (status: string): string => {
    switch (status) {
      case 'healthy':
        return 'green';
      case 'degraded':
        return 'yellow';
      case 'unhealthy':
        return 'red';
      default:
        return 'gray';
    }
  };

  const formatUptime = (uptime: number): string => {
    if (uptime >= 99.9) return '99.9%';
    return `${uptime.toFixed(1)}%`;
  };

  const getOverallHealth = (): { status: string; message: string } => {
    const unhealthyProviders = providers.filter(p => p.status === 'unhealthy').length;
    const degradedProviders = providers.filter(p => p.status === 'degraded').length;
    
    if (unhealthyProviders > 0) {
      return {
        status: 'unhealthy',
        message: `${unhealthyProviders} provider${unhealthyProviders > 1 ? 's' : ''} down`,
      };
    }
    
    if (degradedProviders > 0 || systemHealth?.overall === 'degraded') {
      return {
        status: 'degraded',
        message: 'Some services experiencing issues',
      };
    }
    
    if (systemHealth?.overall === 'unhealthy') {
      return {
        status: 'unhealthy',
        message: 'System components unhealthy',
      };
    }
    
    return {
      status: 'healthy',
      message: 'All systems operational',
    };
  };

  const overallHealth = getOverallHealth();

  return (
    <Stack gap="xl">
      <Alert
        icon={<IconAlertCircle size="1rem" />}
        title="Limited SDK Functionality"
        color="yellow"
        variant="light"
      >
        System health metrics and health events are currently using simulated data. The SDK methods for real-time system monitoring are not yet available.
        Provider health data may be partially available depending on your SDK configuration.
      </Alert>

      <Group justify="space-between">
        <div>
          <Title order={1}>Health Monitoring</Title>
          <Text c="dimmed">Monitor system and provider health status</Text>
        </div>
        <Group>
          <Button
            variant={autoRefresh ? 'filled' : 'light'}
            onClick={() => setAutoRefresh(!autoRefresh)}
            leftSection={<IconClock size={16} />}
          >
            Auto-refresh: {autoRefresh ? 'ON' : 'OFF'}
          </Button>
          <Button
            variant="light"
            leftSection={<IconRefresh size={16} />}
            onClick={fetchHealthData}
            loading={isLoading}
          >
            Refresh
          </Button>
        </Group>
      </Group>

      {/* Overall Status */}
      <Alert
        icon={getStatusIcon(overallHealth.status)}
        title="System Status"
        color={getStatusColor(overallHealth.status)}
        variant="light"
      >
        <Group justify="space-between">
          <Text size="sm" fw={500}>
            {overallHealth.message}
          </Text>
          <Text size="xs" c="dimmed">
            Last updated: {new Date().toLocaleTimeString()}
          </Text>
        </Group>
      </Alert>

      {/* System Metrics */}
      {systemHealth && (
        <Grid>
          <Grid.Col span={{ base: 12, md: 6, lg: 3 }}>
            <Paper p="md" withBorder>
              <Group justify="space-between" mb="xs">
                <Text size="sm" c="dimmed">CPU Usage</Text>
                <ThemeIcon size="sm" variant="light">
                  <IconActivity size={16} />
                </ThemeIcon>
              </Group>
              <Text size="xl" fw={700}>{systemHealth.metrics.cpu}%</Text>
              <Progress value={systemHealth.metrics.cpu} mt="xs" color={systemHealth.metrics.cpu > 80 ? 'red' : 'blue'} />
            </Paper>
          </Grid.Col>
          
          <Grid.Col span={{ base: 12, md: 6, lg: 3 }}>
            <Paper p="md" withBorder>
              <Group justify="space-between" mb="xs">
                <Text size="sm" c="dimmed">Memory Usage</Text>
                <ThemeIcon size="sm" variant="light">
                  <IconServer size={16} />
                </ThemeIcon>
              </Group>
              <Text size="xl" fw={700}>{systemHealth.metrics.memory}%</Text>
              <Progress value={systemHealth.metrics.memory} mt="xs" color={systemHealth.metrics.memory > 80 ? 'red' : 'blue'} />
            </Paper>
          </Grid.Col>
          
          <Grid.Col span={{ base: 12, md: 6, lg: 3 }}>
            <Paper p="md" withBorder>
              <Group justify="space-between" mb="xs">
                <Text size="sm" c="dimmed">Disk Usage</Text>
                <ThemeIcon size="sm" variant="light">
                  <IconDatabase size={16} />
                </ThemeIcon>
              </Group>
              <Text size="xl" fw={700}>{systemHealth.metrics.disk}%</Text>
              <Progress value={systemHealth.metrics.disk} mt="xs" color={systemHealth.metrics.disk > 80 ? 'red' : 'blue'} />
            </Paper>
          </Grid.Col>
          
          <Grid.Col span={{ base: 12, md: 6, lg: 3 }}>
            <Paper p="md" withBorder>
              <Group justify="space-between" mb="xs">
                <Text size="sm" c="dimmed">Active Connections</Text>
                <ThemeIcon size="sm" variant="light">
                  <IconNetwork size={16} />
                </ThemeIcon>
              </Group>
              <Text size="xl" fw={700}>{systemHealth.metrics.activeConnections}</Text>
              <Text size="xs" c="dimmed" mt="xs">Currently active</Text>
            </Paper>
          </Grid.Col>
        </Grid>
      )}

      {/* Provider Health */}
      <Card withBorder>
        <Card.Section withBorder inheritPadding py="xs">
          <Text fw={500}>Provider Health Status</Text>
        </Card.Section>
        <Card.Section inheritPadding py="md">
          <Stack gap="sm">
            {providers.length === 0 ? (
              <Text c="dimmed" ta="center" py="xl">
                No providers configured
              </Text>
            ) : (
              providers.map((provider) => (
                <Paper key={provider.id} p="md" withBorder>
                  <Group justify="space-between">
                    <Group>
                      {getStatusIcon(provider.status)}
                      <div>
                        <Text fw={500}>{provider.name}</Text>
                        <Text size="xs" c="dimmed">
                          Last checked: {new Date(provider.lastChecked).toLocaleTimeString()}
                        </Text>
                      </div>
                    </Group>
                    <Group gap="xl">
                      <div>
                        <Text size="xs" c="dimmed">Response Time</Text>
                        <Text fw={500}>{provider.responseTime}ms</Text>
                      </div>
                      <div>
                        <Text size="xs" c="dimmed">Uptime</Text>
                        <Text fw={500}>{formatUptime(provider.uptime)}</Text>
                      </div>
                      <div>
                        <Text size="xs" c="dimmed">Error Rate</Text>
                        <Text fw={500} c={provider.errorRate > 5 ? 'red' : undefined}>
                          {provider.errorRate.toFixed(1)}%
                        </Text>
                      </div>
                      <Badge color={getStatusColor(provider.status)} variant="light">
                        {provider.status}
                      </Badge>
                    </Group>
                  </Group>
                  {provider.details?.lastError && (
                    <Alert mt="sm" color="red" variant="light" title="Last Error">
                      <Text size="xs">{provider.details.lastError}</Text>
                    </Alert>
                  )}
                </Paper>
              ))
            )}
          </Stack>
        </Card.Section>
      </Card>

      {/* Recent Events */}
      <Card withBorder>
        <Card.Section withBorder inheritPadding py="xs">
          <Text fw={500}>Recent Health Events</Text>
        </Card.Section>
        <Card.Section inheritPadding py="md">
          {healthEvents.length === 0 ? (
            <Text c="dimmed" ta="center" py="xl">
              No recent events
            </Text>
          ) : (
            <Timeline bulletSize={20}>
              {healthEvents.map((event) => (
                <Timeline.Item
                  key={event.id}
                  bullet={
                    event.severity === 'error' ? (
                      <IconCircleX size={16} />
                    ) : event.severity === 'warning' ? (
                      <IconAlertCircle size={16} />
                    ) : (
                      <IconCircleCheck size={16} />
                    )
                  }
                  color={
                    event.severity === 'error' ? 'red' : event.severity === 'warning' ? 'yellow' : 'green'
                  }
                >
                  <Text size="sm" fw={500}>
                    {event.message}
                  </Text>
                  <Text size="xs" c="dimmed">
                    {new Date(event.timestamp).toLocaleString()}
                  </Text>
                </Timeline.Item>
              ))}
            </Timeline>
          )}
        </Card.Section>
      </Card>
    </Stack>
  );
}