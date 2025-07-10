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
  Progress,
  ActionIcon,
  Tooltip,
  Modal,
  Timeline,
  Center,
  Grid,
} from '@mantine/core';
import {
  IconServer,
  IconRefresh,
  IconAlertCircle,
  IconCircleCheck,
  IconCircleX,
  IconAlertTriangle,
  IconClock,
  IconTrendingUp,
  IconEye,
  IconActivity,
  IconBolt,
  IconChartLine,
  IconBell,
  IconWifi,
} from '@tabler/icons-react';
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

export default function ProviderHealthPage() {
  const [timeRangeValue, setTimeRangeValue] = useState('24h');
  const [selectedTab, setSelectedTab] = useState('overview');
  const [selectedProvider, setSelectedProvider] = useState<ProviderHealth | null>(null);
  const [detailsOpened, { open: openDetails, close: closeDetails }] = useDisclosure(false);
  const [incidentOpened, { open: openIncident, close: closeIncident }] = useDisclosure(false);
  interface Incident {
    id: string;
    title: string;
    severity: string;
    providerId: string;
    providerName: string;
    description: string;
    status: string;
    startTime: string;
    endTime?: string;
    affectedModels?: string[];
    updates: Array<{ status: string; timestamp: string; author: string; message: string }>;
    duration?: number;
    impact?: { usersAffected?: number };
  }
  
  const [selectedIncident, setSelectedIncident] = useState<Incident | null>(null);
  
  const { data: providers, isLoading: providersLoading } = useProviderHealthOverview();
  const { data: status, isLoading: statusLoading } = useProviderStatus();
  const { data: incidents, isLoading: incidentsLoading } = useProviderIncidents();
  const { data: alerts, isLoading: alertsLoading } = useProviderAlerts();
  const { data: selectedProviderMetrics } = useProviderMetrics(
    selectedProvider?.providerId || '', timeRangeValue
  );
  const { data: selectedProviderUptime } = useProviderUptime(
    selectedProvider?.providerId || '', timeRangeValue as '24h' | '7d' | '30d' | '90d'
  );
  const { data: selectedProviderLatency } = useProviderLatency(
    selectedProvider?.providerId || '', timeRangeValue
  );
  
  const acknowledgeAlert = useAcknowledgeAlert();
  const triggerHealthCheck = useTriggerHealthCheck();

  const _isLoading = providersLoading || statusLoading;

  const handleRefresh = async () => {
    try {
      await triggerHealthCheck.mutateAsync(undefined);
      notifications.show({
        title: 'Health Check Completed',
        message: 'Provider health status has been updated',
        color: 'green',
      });
    } catch (error: unknown) {
      notifications.show({
        title: 'Health Check Failed',
        message: (error as Error).message || 'Failed to trigger health check',
        color: 'red',
      });
    }
  };

  const handleAcknowledgeAlert = async (alertId: string) => {
    try {
      await acknowledgeAlert.mutateAsync(alertId);
      notifications.show({
        title: 'Alert Acknowledged',
        message: 'Alert has been acknowledged successfully',
        color: 'blue',
      });
    } catch (error: unknown) {
      notifications.show({
        title: 'Acknowledge Failed',
        message: (error as Error).message || 'Failed to acknowledge alert',
        color: 'red',
      });
    }
  };

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
    switch (status) {
      case 'operational': return 'green';
      case 'degraded': return 'orange';
      case 'outage': return 'red';
      case 'maintenance': return 'blue';
      default: return 'gray';
    }
  };

  const formatUptime = (uptime: number) => {
    return `${uptime.toFixed(2)}%`;
  };

  const formatLatency = (ms: number) => {
    if (ms < 1000) return `${Math.round(ms)}ms`;
    return `${(ms / 1000).toFixed(1)}s`;
  };

  const openProviderDetails = (provider: ProviderHealth) => {
    setSelectedProvider(provider);
    openDetails();
  };

  const openIncidentDetails = (incident: Incident) => {
    setSelectedIncident(incident);
    openIncident();
  };

  const getSeverityColor = (severity: string) => {
    switch (severity) {
      case 'critical': return 'red';
      case 'high': return 'orange';
      case 'medium': return 'yellow';
      case 'low': return 'blue';
      default: return 'gray';
    }
  };

  const getAlertTypeIcon = (type: string) => {
    switch (type) {
      case 'latency': return IconClock;
      case 'uptime': return IconWifi;
      case 'error_rate': return IconAlertCircle;
      case 'capacity': return IconBolt;
      case 'availability': return IconServer;
      default: return IconBell;
    }
  };

  const unacknowledgedAlerts = alerts?.filter(alert => !alert.acknowledged) || [];
  const criticalAlerts = unacknowledgedAlerts.filter(alert => alert.severity === 'critical');

  const statusCards = status ? [
    {
      title: 'Overall Status',
      value: status.overall.charAt(0).toUpperCase() + status.overall.slice(1),
      icon: IconServer,
      color: getOverallStatusColor(status.overall),
    },
    {
      title: 'Total Providers',
      value: status.totalProviders,
      icon: IconActivity,
      color: 'blue',
    },
    {
      title: 'Healthy Providers',
      value: status.healthyProviders,
      icon: IconCircleCheck,
      color: 'green',
    },
    {
      title: 'Avg Response Time',
      value: formatLatency(status.averageResponseTime),
      icon: IconClock,
      color: 'orange',
    },
    {
      title: 'Avg Uptime',
      value: formatUptime(status.averageUptime),
      icon: IconTrendingUp,
      color: 'teal',
    },
    {
      title: 'Failed Requests',
      value: `${status.failedRequests.toLocaleString()} / ${status.totalRequests.toLocaleString()}`,
      icon: IconAlertCircle,
      color: 'red',
    },
  ] : [];

  return (
    <Stack gap="xl">
      <Group justify="space-between">
        <div>
          <Title order={1}>Provider Health Monitoring</Title>
          <Text c="dimmed">Real-time monitoring of AI provider health and performance</Text>
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
            ]}
            w={180}
          />
          <Button
            variant="light"
            leftSection={<IconRefresh size={16} />}
            onClick={handleRefresh}
            loading={triggerHealthCheck.isPending}
          >
            Refresh All
          </Button>
        </Group>
      </Group>

      {/* Critical Alerts */}
      {criticalAlerts.length > 0 && (
        <Alert icon={<IconAlertCircle size={16} />} color="red" title="Critical Alerts">
          <Stack gap="xs">
            {criticalAlerts.slice(0, 3).map((alert) => (
              <Group key={alert.id} justify="space-between">
                <Text size="sm">
                  <Text span fw={500}>{alert.providerName}:</Text> {alert.message}
                </Text>
                <Button
                  size="xs"
                  variant="light"
                  onClick={() => handleAcknowledgeAlert(alert.id)}
                  loading={acknowledgeAlert.isPending}
                >
                  Acknowledge
                </Button>
              </Group>
            ))}
            {criticalAlerts.length > 3 && (
              <Text size="sm" c="dimmed">
                +{criticalAlerts.length - 3} more critical alerts
              </Text>
            )}
          </Stack>
        </Alert>
      )}

      {/* Status Overview Cards */}
      <SimpleGrid cols={{ base: 1, sm: 2, md: 3, lg: 6 }} spacing="lg">
        {statusCards.map((stat) => (
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
                    <div>
                      <Text fw={600} size="lg">{selectedProvider.providerName} Metrics</Text>
                      <Text size="sm" c="dimmed">Detailed performance metrics and trends</Text>
                    </div>
                    <Select
                      value={timeRangeValue}
                      onChange={(value) => setTimeRangeValue(value || '24h')}
                      data={[
                        { value: '1h', label: 'Last Hour' },
                        { value: '24h', label: 'Last 24 Hours' },
                        { value: '7d', label: 'Last 7 Days' },
                        { value: '30d', label: 'Last 30 Days' },
                      ]}
                      w={180}
                    />
                  </Group>

                  <SimpleGrid cols={{ base: 1, md: 2 }} spacing="lg">
                    {selectedProviderMetrics && (
                      <CostChart
                        data={selectedProviderMetrics.metrics}
                        title="Response Time"
                        type="line"
                        valueKey="responseTime"
                        nameKey="timestamp"
                        timeKey="timestamp"
                      />
                    )}
                    
                    {selectedProviderMetrics && (
                      <CostChart
                        data={selectedProviderMetrics.metrics}
                        title="Request Volume"
                        type="bar"
                        valueKey="requestCount"
                        nameKey="timestamp"
                        timeKey="timestamp"
                      />
                    )}
                    
                    {selectedProviderLatency && (
                      <CostChart
                        data={selectedProviderLatency.latencyData}
                        title="Latency Percentiles"
                        type="line"
                        valueKey="p95"
                        nameKey="timestamp"
                        timeKey="timestamp"
                      />
                    )}
                    
                    {selectedProviderUptime && (
                      <CostChart
                        data={selectedProviderUptime.uptimeHistory}
                        title="Daily Uptime"
                        type="bar"
                        valueKey="uptime"
                        nameKey="date"
                        timeKey="date"
                      />
                    )}
                  </SimpleGrid>
                </Stack>
              )}
            </div>
          </Tabs.Panel>
        </Tabs>
      </Card>

      {/* Provider Details Modal */}
      <Modal
        opened={detailsOpened}
        onClose={closeDetails}
        title="Provider Details"
        size="xl"
      >
        {selectedProvider && (
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
                  <Text size="sm" c="dimmed">
                    {selectedProvider.activeModels}/{selectedProvider.totalModels} active
                  </Text>
                </Group>
              </Stack>
            </div>

            {selectedProvider.issues.length > 0 && (
              <div>
                <Text fw={500} mb="xs">Active Issues</Text>
                <Stack gap="xs">
                  {selectedProvider.issues.map((issue) => (
                    <Alert key={issue.id} icon={<IconAlertCircle size={16} />} color={getSeverityColor(issue.severity)}>
                      <Group justify="space-between">
                        <Text size="sm">{issue.message}</Text>
                        <Text size="xs" c="dimmed">
                          {new Date(issue.timestamp).toLocaleString()}
                        </Text>
                      </Group>
                    </Alert>
                  ))}
                </Stack>
              </div>
            )}
          </Stack>
        )}
      </Modal>

      {/* Incident Details Modal */}
      <Modal
        opened={incidentOpened}
        onClose={closeIncident}
        title="Incident Details"
        size="lg"
      >
        {selectedIncident && (
          <Stack gap="md">
            <Group justify="space-between">
              <div>
                <Text fw={600} size="lg">{selectedIncident.title}</Text>
                <Group gap="xs">
                  <Badge color={getSeverityColor(selectedIncident.severity)} variant="light">
                    {selectedIncident.severity}
                  </Badge>
                  <Badge color={selectedIncident.status === 'resolved' ? 'green' : 'orange'} variant="light">
                    {selectedIncident.status}
                  </Badge>
                  <Text size="sm" c="dimmed">
                    {selectedIncident.providerName}
                  </Text>
                </Group>
              </div>
            </Group>
            
            <Text>{selectedIncident.description}</Text>
            
            <SimpleGrid cols={2} spacing="lg">
              <div>
                <Text size="sm" c="dimmed" mb="xs">Started</Text>
                <Text>{new Date(selectedIncident.startTime).toLocaleString()}</Text>
              </div>
              {selectedIncident.endTime && (
                <div>
                  <Text size="sm" c="dimmed" mb="xs">Resolved</Text>
                  <Text>{new Date(selectedIncident.endTime).toLocaleString()}</Text>
                </div>
              )}
              <div>
                <Text size="sm" c="dimmed" mb="xs">Duration</Text>
                <Text>{selectedIncident.duration ? `${selectedIncident.duration} minutes` : 'Ongoing'}</Text>
              </div>
              <div>
                <Text size="sm" c="dimmed" mb="xs">Affected Users</Text>
                <Text>{selectedIncident.impact?.usersAffected?.toLocaleString()}</Text>
              </div>
            </SimpleGrid>
            
            <div>
              <Text fw={500} mb="xs">Affected Models</Text>
              <Group gap="xs">
                {selectedIncident.affectedModels?.map((model) => (
                  <Badge key={model} variant="light">
                    {model}
                  </Badge>
                ))}
              </Group>
            </div>
            
            <div>
              <Text fw={500} mb="md">Updates</Text>
              <Timeline>
                {(selectedIncident as { updates: unknown[] }).updates.map((update: unknown, index: number) => (
                  <Timeline.Item key={index} title={(update as { status: string }).status}>
                    <Text size="sm" c="dimmed" mb="xs">
                      {new Date((update as { timestamp: string }).timestamp).toLocaleString()} • {(update as { author: string }).author}
                    </Text>
                    <Text size="sm">{(update as { message: string }).message}</Text>
                  </Timeline.Item>
                ))}
              </Timeline>
            </div>
          </Stack>
        )}
      </Modal>
    </Stack>
  );
}