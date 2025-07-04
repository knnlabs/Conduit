'use client';

import {
  Stack,
  Title,
  Text,
  Group,
  Button,
  Card,
  Badge,
  Alert,
  SimpleGrid,
  ThemeIcon,
  Table,
  Progress,
  Timeline,
  Tabs,
  LoadingOverlay,
  ActionIcon,
  Tooltip,
  Modal,
  Code,
  Select,
  Switch,
  NumberInput,
} from '@mantine/core';
import {
  IconShield,
  IconShieldCheck,
  IconShieldX,
  IconAlertTriangle,
  IconBug,
  IconLock,
  IconRefresh,
  IconSettings,
  IconEye,
  IconBan,
  IconActivity,
  IconClock,
} from '@tabler/icons-react';
import { useState } from 'react';
import { useDisclosure } from '@mantine/hooks';
import { notifications } from '@mantine/notifications';
import { useSecurityEvents, useThreatDetections } from '@/hooks/api/useAdminApi';
import { apiFetch } from '@/lib/utils/fetch-wrapper';

interface SecurityMetrics {
  threatLevel: 'low' | 'medium' | 'high' | 'critical';
  blockedRequests: number;
  suspiciousActivity: number;
  rateLimitHits: number;
  failedAuthentications: number;
  activeThreats: number;
  vulnerabilityScore: number;
  uptime: number;
}

interface SecurityEvent {
  id: string;
  type: 'block' | 'threat' | 'auth_failure' | 'rate_limit' | 'vulnerability';
  severity: 'low' | 'medium' | 'high' | 'critical';
  timestamp: string;
  ipAddress: string;
  description: string;
  details: string;
  action: string;
  resolved: boolean;
}

interface ThreatDetection {
  id: string;
  name: string;
  description: string;
  isEnabled: boolean;
  sensitivity: number;
  lastTriggered?: string;
  triggerCount: number;
}

