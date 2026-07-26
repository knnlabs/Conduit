'use client';

import { Fragment, useEffect, useRef, useState } from 'react';
import {
  ActionIcon,
  Badge,
  Box,
  Group,
  Paper,
  Popover,
  Stack,
  Table,
  Text,
  Tooltip,
  UnstyledButton,
} from '@mantine/core';
import { IconEye, IconInfoCircle } from '@tabler/icons-react';
import { formatters } from '@/lib/utils/formatters';
import type { VirtualKeyDto, VirtualKeyGroupDto } from '@/lib/admin-api';
import type { RequestLogEntry } from '@/hooks/useRequestLogs';
import { RequestLogDetailsDrawer } from './RequestLogDetailsDrawer';
import { formatCost, formatDuration, getHttpStatusColor } from './formatters';

interface RequestLogsTableProps {
  data: RequestLogEntry[];
  virtualKeys?: VirtualKeyDto[];
  virtualKeyGroups?: VirtualKeyGroupDto[];
  isLoading?: boolean;
  onViewVirtualKey?: (virtualKey: VirtualKeyDto) => void;
}

function getStatusLabel(statusCode: number | null): string {
  if (statusCode === null) return 'N/A';
  if (statusCode >= 200 && statusCode < 300) return 'Success';
  if (statusCode >= 400 && statusCode < 500) return 'Client Error';
  if (statusCode >= 500) return 'Server Error';
  return statusCode.toString();
}

function getRequestTypeColor(requestType: string): string {
  switch (requestType.toLowerCase()) {
    case 'chat': return 'blue';
    case 'completion': return 'cyan';
    case 'embedding': return 'grape';
    case 'image': return 'pink';
    case 'video': return 'violet';
    case 'tts':
    case 'transcription': return 'orange';
    case 'function': return 'teal';
    default: return 'gray';
  }
}

function formatRelativeTime(timestamp: string): string {
  const date = new Date(timestamp);
  const diffMs = Date.now() - date.getTime();
  const diffSec = Math.floor(diffMs / 1000);
  const diffMin = Math.floor(diffSec / 60);
  const diffHour = Math.floor(diffMin / 60);
  const diffDay = Math.floor(diffHour / 24);

  if (diffSec < 60) return 'Just now';
  if (diffMin < 60) return `${diffMin} min ago`;
  if (diffHour < 24) return `${diffHour} hr ago`;
  if (diffDay < 7) return `${diffDay} day${diffDay > 1 ? 's' : ''} ago`;
  return formatters.date(timestamp);
}

function getVirtualKeyStatus(virtualKey?: VirtualKeyDto): string {
  if (!virtualKey) return 'Unavailable';
  if (!virtualKey.isEnabled) return 'Disabled';
  if (virtualKey.expiresAt && new Date(virtualKey.expiresAt) < new Date()) return 'Expired';
  return 'Active';
}

function getVirtualKeyStatusColor(status: string): string {
  if (status === 'Active') return 'green';
  if (status === 'Unavailable') return 'gray';
  return 'orange';
}

