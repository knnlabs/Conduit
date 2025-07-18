'use client';

import { Group, Text } from '@mantine/core';
import { useBackendHealth } from '@/hooks/useBackendHealth';
import { ConnectionStatus } from '@/types/navigation';
import { CoreApiStatusIndicator } from './CoreApiStatusIndicator';
import { StatusIndicator } from '@/components/common/StatusIndicator';

interface ConnectionItemProps {
  label: string;
  status: ConnectionStatus['coreApi'] | ConnectionStatus['signalR'];
}

function ConnectionItem({ label, status }: ConnectionItemProps) {
  // Map connection status to our system status types
  const mapToSystemStatus = (status: ConnectionStatus['coreApi'] | ConnectionStatus['signalR']) => {
    switch (status) {
      case 'connected': return 'healthy';
      case 'connecting': return 'connecting';
      case 'reconnecting': return 'connecting';
      case 'error': return 'error';
      default: return 'disconnected';
    }
  };

  return (
    <StatusIndicator
      status={mapToSystemStatus(status)}
      variant="icon"
      size="sm"
      description={`${label} status`}
      animate={status === 'connecting' || status === 'reconnecting'}
    />
  );
}

export function ConnectionIndicator() {
  const { healthStatus } = useBackendHealth();

  // Convert health status to connection status
  const getConnectionStatus = (healthStatus: string): ConnectionStatus['coreApi'] => {
    switch (healthStatus) {
      case 'healthy': return 'connected';
      case 'degraded': return 'error';
      case 'unavailable': return 'disconnected';
      default: return 'disconnected';
    }
  };

  return (
    <Group gap="xs">
      <CoreApiStatusIndicator
        status={healthStatus.coreApi}
        message={healthStatus.coreApiMessage}
        checks={healthStatus.coreApiChecks}
      />
      
      <ConnectionItem
        label="Admin API"
        status={getConnectionStatus(healthStatus.adminApi)}
      />
      
      <Text size="xs" c="dimmed" ml="xs">
        Last check: {healthStatus.lastChecked.toLocaleTimeString()}
      </Text>
    </Group>
  );
}