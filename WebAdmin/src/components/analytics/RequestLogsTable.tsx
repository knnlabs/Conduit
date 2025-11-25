'use client';

import {
  Table,
  Text,
  Badge,
  Box,
  Paper,
  Tooltip,
  Group,
  Popover,
  Code,
  Stack,
} from '@mantine/core';
import { IconInfoCircle } from '@tabler/icons-react';
import { formatters } from '@/lib/utils/formatters';
import type { RequestLogEntry, RequestLogMetadata } from '@/hooks/useRequestLogs';

interface RequestLogsTableProps {
  data: RequestLogEntry[];
  isLoading?: boolean;
}

/**
 * Format cost with 6 decimal places for precision
 */
function formatCost(cost: number): string {
  if (cost === 0) return '$0.00';
  if (cost < 0.01) {
    return `$${cost.toFixed(6)}`;
  }
  return `$${cost.toFixed(4)}`;
}

/**
 * Format latency in milliseconds
 */
function formatLatency(ms: number): string {
  if (ms < 1000) {
    return `${Math.round(ms)} ms`;
  }
  return `${(ms / 1000).toFixed(2)} s`;
}

/**
 * Format token count with thousand separators
 */
function formatTokens(tokens: number): string {
  return tokens.toLocaleString();
}

/**
 * Get status badge color based on HTTP status code
 */
function getStatusColor(statusCode: number | null): string {
  if (statusCode === null) return 'gray';
  if (statusCode >= 200 && statusCode < 300) return 'green';
  if (statusCode >= 300 && statusCode < 400) return 'blue';
  if (statusCode >= 400 && statusCode < 500) return 'orange';
  if (statusCode >= 500) return 'red';
  return 'gray';
}

/**
 * Get status label
 */
function getStatusLabel(statusCode: number | null): string {
  if (statusCode === null) return 'N/A';
  if (statusCode >= 200 && statusCode < 300) return 'Success';
  if (statusCode >= 400 && statusCode < 500) return 'Client Error';
  if (statusCode >= 500) return 'Server Error';
  return statusCode.toString();
}

/**
 * Parse metadata JSON string to object
 */
function parseMetadata(metadata: string | null): RequestLogMetadata | null {
  if (!metadata) return null;
  try {
    return JSON.parse(metadata) as RequestLogMetadata;
  } catch {
    return null;
  }
}

/**
 * Get request type badge color
 */
function getRequestTypeColor(requestType: string): string {
  switch (requestType.toLowerCase()) {
    case 'chat':
      return 'blue';
    case 'completion':
      return 'cyan';
    case 'embedding':
      return 'grape';
    case 'image':
      return 'pink';
    case 'video':
      return 'violet';
    case 'tts':
    case 'transcription':
      return 'orange';
    case 'function':
      return 'teal';
    default:
      return 'gray';
  }
}

/**
 * Format currency for display
 */
function formatMetadataCost(cost: number | undefined): string {
  if (cost === undefined || cost === null) return '-';
  if (cost === 0) return '$0.00';
  if (cost < 0.01) return `$${cost.toFixed(6)}`;
  return `$${cost.toFixed(4)}`;
}

/**
 * Render metadata details for popover
 */
