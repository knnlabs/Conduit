'use client';

import { Group, Indicator, Text, Tooltip, ThemeIcon } from '@mantine/core';
import { IconServer, IconDatabase, IconNetwork } from '@tabler/icons-react';
import { useConnectionStore } from '@/stores/useConnectionStore';
import { useBackendHealth } from '@/hooks/useBackendHealth';
import { ConnectionStatus } from '@/types/navigation';

const getStatusColor = (status: ConnectionStatus['coreApi'] | ConnectionStatus['signalR']) => {
  switch (status) {
    case 'connected':
      return 'green';
    case 'connecting':
    case 'reconnecting':
      return 'yellow';
    case 'error':
      return 'red';
    default:
      return 'gray';
  }
};

const getStatusText = (status: ConnectionStatus['coreApi'] | ConnectionStatus['signalR']) => {
  switch (status) {
    case 'connected':
      return 'Connected';
    case 'connecting':
      return 'Connecting...';
    case 'reconnecting':
      return 'Reconnecting...';
    case 'error':
      return 'Connection Error';
    default:
      return 'Disconnected';
  }
};

interface ConnectionItemProps {
  label: string;
  status: ConnectionStatus['coreApi'] | ConnectionStatus['signalR'];
  icon: React.ComponentType<{ size?: number }>;
}

function ConnectionItem({ label, status, icon: Icon }: ConnectionItemProps) {
  const color = getStatusColor(status);
  const statusText = getStatusText(status);

  return (
    <Tooltip label={`${label}: ${statusText}`} position="bottom">
      <Indicator 
        color={color} 
        size={8} 
        position="top-end"
        processing={status === 'connecting' || status === 'reconnecting'}
      >
        <ThemeIcon 
          size="sm" 
          variant="light" 
          color={color}
        >
          <Icon size={14} />
        </ThemeIcon>
      </Indicator>
    </Tooltip>
  );
}

export function ConnectionIndicator() {
  const { status } = useConnectionStore();
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
      <ConnectionItem
        label="Core API"
        status={getConnectionStatus(healthStatus.coreApi)}
        icon={IconServer}
      />
      
      <ConnectionItem
        label="Admin API"
        status={getConnectionStatus(healthStatus.adminApi)}
        icon={IconDatabase}
      />
      
      <ConnectionItem
        label="SignalR"
        status={status.signalR}
        icon={IconNetwork}
      />
      
      <Text size="xs" c="dimmed" ml="xs">
        Last check: {healthStatus.lastChecked.toLocaleTimeString()}
      </Text>
    </Group>
  );
}