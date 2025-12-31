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
    <Stack gap="sm" mb="md">
      <Group justify="space-between" align="flex-start" wrap="nowrap">
        <Stack gap={4} style={{ flex: 1, minWidth: 0 }}>
          <Text size="sm" fw={500}>
            {parameter.label}
            {parameter.unit && <Text span c="dimmed" size="xs"> ({parameter.unit})</Text>}
          </Text>
          {parameter.description && (
            <Text size="xs" c="dimmed">{parameter.description}</Text>
          )}
        </Stack>
        <NumberInput
          value={currentValue}
          onChange={(val) => typeof val === 'number' && onChange(val)}
          min={parameter.min}
          max={parameter.max}
          step={parameter.step}
          decimalScale={parameter.step < 1 ? 2 : 0}
          disabled={disabled}
          size="xs"
          styles={{ 
            input: { 
              width: 90, 
              textAlign: 'right',
              flexShrink: 0
            },
            wrapper: {
              flexShrink: 0
            }
          }}
        />
      </Group>
      
      <Slider
        value={currentValue}
        onChange={onChange}
        min={parameter.min}
        max={parameter.max}
        step={parameter.step}
        marks={parameter.marks}
        disabled={disabled}
        label={(val) => `${val}${parameter.unit ? ` ${parameter.unit}` : ''}`}
        styles={{
          root: { paddingTop: 8 }
        }}
      />
    </Stack>
  );
}