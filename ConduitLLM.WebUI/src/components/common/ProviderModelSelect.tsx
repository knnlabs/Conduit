'use client';

import { useState, useEffect } from 'react';
import {
  Select,
  Text,
} from '@mantine/core';

interface ProviderModelSelectProps {
  providerId: string | undefined;
  value: string;
  onChange: (value: string) => void;
  onCapabilitiesDetected?: (capabilities: string[]) => void;
  label?: string;
  description?: string;
  placeholder?: string;
  required?: boolean;
  error?: string;
}

export function ProviderModelSelect({
  providerId,
  value,
  onChange,
  onCapabilitiesDetected,
  label = "Provider Model Name",
  description = "The actual model name used by the provider",
  placeholder = "Select or type a model name",
  required = false,
  error,
}: ProviderModelSelectProps) {
  const [searchValue, setSearchValue] = useState(value);

  // For now, just provide a simple text input via Select with creatable
  const commonModels = [
    { value: 'gpt-4', label: 'gpt-4' },
    { value: 'gpt-3.5-turbo', label: 'gpt-3.5-turbo' },
    { value: 'claude-3-opus', label: 'claude-3-opus' },
    { value: 'claude-3-sonnet', label: 'claude-3-sonnet' },
    { value: 'claude-2.1', label: 'claude-2.1' },
  ];

  useEffect(() => {
    setSearchValue(value);
  }, [value]);

  return (
    <>
      <Select
        label={label}
        description={description}
        placeholder={placeholder}
        required={required}
        error={error}
        data={commonModels}
        value={value}
        onChange={(val) => {
          if (val) {
            onChange(val);
            // Default capabilities for known models
            if (onCapabilitiesDetected) {
              onCapabilitiesDetected(['streaming']);
            }
          }
        }}
        searchable
        clearable
        nothingFoundMessage="No matching models"
      />
      {!providerId && (
        <Text size="xs" c="dimmed" mt={4}>
          Select a provider first to see available models
        </Text>
      )}
    </>
  );
}