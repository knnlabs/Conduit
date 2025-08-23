'use client';

import { Switch, Text, Group, Stack } from '@mantine/core';
import type { ToggleParameter } from '../types/parameters';

interface ToggleControlProps {
  parameter: ToggleParameter;
  value: boolean;
  onChange: (value: boolean) => void;
  disabled?: boolean;
}

export function ToggleControl({
  parameter,
  value,
  onChange,
  disabled = false,
}: ToggleControlProps) {
  const currentValue = value ?? parameter.default ?? false;
  
  return (
    <Stack gap="xs">
      <Group justify="space-between">
        <div>
          <Text size="sm" fw={500}>{parameter.label}</Text>
          {parameter.description && (
            <Text size="xs" c="dimmed">{parameter.description}</Text>
          )}
        </div>
        
        <Switch
          checked={currentValue}
          onChange={(e) => onChange(e.currentTarget.checked)}
          disabled={disabled}
          onLabel={parameter.onLabel ?? 'ON'}
          offLabel={parameter.offLabel ?? 'OFF'}
          size="md"
        />
      </Group>
    </Stack>
  );
}