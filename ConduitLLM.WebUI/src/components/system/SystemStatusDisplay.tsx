'use client';

import {
  Card,
  Group,
  Stack,
  Text,
  Grid,
  Alert,
  Divider,
  ActionIcon,
  Box,
  Progress,
} from '@mantine/core';
import {
  IconRefresh,
  IconAlertTriangle,
  IconAlertCircle,
  IconActivity,
  IconServer,
  IconShield,
  IconWifi,
} from '@tabler/icons-react';
import { useSystemStatus } from '@/hooks/useSystemStatus';
import { StatusIndicator, CompositeStatusIndicator } from '@/components/common/StatusIndicator';
import { TimeDisplay } from '@/components/common/TimeDisplay';

export interface SystemStatusDisplayProps {
  variant?: 'compact' | 'detailed' | 'dashboard';
  showRefresh?: boolean;
  showAlerts?: boolean;
  showComponents?: boolean;
  showMetrics?: boolean;
  className?: string;
  testId?: string;
}

export function SystemStatusDisplay({
  variant = 'compact',
  showRefresh = true,
  showAlerts = true,
  showComponents = true,
  showMetrics = false,
  className,
  testId,
}: SystemStatusDisplayProps) {
  const {
    systemStatus,
    isLoading,
    error,
    isSystemHealthy,
    getSystemAlerts,
    getComponentStatus,
    refreshStatus,
  } = useSystemStatus();

  const alerts = getSystemAlerts();

  if (error) {
    return (
      <Alert
        icon={<IconAlertCircle size={16} />}
        title="Status Check Failed"
        color="red"
        className={className}
        data-testid={testId}
      >
        Unable to retrieve system status: {error.message}
      </Alert>
    );
  }

  if (!systemStatus && isLoading) {
    return (
      <Card withBorder className={className} data-testid={testId}>
        <Group>
          <StatusIndicator status="connecting" variant="icon" animate />
          <Text>Loading system status...</Text>
        </Group>
      </Card>
    );
  }

  if (variant === 'compact') {
    return (
      <Group gap="md" className={className} data-testid={testId}>
        <StatusIndicator
          status={systemStatus?.overall.type ?? 'unknown'}
          variant="badge"
          label={isSystemHealthy() ? 'All Systems Operational' : 'System Issues Detected'}
        />
        {showRefresh && (
          <ActionIcon
            size="sm"
            variant="subtle"
            onClick={() => void refreshStatus()}
            loading={isLoading}
            title="Refresh status"
          >
            <IconRefresh size={14} />
          </ActionIcon>
        )}
        {systemStatus?.lastUpdate && (
          <Text size="xs" c="dimmed">
            Updated <TimeDisplay date={systemStatus.lastUpdate} />
          </Text>
        )}
      </Group>
    );
  }

  if (variant === 'dashboard') {
    return (
      <Grid className={className} data-testid={testId}>
        <Grid.Col span={12}>
          <Card withBorder>
            <Group justify="space-between" mb="md">
              <Group>
                <IconActivity size={20} />
                <Text fw={500}>System Status</Text>
              </Group>
              {showRefresh && (
                <ActionIcon
                  variant="subtle"
                  onClick={() => void refreshStatus()}
                  loading={isLoading}
                  title="Refresh status"
                >
                  <IconRefresh size={16} />
                </ActionIcon>
              )}
            </Group>

            <CompositeStatusIndicator
              primary={systemStatus?.overall.type ?? 'unknown'}
              label="Overall System Health"
              description={systemStatus?.overall.description}
              variant="horizontal"
              size="md"
            />

            {showAlerts && alerts.length > 0 && (
              <Stack gap="xs" mt="md">
                {alerts.map((alert) => (
                  <Alert
                    key={`alert-${alert.message}-${alert.type}`}
                    icon={alert.type === 'error' ? <IconAlertCircle size={16} /> : <IconAlertTriangle size={16} />}
                    color={alert.type === 'error' ? 'red' : 'orange'}
                    variant="light"
                  >
                    <Text size="sm" fw={500}>{alert.message}</Text>
                    {alert.details && (
                      <Text size="xs" c="dimmed">{alert.details}</Text>
                    )}
                  </Alert>
                ))}
              </Stack>
            )}
          </Card>
        </Grid.Col>

        {showComponents && (
          <Grid.Col span={12}>
            <Card withBorder>
              <Text fw={500} mb="md">Component Status</Text>
              <Stack gap="md">
                <CompositeStatusIndicator
                  primary={getComponentStatus('core').type}
                  label="Core API"
                  description="Primary API services"
                  variant="horizontal"
                />
                <CompositeStatusIndicator
                  primary={getComponentStatus('admin').type}
                  label="Admin API"
                  description="Administrative services"
                  variant="horizontal"
                />
                <CompositeStatusIndicator
                  primary={getComponentStatus('signalR').type}
                  label="Real-time Updates"
                  description="WebSocket connections"
                  variant="horizontal"
                />
              </Stack>
            </Card>
          </Grid.Col>
        )}

        {showMetrics && systemStatus && (
          <Grid.Col span={12}>
            <Card withBorder>
              <Text fw={500} mb="md">System Metrics</Text>
              <Stack gap="md">
                <Box>
                  <Group justify="space-between" mb={4}>
                    <Text size="sm">System Health</Text>
                    <Text size="sm" c={isSystemHealthy() ? 'green' : 'red'}>
                      {isSystemHealthy() ? '100%' : '75%'}
                    </Text>
                  </Group>
                  <Progress 
                    value={isSystemHealthy() ? 100 : 75} 
                    color={isSystemHealthy() ? 'green' : 'orange'}
                    size="sm"
                  />
                </Box>
                <Text size="xs" c="dimmed">
                  Last updated: <TimeDisplay date={systemStatus.lastUpdate} format="datetime" />
                </Text>
              </Stack>
            </Card>
          </Grid.Col>
        )}
      </Grid>
    );
  }

  // Detailed variant
  return (
    <Card withBorder className={className} data-testid={testId}>
      <Group justify="space-between" mb="md">
        <Group>
          <IconServer size={20} />
          <Text fw={500}>System Status</Text>
        </Group>
        {showRefresh && (
          <ActionIcon
            variant="subtle"
            onClick={() => void refreshStatus()}
            loading={isLoading}
            title="Refresh status"
          >
            <IconRefresh size={16} />
          </ActionIcon>
        )}
      </Group>

      <Stack gap="md">
        <CompositeStatusIndicator
          primary={systemStatus?.overall.type ?? 'unknown'}
          label="Overall Status"
          description={systemStatus?.overall.description}
          variant="horizontal"
          size="md"
        />

        {showAlerts && alerts.length > 0 && (
          <>
            <Divider />
            <Stack gap="xs">
              <Text size="sm" fw={500}>Active Alerts</Text>
              {alerts.map((alert) => (
                <Alert
                  key={`alert-${alert.message}-${alert.type}`}
                  icon={alert.type === 'error' ? <IconAlertCircle size={16} /> : <IconAlertTriangle size={16} />}
                  color={alert.type === 'error' ? 'red' : 'orange'}
                  variant="light"
                >
                  {alert.message}
                </Alert>
              ))}
            </Stack>
          </>
        )}

        {showComponents && (
          <>
            <Divider />
            <Stack gap="sm">
              <Text size="sm" fw={500}>Component Health</Text>
              
              <Group justify="space-between">
                <Group gap="xs">
                  <IconServer size={16} />
                  <Text size="sm">Core API</Text>
                </Group>
                <StatusIndicator
                  status={getComponentStatus('core').type}
                  variant="badge"
                  size="xs"
                />
              </Group>

              <Group justify="space-between">
                <Group gap="xs">
                  <IconShield size={16} />
                  <Text size="sm">Admin API</Text>
                </Group>
                <StatusIndicator
                  status={getComponentStatus('admin').type}
                  variant="badge"
                  size="xs"
                />
              </Group>

              <Group justify="space-between">
                <Group gap="xs">
                  <IconWifi size={16} />
                  <Text size="sm">Real-time</Text>
                </Group>
                <StatusIndicator
                  status={getComponentStatus('signalR').type}
                  variant="badge"
                  size="xs"
                />
              </Group>
            </Stack>
          </>
        )}

        {systemStatus?.lastUpdate && (
          <>
            <Divider />
            <Text size="xs" c="dimmed" ta="center">
              Last updated: {systemStatus.lastUpdate.toLocaleString()}
            </Text>
          </>
        )}
      </Stack>
    </Card>
  );
}

// Specialized components for specific use cases
export function HeaderStatusIndicator() {
  return (
    <SystemStatusDisplay
      variant="compact"
      showRefresh={false}
      showAlerts={false}
      showComponents={false}
    />
  );
}

export function DashboardStatusCard() {
  return (
    <SystemStatusDisplay
      variant="dashboard"
      showRefresh={true}
      showAlerts={true}
      showComponents={true}
      showMetrics={true}
    />
  );
}

export function SidebarStatusIndicator() {
  return (
    <SystemStatusDisplay
      variant="detailed"
      showRefresh={true}
      showAlerts={true}
      showComponents={true}
      showMetrics={false}
    />
  );
}