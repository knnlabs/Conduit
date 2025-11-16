'use client';

import { ColorInput, Text, Stack } from '@mantine/core';
import type { ColorParameter } from '../types/parameters';

interface ColorControlProps {
  parameter: ColorParameter;
  value: string;
  onChange: (value: string) => void;
  disabled?: boolean;
}

export function ColorControl({
  parameter,
  value,
  onChange,
  disabled = false,
}: ColorControlProps) {
  const currentValue = value ?? parameter.default ?? '#000000';
  
  return (
    <Stack gap="xs">
      <Text size="sm" fw={500}>{parameter.label}</Text>
      
      {parameter.description && (
        <Text size="xs" c="dimmed">{parameter.description}</Text>
      )}
      
      <ColorInput
        value={currentValue}
        onChange={onChange}
        format={parameter.format ?? 'hex'}
        swatches={parameter.swatches ?? [
          '#FF6B6B', '#4ECDC4', '#45B7D1', '#96CEB4', '#FFEAA7',
          '#D63031', '#74B9FF', '#A29BFE', '#FD79A8', '#FDCB6E',
          '#6C5CE7', '#00B894', '#00CEC9', '#0984E3', '#E17055',
        ]}
        disabled={disabled}
        required={parameter.required}
      />
    </Stack>
  );
}