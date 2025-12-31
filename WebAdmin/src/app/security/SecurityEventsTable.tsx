'use client';

import {
  Table,
  Badge,
  Text,
  Group,
  Paper,
  Box,
  ActionIcon,
  Tooltip,
  Skeleton,
} from '@mantine/core';
import { IconCopy, IconEye } from '@tabler/icons-react';
import { notifications } from '@mantine/notifications';
import { formatters } from '@/lib/utils/formatters';
import type { SecurityEvent } from './types';

interface SecurityEventsTableProps {
  events: SecurityEvent[];
  isLoading?: boolean;
  onViewDetails?: (event: SecurityEvent) => void;
}

type EventSeverity = SecurityEvent['severity'];
type EventType = SecurityEvent['type'];

const SEVERITY_COLORS: Record<EventSeverity, string> = {
  low: 'blue',
  medium: 'yellow',
  high: 'orange',
  critical: 'red',
};

const EVENT_TYPE_LABELS: Record<EventType, string> = {
  authentication_failure: 'Auth Failure',
  rate_limit_exceeded: 'Rate Limit',
  suspicious_activity: 'Suspicious',
  invalid_api_key: 'Invalid Key',
};

export function SecurityEventsTable({
  events,
  isLoading = false,
  onViewDetails,
}: SecurityEventsTableProps) {
  const handleCopyIp = (ip: string) => {
    void navigator.clipboard.writeText(ip);
    notifications.show({
      title: 'Copied',
      message: 'IP address copied to clipboard',
      color: 'green',
    });
  };

  if (isLoading) {
    return (
      <Table.ScrollContainer minWidth={800}>
        <Table verticalSpacing="sm" horizontalSpacing="md" striped>
          <Table.Thead>
            <Table.Tr>
              <Table.Th>Timestamp</Table.Th>
              <Table.Th>Type</Table.Th>
              <Table.Th>Severity</Table.Th>
              <Table.Th>Source</Table.Th>
              <Table.Th>IP Address</Table.Th>
              <Table.Th w={80} />
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {[...Array(5).keys()].map((index) => (
              <Table.Tr key={index}>
                <Table.Td><Skeleton height={20} width={120} /></Table.Td>
                <Table.Td><Skeleton height={24} width={80} /></Table.Td>
                <Table.Td><Skeleton height={24} width={60} /></Table.Td>
                <Table.Td><Skeleton height={20} width={100} /></Table.Td>
                <Table.Td><Skeleton height={20} width={100} /></Table.Td>
                <Table.Td><Skeleton height={24} width={24} /></Table.Td>
              </Table.Tr>
            ))}
          </Table.Tbody>
        </Table>
      </Table.ScrollContainer>
    );
  }

  if (events.length === 0) {
    return (
      <Paper withBorder radius="md">
        <Box p="xl" style={{ textAlign: 'center' }}>
          <Text c="dimmed">No security events found.</Text>
        </Box>
      </Paper>
    );
  }

  return (
    <Table.ScrollContainer minWidth={800}>
      <Table verticalSpacing="sm" horizontalSpacing="md" striped highlightOnHover>
        <Table.Thead>
          <Table.Tr>
            <Table.Th>Timestamp</Table.Th>
            <Table.Th>Type</Table.Th>
            <Table.Th>Severity</Table.Th>
            <Table.Th>Source</Table.Th>
            <Table.Th>IP Address</Table.Th>
            <Table.Th w={80} />
          </Table.Tr>
        </Table.Thead>
        <Table.Tbody>
          {events.map((event) => (
            <Table.Tr key={event.id}>
              <Table.Td>
                <Text size="sm">
                  {formatters.date(event.timestamp, { includeTime: true })}
                </Text>
              </Table.Td>
              <Table.Td>
                <Badge variant="light" color="gray">
                  {EVENT_TYPE_LABELS[event.type] ?? event.type}
                </Badge>
              </Table.Td>
              <Table.Td>
                <Badge color={SEVERITY_COLORS[event.severity]} variant="filled">
                  {event.severity}
                </Badge>
              </Table.Td>
              <Table.Td>
                <Text size="sm" lineClamp={1}>
                  {event.source}
                </Text>
              </Table.Td>
              <Table.Td>
                {event.ipAddress ? (
                  <Group gap="xs">
                    <Text size="sm" style={{ fontFamily: 'monospace' }}>
                      {event.ipAddress}
                    </Text>
                    <Tooltip label="Copy IP">
                      <ActionIcon
                        variant="subtle"
                        size="xs"
                        onClick={() => handleCopyIp(event.ipAddress as string)}
                      >
                        <IconCopy size={14} />
                      </ActionIcon>
                    </Tooltip>
                  </Group>
                ) : (
                  <Text size="sm" c="dimmed">
                    -
                  </Text>
                )}
              </Table.Td>
              <Table.Td>
                {onViewDetails && (
                  <Tooltip label="View details">
                    <ActionIcon
                      variant="subtle"
                      onClick={() => onViewDetails(event)}
                    >
                      <IconEye size={16} />
                    </ActionIcon>
                  </Tooltip>
                )}
              </Table.Td>
            </Table.Tr>
          ))}
        </Table.Tbody>
      </Table>
    </Table.ScrollContainer>
  );
}