function VirtualKeyCell({
  log,
  virtualKey,
  group,
  onView,
}: {
  log: RequestLogEntry;
  virtualKey?: VirtualKeyDto;
  group?: VirtualKeyGroupDto;
  onView?: (virtualKey: VirtualKeyDto) => void;
}) {
  const [contextOpened, setContextOpened] = useState(false);
  const closeTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const displayName = virtualKey?.keyName ?? log.userId ?? `Key #${log.virtualKeyId}`;
  const secondary = [virtualKey?.keyPrefix, log.virtualKeyId > 0 ? `#${log.virtualKeyId}` : undefined]
    .filter(Boolean)
    .join(' · ');
  const status = getVirtualKeyStatus(virtualKey);

  const openContext = () => {
    if (closeTimer.current) clearTimeout(closeTimer.current);
    setContextOpened(true);
  };
  const closeContext = () => {
    closeTimer.current = setTimeout(() => setContextOpened(false), 100);
  };

  useEffect(() => () => {
    if (closeTimer.current) clearTimeout(closeTimer.current);
  }, []);

  return (
    <Popover width={320} shadow="md" withArrow opened={contextOpened} onChange={setContextOpened}>
      <Popover.Target>
        <UnstyledButton
          onClick={() => virtualKey && onView?.(virtualKey)}
          onMouseEnter={openContext}
          onMouseLeave={closeContext}
          onFocus={openContext}
          onBlur={closeContext}
          aria-label={virtualKey ? `View virtual key ${displayName}` : `${displayName}; current key details unavailable`}
          style={{ textAlign: 'left', cursor: virtualKey ? 'pointer' : 'default' }}
        >
          <Stack gap={1}>
            <Text size="sm" fw={500} c={virtualKey ? undefined : 'dimmed'}>{displayName}</Text>
            {secondary && <Text size="xs" c="dimmed" ff="monospace">{secondary}</Text>}
          </Stack>
        </UnstyledButton>
      </Popover.Target>
      <Popover.Dropdown onMouseEnter={openContext} onMouseLeave={closeContext}>
        <Stack gap="xs">
          <Group justify="space-between" align="flex-start">
            <Stack gap={1}>
              <Text size="sm" fw={600}>{displayName}</Text>
              <Text size="xs" c="dimmed">Virtual key #{log.virtualKeyId}</Text>
            </Stack>
            <Badge
              size="sm"
              variant="light"
              color={getVirtualKeyStatusColor(status)}
            >
              {status}
            </Badge>
          </Group>
          <div>
            <Text size="xs" c="dimmed">Customer</Text>
            <Text size="sm">{group?.groupName ?? 'Current group unavailable'}</Text>
            {group?.externalGroupId && <Text size="xs" c="dimmed">{group.externalGroupId}</Text>}
          </div>
          {virtualKey?.description && (
            <div>
              <Text size="xs" c="dimmed">Description</Text>
              <Text size="sm">{virtualKey.description}</Text>
            </div>
          )}
          {virtualKey && (
            <Group gap="lg">
              <div>
                <Text size="xs" c="dimmed">RPM</Text>
                <Text size="sm">{virtualKey.rateLimitRpm ?? 'Unlimited'}</Text>
              </div>
              <div>
                <Text size="xs" c="dimmed">RPD</Text>
                <Text size="sm">{virtualKey.rateLimitRpd ?? 'Unlimited'}</Text>
              </div>
              <div>
                <Text size="xs" c="dimmed">Expires</Text>
                <Text size="sm">{virtualKey.expiresAt ? formatters.date(virtualKey.expiresAt) : 'Never'}</Text>
              </div>
            </Group>
          )}
          {!virtualKey && (
            <Text size="xs" c="dimmed">
              This historical log remains available, but the current key record could not be found.
            </Text>
          )}
        </Stack>
      </Popover.Dropdown>
    </Popover>
  );
}

