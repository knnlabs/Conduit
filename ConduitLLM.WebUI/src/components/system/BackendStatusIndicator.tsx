'use client';

import {
  Alert,
  Badge,
  Group,
  Text,
  ActionIcon,
  Tooltip,
  Stack,
  // Removed unused Progress, Button imports
  Collapse,
  Card,
} from '@mantine/core';
import {
  IconAlertCircle,
  IconCheck,
  IconExclamationMark,
  IconRefresh,
  IconChevronDown,
  IconChevronUp,
  // Removed unused IconWifi import
  IconWifiOff,
} from '@tabler/icons-react';
import { useState } from 'react';
import { useBackendHealth } from '@/hooks/useBackendHealth';
import { BackendErrorHandler } from '@/lib/errors/BackendErrorHandler';

interface BackendStatusIndicatorProps {
  compact?: boolean;
  showDetails?: boolean;
}

export function BackendStatusIndicator({ 
  compact = false, 
  showDetails = false 
}: BackendStatusIndicatorProps) {
  const [expanded, setExpanded] = useState(false);
  const { healthStatus, isHealthy, isDegraded, isUnavailable, adminError, coreError, refetch } = useBackendHealth();

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'healthy': return 'green';
      case 'degraded': return 'orange';
      case 'unavailable': return 'red';
      default: return 'gray';
    }
  };

  const getStatusIcon = () => {
    if (isHealthy) return <IconCheck size={16} />;
    if (isDegraded) return <IconExclamationMark size={16} />;
    return <IconWifiOff size={16} />;
  };

  const getOverallStatus = () => {
    if (isHealthy) return 'All systems operational';
    if (isDegraded) return 'Some services degraded';
    return 'Services unavailable';
  };

  const getOverallColor = () => {
    if (isHealthy) return 'green';
    if (isDegraded) return 'orange';
    return 'red';
  };

  if (compact) {
    return (
      <Group gap="xs">
        <Badge 
          size="sm" 
          color={getOverallColor()} 
          variant="light"
          leftSection={getStatusIcon()}
        >
          {isHealthy ? 'Online' : isDegraded ? 'Degraded' : 'Offline'}
        </Badge>
        {!isHealthy && (
          <Tooltip label="Refresh status">
            <ActionIcon size="sm" variant="subtle" onClick={refetch}>
              <IconRefresh size={14} />
            </ActionIcon>
          </Tooltip>
        )}
      </Group>
    );
  }

  return (
    <Card withBorder>
      <Stack gap="md">
        {/* Overall Status */}
        <Group justify="space-between">
          <Group gap="sm">
            <Badge 
              size="md" 
              color={getOverallColor()} 
              variant="light"
              leftSection={getStatusIcon()}
            >
              System Status
            </Badge>
            <Text size="sm" fw={500}>{getOverallStatus()}</Text>
          </Group>
          
          <Group gap="xs">
            <Tooltip label="Refresh status">
              <ActionIcon variant="subtle" onClick={refetch}>
                <IconRefresh size={16} />
              </ActionIcon>
            </Tooltip>
            {showDetails && (
              <ActionIcon 
                variant="subtle" 
                onClick={() => setExpanded(!expanded)}
              >
                {expanded ? <IconChevronUp size={16} /> : <IconChevronDown size={16} />}
              </ActionIcon>
            )}
          </Group>
        </Group>

        {/* Service Status */}
        <Group gap="md">
          <Group gap="xs">
            <Text size="xs" c="dimmed">Admin API:</Text>
            <Badge 
              size="xs" 
              color={getStatusColor(healthStatus.adminApi)}
              variant="light"
            >
              {healthStatus.adminApi}
            </Badge>
          </Group>
          
          <Group gap="xs">
            <Text size="xs" c="dimmed">Core API:</Text>
            <Badge 
              size="xs" 
              color={getStatusColor(healthStatus.coreApi)}
              variant="light"
            >
              {healthStatus.coreApi}
            </Badge>
          </Group>
        </Group>

        {/* Error Messages */}
        {(adminError || coreError) && (
          <Stack gap="xs">
            {adminError && (
              <Alert 
                icon={<IconAlertCircle size={16} />} 
                color="red" 
                title="Admin API Issue"
              >
                <Text size="xs">
                  {BackendErrorHandler.getUserFriendlyMessage(adminError)}
                </Text>
                <Text size="xs" c="dimmed" mt="xs">
                  {BackendErrorHandler.getActionableMessage(adminError)}
                </Text>
              </Alert>
            )}
            
            {coreError && (
              <Alert 
                icon={<IconAlertCircle size={16} />} 
                color="red" 
                title="Core API Issue"
              >
                <Text size="xs">
                  {BackendErrorHandler.getUserFriendlyMessage(coreError)}
                </Text>
                <Text size="xs" c="dimmed" mt="xs">
                  {BackendErrorHandler.getActionableMessage(coreError)}
                </Text>
              </Alert>
            )}
          </Stack>
        )}

        {/* Graceful Degradation Messages */}
        {isUnavailable && (
          <Alert 
            icon={<IconWifiOff size={16} />} 
            color="orange" 
            title="Limited Functionality"
          >
            <Text size="xs" mb="xs">
              Some features may not be available while we restore full service.
            </Text>
            <Text size="xs" c="dimmed">
              • Virtual key management may be unavailable<br/>
              • Real-time features may not work<br/>
              • New AI requests may fail
            </Text>
          </Alert>
        )}

        {/* Detailed Status */}
        {showDetails && (
          <Collapse in={expanded}>
            <Stack gap="xs">
              <Text size="xs" fw={500}>Service Details</Text>
              
              {healthStatus.adminApiDetails && (
                <Card withBorder p="xs">
                  <Text size="xs" fw={500} mb="xs">Admin API</Text>
                  <Text size="xs" c="dimmed">
                    Status: {healthStatus.adminApiDetails.status}<br/>
                    Last checked: {healthStatus.lastChecked.toLocaleTimeString()}
                  </Text>
                </Card>
              )}
              
              {healthStatus.coreApiDetails && (
                <Card withBorder p="xs">
                  <Text size="xs" fw={500} mb="xs">Core API</Text>
                  <Text size="xs" c="dimmed">
                    Status: {healthStatus.coreApiDetails.status}<br/>
                    Last checked: {healthStatus.lastChecked.toLocaleTimeString()}
                  </Text>
                </Card>
              )}
            </Stack>
          </Collapse>
        )}

        <Text size="xs" c="dimmed" ta="center">
          Last updated: {healthStatus.lastChecked.toLocaleTimeString()}
        </Text>
      </Stack>
    </Card>
  );
}

export default BackendStatusIndicator;