'use client';

import { Badge, Group, Tooltip, Text, Indicator } from '@mantine/core';
import { IconWifi, IconWifiOff, IconRefresh } from '@tabler/icons-react';
import type { WebSocketStatus } from '../hooks/useWebSocketChat';

interface ConnectionStatusProps {
  status: WebSocketStatus;
  activeUsers?: string[];
  onReconnect?: () => void;
}

/**
 * Connection Status Component - Shows WebSocket connection state
 * 
 * TODO: In the full implementation, this will:
 * - Show real-time connection status
 * - Display active user count for collaboration
 * - Provide manual reconnection option
 * - Show provider availability indicators
 * - Display latency metrics
 */
export function ConnectionStatus({ status, activeUsers = [], onReconnect }: ConnectionStatusProps) {
  const getStatusColor = () => {
    switch (status) {
      case 'connected':
        return 'green';
      case 'connecting':
        return 'yellow';
      case 'disconnected':
        return 'gray';
      case 'error':
        return 'red';
      default:
        return 'gray';
    }
  };

  const getStatusText = () => {
    switch (status) {
      case 'connected':
        return 'Connected';
      case 'connecting':
        return 'Connecting...';
      case 'disconnected':
        return 'Disconnected';
      case 'error':
        return 'Connection Error';
      default:
        return 'Unknown';
    }
  };

  const getStatusIcon = () => {
    switch (status) {
      case 'connected':
      case 'connecting':
        return <IconWifi size={14} />;
      case 'disconnected':
      case 'error':
        return <IconWifiOff size={14} />;
      default:
        return <IconWifiOff size={14} />;
    }
  };

  return (
    <Group gap="xs">
      <Tooltip
        label={
          <div>
            <Text size="xs" fw={500}>Connection Status</Text>
            <Text size="xs" c="dimmed">{getStatusText()}</Text>
            {activeUsers.length > 0 && (
              <Text size="xs" c="dimmed" mt={4}>
                {activeUsers.length} active user{activeUsers.length !== 1 ? 's' : ''}
              </Text>
            )}
            {status === 'disconnected' && (
              <Text size="xs" c="dimmed" mt={4}>
                Click to reconnect
              </Text>
            )}
          </div>
        }
      >
        <Badge
          size="sm"
          variant="dot"
          color={getStatusColor()}
          leftSection={getStatusIcon()}
          style={{ cursor: status === 'disconnected' ? 'pointer' : 'default' }}
          onClick={status === 'disconnected' ? onReconnect : undefined}
        >
          {getStatusText()}
        </Badge>
      </Tooltip>

      {activeUsers.length > 1 && (
        <Tooltip label={`Active users: ${activeUsers.join(', ')}`}>
          <Badge size="sm" variant="light" color="blue">
            {activeUsers.length} users
          </Badge>
        </Tooltip>
      )}

      {/* TODO: Add provider status indicators */}
      {/* {Object.entries(providerStatuses).map(([provider, status]) => (
        <Indicator
          key={provider}
          color={status === 'online' ? 'green' : 'red'}
          size={8}
          processing={status === 'online'}
        >
          <Text size="xs">{provider}</Text>
        </Indicator>
      ))} */}
    </Group>
  );
}