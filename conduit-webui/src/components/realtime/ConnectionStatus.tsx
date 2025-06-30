'use client';

import { Badge, Group, Tooltip, Text } from '@mantine/core';
import { IconCircle, IconCircleOff, IconRefresh } from '@tabler/icons-react';
import { useConnectionStore } from '@/stores/useConnectionStore';

export function ConnectionStatus() {
  const { status: connectionStatus } = useConnectionStore();
  const signalRStatus = connectionStatus.signalR;

  const getStatusInfo = () => {
    switch (signalRStatus) {
      case 'connected':
        return {
          color: 'green',
          icon: <IconCircle size={8} fill="currentColor" />,
          label: 'Connected',
          tooltip: 'Real-time updates active',
        };
      case 'connecting':
        return {
          color: 'blue',
          icon: <IconRefresh size={8} className="animate-spin" />,
          label: 'Connecting',
          tooltip: 'Establishing connection...',
        };
      case 'reconnecting':
        return {
          color: 'yellow',
          icon: <IconRefresh size={8} className="animate-spin" />,
          label: 'Reconnecting',
          tooltip: 'Connection lost, attempting to reconnect...',
        };
      case 'disconnected':
        return {
          color: 'gray',
          icon: <IconCircleOff size={8} />,
          label: 'Disconnected',
          tooltip: 'Real-time updates inactive',
        };
      case 'error':
        return {
          color: 'red',
          icon: <IconCircleOff size={8} />,
          label: 'Error',
          tooltip: 'Connection failed',
        };
      default:
        return {
          color: 'gray',
          icon: <IconCircleOff size={8} />,
          label: 'Unknown',
          tooltip: 'Connection status unknown',
        };
    }
  };

  const status = getStatusInfo();

  return (
    <Tooltip label={status.tooltip} position="bottom">
      <Badge
        size="sm"
        color={status.color}
        variant="dot"
        leftSection={status.icon}
        style={{ cursor: 'help' }}
      >
        <Text size="xs" fw={500}>
          {status.label}
        </Text>
      </Badge>
    </Tooltip>
  );
}