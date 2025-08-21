'use client';

import { useState } from 'react';
import { Select, Text, Stack, Group, NumberInput, Badge } from '@mantine/core';
import type { ResolutionParameter, ParameterContext } from '../types/parameters';

interface ResolutionControlProps {
  parameter: ResolutionParameter;
  value: string;
  onChange: (value: string) => void;
  disabled?: boolean;
  context: ParameterContext;
}

export function ResolutionControl({
  parameter,
  value,
  onChange,
  disabled = false,
  context,
}: ResolutionControlProps) {
  const currentValue = value ?? parameter.default ?? parameter.options[0]?.value ?? '';
  const [customMode, setCustomMode] = useState(false);
  const [customWidth, setCustomWidth] = useState(1024);
  const [customHeight, setCustomHeight] = useState(1024);
  
  const selectedOption = parameter.options.find(opt => opt.value === currentValue);
  
  const selectData = parameter.options.map(option => ({
    value: option.value,
    label: option.label,
    description: option.aspectRatio,
  }));
  
  if (parameter.allowCustom) {
    selectData.push({
      value: 'custom',
      label: 'Custom',
      description: 'Set custom dimensions',
    });
  }
  
  const handleSelectChange = (val: string | null) => {
    if (!val) return;
    
    if (val === 'custom') {
      setCustomMode(true);
      onChange(`${customWidth}x${customHeight}`);
    } else {
      setCustomMode(false);
      onChange(val);
    }
  };
  
  const handleCustomChange = (width: number, height: number) => {
    setCustomWidth(width);
    setCustomHeight(height);
    onChange(`${width}x${height}`);
  };
  
  return (
    <Stack gap="xs">
      <Group justify="space-between">
        <Text size="sm" fw={500}>{parameter.label}</Text>
        {selectedOption && (
          <Badge size="sm" variant="light">
            {selectedOption.width} × {selectedOption.height}
            {selectedOption.aspectRatio && ` (${selectedOption.aspectRatio})`}
          </Badge>
        )}
      </Group>
      
      {parameter.description && (
        <Text size="xs" c="dimmed">{parameter.description}</Text>
      )}
      
      <Select
        value={customMode ? 'custom' : currentValue}
        onChange={handleSelectChange}
        data={selectData}
        disabled={disabled}
        placeholder="Select resolution..."
      />
      
      {customMode && parameter.allowCustom && (
        <Group grow>
          <NumberInput
            value={customWidth}
            onChange={(val) => typeof val === 'number' && handleCustomChange(val, customHeight)}
            min={context === 'video' ? 128 : 64}
            max={context === 'video' ? 1920 : 4096}
            step={context === 'video' ? 16 : 64}
            label="Width"
            disabled={disabled}
          />
          <NumberInput
            value={customHeight}
            onChange={(val) => typeof val === 'number' && handleCustomChange(customWidth, val)}
            min={context === 'video' ? 128 : 64}
            max={context === 'video' ? 1080 : 4096}
            step={context === 'video' ? 16 : 64}
            label="Height"
            disabled={disabled}
          />
        </Group>
      )}
    </Stack>
  );
}