export default function SecurityPage() {
  const [selectedEvent, setSelectedEvent] = useState<SecurityEvent | null>(null);
  const [modalOpened, { open: openModal, close: closeModal }] = useDisclosure(false);
  const [settingsOpened, { open: openSettings, close: closeSettings }] = useDisclosure(false);
  const [autoRefresh, setAutoRefresh] = useState(true);
  const [isLoading, setIsLoading] = useState(false);

  // Fetch real data from API
  const { data: eventsData, isLoading: eventsLoading, refetch: refetchEvents } = useSecurityEvents();
  const { data: threatsData, isLoading: threatsLoading, refetch: refetchThreats } = useThreatDetections();

  const events = eventsData?.items || [];
  const threats = threatsData?.items || [];

  // Calculate metrics from real data
  const metrics: SecurityMetrics = calculateMetrics(events);

  function calculateMetrics(events: SecurityEvent[]): SecurityMetrics {
    const now = Date.now();
    const dayAgo = now - 24 * 60 * 60 * 1000;
    const recentEvents = events.filter(e => new Date(e.timestamp).getTime() > dayAgo);
    
    const blockedRequests = recentEvents.filter(e => e.type === 'block').length;
    const suspiciousActivity = recentEvents.filter(e => e.type === 'threat' || e.severity === 'high').length;
    const rateLimitHits = recentEvents.filter(e => e.type === 'rate_limit').length;
    const failedAuthentications = recentEvents.filter(e => e.description?.includes('authentication')).length;
    const activeThreats = events.filter(e => !e.resolved && e.severity === 'critical').length;
    
    // Calculate threat level based on recent activity
    let threatLevel: 'low' | 'medium' | 'high' | 'critical' = 'low';
    if (activeThreats > 5 || suspiciousActivity > 20) threatLevel = 'critical';
    else if (activeThreats > 2 || suspiciousActivity > 10) threatLevel = 'high';
    else if (activeThreats > 0 || suspiciousActivity > 5) threatLevel = 'medium';
    
    // Calculate vulnerability score (0-100, where 100 is most secure)
    const vulnerabilityScore = Math.max(0, 100 - (activeThreats * 10) - (suspiciousActivity * 2));
    
    return {
      threatLevel,
      blockedRequests,
      suspiciousActivity,
      rateLimitHits,
      failedAuthentications,
      activeThreats,
      vulnerabilityScore,
      uptime: 99.97, // This would come from a different endpoint
    };
  }

  const handleRefresh = async () => {
    setIsLoading(true);
    try {
      await Promise.all([
        refetchEvents(),
        refetchThreats(),
      ]);
      notifications.show({
        title: 'Security Data Refreshed',
        message: 'All security metrics have been updated',
        color: 'green',
      });
    } catch (_error) {
      notifications.show({
        title: 'Refresh Failed',
        message: 'Failed to refresh security data',
        color: 'red',
      });
    } finally {
      setIsLoading(false);
    }
  };

  const handleViewEvent = (event: SecurityEvent) => {
    setSelectedEvent(event);
    openModal();
  };

  const handleToggleThreat = async (threatId: string, enabled: boolean) => {
    try {
      const response = await apiFetch('/api/admin/security/threats', {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          threatId,
          action: enabled ? 'enable' : 'disable',
        }),
      });

      if (!response.ok) {
        throw new Error('Failed to update threat detection');
      }

      await refetchThreats();
      
      notifications.show({
        title: 'Threat Detection Updated',
        message: `Threat detection ${enabled ? 'enabled' : 'disabled'}`,
        color: 'blue',
      });
    } catch (_error) {
      notifications.show({
        title: 'Update Failed',
        message: 'Failed to update threat detection settings',
        color: 'red',
      });
    }
  };

  const getThreatLevelColor = (level: string) => {
    switch (level) {
      case 'low': return 'green';
      case 'medium': return 'yellow';
      case 'high': return 'orange';
      case 'critical': return 'red';
      default: return 'gray';
    }
  };

  const getSeverityColor = (severity: string) => {
    switch (severity) {
      case 'low': return 'blue';
      case 'medium': return 'yellow';
      case 'high': return 'orange';
      case 'critical': return 'red';
      default: return 'gray';
    }
  };

  const getEventTypeIcon = (type: string) => {
    switch (type) {
      case 'block': return IconBan;
      case 'threat': return IconShieldX;
      case 'auth_failure': return IconLock;
      case 'rate_limit': return IconClock;
      case 'vulnerability': return IconBug;
      default: return IconAlertTriangle;
    }
  };

  return (
    <Stack gap="xl">
      <Group justify="space-between">
        <div>
          <Title order={1}>Security Dashboard</Title>
          <Text c="dimmed">Monitor security threats and system protection status</Text>
        </div>

        <Group>
          <Switch
            label="Auto-refresh"
            checked={autoRefresh}
            onChange={(event) => setAutoRefresh(event.currentTarget.checked)}
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
            variant="light"
            leftSection={<IconSettings size={16} />}
            onClick={openSettings}
          >
            Settings
          </Button>
        </Group>
      </Group>

      {/* Security Overview */}
      {metrics && (
        <SimpleGrid cols={{ base: 1, sm: 2, md: 4 }} spacing="lg">
          <Card p="md" withBorder>
            <Group justify="space-between" mb="xs">
              <Text size="xs" tt="uppercase" fw={700} c="dimmed">
                Threat Level
              </Text>
              <ThemeIcon size="sm" variant="light" color={getThreatLevelColor(metrics.threatLevel)}>
                <IconShield size={16} />
              </ThemeIcon>
            </Group>
            <Badge color={getThreatLevelColor(metrics.threatLevel)} size="lg" variant="light">
              {metrics.threatLevel.toUpperCase()}
            </Badge>
          </Card>

          <Card p="md" withBorder>
            <Group justify="space-between" mb="xs">
              <Text size="xs" tt="uppercase" fw={700} c="dimmed">
                Blocked Requests
              </Text>
              <ThemeIcon size="sm" variant="light" color="red">
                <IconBan size={16} />
              </ThemeIcon>
            </Group>
            <Text fw={700} size="xl">
              {metrics.blockedRequests.toLocaleString()}
            </Text>
            <Text size="xs" c="dimmed">
              Last 24 hours
            </Text>
          </Card>

          <Card p="md" withBorder>
            <Group justify="space-between" mb="xs">
              <Text size="xs" tt="uppercase" fw={700} c="dimmed">
                Active Threats
              </Text>
              <ThemeIcon size="sm" variant="light" color="orange">
                <IconAlertTriangle size={16} />
              </ThemeIcon>
            </Group>
            <Text fw={700} size="xl">
              {metrics.activeThreats}
            </Text>
          </Card>

          <Card p="md" withBorder>
            <Group justify="space-between" mb="xs">
              <Text size="xs" tt="uppercase" fw={700} c="dimmed">
                System Uptime
              </Text>
              <ThemeIcon size="sm" variant="light" color="green">
                <IconActivity size={16} />
              </ThemeIcon>
            </Group>
            <Text fw={700} size="xl">
              {metrics.uptime}%
            </Text>
          </Card>
        </SimpleGrid>
      )}

      {/* Security Status Alert */}
      {metrics && metrics.threatLevel !== 'low' && (
        <Alert
          icon={<IconAlertTriangle size={16} />}
          color={getThreatLevelColor(metrics.threatLevel)}
          title={`${metrics.threatLevel.toUpperCase()} Threat Level Detected`}
        >
          <Text size="sm">
            Your system is currently under {metrics.threatLevel} threat level. 
            Review recent security events and consider additional protective measures.
          </Text>
        </Alert>
      )}

      <Tabs defaultValue="events">
        <Tabs.List>
          <Tabs.Tab value="events" leftSection={<IconAlertTriangle size={16} />}>
            Security Events
          </Tabs.Tab>
          <Tabs.Tab value="threats" leftSection={<IconShieldCheck size={16} />}>
            Threat Detection
          </Tabs.Tab>
          <Tabs.Tab value="metrics" leftSection={<IconActivity size={16} />}>
            Security Metrics
          </Tabs.Tab>
        </Tabs.List>

        <Tabs.Panel value="events" pt="md">
          <Card>
            <div style={{ position: 'relative' }}>
              <LoadingOverlay visible={isLoading || eventsLoading || threatsLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
              
              <Table striped highlightOnHover>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Timestamp</Table.Th>
                    <Table.Th>Type</Table.Th>
                    <Table.Th>Severity</Table.Th>
                    <Table.Th>IP Address</Table.Th>
                    <Table.Th>Description</Table.Th>
                    <Table.Th>Status</Table.Th>
                    <Table.Th>Actions</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {events.map((event: SecurityEvent) => {
                    const TypeIcon = getEventTypeIcon(event.type);
                    return (
                      <Table.Tr key={event.id}>
                        <Table.Td>
                          <Text size="xs">
                            {new Date(event.timestamp).toLocaleString()}
                          </Text>
                        </Table.Td>
                        <Table.Td>
                          <Group gap="xs">
                            <TypeIcon size={16} />
                            <Text size="sm" tt="capitalize">{event.type.replace('_', ' ')}</Text>
                          </Group>
                        </Table.Td>
                        <Table.Td>
                          <Badge color={getSeverityColor(event.severity)} variant="light">
                            {event.severity.toUpperCase()}
                          </Badge>
                        </Table.Td>
                        <Table.Td>
                          <Text size="sm">{event.ipAddress}</Text>
                        </Table.Td>
                        <Table.Td>
                          <Text size="sm" style={{ maxWidth: 200 }} truncate>
                            {event.description}
                          </Text>
                        </Table.Td>
                        <Table.Td>
                          <Badge color={event.resolved ? 'green' : 'red'} variant="light">
                            {event.resolved ? 'Resolved' : 'Active'}
                          </Badge>
                        </Table.Td>
                        <Table.Td>
                          <Tooltip label="View details">
                            <ActionIcon
                              variant="subtle"
                              size="sm"
                              onClick={() => handleViewEvent(event)}
                            >
                              <IconEye size={16} />
                            </ActionIcon>
                          </Tooltip>
                        </Table.Td>
                      </Table.Tr>
                    );
                  })}
                </Table.Tbody>
              </Table>

              {events.length === 0 && !isLoading && (
                <Text c="dimmed" ta="center" py="xl">
                  No security events found. Your system is secure.
                </Text>
              )}
            </div>
          </Card>
        </Tabs.Panel>

        <Tabs.Panel value="threats" pt="md">
          <SimpleGrid cols={{ base: 1, md: 2 }} spacing="lg">
            {threats.map((threat: ThreatDetection) => (
              <Card key={threat.id} withBorder>
                <Stack gap="md">
                  <Group justify="space-between">
                    <div>
                      <Text fw={600}>{threat.name}</Text>
                      <Text size="sm" c="dimmed">{threat.description}</Text>
                    </div>
                    <Switch
                      checked={threat.isEnabled}
                      onChange={(event) => handleToggleThreat(threat.id, event.currentTarget.checked)}
                    />
                  </Group>

                  <div>
                    <Group justify="space-between" mb="xs">
                      <Text size="sm">Sensitivity</Text>
                      <Text size="sm">{threat.sensitivity}%</Text>
                    </Group>
                    <Progress value={threat.sensitivity} size="sm" />
                  </div>

                  <Group grow>
                    <div>
                      <Text size="xs" c="dimmed">Triggers</Text>
                      <Text fw={500}>{threat.triggerCount}</Text>
                    </div>
                    <div>
                      <Text size="xs" c="dimmed">Last Triggered</Text>
                      <Text size="xs">
                        {threat.lastTriggered ? 
                          new Date(threat.lastTriggered).toLocaleDateString() : 
                          'Never'
                        }
                      </Text>
                    </div>
                  </Group>
                </Stack>
              </Card>
            ))}
          </SimpleGrid>
        </Tabs.Panel>

        <Tabs.Panel value="metrics" pt="md">
          <SimpleGrid cols={{ base: 1, md: 2 }} spacing="lg">
            <Card withBorder>
              <Card.Section p="md" withBorder>
                <Text fw={600}>Security Metrics</Text>
              </Card.Section>
              <Card.Section p="md">
                <Stack gap="md">
                  <Group justify="space-between">
                    <Text size="sm">Failed Authentications</Text>
                    <Badge color="red" variant="light">
                      {metrics?.failedAuthentications}
                    </Badge>
                  </Group>
                  <Group justify="space-between">
                    <Text size="sm">Rate Limit Hits</Text>
                    <Badge color="orange" variant="light">
                      {metrics?.rateLimitHits}
                    </Badge>
                  </Group>
                  <Group justify="space-between">
                    <Text size="sm">Suspicious Activity</Text>
                    <Badge color="yellow" variant="light">
                      {metrics?.suspiciousActivity}
                    </Badge>
                  </Group>
                  <Group justify="space-between">
                    <Text size="sm">Vulnerability Score</Text>
                    <Group gap="xs">
                      <Text size="sm">{metrics?.vulnerabilityScore}/100</Text>
                      <Progress
                        value={metrics?.vulnerabilityScore ?? 0}
                        size="sm"
                        w={100}
                        color={metrics && metrics.vulnerabilityScore > 80 ? 'green' : 'orange'}
                      />
                    </Group>
                  </Group>
                </Stack>
              </Card.Section>
            </Card>

            <Card withBorder>
              <Card.Section p="md" withBorder>
                <Text fw={600}>Recent Activity Timeline</Text>
              </Card.Section>
              <Card.Section p="md">
                <Timeline active={events.length} bulletSize={24} lineWidth={2}>
                  {events.slice(0, 5).map((event: SecurityEvent, _index: number) => {
                    const TypeIcon = getEventTypeIcon(event.type);
                    return (
                      <Timeline.Item
                        key={event.id}
                        bullet={<TypeIcon size={12} />}
                        title={event.description}
                        color={getSeverityColor(event.severity)}
                      >
                        <Text c="dimmed" size="sm">
                          {new Date(event.timestamp).toLocaleString()}
                        </Text>
                        <Text size="xs" c="dimmed">
                          {event.ipAddress} - {event.action}
                        </Text>
                      </Timeline.Item>
                    );
                  })}
                </Timeline>
              </Card.Section>
            </Card>
          </SimpleGrid>
        </Tabs.Panel>
      </Tabs>

      {/* Event Details Modal */}
      <Modal
        opened={modalOpened}
        onClose={closeModal}
        title="Security Event Details"
        size="lg"
      >
        {selectedEvent && (
          <Stack gap="md">
            <Group>
              <Badge color={getSeverityColor(selectedEvent.severity)} size="lg">
                {selectedEvent.severity.toUpperCase()}
              </Badge>
              <Badge color={selectedEvent.resolved ? 'green' : 'red'} variant="light">
                {selectedEvent.resolved ? 'Resolved' : 'Active'}
              </Badge>
            </Group>

            <div>
              <Text fw={500} mb="xs">Event Details</Text>
              <Text size="sm" c="dimmed">{selectedEvent.description}</Text>
            </div>

            <div>
              <Text fw={500} mb="xs">Technical Details</Text>
              <Code block>{selectedEvent.details}</Code>
            </div>

            <Group grow>
              <div>
                <Text size="sm" fw={500}>IP Address</Text>
                <Text size="sm" c="dimmed">{selectedEvent.ipAddress}</Text>
              </div>
              <div>
                <Text size="sm" fw={500}>Timestamp</Text>
                <Text size="sm" c="dimmed">
                  {new Date(selectedEvent.timestamp).toLocaleString()}
                </Text>
              </div>
            </Group>

            <div>
              <Text fw={500} mb="xs">Action Taken</Text>
              <Text size="sm" c="dimmed">{selectedEvent.action}</Text>
            </div>
          </Stack>
        )}
      </Modal>

      {/* Settings Modal */}
      <Modal
        opened={settingsOpened}
        onClose={closeSettings}
        title="Security Settings"
        size="md"
      >
        <Stack gap="md">
          <div>
            <Text fw={500} mb="xs">Auto-refresh Interval</Text>
            <Select
              data={[
                { value: '30', label: '30 seconds' },
                { value: '60', label: '1 minute' },
                { value: '300', label: '5 minutes' },
                { value: '0', label: 'Disabled' },
              ]}
              defaultValue="60"
            />
          </div>

          <div>
            <Text fw={500} mb="xs">Threat Detection Sensitivity</Text>
            <NumberInput
              min={1}
              max={100}
              defaultValue={80}
              description="Higher values increase detection sensitivity"
            />
          </div>

          <Switch
            label="Email Notifications"
            description="Send email alerts for critical security events"
            defaultChecked
          />

          <Switch
            label="Real-time Monitoring"
            description="Enable real-time threat monitoring"
            defaultChecked
          />

          <Group justify="flex-end" mt="md">
            <Button variant="light" onClick={closeSettings}>
              Cancel
            </Button>
            <Button>
              Save Settings
            </Button>
          </Group>
        </Stack>
      </Modal>
    </Stack>
  );
}