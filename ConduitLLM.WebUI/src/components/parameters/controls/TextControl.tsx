'use client';

import { TextInput, Text, Stack } from '@mantine/core';
import type { TextParameter } from '../types/parameters';

interface TextControlProps {
  parameter: TextParameter;
  value: string;
  onChange: (value: string) => void;
  disabled?: boolean;
}

export function TextControl({
  parameter,
  value,
  onChange,
  disabled = false,
}: TextControlProps) {
  const currentValue = value ?? parameter.default ?? '';
  
  return (
    <Stack gap="xs">
      <Text size="sm" fw={500}>{parameter.label}</Text>
      
      {parameter.description && (
        <Text size="xs" c="dimmed">{parameter.description}</Text>
      )}
      
      <TextInput
        value={currentValue}
        onChange={(e) => onChange(e.target.value)}
        placeholder={parameter.placeholder}
        maxLength={parameter.maxLength}
        disabled={disabled}
        required={parameter.required}
        error={parameter.pattern && !new RegExp(parameter.pattern).test(currentValue) 
          ? 'Invalid format' 
          : undefined}
      />
    </Stack>
  );
}