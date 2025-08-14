'use client';

import React, { useMemo, useState, useEffect } from 'react';
import { Combobox, useCombobox, Loader, Text, Stack, Badge, Group, TextInput, Divider } from '@mantine/core';
import { useDebouncedValue } from '@mantine/hooks';
import { IconSearch } from '@tabler/icons-react';
import { useModelMappings } from '@/hooks/useModelMappingsApi';
import type { ProviderType } from '@knn_labs/conduit-admin-client';

interface ModelPatternComboboxProps {
  value: string;
  onChange: (value: string) => void;
  selectedProvider: string | null;
  selectedProviderId?: number;
  selectedProviderType?: number;
  error?: string;
  required?: boolean;
  disabled?: boolean;
}

interface ComboboxOption {
  value: string;
  label: string;
  group: 'pattern' | 'models';
  modelId?: string;
}

export function ModelPatternCombobox({
  value,
  onChange,
  selectedProvider,
  selectedProviderId,
  selectedProviderType,
  error,
  required = false,
  disabled = false,
}: ModelPatternComboboxProps) {
  const [search, setSearch] = useState(value || '');
  const [debouncedSearch] = useDebouncedValue(search, 200);
  const { mappings, isLoading } = useModelMappings();
  
  const combobox = useCombobox({
    onDropdownClose: () => combobox.resetSelectedOption(),
  });

  // Update search when value changes externally
  useEffect(() => {
    setSearch(value || '');
  }, [value]);

  // Filter mappings based on selected provider and search term
  const options = useMemo(() => {
    const items: ComboboxOption[] = [];
    
    if (!selectedProvider || (!selectedProviderId && !selectedProviderType)) {
      return items;
    }

    // Filter mappings by selected provider - prefer providerId, fall back to providerType
    const providerMappings = mappings.filter((mapping) => {
      if (selectedProviderId && mapping.providerId) {
        return mapping.providerId === selectedProviderId;
      }
      if (selectedProviderType !== undefined && mapping.providerType !== undefined) {
        return mapping.providerType === selectedProviderType as ProviderType;
      }
      return false;
    });

    // If there's a search term, add it as a pattern option
    if (debouncedSearch.trim()) {
      items.push({
        value: debouncedSearch,
        label: debouncedSearch,
        group: 'pattern',
      });
    }

    // Add filtered model IDs
    const filteredModels = providerMappings.filter((mapping) => {
      if (!debouncedSearch.trim()) return true;
      return mapping.modelId.toLowerCase().includes(debouncedSearch.toLowerCase());
    });

    // Remove duplicates and add to options
    const uniqueModelIds = Array.from(new Set(filteredModels.map(m => m.modelId)));
    uniqueModelIds.forEach((modelId) => {
      items.push({
        value: modelId,
        label: modelId,
        group: 'models',
        modelId: modelId,
      });
    });

    return items;
  }, [mappings, selectedProvider, selectedProviderId, selectedProviderType, debouncedSearch]);

  // Group options by type
  const groupedOptions = useMemo(() => {
    const groups: Record<string, ComboboxOption[]> = {
      pattern: [],
      models: [],
    };

    options.forEach((option) => {
      groups[option.group].push(option);
    });

    return groups;
  }, [options]);

  const handleOptionSubmit = (val: string) => {
    onChange(val);
    setSearch(val);
    combobox.closeDropdown();
  };

  const handleSearchChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const newValue = event.currentTarget.value;
    setSearch(newValue);
    onChange(newValue);
    combobox.openDropdown();
    combobox.updateSelectedOptionIndex();
  };

  const shouldShowDropdown = selectedProvider && (options.length > 0 || isLoading);

  return (
    <Combobox
      store={combobox}
      withinPortal={false}
      onOptionSubmit={handleOptionSubmit}
    >
      <Combobox.Target>
        <TextInput
          label="Model Pattern"
          placeholder={
            selectedProvider
              ? "e.g., openai/gpt-4, anthropic/claude-3*, minimax/abab6.5g"
              : "Select a provider first"
          }
          value={search}
          onChange={handleSearchChange}
          onClick={() => {
            if (shouldShowDropdown) {
              combobox.openDropdown();
            }
          }}
          onFocus={() => {
            if (shouldShowDropdown) {
              combobox.openDropdown();
            }
          }}
          onBlur={() => {
            if (!combobox.dropdownOpened) {
              onChange(search);
            }
          }}
          error={error}
          required={required}
          disabled={disabled || !selectedProvider}
          rightSection={isLoading ? <Loader size={18} /> : <IconSearch size={18} />}
          description="Exact model ID or pattern with * wildcard"
        />
      </Combobox.Target>

      <Combobox.Dropdown>
        <Combobox.Options>
          {isLoading && (
            <Combobox.Empty>
              <Group gap="xs">
                <Loader size={18} />
                <Text size="sm" c="dimmed">Loading models...</Text>
              </Group>
            </Combobox.Empty>
          )}

          {!isLoading && options.length === 0 && selectedProvider && (
            <Combobox.Empty>
              <Text size="sm" c="dimmed">
                {debouncedSearch.trim() 
                  ? 'No matching models found' 
                  : 'No models mapped for this provider'}
              </Text>
            </Combobox.Empty>
          )}

          {groupedOptions.pattern.length > 0 && (
            <>
              <Combobox.Group label="Your Pattern">
                {groupedOptions.pattern.map((item) => (
                  <Combobox.Option value={item.value} key={`pattern-${item.value}`}>
                    <Stack gap={4}>
                      <Text size="sm">{item.label}</Text>
                      <Badge size="xs" variant="light" color="blue">
                        Custom Pattern
                      </Badge>
                    </Stack>
                  </Combobox.Option>
                ))}
              </Combobox.Group>
            </>
          )}

          {groupedOptions.models.length > 0 && (
            <>
              {groupedOptions.pattern.length > 0 && <Divider />}
              <Combobox.Group label="Existing Models">
                {groupedOptions.models.map((item) => (
                  <Combobox.Option value={item.value} key={`model-${item.value}`}>
                    <Stack gap={4}>
                      <Text size="sm">{item.label}</Text>
                      <Badge size="xs" variant="light" color="green">
                        Mapped Model
                      </Badge>
                    </Stack>
                  </Combobox.Option>
                ))}
              </Combobox.Group>
            </>
          )}
        </Combobox.Options>
      </Combobox.Dropdown>
    </Combobox>
  );
}