function MetadataDetails({ metadata }: { metadata: RequestLogMetadata }) {
  // Special handling for chat_with_functions type (agentic chat with function executions)
  if (metadata.type === 'chat_with_functions' && metadata.functionCalls) {
    return (
      <Stack gap="xs">
        <Group gap="xs">
          <Text size="xs" fw={600} c="teal">
            Function Executions ({metadata.functionCallCount ?? metadata.functionCalls.length})
          </Text>
          {metadata.totalCost !== undefined && metadata.totalCost > 0 && (
            <Badge size="xs" variant="light" color="orange">
              {formatMetadataCost(metadata.totalCost)}
            </Badge>
          )}
        </Group>
        {metadata.successCount !== undefined && metadata.failedCount !== undefined && (
          <Group gap="xs">
            <Badge size="xs" variant="light" color="green">
              {metadata.successCount} succeeded
            </Badge>
            {metadata.failedCount > 0 && (
              <Badge size="xs" variant="light" color="red">
                {metadata.failedCount} failed
              </Badge>
            )}
          </Group>
        )}
        <Stack gap={4}>
          {metadata.functionCalls.map((fc, index) => (
            <Group key={fc.toolCallId ?? index} gap="xs" wrap="nowrap" style={{ alignItems: 'flex-start' }}>
              <Badge
                size="xs"
                variant="light"
                color={fc.status === 'completed' ? 'green' : 'red'}
              >
                {fc.status === 'completed' ? '✓' : '✗'}
              </Badge>
              <Stack gap={2}>
                <Text size="xs" fw={500}>
                  {fc.functionName ?? 'unknown'}
                </Text>
                <Group gap="xs">
                  {fc.cost !== undefined && fc.cost > 0 && (
                    <Text size="xs" c="dimmed">
                      {formatMetadataCost(fc.cost)}
                    </Text>
                  )}
                  {fc.functionExecutionId && (
                    <Text size="xs" c="dimmed" style={{ fontFamily: 'monospace' }}>
                      {fc.functionExecutionId.substring(0, 8)}...
                    </Text>
                  )}
                </Group>
                {fc.errorMessage && (
                  <Text size="xs" c="red">
                    {fc.errorMessage}
                  </Text>
                )}
              </Stack>
            </Group>
          ))}
        </Stack>
      </Stack>
    );
  }

  // Special handling for chat_with_tools type (basic tool calls without execution results)
  if (metadata.type === 'chat_with_tools' && metadata.toolCalls) {
    return (
      <Stack gap="xs">
        <Text size="xs" fw={600} c="blue">
          Tool Calls ({metadata.toolCallCount ?? metadata.toolCalls.length})
        </Text>
        {metadata.toolCalls.map((tc, index) => (
          <Group key={tc.id ?? index} gap="xs" wrap="nowrap">
            <Badge size="xs" variant="light" color="teal">
              {tc.functionName ?? 'unknown'}
            </Badge>
            {tc.id && (
              <Text size="xs" c="dimmed" style={{ fontFamily: 'monospace' }}>
                {tc.id}
              </Text>
            )}
          </Group>
        ))}
      </Stack>
    );
  }

  const entries = Object.entries(metadata).filter(
    ([key, value]) => value !== null && value !== undefined && key !== 'type' && key !== 'toolCalls' && key !== 'functionCalls'
  );

  if (entries.length === 0) return <Text size="xs" c="dimmed">No additional details</Text>;

  return (
    <Stack gap="xs">
      {entries.map(([key, value]) => (
        <Group key={key} gap="xs">
          <Text size="xs" fw={500} c="dimmed" style={{ minWidth: 100 }}>
            {key}:
          </Text>
          <Code fz="xs">{typeof value === 'object' ? JSON.stringify(value) : String(value)}</Code>
        </Group>
      ))}
    </Stack>
  );
}

/**
 * Format relative time (e.g., "2 min ago")
 */
