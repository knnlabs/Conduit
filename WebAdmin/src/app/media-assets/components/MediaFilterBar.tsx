'use client';

import { useState, useEffect } from 'react';
import { 
  Group, 
  Select, 
  TextInput, 
  Button, 
  SegmentedControl,
  Popover,
  Stack,
  Text,
} from '@mantine/core';
import { DatePickerInput } from '@mantine/dates';
import { IconSearch, IconCalendar } from '@tabler/icons-react';
import { MediaFilters } from '../types';
import { useDebouncedValue } from '@mantine/hooks';

interface MediaFilterBarProps {
  filters: MediaFilters;
  onFiltersChange: (filters: Partial<MediaFilters>) => void;
  providers: string[];
  virtualKeyId?: number;
}

export default function MediaFilterBar({ 
  filters, 
  onFiltersChange, 
  providers,
}: MediaFilterBarProps) {
  const [searchValue, setSearchValue] = useState(filters.searchQuery ?? '');
  const [debouncedSearch] = useDebouncedValue(searchValue, 300);
  const [dateRangeOpened, setDateRangeOpened] = useState(false);

  useEffect(() => {
    onFiltersChange({ searchQuery: debouncedSearch });
  }, [debouncedSearch, onFiltersChange]);

  return (
    <Group>
      <SegmentedControl
        value={filters.mediaType ?? 'all'}
        onChange={(value) => onFiltersChange({ mediaType: value as 'all' | 'image' | 'video' })}
        data={[
          { label: 'All', value: 'all' },
          { label: 'Images', value: 'image' },
          { label: 'Videos', value: 'video' },
        ]}
      />

      <Select
        placeholder="All providers"
        value={filters.provider ?? null}
        onChange={(value) => onFiltersChange({ provider: value ?? undefined })}
        data={providers}
        clearable
        w={200}
      />

      <TextInput
        placeholder="Search by prompt or key..."
        value={searchValue}
        onChange={(e) => setSearchValue(e.currentTarget.value)}
        leftSection={<IconSearch size={16} />}
        w={300}
      />

      <Popover opened={dateRangeOpened} onChange={setDateRangeOpened}>
        <Popover.Target>
          <Button 
            variant="light" 
            leftSection={<IconCalendar size={16} />}
            onClick={() => setDateRangeOpened((o) => !o)}
          >
            Date Range
          </Button>
        </Popover.Target>
        <Popover.Dropdown>
          <Stack gap="md">
            <Text size="sm" fw={500}>Filter by date range</Text>
            <DatePickerInput
              label="From"
              placeholder="Start date"
              value={(() => {
                if (!filters.fromDate) return null;
                return filters.fromDate instanceof Date ? filters.fromDate : new Date(filters.fromDate);
              })()}
              onChange={(value) => onFiltersChange({ fromDate: value ?? undefined })}
              clearable
            />
            <DatePickerInput
              label="To"
              placeholder="End date"
              value={(() => {
                if (!filters.toDate) return null;
                return filters.toDate instanceof Date ? filters.toDate : new Date(filters.toDate);
              })()}
              onChange={(value) => onFiltersChange({ toDate: value ?? undefined })}
              clearable
            />
            <Button 
              size="sm" 
              variant="light"
              onClick={() => {
                onFiltersChange({ fromDate: undefined, toDate: undefined });
                setDateRangeOpened(false);
              }}
            >
              Clear dates
            </Button>
          </Stack>
        </Popover.Dropdown>
      </Popover>

      <Select
        placeholder="Sort by"
        value={`${filters.sortBy}:${filters.sortOrder}`}
        onChange={(value) => {
          if (value) {
            const [sortBy, sortOrder] = value.split(':') as ['createdAt' | 'sizeBytes' | 'accessCount', 'asc' | 'desc'];
            onFiltersChange({ sortBy, sortOrder });
          }
        }}
        data={[
          { label: 'Newest first', value: 'createdAt:desc' },
          { label: 'Oldest first', value: 'createdAt:asc' },
          { label: 'Largest first', value: 'sizeBytes:desc' },
          { label: 'Smallest first', value: 'sizeBytes:asc' },
          { label: 'Most accessed', value: 'accessCount:desc' },
        ]}
        w={180}
      />
    </Group>
  );
}