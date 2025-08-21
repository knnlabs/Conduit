'use client';

import { Slider, Text, Group, NumberInput, Stack } from '@mantine/core';
import type { SliderParameter } from '../types/parameters';

interface SliderControlProps {
  parameter: SliderParameter;
  value: number;
  onChange: (value: number) => void;
  disabled?: boolean;
}

export function SliderControl({
  parameter,
  value,
  onChange,
  disabled = false,
}: SliderControlProps) {
  const currentValue = value ?? parameter.default ?? parameter.min;
  
  return (
    <Stack gap="xs">
      <Group justify="space-between" mb={4}>
        <Text size="sm" fw={500}>
          {parameter.label}
          {parameter.unit && <Text span c="dimmed" size="xs"> ({parameter.unit})</Text>}
        </Text>
        <NumberInput
          value={currentValue}
          onChange={(val) => typeof val === 'number' && onChange(val)}
          min={parameter.min}
          max={parameter.max}
          step={parameter.step}
          decimalScale={parameter.step < 1 ? 2 : 0}
          disabled={disabled}
          size="xs"
          styles={{ input: { width: 80, textAlign: 'right' } }}
        />
      </Group>
      
      {parameter.description && (
        <Text size="xs" c="dimmed">{parameter.description}</Text>
      )}
      
      <Slider
        value={currentValue}
        onChange={onChange}
        min={parameter.min}
        max={parameter.max}
        step={parameter.step}
        marks={parameter.marks}
        disabled={disabled}
        label={(val) => `${val}${parameter.unit ? ` ${parameter.unit}` : ''}`}
      />
    </Stack>
  );
}