function formatRelativeTime(timestamp: string): string {
  const date = new Date(timestamp);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
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

export function RequestLogsTable({ data, isLoading }: RequestLogsTableProps) {
  const rows = data.map((log) => (
    <Table.Tr key={log.id}>
      <Table.Td>
        <Tooltip label={formatters.date(log.timestamp, { includeTime: true, includeSeconds: true, relativeDays: 0 })} position="top-start">
          <Text size="sm">{formatRelativeTime(log.timestamp)}</Text>
        </Tooltip>
      </Table.Td>

      <Table.Td>
        <Text size="sm" fw={500} style={{ fontFamily: 'monospace' }}>
          {log.modelName || 'Unknown'}
        </Text>
      </Table.Td>

      <Table.Td>
        <Badge
          variant="light"
          color={getRequestTypeColor(log.requestType)}
          size="sm"
        >
          {log.requestType || 'N/A'}
        </Badge>
      </Table.Td>

      <Table.Td>
        <Group gap={4}>
          <Tooltip label="Input tokens">
            <Badge variant="light" color="blue" size="sm">
              {formatTokens(log.inputTokens)}
            </Badge>
          </Tooltip>
          <Text size="xs" c="dimmed">/</Text>
          <Tooltip label="Output tokens">
            <Badge variant="light" color="green" size="sm">
              {formatTokens(log.outputTokens)}
            </Badge>
          </Tooltip>
        </Group>
      </Table.Td>

      <Table.Td>
        <Text
          size="sm"
          fw={500}
          style={{ fontFamily: 'monospace' }}
          c={log.cost > 0 ? undefined : 'dimmed'}
        >
          {formatCost(log.cost)}
        </Text>
      </Table.Td>

      <Table.Td>
        <Text size="sm" c={log.responseTimeMs > 5000 ? 'orange' : undefined}>
          {formatLatency(log.responseTimeMs)}
        </Text>
      </Table.Td>

      <Table.Td>
        <Tooltip label={log.statusCode !== null ? `HTTP ${log.statusCode}` : 'No status'}>
          <Badge
            color={getStatusColor(log.statusCode)}
            variant="light"
            size="sm"
          >
            {getStatusLabel(log.statusCode)}
          </Badge>
        </Tooltip>
      </Table.Td>

      <Table.Td>
        <Text size="sm" c="dimmed">
          {log.virtualKeyId > 0 ? `Key #${log.virtualKeyId}` : 'N/A'}
        </Text>
      </Table.Td>

      <Table.Td>
        {(() => {
          const parsedMetadata = parseMetadata(log.metadata);
          if (!parsedMetadata) return <Text size="xs" c="dimmed">-</Text>;

          return (
            <Popover width={300} position="left" withArrow shadow="md">
              <Popover.Target>
                <Badge
                  variant="light"
                  color="gray"
                  size="sm"
                  style={{ cursor: 'pointer' }}
                  leftSection={<IconInfoCircle size={12} />}
                >
                  Details
                </Badge>
              </Popover.Target>
              <Popover.Dropdown>
                <MetadataDetails metadata={parsedMetadata} />
              </Popover.Dropdown>
            </Popover>
          );
        })()}
      </Table.Td>
    </Table.Tr>
  ));

  return (
    <Paper withBorder radius="md">
      <Box pos="relative">
        <Table.ScrollContainer minWidth={900}>
          <Table verticalSpacing="sm" horizontalSpacing="md" striped highlightOnHover>
            <Table.Thead>
              <Table.Tr>
                <Table.Th style={{ width: 120 }}>Time</Table.Th>
                <Table.Th style={{ width: 180 }}>Model</Table.Th>
                <Table.Th style={{ width: 100 }}>Type</Table.Th>
                <Table.Th style={{ width: 140 }}>Tokens (In/Out)</Table.Th>
                <Table.Th style={{ width: 100 }}>Cost</Table.Th>
                <Table.Th style={{ width: 90 }}>Latency</Table.Th>
                <Table.Th style={{ width: 100 }}>Status</Table.Th>
                <Table.Th style={{ width: 100 }}>Virtual Key</Table.Th>
                <Table.Th style={{ width: 80 }}>Details</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {isLoading ? (
                <Table.Tr>
                  <Table.Td colSpan={9}>
                    <Box p="xl" style={{ textAlign: 'center' }}>
                      <Text c="dimmed">Loading request logs...</Text>
                    </Box>
                  </Table.Td>
                </Table.Tr>
              ) : (
                rows
              )}
            </Table.Tbody>
          </Table>
        </Table.ScrollContainer>

        {!isLoading && data.length === 0 && (
          <Box p="xl" style={{ textAlign: 'center' }}>
            <Text c="dimmed">No request logs found for the selected filters.</Text>
          </Box>
        )}
      </Box>
    </Paper>
  );
}
