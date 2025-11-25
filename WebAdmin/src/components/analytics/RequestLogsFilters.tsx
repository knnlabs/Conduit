'use client';

import { useState, useEffect, useCallback } from 'react';
import {
  Group,
  Select,
  Button,
  Popover,
  Stack,
  Text,
  SegmentedControl,
} from '@mantine/core';
import { DatePickerInput } from '@mantine/dates';
import { IconCalendar, IconRefresh, IconX } from '@tabler/icons-react';
import type { RequestLogFilters } from '@/hooks/useRequestLogs';
import type { VirtualKeyDto } from '@knn_labs/conduit-admin-client';

interface RequestLogsFiltersProps {
  filters: RequestLogFilters;
  onFiltersChange: (filters: RequestLogFilters) => void;
  models: string[];
  virtualKeys: VirtualKeyDto[];
  isLoading?: boolean;
  onRefresh?: () => void;
}

type DatePreset = 'today' | '7days' | '30days' | 'custom';

export function RequestLogsFilters({
  filters,
  onFiltersChange,
  models,
  virtualKeys,
  isLoading,
  onRefresh,
}: RequestLogsFiltersProps) {
  const [dateRangeOpened, setDateRangeOpened] = useState(false);
  const [datePreset, setDatePreset] = useState<DatePreset>('7days');

  // Apply date preset
  const applyDatePreset = useCallback((preset: DatePreset) => {
    const now = new Date();
    let startDate: Date | undefined;
    let endDate: Date | undefined = now;

    switch (preset) {
      case 'today':
        startDate = new Date(now.getFullYear(), now.getMonth(), now.getDate());
        break;
      case '7days':
        startDate = new Date(now);
        startDate.setDate(now.getDate() - 7);
        break;
      case '30days':
        startDate = new Date(now);
        startDate.setDate(now.getDate() - 30);
        break;
      case 'custom':
        // Keep existing dates for custom
        startDate = filters.startDate;
        endDate = filters.endDate;
        break;
    }

    setDatePreset(preset);
    if (preset !== 'custom') {
      onFiltersChange({
        ...filters,
        startDate,
        endDate,
      });
    }
  }, [filters, onFiltersChange]);

  // Initialize with 7 days preset on mount
  useEffect(() => {
    if (!filters.startDate && !filters.endDate) {
      applyDatePreset('7days');
    }
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  const handleModelChange = (value: string | null) => {
    onFiltersChange({
      ...filters,
      model: value ?? undefined,
    });
  };

  const handleVirtualKeyChange = (value: string | null) => {
    onFiltersChange({
      ...filters,
      virtualKeyId: value ? parseInt(value, 10) : undefined,
    });
  };

  const handleStatusChange = (value: string | null) => {
    let status: number | undefined;
    if (value === 'success') {
      status = 200;
    } else if (value === 'error') {
      status = 500;
    }
    onFiltersChange({
      ...filters,
      status,
    });
  };

  const handleClearFilters = () => {
    setDatePreset('7days');
    const now = new Date();
    const startDate = new Date(now);
    startDate.setDate(now.getDate() - 7);
    onFiltersChange({
      startDate,
      endDate: now,
    });
  };

  const hasActiveFilters =
    filters.model !== undefined ||
    filters.virtualKeyId !== undefined ||
    filters.status !== undefined;

  // Map virtual keys to select data
  const virtualKeyOptions = virtualKeys.map((key) => ({
    value: key.id.toString(),
    label: key.keyName,
  }));

  // Map models to select data
  const modelOptions = models.map((model) => ({
    value: model,
    label: model,
  }));

  return (
    <Stack gap="sm">
      <Group>
        {/* Date Range Presets */}
        <SegmentedControl
          value={datePreset}
          onChange={(value) => applyDatePreset(value as DatePreset)}
          data={[
            { label: 'Today', value: 'today' },
            { label: 'Last 7 Days', value: '7days' },
            { label: 'Last 30 Days', value: '30days' },
            { label: 'Custom', value: 'custom' },
          ]}
        />

        {/* Custom Date Range Popover */}
        {datePreset === 'custom' && (
          <Popover opened={dateRangeOpened} onChange={setDateRangeOpened}>
            <Popover.Target>
              <Button
                variant="light"
                leftSection={<IconCalendar size={16} />}
                onClick={() => setDateRangeOpened((o) => !o)}
              >
                {filters.startDate && filters.endDate
                  ? `${filters.startDate.toLocaleDateString()} - ${filters.endDate.toLocaleDateString()}`
                  : 'Select Dates'}
              </Button>
            </Popover.Target>
            <Popover.Dropdown>
              <Stack gap="md">
                <Text size="sm" fw={500}>
                  Select date range
                </Text>
                <DatePickerInput
                  label="From"
                  placeholder="Start date"
                  value={filters.startDate ?? null}
                  onChange={(value) =>
                    onFiltersChange({
                      ...filters,
                      startDate: value ? new Date(value) : undefined,
                    })
                  }
                  clearable
                  maxDate={filters.endDate ?? new Date()}
                />
                <DatePickerInput
                  label="To"
                  placeholder="End date"
                  value={filters.endDate ?? null}
                  onChange={(value) =>
                    onFiltersChange({
                      ...filters,
                      endDate: value ? new Date(value) : undefined,
                    })
                  }
                  clearable
                  minDate={filters.startDate}
                  maxDate={new Date()}
                />
                <Button
                  size="sm"
                  variant="light"
                  onClick={() => setDateRangeOpened(false)}
                >
                  Apply
                </Button>
              </Stack>
            </Popover.Dropdown>
          </Popover>
        )}
      </Group>

      <Group>
        {/* Model Filter */}
        <Select
          placeholder="All models"
          value={filters.model ?? null}
          onChange={handleModelChange}
          data={modelOptions}
          clearable
          searchable
          w={200}
          disabled={models.length === 0}
        />

        {/* Virtual Key Filter */}
        <Select
          placeholder="All virtual keys"
          value={filters.virtualKeyId?.toString() ?? null}
          onChange={handleVirtualKeyChange}
          data={virtualKeyOptions}
          clearable
          searchable
          w={200}
          disabled={virtualKeys.length === 0}
        />

        {/* Status Filter */}
        <Select
          placeholder="All statuses"
          value={
            filters.status === 200
              ? 'success'
              : filters.status === 500
                ? 'error'
                : null
          }
          onChange={handleStatusChange}
          data={[
            { value: 'success', label: 'Success (2xx)' },
            { value: 'error', label: 'Error (4xx/5xx)' },
          ]}
          clearable
          w={150}
        />

        {/* Clear Filters */}
        {hasActiveFilters && (
          <Button
            variant="subtle"
            color="gray"
            size="sm"
            leftSection={<IconX size={14} />}
            onClick={handleClearFilters}
          >
            Clear filters
          </Button>
        )}

        {/* Refresh Button */}
        {onRefresh && (
          <Button
            variant="light"
            leftSection={<IconRefresh size={16} />}
            onClick={onRefresh}
            loading={isLoading}
          >
            Refresh
          </Button>
        )}
      </Group>
    </Stack>
  );
}