export function RequestLogsTable({
  data,
  virtualKeys = [],
  virtualKeyGroups = [],
  isLoading,
  onViewVirtualKey,
}: RequestLogsTableProps) {
  const [selectedLog, setSelectedLog] = useState<RequestLogEntry | null>(null);
  const keyMap = new Map(virtualKeys.map((key) => [key.id, key]));
  const groupMap = new Map(virtualKeyGroups.map((group) => [group.id, group]));

  const rows = data.map((log) => {
    const virtualKey = keyMap.get(log.virtualKeyId);
    const group = virtualKey ? groupMap.get(virtualKey.virtualKeyGroupId) : undefined;

    return (
      <Table.Tr key={log.id}>
        <Table.Td>
          <Tooltip label={formatters.date(log.timestamp, { includeTime: true, includeSeconds: true, relativeDays: 0 })} position="top-start">
            <Text size="sm">{formatRelativeTime(log.timestamp)}</Text>
          </Tooltip>
        </Table.Td>
        <Table.Td>
          <Text size="sm" fw={500} ff="monospace">{log.modelName || 'Unknown'}</Text>
        </Table.Td>
        <Table.Td>
          <Badge variant="light" color={getRequestTypeColor(log.requestType)} size="sm">
            {log.requestType || 'N/A'}
          </Badge>
        </Table.Td>
        <Table.Td>
          <Group gap={4}>
            <Tooltip label="Input tokens">
              <Badge variant="light" color="blue" size="sm">{log.inputTokens.toLocaleString()}</Badge>
            </Tooltip>
            <Text size="xs" c="dimmed">/</Text>
            <Tooltip label="Output tokens">
              <Badge variant="light" color="green" size="sm">{log.outputTokens.toLocaleString()}</Badge>
            </Tooltip>
          </Group>
        </Table.Td>
        <Table.Td>
          <Text size="sm" fw={500} ff="monospace" c={log.cost > 0 ? undefined : 'dimmed'}>
            {formatCost(log.cost)}
          </Text>
        </Table.Td>
        <Table.Td>
          <Text size="sm" c={log.responseTimeMs > 5000 ? 'orange' : undefined}>
            {formatDuration(log.responseTimeMs)}
          </Text>
        </Table.Td>
        <Table.Td>
          <Tooltip label={log.statusCode !== null ? `HTTP ${log.statusCode}` : 'No status'}>
            <Badge color={getHttpStatusColor(log.statusCode)} variant="light" size="sm">
              {getStatusLabel(log.statusCode)}
            </Badge>
          </Tooltip>
        </Table.Td>
        <Table.Td>
          <VirtualKeyCell log={log} virtualKey={virtualKey} group={group} onView={onViewVirtualKey} />
        </Table.Td>
        <Table.Td>
          <Tooltip label="View request details">
            <ActionIcon
              variant="subtle"
              color="gray"
              onClick={() => setSelectedLog(log)}
              aria-label={`View request details for request ${log.id}`}
            >
              <IconEye size={16} />
            </ActionIcon>
          </Tooltip>
        </Table.Td>
      </Table.Tr>
    );
  });

  const selectedKey = selectedLog ? keyMap.get(selectedLog.virtualKeyId) : undefined;
  const selectedGroup = selectedKey ? groupMap.get(selectedKey.virtualKeyGroupId) : undefined;

  return (
    <Fragment>
      <Paper withBorder radius="md">
        <Box pos="relative">
          <Table.ScrollContainer minWidth={980}>
            <Table verticalSpacing="sm" horizontalSpacing="md" striped highlightOnHover>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th style={{ width: 120 }}>Time</Table.Th>
                  <Table.Th style={{ width: 180 }}>Model</Table.Th>
                  <Table.Th style={{ width: 100 }}>Type</Table.Th>
                  <Table.Th style={{ width: 140 }}>Tokens (In/Out)</Table.Th>
                  <Table.Th style={{ width: 100 }}>Cost</Table.Th>
                  <Table.Th style={{ width: 105 }}>
                    <Tooltip
                      multiline
                      w={320}
                      label="Gateway processing time measured after virtual-key authentication. Streaming requests cover the full stream; this is not provider-only latency or time-to-first-token."
                    >
                      <Group gap={4} wrap="nowrap">
                        <Text inherit>Duration</Text>
                        <IconInfoCircle size={14} />
                      </Group>
                    </Tooltip>
                  </Table.Th>
                  <Table.Th style={{ width: 100 }}>Status</Table.Th>
                  <Table.Th style={{ width: 180 }}>Virtual Key</Table.Th>
                  <Table.Th style={{ width: 70 }}>Details</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {isLoading ? (
                  <Table.Tr>
                    <Table.Td colSpan={9}>
                      <Box p="xl" ta="center"><Text c="dimmed">Loading request logs...</Text></Box>
                    </Table.Td>
                  </Table.Tr>
                ) : rows}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
          {!isLoading && data.length === 0 && (
            <Box p="xl" ta="center"><Text c="dimmed">No request logs found for the selected filters.</Text></Box>
          )}
        </Box>
      </Paper>

      <RequestLogDetailsDrawer
        opened={selectedLog !== null}
        onClose={() => setSelectedLog(null)}
        log={selectedLog}
        virtualKey={selectedKey}
        virtualKeyGroup={selectedGroup}
      />
    </Fragment>
  );
}
