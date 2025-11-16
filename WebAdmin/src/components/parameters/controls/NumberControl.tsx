'use client';

import { NumberInput, Text, Stack } from '@mantine/core';
import type { NumberParameter } from '../types/parameters';

interface NumberControlProps {
  parameter: NumberParameter;
  value: number;
  onChange: (value: number) => void;
  disabled?: boolean;
}

export function NumberControl({
  parameter,
  value,
  onChange,
  disabled = false,
}: NumberControlProps) {
  const currentValue = value ?? parameter.default ?? 0;
  
  return (
    <Stack gap="xs">
      <Text size="sm" fw={500}>{parameter.label}</Text>
      
      {parameter.description && (
        <Text size="xs" c="dimmed">{parameter.description}</Text>
      )}
      
      <NumberInput
        value={currentValue}
        onChange={(val) => typeof val === 'number' && onChange(val)}
        min={parameter.min}
        max={parameter.max}
        step={parameter.step}
        decimalScale={parameter.precision}
        disabled={disabled}
        required={parameter.required}
        placeholder="Enter a number..."
      />
    </Stack>
  );
}