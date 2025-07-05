'use client';

import {
  Stack,
  Title,
  Text,
  Card,
  Grid,
  Group,
  Button,
  Table,
  ScrollArea,
  Badge,
  ThemeIcon,
  Progress,
  Select,
  TextInput,
  Tabs,
  RingProgress,
  Center,
  Tooltip,
  Code,
  LoadingOverlay,
} from '@mantine/core';
import {
  IconShield,
  IconAlertTriangle,
  IconLock,
  IconKey,
  IconRefresh,
  IconDownload,
  IconSearch,
  IconActivity,
  IconAlertCircle,
  IconCheck,
  IconX,
  IconUserX,
  IconBan,
  IconEye,
} from '@tabler/icons-react';
import { useState } from 'react';
// Removed unused DatePickerInput
import { notifications } from '@mantine/notifications';
import { 
  BarChart, 
  Bar, 
  PieChart, 
  Pie, 
  Cell, 
  XAxis, 
  YAxis, 
  CartesianGrid, 
  Tooltip as RechartsTooltip, 
  ResponsiveContainer, 
  Legend, 
  LineChart, 
  Line 
} from 'recharts';
import { formatters } from '@/lib/utils/formatters';
import { useSecurityEvents, useThreatAnalytics, useComplianceMetrics } from '@/hooks/api/useSecurityApi';
import { FeatureUnavailable } from '@/components/error/FeatureUnavailable';

// Type to severity mapping
const _getEventSeverity = (type: string): 'low' | 'medium' | 'high' | 'critical' => {
  switch (type) {
    case 'auth_failure':
    case 'rate_limit':
      return 'medium';
    case 'blocked_ip':
    case 'suspicious_activity':
      return 'high';
    default:
      return 'low';
  }
};

// Type to icon mapping
const getEventIcon = (type: string) => {
  switch (type) {
    case 'auth_failure':
      return IconUserX;
    case 'rate_limit':
      return IconAlertTriangle;
    case 'blocked_ip':
      return IconBan;
    case 'suspicious_activity':
      return IconActivity;
    default:
      return IconAlertCircle;
  }
};

// Severity to color mapping
const getSeverityColor = (severity: string) => {
  switch (severity) {
    case 'critical':
      return 'red';
    case 'high':
      return 'orange';
    case 'warning':
    case 'medium':
      return 'yellow';
    default:
      return 'blue';
  }
};

const COLORS = ['#3b82f6', '#10b981', '#f59e0b', '#ef4444', '#8b5cf6', '#ec4899'];

