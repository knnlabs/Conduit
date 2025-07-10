'use client';

import {
  Stack,
  Title,
  Text,
  Group,
  Grid,
  Card,
  Badge,
  ThemeIcon,
  Table,
  ScrollArea,
  Button,
  Select,
  Switch,
  Progress,
  Skeleton,
  Timeline,
  RingProgress,
  Tooltip,
  ActionIcon,
  Modal,
  Code,
  Tabs,
  Alert,
} from '@mantine/core';
import {
  LineChart,
  AreaChart,
} from '@mantine/charts';
import {
  IconCircleCheck,
  IconCircleX,
  IconAlertTriangle,
  IconRefresh,
  IconDownload,
  IconClock,
  IconActivity,
  IconApi,
  IconBolt,
  IconWifi,
  IconInfoCircle,
  IconChartLine,
  IconServer,
  IconAlertCircle,
} from '@tabler/icons-react';
<<<<<<< HEAD
import { useState, useEffect, useCallback } from 'react';
import { CardSkeleton } from '@/components/common/LoadingState';
import { formatters } from '@/lib/utils/formatters';

interface ProviderHealthData {
  id: string;
  name: string;
  status: 'healthy' | 'degraded' | 'down';
  uptime: number;
  responseTime: number;
  errorRate: number;
  successRate: number;
  lastCheck: string;
  endpoints: {
    name: string;
    status: 'healthy' | 'degraded' | 'down';
    responseTime: number;
    lastCheck: string;
  }[];
  models: {
    name: string;
    available: boolean;
    responseTime: number;
    tokenCapacity: {
      used: number;
      total: number;
    };
  }[];
  rateLimit: {
    requests: {
      used: number;
      limit: number;
      reset: string;
    };
    tokens: {
      used: number;
      limit: number;
      reset: string;
    };
  };
  recentIncidents: {
    id: string;
    timestamp: string;
    type: 'outage' | 'degradation' | 'rate_limit';
    duration: number;
    message: string;
    resolved: boolean;
  }[];
}

interface HealthHistory {
  timestamp: string;
  responseTime: number;
  errorRate: number;
  availability: number;
}

interface ProviderMetrics {
  totalRequests: number;
  failedRequests: number;
  avgResponseTime: number;
  p95ResponseTime: number;
  p99ResponseTime: number;
  availability: number;
}
=======
import { useState } from 'react';
import { useDisclosure } from '@mantine/hooks';
import { StatusIndicator } from '@/components/common/StatusIndicator';
import { 
  useProviderHealthOverview,
  useProviderStatus,
  useProviderMetrics,
  useProviderIncidents,
  useProviderUptime,
  useProviderLatency,
  useProviderAlerts,
  useAcknowledgeAlert,
  useTriggerHealthCheck,
  type ProviderHealth
} from '@/hooks/useConduitAdmin';
import { notifications } from '@mantine/notifications';
import { CostChart } from '@/components/charts/CostChart';
>>>>>>> 8c6e680a0779d662d0317f0cdb2a8f3f34cd47a6

