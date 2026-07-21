'use client';

import { Table, Text, Alert, Stack, Badge, Group } from '@mantine/core';
import { IconAlertTriangle } from '@tabler/icons-react';
import type { DriftItemDto } from '@/lib/admin-api';
import {
  parseDriftPayload,
  fieldLabel,
  formatDriftValue,
  driftWarnings,
  driftTypeColor,
  driftTypeLabel,
} from '../utils/driftHelpers';

interface DriftDiffViewProps {
  item: DriftItemDto;
}

/**
 * Renders a single drift item's current-vs-proposed comparison, with warning banners
 * for capability drift (shared-flag fan-out) and zero-price proposals.
 */
export function DriftDiffView({ item }: DriftDiffViewProps) {
  const current = parseDriftPayload(item.currentValuesJson);
  const proposed = parseDriftPayload(item.proposedValuesJson);
  const warnings = driftWarnings(item);

  // Union of keys across both sides, preserving a stable order.
  const keys = Array.from(new Set([...Object.keys(current), ...Object.keys(proposed)]));

  return (
    <Stack gap="md">
      <Group gap="xs">
        <Badge color={driftTypeColor(item.driftType)} variant="light">
          {driftTypeLabel(item.driftType)}
        </Badge>
        <Text size="sm" fw={500}>
          {item.modelAlias}
        </Text>
        <Text size="sm" c="dimmed">
          {item.providerName} · {item.openRouterModelId}
        </Text>
      </Group>

      {warnings.map((warning, idx) => (
        <Alert
          key={idx}
          color={warning.color}
          icon={<IconAlertTriangle size={16} />}
          variant="light"
        >
          {warning.message}
        </Alert>
      ))}

      {keys.length === 0 ? (
        <Text size="sm" c="dimmed">
          No field-level detail for this drift type.
        </Text>
      ) : (
        <Table withTableBorder withColumnBorders>
          <Table.Thead>
            <Table.Tr>
              <Table.Th>Field</Table.Th>
              <Table.Th>Current (Conduit)</Table.Th>
              <Table.Th>Proposed (OpenRouter)</Table.Th>
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {keys.map((key) => {
              const currentValue = formatDriftValue(current[key]);
              const proposedValue = formatDriftValue(proposed[key]);
              const changed = currentValue !== proposedValue;
              return (
                <Table.Tr key={key}>
                  <Table.Td>
                    <Text size="sm">{fieldLabel(key)}</Text>
                  </Table.Td>
                  <Table.Td>
                    <Text size="sm" c={changed ? 'dimmed' : undefined}>
                      {currentValue}
                    </Text>
                  </Table.Td>
                  <Table.Td>
                    <Text size="sm" fw={changed ? 600 : 400} c={changed ? 'teal' : undefined}>
                      {proposedValue}
                    </Text>
                  </Table.Td>
                </Table.Tr>
              );
            })}
          </Table.Tbody>
        </Table>
      )}
    </Stack>
  );
}