export default function SecurityMonitoringPage() {
  const [selectedTimeRange, setSelectedTimeRange] = useState<string>('24');
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedSeverity, setSelectedSeverity] = useState<string | null>(null);
  const [selectedType, setSelectedType] = useState<string | null>(null);

  // Fetch data using the security API hooks (must be called before any conditional returns)
  const { data: eventsData, isLoading: eventsLoading, refetch: refetchEvents } = useSecurityEvents(parseInt(selectedTimeRange));
  const { data: threatsData, isLoading: threatsLoading } = useThreatAnalytics();
  const { data: complianceData, isLoading: complianceLoading } = useComplianceMetrics();

  // Check if security monitoring feature is available
  const isFeatureAvailable = false; // Security monitoring is not yet implemented

  // Show feature unavailable message if not available
  if (!isFeatureAvailable) {
    return (
      <FeatureUnavailable 
        feature="security-event-reporting"
        title="Security Monitoring"
      />
    );
  }

  // Filter events based on search and filters
  const filteredEvents = eventsData?.events?.filter(event => {
    if (searchQuery && !event.source.toLowerCase().includes(searchQuery.toLowerCase()) && 
        !event.details.toLowerCase().includes(searchQuery.toLowerCase())) {
      return false;
    }
    if (selectedSeverity && event.severity !== selectedSeverity) {
      return false;
    }
    if (selectedType && event.type !== selectedType) {
      return false;
    }
    return true;
  }) || [];

  const handleRefresh = () => {
    refetchEvents();
    notifications.show({
      title: 'Refreshing Data',
      message: 'Security monitoring data is being updated',
      color: 'blue',
    });
  };

  const handleExportData = () => {
    notifications.show({
      title: 'Export Started',
      message: 'Preparing security report for download',
      color: 'blue',
    });
  };

  return (
    <Stack gap="md">
      <Group justify="space-between">
        <div>
          <Title order={1}>Security Monitoring</Title>
          <Text c="dimmed">Monitor security events, threats, and compliance status</Text>
        </div>
        <Group>
          <Select
            value={selectedTimeRange}
            onChange={(value) => setSelectedTimeRange(value || '24')}
            data={[
              { value: '1', label: 'Last Hour' },
              { value: '24', label: 'Last 24 Hours' },
              { value: '168', label: 'Last 7 Days' },
              { value: '720', label: 'Last 30 Days' },
            ]}
            w={150}
          />
          <Button
            variant="light"
            leftSection={<IconRefresh size={16} />}
            onClick={handleRefresh}
          >
            Refresh
          </Button>
          <Button
            leftSection={<IconDownload size={16} />}
            onClick={handleExportData}
          >
            Export Report
          </Button>
        </Group>
      </Group>

      {/* Security Overview Cards */}
      <Grid>
        <Grid.Col span={{ base: 12, md: 3 }}>
          <Card>
            <Group justify="space-between" mb="xs">
              <Text size="sm" c="dimmed">Total Events</Text>
              <ThemeIcon color="blue" variant="light" size="sm">
                <IconActivity size={16} />
              </ThemeIcon>
            </Group>
            <Text size="xl" fw={700}>
              {formatters.number(eventsData?.totalEvents || 0)}
            </Text>
            <Text size="xs" c="dimmed" mt="xs">
              Last {selectedTimeRange === '1' ? 'hour' : `${selectedTimeRange} hours`}
            </Text>
          </Card>
        </Grid.Col>

        <Grid.Col span={{ base: 12, md: 3 }}>
          <Card>
            <Group justify="space-between" mb="xs">
              <Text size="sm" c="dimmed">Active Threats</Text>
              <ThemeIcon color="red" variant="light" size="sm">
                <IconAlertTriangle size={16} />
              </ThemeIcon>
            </Group>
            <Text size="xl" fw={700}>
              {formatters.number(threatsData?.metrics.totalThreatsToday || 0)}
            </Text>
            <Text size="xs" c="dimmed" mt="xs">
              {threatsData?.metrics.uniqueThreatsToday || 0} unique sources
            </Text>
          </Card>
        </Grid.Col>

        <Grid.Col span={{ base: 12, md: 3 }}>
          <Card>
            <Group justify="space-between" mb="xs">
              <Text size="sm" c="dimmed">Blocked IPs</Text>
              <ThemeIcon color="orange" variant="light" size="sm">
                <IconBan size={16} />
              </ThemeIcon>
            </Group>
            <Text size="xl" fw={700}>
              {formatters.number(threatsData?.metrics.blockedIPs || 0)}
            </Text>
            <Text size="xs" c="dimmed" mt="xs">
              Active blacklist entries
            </Text>
          </Card>
        </Grid.Col>

        <Grid.Col span={{ base: 12, md: 3 }}>
          <Card>
            <Group justify="space-between" mb="xs">
              <Text size="sm" c="dimmed">Compliance Score</Text>
              <ThemeIcon color="green" variant="light" size="sm">
                <IconShield size={16} />
              </ThemeIcon>
            </Group>
            <Text size="xl" fw={700}>
              {complianceData?.complianceScore || 0}%
            </Text>
            <Progress value={complianceData?.complianceScore || 0} color="green" size="sm" mt="xs" />
          </Card>
        </Grid.Col>
      </Grid>

      <Tabs defaultValue="events">
        <Tabs.List>
          <Tabs.Tab value="events" leftSection={<IconAlertCircle size={16} />}>
            Security Events
          </Tabs.Tab>
          <Tabs.Tab value="threats" leftSection={<IconAlertTriangle size={16} />}>
            Threat Analytics
          </Tabs.Tab>
          <Tabs.Tab value="compliance" leftSection={<IconShield size={16} />}>
            Compliance
          </Tabs.Tab>
        </Tabs.List>

        <Tabs.Panel value="events" pt="md">
          <Stack gap="md">
            {/* Event Filters */}
            <Card withBorder>
              <Group>
                <TextInput
                  placeholder="Search events..."
                  leftSection={<IconSearch size={16} />}
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.currentTarget.value)}
                  style={{ flex: 1 }}
                />
                <Select
                  placeholder="Severity"
                  data={[
                    { value: 'warning', label: 'Warning' },
                    { value: 'high', label: 'High' },
                  ]}
                  value={selectedSeverity}
                  onChange={setSelectedSeverity}
                  clearable
                  w={150}
                />
                <Select
                  placeholder="Type"
                  data={[
                    { value: 'auth_failure', label: 'Auth Failure' },
                    { value: 'rate_limit', label: 'Rate Limit' },
                    { value: 'blocked_ip', label: 'Blocked IP' },
                    { value: 'suspicious_activity', label: 'Suspicious Activity' },
                  ]}
                  value={selectedType}
                  onChange={setSelectedType}
                  clearable
                  w={180}
                />
              </Group>
            </Card>

            {/* Events Table */}
            <Card withBorder>
              <LoadingOverlay visible={eventsLoading} />
              <ScrollArea h={400}>
                <Table striped highlightOnHover>
                  <Table.Thead>
                    <Table.Tr>
                      <Table.Th>Time</Table.Th>
                      <Table.Th>Type</Table.Th>
                      <Table.Th>Severity</Table.Th>
                      <Table.Th>Source</Table.Th>
                      <Table.Th>Details</Table.Th>
                      <Table.Th>Virtual Key</Table.Th>
                    </Table.Tr>
                  </Table.Thead>
                  <Table.Tbody>
                    {filteredEvents.map((event) => {
                      const Icon = getEventIcon(event.type);
                      return (
                        <Table.Tr key={`${event.timestamp}-${event.source}`}>
                          <Table.Td>
                            <Text size="sm">{formatters.date(event.timestamp)}</Text>
                          </Table.Td>
                          <Table.Td>
                            <Group gap="xs">
                              <ThemeIcon size="xs" variant="light" color={getSeverityColor(event.severity)}>
                                <Icon size={14} />
                              </ThemeIcon>
                              <Text size="sm">{event.type.replace(/_/g, ' ').replace(/\b\w/g, l => l.toUpperCase())}</Text>
                            </Group>
                          </Table.Td>
                          <Table.Td>
                            <Badge color={getSeverityColor(event.severity)} variant="light">
                              {event.severity}
                            </Badge>
                          </Table.Td>
                          <Table.Td>
                            <Tooltip label={event.source}>
                              <Code>{event.source}</Code>
                            </Tooltip>
                          </Table.Td>
                          <Table.Td>
                            <Text size="sm" lineClamp={1}>
                              {event.details}
                            </Text>
                          </Table.Td>
                          <Table.Td>
                            {event.virtualKeyId ? (
                              <Code>{event.virtualKeyId}</Code>
                            ) : (
                              <Text size="sm" c="dimmed">-</Text>
                            )}
                          </Table.Td>
                        </Table.Tr>
                      );
                    })}
                  </Table.Tbody>
                </Table>
              </ScrollArea>
            </Card>

            {/* Event Distribution Chart */}
            {eventsData?.eventsByType && eventsData.eventsByType.length > 0 && (
              <Card withBorder>
                <Title order={3} mb="md">Event Distribution</Title>
                <ResponsiveContainer width="100%" height={300}>
                  <PieChart>
                    <Pie
                      data={eventsData.eventsByType}
                      cx="50%"
                      cy="50%"
                      labelLine={false}
                      label={({ name, percent }) => `${name}: ${(percent * 100).toFixed(0)}%`}
                      outerRadius={80}
                      fill="#8884d8"
                      dataKey="count"
                      nameKey="type"
                    >
                      {eventsData.eventsByType.map((entry, index) => (
                        <Cell key={`cell-${index}`} fill={COLORS[index % COLORS.length]} />
                      ))}
                    </Pie>
                    <RechartsTooltip />
                    <Legend />
                  </PieChart>
                </ResponsiveContainer>
              </Card>
            )}
          </Stack>
        </Tabs.Panel>

        <Tabs.Panel value="threats" pt="md">
          <Stack gap="md">
            {/* Threat Trend Chart */}
            {threatsData?.threatTrend && threatsData.threatTrend.length > 0 && (
              <Card withBorder>
                <Title order={3} mb="md">Threat Activity Trend</Title>
                <ResponsiveContainer width="100%" height={300}>
                  <LineChart data={threatsData.threatTrend}>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis dataKey="date" />
                    <YAxis />
                    <RechartsTooltip />
                    <Legend />
                    <Line type="monotone" dataKey="threats" stroke="#ef4444" name="Threat Events" />
                  </LineChart>
                </ResponsiveContainer>
              </Card>
            )}

            {/* Top Threat Sources */}
            <Card withBorder>
              <Title order={3} mb="md">Top Threat Sources</Title>
              <LoadingOverlay visible={threatsLoading} />
              <ScrollArea h={300}>
                <Table>
                  <Table.Thead>
                    <Table.Tr>
                      <Table.Th>IP Address</Table.Th>
                      <Table.Th>Failed Attempts</Table.Th>
                      <Table.Th>Days Active</Table.Th>
                      <Table.Th>Risk Score</Table.Th>
                      <Table.Th>Last Seen</Table.Th>
                    </Table.Tr>
                  </Table.Thead>
                  <Table.Tbody>
                    {threatsData?.topThreats?.map((threat, index) => (
                      <Table.Tr key={index}>
                        <Table.Td>
                          <Code>{threat.ipAddress}</Code>
                        </Table.Td>
                        <Table.Td>{threat.totalFailures}</Table.Td>
                        <Table.Td>{threat.daysActive}</Table.Td>
                        <Table.Td>
                          <Badge color={threat.riskScore > 10 ? 'red' : threat.riskScore > 5 ? 'orange' : 'yellow'}>
                            {threat.riskScore.toFixed(1)}
                          </Badge>
                        </Table.Td>
                        <Table.Td>{formatters.date(threat.lastSeen)}</Table.Td>
                      </Table.Tr>
                    ))}
                  </Table.Tbody>
                </Table>
              </ScrollArea>
            </Card>

            {/* Threat Distribution by Type */}
            {threatsData?.threatDistribution && threatsData.threatDistribution.length > 0 && (
              <Card withBorder>
                <Title order={3} mb="md">Threats by Type</Title>
                <ResponsiveContainer width="100%" height={300}>
                  <BarChart data={threatsData.threatDistribution}>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis dataKey="type" />
                    <YAxis />
                    <RechartsTooltip />
                    <Legend />
                    <Bar dataKey="count" fill="#ef4444" name="Count" />
                    <Bar dataKey="uniqueIPs" fill="#f59e0b" name="Unique IPs" />
                  </BarChart>
                </ResponsiveContainer>
              </Card>
            )}
          </Stack>
        </Tabs.Panel>

        <Tabs.Panel value="compliance" pt="md">
          <Stack gap="md">
            {/* Compliance Overview */}
            <Card withBorder>
              <LoadingOverlay visible={complianceLoading} />
              <Title order={3} mb="md">Compliance Overview</Title>
              <Center>
                <RingProgress
                  size={200}
                  thickness={20}
                  sections={[{ value: complianceData?.complianceScore || 0, color: 'green' }]}
                  label={
                    <Center>
                      <Stack gap={0} align="center">
                        <Text size="xl" fw={700}>{complianceData?.complianceScore || 0}%</Text>
                        <Text size="sm" c="dimmed">Compliance Score</Text>
                      </Stack>
                    </Center>
                  }
                />
              </Center>
            </Card>

            {/* Compliance Details */}
            <Grid>
              <Grid.Col span={{ base: 12, md: 4 }}>
                <Card withBorder>
                  <Group justify="space-between" mb="md">
                    <Text fw={500}>Data Protection</Text>
                    <ThemeIcon color="blue" variant="light">
                      <IconLock size={20} />
                    </ThemeIcon>
                  </Group>
                  <Stack gap="xs">
                    <Group justify="space-between">
                      <Text size="sm">Encrypted Keys</Text>
                      <Badge color="green">{complianceData?.dataProtection.encryptedKeys || 0}</Badge>
                    </Group>
                    <Group justify="space-between">
                      <Text size="sm">Secure Endpoints</Text>
                      <ThemeIcon size="xs" color={complianceData?.dataProtection.secureEndpoints ? 'green' : 'red'}>
                        {complianceData?.dataProtection.secureEndpoints ? <IconCheck size={14} /> : <IconX size={14} />}
                      </ThemeIcon>
                    </Group>
                    <Group justify="space-between">
                      <Text size="sm">Data Retention</Text>
                      <Text size="sm">{complianceData?.dataProtection.dataRetentionDays || 0} days</Text>
                    </Group>
                    <Group justify="space-between">
                      <Text size="sm">Last Audit</Text>
                      <Text size="sm">{complianceData?.dataProtection.lastAudit ? formatters.date(complianceData.dataProtection.lastAudit) : 'Never'}</Text>
                    </Group>
                  </Stack>
                </Card>
              </Grid.Col>

              <Grid.Col span={{ base: 12, md: 4 }}>
                <Card withBorder>
                  <Group justify="space-between" mb="md">
                    <Text fw={500}>Access Control</Text>
                    <ThemeIcon color="orange" variant="light">
                      <IconKey size={20} />
                    </ThemeIcon>
                  </Group>
                  <Stack gap="xs">
                    <Group justify="space-between">
                      <Text size="sm">Active Keys</Text>
                      <Badge color="blue">{complianceData?.accessControl.activeKeys || 0}</Badge>
                    </Group>
                    <Group justify="space-between">
                      <Text size="sm">Keys with Budgets</Text>
                      <Badge color="green">{complianceData?.accessControl.keysWithBudgets || 0}</Badge>
                    </Group>
                    <Group justify="space-between">
                      <Text size="sm">IP Whitelist</Text>
                      <ThemeIcon size="xs" color={complianceData?.accessControl.ipWhitelistEnabled ? 'green' : 'orange'}>
                        {complianceData?.accessControl.ipWhitelistEnabled ? <IconCheck size={14} /> : <IconX size={14} />}
                      </ThemeIcon>
                    </Group>
                    <Group justify="space-between">
                      <Text size="sm">Rate Limiting</Text>
                      <ThemeIcon size="xs" color={complianceData?.accessControl.rateLimitingEnabled ? 'green' : 'red'}>
                        {complianceData?.accessControl.rateLimitingEnabled ? <IconCheck size={14} /> : <IconX size={14} />}
                      </ThemeIcon>
                    </Group>
                  </Stack>
                </Card>
              </Grid.Col>

              <Grid.Col span={{ base: 12, md: 4 }}>
                <Card withBorder>
                  <Group justify="space-between" mb="md">
                    <Text fw={500}>Monitoring</Text>
                    <ThemeIcon color="green" variant="light">
                      <IconEye size={20} />
                    </ThemeIcon>
                  </Group>
                  <Stack gap="xs">
                    <Group justify="space-between">
                      <Text size="sm">Log Retention</Text>
                      <Text size="sm">{complianceData?.monitoring.logRetentionDays || 0} days</Text>
                    </Group>
                    <Group justify="space-between">
                      <Text size="sm">Request Logging</Text>
                      <ThemeIcon size="xs" color={complianceData?.monitoring.requestLoggingEnabled ? 'green' : 'red'}>
                        {complianceData?.monitoring.requestLoggingEnabled ? <IconCheck size={14} /> : <IconX size={14} />}
                      </ThemeIcon>
                    </Group>
                    <Group justify="space-between">
                      <Text size="sm">Security Alerts</Text>
                      <ThemeIcon size="xs" color={complianceData?.monitoring.securityAlertsEnabled ? 'green' : 'orange'}>
                        {complianceData?.monitoring.securityAlertsEnabled ? <IconCheck size={14} /> : <IconX size={14} />}
                      </ThemeIcon>
                    </Group>
                    <Group justify="space-between">
                      <Text size="sm">Last Review</Text>
                      <Text size="sm">{complianceData?.monitoring.lastSecurityReview ? formatters.date(complianceData.monitoring.lastSecurityReview) : 'Never'}</Text>
                    </Group>
                  </Stack>
                </Card>
              </Grid.Col>
            </Grid>
          </Stack>
        </Tabs.Panel>
      </Tabs>
    </Stack>
  );
}