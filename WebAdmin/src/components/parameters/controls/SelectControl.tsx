'use client';

import { Select, MultiSelect, Text, Stack } from '@mantine/core';
import type { SelectParameter } from '../types/parameters';

interface SelectControlProps {
  parameter: SelectParameter;
  value: string | string[];
  onChange: (value: string | string[]) => void;
  disabled?: boolean;
}

export function SelectControl({
  parameter,
  value,
  onChange,
  disabled = false,
}: SelectControlProps) {
  const currentValue = value ?? parameter.default ?? (parameter.multiple ? [] : '');
  
  const selectData = parameter.options.map(option => ({
    value: option.value,
    label: option.label,
    description: option.description,
  }));
  
  return (
    <Stack gap="xs">
      <Text size="sm" fw={500}>{parameter.label}</Text>
      
      {parameter.description && (
        <Text size="xs" c="dimmed">{parameter.description}</Text>
      )}
      
      {parameter.multiple ? (
        <MultiSelect
          value={currentValue as string[]}
          onChange={onChange}
          data={selectData}
          disabled={disabled}
          placeholder="Select options..."
          clearable
        />
      ) : (
        <Select
          value={currentValue as string}
          onChange={(val) => val && onChange(val)}
          data={selectData}
          disabled={disabled}
          placeholder="Select an option..."
          clearable
          allowDeselect
        />
      )}
    </Stack>
  );
}