'use client';

import { Group, Select } from '@mantine/core';

interface DriftFiltersProps {
  status: string | null;
  driftType: string | null;
  onStatusChange: (value: string | null) => void;
  onDriftTypeChange: (value: string | null) => void;
}

const STATUS_OPTIONS = [
  { value: 'Pending', label: 'Pending' },
  { value: 'Applied', label: 'Applied' },
  { value: 'Dismissed', label: 'Dismissed' },
  { value: 'AutoResolved', label: 'Auto-resolved' },
];

const DRIFT_TYPE_OPTIONS = [
  { value: 'Pricing', label: 'Pricing' },
  { value: 'MissingCost', label: 'Missing cost' },
  { value: 'ContextWindow', label: 'Context window' },
  { value: 'Capabilities', label: 'Capabilities' },
  { value: 'ModelRemoved', label: 'Model removed' },
  { value: 'ModelDeprecated', label: 'Model deprecated' },
];

export function DriftFilters({
  status,
  driftType,
  onStatusChange,
  onDriftTypeChange,
}: DriftFiltersProps) {
  return (
    <Group gap="sm">
      <Select
        label="Status"
        placeholder="Pending"
        data={STATUS_OPTIONS}
        value={status}
        onChange={onStatusChange}
        clearable
        w={180}
      />
      <Select
        label="Drift type"
        placeholder="All types"
        data={DRIFT_TYPE_OPTIONS}
        value={driftType}
        onChange={onDriftTypeChange}
        clearable
        w={200}
      />
    </Group>
  );
}
