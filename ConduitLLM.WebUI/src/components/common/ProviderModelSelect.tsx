'use client';

import { useState, useEffect, useMemo } from 'react';
import {
  Select,
  Group,
  ActionIcon,
  Loader,
  Text,
  Tooltip,
} from '@mantine/core';
import { IconRefresh, IconAlertCircle } from '@tabler/icons-react';
import { useProviderModels, useRefreshProviderModels } from '@/hooks/api/useAdminApi';

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

interface ModelOption {
  value: string;
  label: string;
  capabilities: string[];
  group?: string;
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
  const { data: modelsData, isLoading, error: loadError } = useProviderModels(providerId);
  const refreshMutation = useRefreshProviderModels(providerId);
  const [searchValue, setSearchValue] = useState(value);

  // Transform models to options
  const modelOptions = useMemo<ModelOption[]>(() => {
    // When providerId is undefined, modelsData will be undefined (query is disabled)
    if (!providerId || !modelsData) {
      return [];
    }
    
    // Ensure models is an array - handle case where API returns models: undefined
    const models = modelsData.models;
    
    // If there's an error or models is not an array, return empty array
    if (!models || !Array.isArray(models)) {
      return [];
    }

    return models.map((model) => ({
      value: model.id || '',
      label: model.name || model.id || 'Unknown Model',
      capabilities: Array.isArray(model.capabilities) ? model.capabilities : [],
      group: model.owned_by,
    })).filter(model => model.value);
  }, [modelsData, providerId]);

  // Include the current value if it's not in the list (for custom models)
  const allOptions = useMemo(() => {
    const hasValue = modelOptions.some(opt => opt.value === value);
    if (value && !hasValue) {
      return [
        { value, label: value, capabilities: [], group: 'Custom' },
        ...modelOptions
      ];
    }
    return modelOptions;
  }, [modelOptions, value]);

  // Filter options based on search and include the search value as an option
  const filteredOptions = useMemo(() => {
    try {
      console.log('filteredOptions called with:', { allOptions, searchValue });
      
      // Ensure allOptions is always an array
      if (!Array.isArray(allOptions)) {
        console.error('allOptions is not an array!', allOptions);
        return [];
      }
      
      let options = allOptions;
      
      if (searchValue) {
        const search = searchValue.toLowerCase();
        console.log('Filtering with search:', search);
        options = allOptions.filter((opt, index) => {
          try {
            if (!opt) {
              console.error(`Option at index ${index} is null/undefined`);
              return false;
            }
            const matches = opt?.value?.toLowerCase().includes(search) ||
                          opt?.label?.toLowerCase().includes(search);
            console.log(`Option ${index} matches:`, matches, opt);
            return matches;
          } catch (filterErr) {
            console.error(`Error filtering option ${index}:`, filterErr, opt);
            return false;
          }
        });
      
      // Add the search value as an option if it's not already in the list
      const exactMatch = options.some(opt => opt.value === searchValue);
      if (!exactMatch && searchValue.trim()) {
        options = [
          { value: searchValue, label: `Use custom model: ${searchValue}`, capabilities: [], group: 'Custom' },
          ...options
        ];
      }
    }
    
    console.log('Final filtered options:', options);
    return options;
  } catch (err) {
    console.error('ERROR in filteredOptions useMemo:', err);
    return [];
  }
  }, [allOptions, searchValue]);

  // Auto-detect capabilities when a model is selected
  useEffect(() => {
    if (value && onCapabilitiesDetected) {
      const selectedModel = modelOptions.find(opt => opt.value === value);
      if (selectedModel && selectedModel.capabilities.length > 0) {
        onCapabilitiesDetected(selectedModel.capabilities);
      }
    }
  }, [value, modelOptions, onCapabilitiesDetected]);

  const handleRefresh = async (e: React.MouseEvent) => {
    e.stopPropagation();
    await refreshMutation.mutateAsync();
  };


  return (
    <Select
      label={
        <Group justify="space-between">
          <Text>{label}{required && <span style={{ color: 'var(--mantine-color-red-6)' }}> *</span>}</Text>
          {providerId && (
            <Tooltip label={refreshMutation.isPending ? "Refreshing..." : "Refresh model list"}>
              <ActionIcon
                size="xs"
                variant="subtle"
                onClick={handleRefresh}
                loading={refreshMutation.isPending}
                disabled={!providerId || refreshMutation.isPending}
              >
                <IconRefresh size={14} />
              </ActionIcon>
            </Tooltip>
          )}
        </Group>
      }
      description={
        loadError ? (
          <Group gap="xs">
            <IconAlertCircle size={14} color="var(--mantine-color-red-6)" />
            <Text size="xs" c="red">Failed to load models. You can still type a model name.</Text>
          </Group>
        ) : modelsData?.error ? (
          <Group gap="xs">
            <IconAlertCircle size={14} color="var(--mantine-color-orange-6)" />
            <Text size="xs" c="orange">No models found. Type a model name manually.</Text>
          </Group>
        ) : (
          description
        )
      }
      placeholder={placeholder}
      value={value}
      onChange={(val) => {
        onChange(val || '');
        setSearchValue(val || '');
      }}
      onSearchChange={setSearchValue}
      searchValue={searchValue}
      data={filteredOptions}
      error={error}
      searchable
      clearable
      allowDeselect
      rightSection={isLoading ? <Loader size="xs" /> : null}
      disabled={!providerId}
      nothingFoundMessage="Type to search or add custom model"
      maxDropdownHeight={280}
    />
  );
}