export default function ProviderHealthPage() {
  const [timeRange, setTimeRange] = useState('24h');
  const [autoRefresh, setAutoRefresh] = useState(true);
  const [isLoading, setIsLoading] = useState(true);
  const [providers, setProviders] = useState<ProviderHealthData[]>([]);
  const [selectedProvider, setSelectedProvider] = useState<ProviderHealthData | null>(null);
  const [healthHistory, setHealthHistory] = useState<Record<string, HealthHistory[]>>({});
  const [providerMetrics, setProviderMetrics] = useState<Record<string, ProviderMetrics>>({});

  const fetchProviderHealth = useCallback(async () => {
    try {
      setIsLoading(true);
      const response = await fetch(`/api/provider-health?range=${timeRange}`);
      if (!response.ok) {
        throw new Error('Failed to fetch provider health');
      }
      const data = await response.json();
      
      setProviders(data.providers);
      setHealthHistory(data.history);
      setProviderMetrics(data.metrics);
    } catch (error) {
      console.error('Error fetching provider health:', error);
    } finally {
      setIsLoading(false);
    }
  }, [timeRange]);

  useEffect(() => {
    fetchProviderHealth();
    
    if (autoRefresh) {
      const interval = setInterval(fetchProviderHealth, 30000); // Refresh every 30 seconds
      return () => clearInterval(interval);
    }
  }, [fetchProviderHealth, autoRefresh]);

<<<<<<< HEAD
  const getStatusColor = (status: string): string => {
    switch (status) {
      case 'healthy': return 'green';
      case 'degraded': return 'yellow';
      case 'down': return 'red';
      default: return 'gray';
    }
  };

  const getStatusIcon = (status: string) => {
=======
  // Convert provider status to SystemStatusType
  const mapProviderStatusToSystemStatus = (status: ProviderHealth['status']) => {
    switch (status) {
      case 'healthy': return 'healthy';
      case 'degraded': return 'degraded';
      case 'down': return 'unhealthy';
      case 'maintenance': return 'maintenance';
      default: return 'unknown';
    }
  };
  
  const mapOverallStatusToSystemStatus = (status: string) => {
    switch (status) {
      case 'operational': return 'healthy';
      case 'degraded': return 'degraded';
      case 'outage': return 'unhealthy';
      case 'maintenance': return 'maintenance';
      default: return 'unknown';
    }
  };
  
  // Helper for card colors (keeping for backward compatibility with cards)
  const getOverallStatusColor = (status: string) => {
>>>>>>> 8c6e680a0779d662d0317f0cdb2a8f3f34cd47a6
    switch (status) {
      case 'healthy': return IconCircleCheck;
      case 'degraded': return IconAlertTriangle;
      case 'down': return IconCircleX;
      default: return IconAlertCircle;
    }
  };

  const getIncidentTypeColor = (type: string): string => {
    switch (type) {
      case 'outage': return 'red';
      case 'degradation': return 'orange';
      case 'rate_limit': return 'yellow';
      default: return 'gray';
    }
  };

  const handleExport = async () => {
    try {
      const response = await fetch(`/api/provider-health/export?range=${timeRange}`);
      if (!response.ok) throw new Error('Export failed');

      const blob = await response.blob();
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `provider-health-${timeRange}-${new Date().toISOString()}.csv`;
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      document.body.removeChild(a);
    } catch (error) {
      console.error('Export failed:', error);
    }
  };

  const healthyCount = providers.filter(p => p.status === 'healthy').length;
  const degradedCount = providers.filter(p => p.status === 'degraded').length;
  const downCount = providers.filter(p => p.status === 'down').length;
  const overallHealth = providers.length > 0 ? (healthyCount / providers.length) * 100 : 0;

  return (
    <Stack gap="xl">
      <Group justify="space-between">
        <div>
          <Title order={1}>Provider Health</Title>
          <Text c="dimmed">Monitor LLM provider availability and performance</Text>
        </div>
        <Group>
          <Select
            value={timeRange}
            onChange={(value) => setTimeRange(value || '24h')}
            data={[
              { value: '1h', label: 'Last Hour' },
              { value: '24h', label: 'Last 24 Hours' },
              { value: '7d', label: 'Last 7 Days' },
              { value: '30d', label: 'Last 30 Days' },
            ]}
          />
          <Switch
            label="Auto-refresh"
            checked={autoRefresh}
            onChange={(event) => setAutoRefresh(event.currentTarget.checked)}
          />
          <Button
            variant="light"
            leftSection={<IconDownload size={16} />}
            onClick={handleExport}
          >
            Export
          </Button>
          <Button
            variant="light"
            leftSection={<IconRefresh size={16} />}
            onClick={fetchProviderHealth}
            loading={isLoading}
          >
            Refresh
          </Button>
        </Group>
      </Group>

      {/* Overall Health Summary */}
      <Card withBorder>
        <Group justify="space-between" mb="md">
          <div>
            <Text size="lg" fw={500}>Overall Provider Health</Text>
            <Text size="sm" c="dimmed">Across all {providers.length} providers</Text>
          </div>
          <RingProgress
            sections={[
              { value: (healthyCount / Math.max(providers.length, 1)) * 100, color: 'green' },
              { value: (degradedCount / Math.max(providers.length, 1)) * 100, color: 'yellow' },
              { value: (downCount / Math.max(providers.length, 1)) * 100, color: 'red' },
            ]}
            size={80}
            thickness={8}
            label={
              <Text ta="center" size="lg" fw={700}>
                {Math.round(overallHealth)}%
              </Text>
            }
          />
        </Group>
        <Group gap="xl">
          <div>
            <Text size="xs" c="dimmed" tt="uppercase" fw={600}>Healthy</Text>
            <Group gap="xs">
              <ThemeIcon color="green" variant="light" size="sm">
                <IconCircleCheck size={16} />
              </ThemeIcon>
              <Text size="xl" fw={700}>{healthyCount}</Text>
            </Group>
          </div>
          <div>
            <Text size="xs" c="dimmed" tt="uppercase" fw={600}>Degraded</Text>
            <Group gap="xs">
              <ThemeIcon color="yellow" variant="light" size="sm">
                <IconAlertTriangle size={16} />
              </ThemeIcon>
              <Text size="xl" fw={700}>{degradedCount}</Text>
            </Group>
          </div>
          <div>
            <Text size="xs" c="dimmed" tt="uppercase" fw={600}>Down</Text>
            <Group gap="xs">
              <ThemeIcon color="red" variant="light" size="sm">
                <IconCircleX size={16} />
              </ThemeIcon>
              <Text size="xl" fw={700}>{downCount}</Text>
            </Group>
          </div>
        </Group>
      </Card>

<<<<<<< HEAD
      {/* Provider Cards Grid */}
      <Grid>
        {isLoading ? (
          [...Array(6)].map((_, i) => (
            <Grid.Col key={i} span={{ base: 12, sm: 6, md: 4 }}>
              <CardSkeleton height={200} />
            </Grid.Col>
          ))
        ) : (
          providers.map((provider) => {
            const metrics = providerMetrics[provider.id];
            return (
              <Grid.Col key={provider.id} span={{ base: 12, sm: 6, md: 4 }}>
                <Card 
                  withBorder 
                  style={{ cursor: 'pointer' }}
                  onClick={() => setSelectedProvider(provider)}
                >
                  <Group justify="space-between" mb="md">
=======
      {/* Provider Health Overview */}
      <Card>
        <Tabs value={selectedTab} onChange={(value) => setSelectedTab(value || 'overview')}>
          <Tabs.List>
            <Tabs.Tab value="overview">Provider Status</Tabs.Tab>
            <Tabs.Tab value="incidents">Incidents</Tabs.Tab>
            <Tabs.Tab value="alerts">Alerts</Tabs.Tab>
            <Tabs.Tab value="metrics">Metrics</Tabs.Tab>
          </Tabs.List>

          <Tabs.Panel value="overview" pt="md">
            <div style={{ position: 'relative' }}>
              <LoadingOverlay visible={providersLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
              
              <Stack gap="md">
                {providers?.map((provider) => (
                  <Card key={provider.providerId} withBorder p="md">
                    <Grid>
                      <Grid.Col span={{ base: 12, md: 8 }}>
                        <Group justify="space-between" mb="md">
                          <Group gap="md">
                            <StatusIndicator
                              status={mapProviderStatusToSystemStatus(provider.status)}
                              variant="icon"
                              size="lg"
                            />
                            <div>
                              <Text fw={600} size="lg">{provider.providerName}</Text>
                              <Group gap="xs">
                                <StatusIndicator
                                  status={mapProviderStatusToSystemStatus(provider.status)}
                                  variant="badge"
                                  size="sm"
                                />
                                <Text size="sm" c="dimmed">
                                  {provider.region}
                                </Text>
                              </Group>
                            </div>
                          </Group>
                          
                          <Group gap="xs">
                            <Tooltip label="View details">
                              <ActionIcon
                                variant="light"
                                onClick={() => openProviderDetails(provider)}
                              >
                                <IconEye size={16} />
                              </ActionIcon>
                            </Tooltip>
                            <Tooltip label="Trigger health check">
                              <ActionIcon
                                variant="light"
                                onClick={() => triggerHealthCheck.mutateAsync(provider.providerId)}
                                loading={triggerHealthCheck.isPending}
                              >
                                <IconRefresh size={16} />
                              </ActionIcon>
                            </Tooltip>
                          </Group>
                        </Group>

                        <SimpleGrid cols={{ base: 2, sm: 4 }} spacing="md">
                          <div>
                            <Text size="xs" c="dimmed" mb={4}>Response Time</Text>
                            <Text fw={500}>{formatLatency(provider.responseTime)}</Text>
                          </div>
                          <div>
                            <Text size="xs" c="dimmed" mb={4}>Uptime</Text>
                            <Text fw={500}>{formatUptime(provider.uptime)}</Text>
                          </div>
                          <div>
                            <Text size="xs" c="dimmed" mb={4}>Error Rate</Text>
                            <Text fw={500}>{provider.errorRate.toFixed(1)}%</Text>
                          </div>
                          <div>
                            <Text size="xs" c="dimmed" mb={4}>Requests/min</Text>
                            <Text fw={500}>{provider.requestsPerMinute.toLocaleString()}</Text>
                          </div>
                        </SimpleGrid>

                        <Group gap="md" mt="md">
                          <div>
                            <Text size="xs" c="dimmed" mb={4}>Active Models</Text>
                            <Badge variant="light">
                              {provider.activeModels}/{provider.totalModels}
                            </Badge>
                          </div>
                          <div>
                            <Text size="xs" c="dimmed" mb={4}>Capabilities</Text>
                            <Group gap="xs">
                              {provider.capabilities.slice(0, 3).map((cap) => (
                                <Badge key={cap} size="xs" variant="light">
                                  {cap}
                                </Badge>
                              ))}
                              {provider.capabilities.length > 3 && (
                                <Badge size="xs" variant="light" color="gray">
                                  +{provider.capabilities.length - 3}
                                </Badge>
                              )}
                            </Group>
                          </div>
                        </Group>
                      </Grid.Col>
                      
                      <Grid.Col span={{ base: 12, md: 4 }}>
                        <Stack gap="md">
                          <div>
                            <Group justify="space-between" mb="xs">
                              <Text size="sm">Uptime</Text>
                              <Text size="sm">{formatUptime(provider.uptime)}</Text>
                            </Group>
                            <Progress
                              value={provider.uptime}
                              color={provider.uptime >= 99 ? 'green' : provider.uptime >= 95 ? 'orange' : 'red'}
                              size="lg"
                            />
                          </div>
                          
                          <div>
                            <Group justify="space-between" mb="xs">
                              <Text size="sm">Availability</Text>
                              <Text size="sm">{formatUptime(provider.availability)}</Text>
                            </Group>
                            <Progress
                              value={provider.availability}
                              color={provider.availability >= 99 ? 'green' : provider.availability >= 95 ? 'orange' : 'red'}
                              size="lg"
                            />
                          </div>

                          {provider.issues.length > 0 && (
                            <Alert icon={<IconAlertCircle size={16} />} color="orange">
                              <Text size="sm">
                                {provider.issues.length} active issue{provider.issues.length > 1 ? 's' : ''}
                              </Text>
                            </Alert>
                          )}
                        </Stack>
                      </Grid.Col>
                    </Grid>
                  </Card>
                ))}
              </Stack>
            </div>
          </Tabs.Panel>

          <Tabs.Panel value="incidents" pt="md">
            <div style={{ position: 'relative' }}>
              <LoadingOverlay visible={incidentsLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
              
              <Stack gap="md">
                {incidents?.length === 0 ? (
                  <Alert icon={<IconCircleCheck size={16} />} color="green" variant="light">
                    <Text>No active incidents. All providers are operating normally.</Text>
                  </Alert>
                ) : (
                  incidents?.map((incident) => (
                    <Card key={incident.id} withBorder p="md">
                      <Group justify="space-between" mb="md">
                        <Group gap="md">
                          <Badge color={getSeverityColor(incident.severity)} variant="light">
                            {incident.severity}
                          </Badge>
                          <div>
                            <Text fw={600}>{incident.title}</Text>
                            <Text size="sm" c="dimmed">
                              {incident.providerName} • Started {new Date(incident.startTime).toLocaleString()}
                            </Text>
                          </div>
                        </Group>
                        
                        <Group gap="xs">
                          <Badge color={incident.status === 'resolved' ? 'green' : 'orange'} variant="light">
                            {incident.status}
                          </Badge>
                          <ActionIcon
                            variant="light"
                            onClick={() => openIncidentDetails(incident)}
                          >
                            <IconEye size={16} />
                          </ActionIcon>
                        </Group>
                      </Group>

                      <Text size="sm" mb="md">
                        {incident.description}
                      </Text>

                      <Group gap="md">
                        <div>
                          <Text size="xs" c="dimmed" mb={4}>Affected Models</Text>
                          <Group gap="xs">
                            {incident.affectedModels.slice(0, 3).map((model) => (
                              <Badge key={model} size="xs" variant="light">
                                {model}
                              </Badge>
                            ))}
                            {incident.affectedModels.length > 3 && (
                              <Badge size="xs" variant="light" color="gray">
                                +{incident.affectedModels.length - 3}
                              </Badge>
                            )}
                          </Group>
                        </div>
                        
                        <div>
                          <Text size="xs" c="dimmed" mb={4}>Impact</Text>
                          <Text size="sm">
                            {incident.impact.requestsAffected.toLocaleString()} requests, {incident.impact.usersAffected} users
                          </Text>
                        </div>
                      </Group>
                    </Card>
                  ))
                )}
              </Stack>
            </div>
          </Tabs.Panel>

          <Tabs.Panel value="alerts" pt="md">
            <div style={{ position: 'relative' }}>
              <LoadingOverlay visible={alertsLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
              
              <Stack gap="md">
                {alerts?.length === 0 ? (
                  <Alert icon={<IconCircleCheck size={16} />} color="green" variant="light">
                    <Text>No active alerts. All providers are operating within normal parameters.</Text>
                  </Alert>
                ) : (
                  alerts?.map((alert) => {
                    const AlertIcon = getAlertTypeIcon(alert.type);
                    return (
                      <Card key={alert.id} withBorder p="md">
                        <Group justify="space-between" mb="md">
                          <Group gap="md">
                            <ThemeIcon color={getSeverityColor(alert.severity)} variant="light">
                              <AlertIcon size={16} />
                            </ThemeIcon>
                            <div>
                              <Text fw={600}>{alert.title}</Text>
                              <Text size="sm" c="dimmed">
                                {alert.providerName} • {new Date(alert.timestamp).toLocaleString()}
                              </Text>
                            </div>
                          </Group>
                          
                          <Group gap="xs">
                            <Badge color={getSeverityColor(alert.severity)} variant="light">
                              {alert.severity}
                            </Badge>
                            {alert.resolved ? (
                              <Badge color="green" variant="light">
                                Resolved
                              </Badge>
                            ) : alert.acknowledged ? (
                              <Badge color="blue" variant="light">
                                Acknowledged
                              </Badge>
                            ) : (
                              <Button
                                size="xs"
                                variant="light"
                                onClick={() => handleAcknowledgeAlert(alert.id)}
                                loading={acknowledgeAlert.isPending}
                              >
                                Acknowledge
                              </Button>
                            )}
                          </Group>
                        </Group>

                        <Text size="sm" mb="md">
                          {alert.message}
                        </Text>

                        <Group gap="md">
                          <div>
                            <Text size="xs" c="dimmed" mb={4}>Current Value</Text>
                            <Text fw={500}>
                              {alert.type === 'latency' ? formatLatency(alert.currentValue) : 
                               alert.type === 'uptime' || alert.type === 'availability' ? formatUptime(alert.currentValue) :
                               `${alert.currentValue.toFixed(1)}%`}
                            </Text>
                          </div>
                          <div>
                            <Text size="xs" c="dimmed" mb={4}>Threshold</Text>
                            <Text fw={500}>
                              {alert.type === 'latency' ? formatLatency(alert.threshold) : 
                               alert.type === 'uptime' || alert.type === 'availability' ? formatUptime(alert.threshold) :
                               `${alert.threshold.toFixed(1)}%`}
                            </Text>
                          </div>
                          <div>
                            <Text size="xs" c="dimmed" mb={4}>Duration</Text>
                            <Text fw={500}>{alert.duration} min</Text>
                          </div>
                        </Group>
                      </Card>
                    );
                  })
                )}
              </Stack>
            </div>
          </Tabs.Panel>

          <Tabs.Panel value="metrics" pt="md">
            <div style={{ position: 'relative' }}>
              <LoadingOverlay visible={!selectedProvider} overlayProps={{ radius: 'sm', blur: 2 }} />
              
              {!selectedProvider ? (
                <Center py="xl">
                  <Stack align="center" gap="md">
                    <IconChartLine size={48} color="var(--mantine-color-gray-5)" />
                    <Text c="dimmed">Select a provider from the overview tab to view detailed metrics</Text>
                  </Stack>
                </Center>
              ) : (
                <Stack gap="lg">
                  <Group justify="space-between">
>>>>>>> 8c6e680a0779d662d0317f0cdb2a8f3f34cd47a6
                    <div>
                      <Group gap="xs">
                        <ThemeIcon 
                          color={getStatusColor(provider.status)} 
                          variant="light" 
                          size="sm"
                        >
                          {getStatusIcon(provider.status)({ size: 16 })}
                        </ThemeIcon>
                        <Text fw={500}>{provider.name}</Text>
                      </Group>
                      <Badge 
                        color={getStatusColor(provider.status)} 
                        variant="light" 
                        size="sm" 
                        mt="xs"
                      >
                        {provider.status}
                      </Badge>
                    </div>
                    <Tooltip label="View Details">
                      <ActionIcon variant="subtle">
                        <IconInfoCircle size={18} />
                      </ActionIcon>
                    </Tooltip>
                  </Group>

                  <Stack gap="xs">
                    <Group justify="space-between">
                      <Text size="sm" c="dimmed">Uptime</Text>
                      <Text size="sm" fw={500}>
                        {provider.uptime.toFixed(2)}%
                      </Text>
                    </Group>
                    <Group justify="space-between">
                      <Text size="sm" c="dimmed">Avg Response</Text>
                      <Text size="sm" fw={500}>
                        {provider.responseTime}ms
                      </Text>
                    </Group>
                    <Group justify="space-between">
                      <Text size="sm" c="dimmed">Error Rate</Text>
                      <Text size="sm" fw={500} c={provider.errorRate > 5 ? 'red' : undefined}>
                        {provider.errorRate.toFixed(1)}%
                      </Text>
                    </Group>
                    <Group justify="space-between">
                      <Text size="sm" c="dimmed">Success Rate</Text>
                      <Text size="sm" fw={500} c={provider.successRate < 95 ? 'orange' : 'green'}>
                        {provider.successRate.toFixed(1)}%
                      </Text>
                    </Group>
                  </Stack>

                  <Text size="xs" c="dimmed" mt="xs">
                    Last checked: {formatters.date(provider.lastCheck, { relativeDays: 0 })}
                  </Text>
                </Card>
              </Grid.Col>
            );
          })
        )}
      </Grid>

      {/* Recent Incidents Timeline */}
      <Card withBorder>
        <Card.Section withBorder inheritPadding py="xs">
          <Group justify="space-between">
            <Text fw={500}>Recent Incidents</Text>
            <Text size="sm" c="dimmed">Last 7 days</Text>
          </Group>
        </Card.Section>
        <Card.Section inheritPadding py="md">
          {isLoading ? (
            <Skeleton height={200} />
          ) : (
            <ScrollArea h={250}>
              <Timeline bulletSize={20} lineWidth={2}>
                {providers.flatMap(p => 
                  p.recentIncidents.map(incident => ({
                    ...incident,
                    providerName: p.name,
                  }))
                )
                .sort((a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime())
                .slice(0, 10)
                .map((incident) => (
                  <Timeline.Item
                    key={incident.id}
                    bullet={
                      <ThemeIcon 
                        size={20} 
                        variant="light" 
                        color={getIncidentTypeColor(incident.type)}
                      >
                        {incident.type === 'outage' ? <IconCircleX size={12} /> : <IconAlertTriangle size={12} />}
                      </ThemeIcon>
                    }
                    title={
                      <Group gap="xs">
                        <Badge size="sm" color={getIncidentTypeColor(incident.type)} variant="light">
                          {incident.type.replace('_', ' ')}
                        </Badge>
                        <Text size="sm" fw={500}>{incident.providerName}</Text>
                        {incident.resolved && (
                          <Badge size="xs" color="green" variant="light">
                            Resolved
                          </Badge>
                        )}
                      </Group>
                    }
                  >
                    <Text c="dimmed" size="sm">{incident.message}</Text>
                    <Text size="xs" c="dimmed" mt={4}>
                      {formatters.date(incident.timestamp)} • Duration: {formatters.duration(incident.duration)}
                    </Text>
                  </Timeline.Item>
                ))}
                {providers.flatMap(p => p.recentIncidents).length === 0 && (
                  <Text size="sm" c="dimmed" ta="center" py="md">
                    No incidents in the last 7 days
                  </Text>
                )}
              </Timeline>
            </ScrollArea>
          )}
        </Card.Section>
      </Card>

      {/* Provider Details Modal */}
      <Modal
        opened={!!selectedProvider}
        onClose={() => setSelectedProvider(null)}
        title={selectedProvider?.name || ''}
        size="xl"
      >
        {selectedProvider && (
<<<<<<< HEAD
          <Tabs defaultValue="overview">
            <Tabs.List>
              <Tabs.Tab value="overview" leftSection={<IconInfoCircle size={16} />}>
                Overview
              </Tabs.Tab>
              <Tabs.Tab value="endpoints" leftSection={<IconApi size={16} />}>
                Endpoints
              </Tabs.Tab>
              <Tabs.Tab value="models" leftSection={<IconServer size={16} />}>
                Models
              </Tabs.Tab>
              <Tabs.Tab value="metrics" leftSection={<IconChartLine size={16} />}>
                Metrics
              </Tabs.Tab>
            </Tabs.List>

            <Tabs.Panel value="overview" pt="md">
              <Stack gap="md">
                <Group justify="space-between">
                  <Badge color={getStatusColor(selectedProvider.status)} size="lg">
                    {selectedProvider.status}
                  </Badge>
=======
          <Stack gap="md">
            <Group justify="space-between">
              <Group gap="md">
                <StatusIndicator
                  status={mapProviderStatusToSystemStatus(selectedProvider.status)}
                  variant="icon"
                  size="lg"
                />
                <div>
                  <Text fw={600} size="lg">{selectedProvider.providerName}</Text>
                  <StatusIndicator
                    status={mapProviderStatusToSystemStatus(selectedProvider.status)}
                    variant="badge"
                    size="sm"
                  />
                </div>
              </Group>
              <Text size="sm" c="dimmed">
                Last checked: {new Date(selectedProvider.lastChecked).toLocaleString()}
              </Text>
            </Group>
            
            <SimpleGrid cols={2} spacing="lg">
              <Card withBorder>
                <Text size="sm" c="dimmed" mb="xs">Response Time</Text>
                <Text fw={600} size="xl">{formatLatency(selectedProvider.responseTime)}</Text>
              </Card>
              <Card withBorder>
                <Text size="sm" c="dimmed" mb="xs">Uptime</Text>
                <Text fw={600} size="xl">{formatUptime(selectedProvider.uptime)}</Text>
              </Card>
              <Card withBorder>
                <Text size="sm" c="dimmed" mb="xs">Error Rate</Text>
                <Text fw={600} size="xl">{selectedProvider.errorRate.toFixed(1)}%</Text>
              </Card>
              <Card withBorder>
                <Text size="sm" c="dimmed" mb="xs">Requests/min</Text>
                <Text fw={600} size="xl">{selectedProvider.requestsPerMinute.toLocaleString()}</Text>
              </Card>
            </SimpleGrid>
            
            <div>
              <Text fw={500} mb="xs">Capabilities</Text>
              <Group gap="xs">
                {selectedProvider.capabilities.map((capability) => (
                  <Badge key={capability} variant="light">
                    {capability}
                  </Badge>
                ))}
              </Group>
            </div>
            
            <div>
              <Text fw={500} mb="xs">Configuration</Text>
              <Stack gap="xs">
                <Group justify="space-between">
                  <Text size="sm">Endpoint:</Text>
                  <Text size="sm" c="dimmed">{selectedProvider.endpoint}</Text>
                </Group>
                <Group justify="space-between">
                  <Text size="sm">Region:</Text>
                  <Text size="sm" c="dimmed">{selectedProvider.region}</Text>
                </Group>
                <Group justify="space-between">
                  <Text size="sm">Version:</Text>
                  <Text size="sm" c="dimmed">{selectedProvider.version || 'N/A'}</Text>
                </Group>
                <Group justify="space-between">
                  <Text size="sm">Models:</Text>
>>>>>>> 8c6e680a0779d662d0317f0cdb2a8f3f34cd47a6
                  <Text size="sm" c="dimmed">
                    Last check: {formatters.date(selectedProvider.lastCheck)}
                  </Text>
                </Group>

                <Grid>
                  <Grid.Col span={6}>
                    <Text size="sm" c="dimmed">Uptime</Text>
                    <Text size="lg" fw={700}>{selectedProvider.uptime.toFixed(2)}%</Text>
                  </Grid.Col>
                  <Grid.Col span={6}>
                    <Text size="sm" c="dimmed">Response Time</Text>
                    <Text size="lg" fw={700}>{selectedProvider.responseTime}ms</Text>
                  </Grid.Col>
                  <Grid.Col span={6}>
                    <Text size="sm" c="dimmed">Error Rate</Text>
                    <Text size="lg" fw={700}>{selectedProvider.errorRate.toFixed(1)}%</Text>
                  </Grid.Col>
                  <Grid.Col span={6}>
                    <Text size="sm" c="dimmed">Success Rate</Text>
                    <Text size="lg" fw={700}>{selectedProvider.successRate.toFixed(1)}%</Text>
                  </Grid.Col>
                </Grid>

                <div>
                  <Text size="sm" fw={500} mb="xs">Rate Limits</Text>
                  <Stack gap="xs">
                    <div>
                      <Group justify="space-between" mb={4}>
                        <Text size="xs" c="dimmed">Requests</Text>
                        <Text size="xs">
                          {selectedProvider.rateLimit.requests.used} / {selectedProvider.rateLimit.requests.limit}
                        </Text>
                      </Group>
                      <Progress 
                        value={(selectedProvider.rateLimit.requests.used / selectedProvider.rateLimit.requests.limit) * 100}
                        color={selectedProvider.rateLimit.requests.used > selectedProvider.rateLimit.requests.limit * 0.8 ? 'orange' : 'blue'}
                      />
                      <Text size="xs" c="dimmed" mt={2}>
                        Resets: {formatters.date(selectedProvider.rateLimit.requests.reset)}
                      </Text>
                    </div>
                    <div>
                      <Group justify="space-between" mb={4}>
                        <Text size="xs" c="dimmed">Tokens</Text>
                        <Text size="xs">
                          {formatters.shortNumber(selectedProvider.rateLimit.tokens.used)} / {formatters.shortNumber(selectedProvider.rateLimit.tokens.limit)}
                        </Text>
                      </Group>
                      <Progress 
                        value={(selectedProvider.rateLimit.tokens.used / selectedProvider.rateLimit.tokens.limit) * 100}
                        color={selectedProvider.rateLimit.tokens.used > selectedProvider.rateLimit.tokens.limit * 0.8 ? 'orange' : 'green'}
                      />
                      <Text size="xs" c="dimmed" mt={2}>
                        Resets: {formatters.date(selectedProvider.rateLimit.tokens.reset)}
                      </Text>
                    </div>
                  </Stack>
                </div>
              </Stack>
            </Tabs.Panel>

            <Tabs.Panel value="endpoints" pt="md">
              <Table>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Endpoint</Table.Th>
                    <Table.Th>Status</Table.Th>
                    <Table.Th>Response Time</Table.Th>
                    <Table.Th>Last Check</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {selectedProvider.endpoints.map((endpoint) => (
                    <Table.Tr key={endpoint.name}>
                      <Table.Td>{endpoint.name}</Table.Td>
                      <Table.Td>
                        <Badge color={getStatusColor(endpoint.status)} variant="light">
                          {endpoint.status}
                        </Badge>
                      </Table.Td>
                      <Table.Td>{endpoint.responseTime}ms</Table.Td>
                      <Table.Td>{formatters.date(endpoint.lastCheck)}</Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </Tabs.Panel>

            <Tabs.Panel value="models" pt="md">
              <Table>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Model</Table.Th>
                    <Table.Th>Available</Table.Th>
                    <Table.Th>Response Time</Table.Th>
                    <Table.Th>Token Usage</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {selectedProvider.models.map((model) => (
                    <Table.Tr key={model.name}>
                      <Table.Td>{model.name}</Table.Td>
                      <Table.Td>
                        <Badge 
                          color={model.available ? 'green' : 'red'} 
                          variant="light"
                        >
                          {model.available ? 'Available' : 'Unavailable'}
                        </Badge>
                      </Table.Td>
                      <Table.Td>{model.responseTime}ms</Table.Td>
                      <Table.Td>
                        <div>
                          <Text size="xs" c="dimmed" mb={2}>
                            {Math.round((model.tokenCapacity.used / model.tokenCapacity.total) * 100)}%
                          </Text>
                          <Progress
                            value={(model.tokenCapacity.used / model.tokenCapacity.total) * 100}
                            size="sm"
                          />
                        </div>
                      </Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </Tabs.Panel>

            <Tabs.Panel value="metrics" pt="md">
              {healthHistory[selectedProvider.id] && (
                <Stack gap="md">
                  <LineChart
                    h={300}
                    data={healthHistory[selectedProvider.id]}
                    dataKey="timestamp"
                    series={[
                      { name: 'responseTime', color: 'blue.6', label: 'Response Time (ms)' },
                      { name: 'errorRate', color: 'red.6', label: 'Error Rate (%)' },
                      { name: 'availability', color: 'green.6', label: 'Availability (%)' },
                    ]}
                    curveType="linear"
                    withLegend
                    legendProps={{ verticalAlign: 'bottom', height: 50 }}
                  />
                  
                  {providerMetrics[selectedProvider.id] && (
                    <Grid>
                      <Grid.Col span={6}>
                        <Text size="sm" c="dimmed">Total Requests</Text>
                        <Text size="lg" fw={700}>
                          {formatters.number(providerMetrics[selectedProvider.id].totalRequests)}
                        </Text>
                      </Grid.Col>
                      <Grid.Col span={6}>
                        <Text size="sm" c="dimmed">Failed Requests</Text>
                        <Text size="lg" fw={700}>
                          {formatters.number(providerMetrics[selectedProvider.id].failedRequests)}
                        </Text>
                      </Grid.Col>
                      <Grid.Col span={4}>
                        <Text size="sm" c="dimmed">Avg Response</Text>
                        <Text size="lg" fw={700}>
                          {providerMetrics[selectedProvider.id].avgResponseTime}ms
                        </Text>
                      </Grid.Col>
                      <Grid.Col span={4}>
                        <Text size="sm" c="dimmed">P95 Response</Text>
                        <Text size="lg" fw={700}>
                          {providerMetrics[selectedProvider.id].p95ResponseTime}ms
                        </Text>
                      </Grid.Col>
                      <Grid.Col span={4}>
                        <Text size="sm" c="dimmed">P99 Response</Text>
                        <Text size="lg" fw={700}>
                          {providerMetrics[selectedProvider.id].p99ResponseTime}ms
                        </Text>
                      </Grid.Col>
                    </Grid>
                  )}
                </Stack>
              )}
            </Tabs.Panel>
          </Tabs>
        )}
      </Modal>
    </Stack>
  );
}