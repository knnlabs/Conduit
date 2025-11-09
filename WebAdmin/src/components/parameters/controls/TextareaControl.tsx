'use client';

import { Textarea, Text, Stack } from '@mantine/core';
import type { TextareaParameter } from '../types/parameters';

interface TextareaControlProps {
  parameter: TextareaParameter;
  value: string;
  onChange: (value: string) => void;
  disabled?: boolean;
}

export function TextareaControl({
  parameter,
  value,
  onChange,
  disabled = false,
}: TextareaControlProps) {
  const currentValue = value ?? parameter.default ?? '';
  
  return (
    <Stack gap="xs">
      <Text size="sm" fw={500}>{parameter.label}</Text>
      
      {parameter.description && (
        <Text size="xs" c="dimmed">{parameter.description}</Text>
      )}
      
      <Textarea
        value={currentValue}
        onChange={(e) => onChange(e.target.value)}
        placeholder={parameter.placeholder}
        maxLength={parameter.maxLength}
        rows={parameter.rows ?? 3}
        disabled={disabled}
        required={parameter.required}
        autosize
        minRows={parameter.rows ?? 3}
        maxRows={8}
      />
    </Stack>
  );
}