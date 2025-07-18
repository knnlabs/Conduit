'use client';

import {
  Table,
  Paper,
  Box,
  LoadingOverlay,
  Text,
  Group,
  ActionIcon,
  Stack,
  UnstyledButton,
} from '@mantine/core';
import { IconRefresh, IconChevronUp, IconChevronDown, IconSelector } from '@tabler/icons-react';
import { useState, useMemo, useCallback } from 'react';
import { TableActionMenu } from './TableActionMenu';
import { ErrorDisplay } from './ErrorDisplay';
import { TablePagination } from './TablePagination';

export type SortDirection = 'asc' | 'desc';

export interface SortConfig {
  key: string;
  direction: SortDirection;
}

export interface ColumnDef<T> {
  key: string;
  label: string;
  render: (item: T) => React.ReactNode;
  width?: string;
  sortable?: boolean;
  sortType?: 'string' | 'number' | 'date' | 'currency' | 'boolean';
  accessor?: keyof T | ((item: T) => unknown);
  customSort?: (a: T, b: T, direction: SortDirection) => number;
  filterable?: boolean;
  filterType?: 'text' | 'select' | 'number' | 'date' | 'boolean';
  filterOptions?: { label: string; value: unknown }[];
}

export interface ActionDef<T> {
  label: string;
  icon: React.ComponentType<{ size?: number }>;
  onClick: (item: T) => void;
  color?: string;
  disabled?: (item: T) => boolean;
  tooltip?: string;
}

export interface DeleteConfirmation<T> {
  title: string;
  message: (item: T) => string;
}

export interface FilterConfig {
  key: string;
  value: unknown;
  operator?: 'equals' | 'contains' | 'startsWith' | 'endsWith' | 'gt' | 'gte' | 'lt' | 'lte';
}

export interface BaseTableProps<T> {
  // Data props
  data?: T[];
  isLoading?: boolean;
  error?: Error | null;
  
  // Table configuration
  columns: ColumnDef<T>[];
  minWidth?: number;
  
  // Filtering
  filters?: FilterConfig[];
  onFiltersChange?: (filters: FilterConfig[]) => void;
  searchable?: boolean;
  searchPlaceholder?: string;
  
  // Pagination
  pagination?: {
    page: number;
    pageSize: number;
    total: number;
    onPageChange: (page: number) => void;
    onPageSizeChange: (pageSize: number) => void;
    pageSizeOptions?: string[];
  };
  
  // Actions
  onEdit?: (item: T) => void;
  onDelete?: (item: T) => void;
  onRefresh?: () => void;
  customActions?: ActionDef<T>[];
  
  // Messages
  emptyMessage?: string;
  loadingMessage?: string;
  errorMessage?: string;
  
  // Delete confirmation
  deleteConfirmation?: DeleteConfirmation<T>;
  
  // Styling
  withBorder?: boolean;
  radius?: string;
  
  // Additional props
  className?: string;
  id?: string;
}

export function BaseTable<T extends Record<string, unknown>>({
  data,
  isLoading = false,
  error,
  columns,
  minWidth = 800,
  filters = [],
  pagination,
  onEdit,
  onDelete,
  onRefresh,
  customActions = [],
  emptyMessage = 'No data available',
  loadingMessage = 'Loading...',
  deleteConfirmation,
  withBorder = true,
  radius = 'md',
  className,
  id,
}: BaseTableProps<T>) {
  // Sort state
  const [sortConfig, setSortConfig] = useState<SortConfig | null>(null);
  
  // Filter state
  const [localFilters] = useState<FilterConfig[]>(filters);
  
  // Sort comparison functions
  const getSortValue = useCallback((item: T, column: ColumnDef<T>) => {
    if (column.accessor) {
      return typeof column.accessor === 'function' 
        ? column.accessor(item) 
        : item[column.accessor];
    }
    return item[column.key];
  }, []);
  
  const compareSortValues = useCallback((a: unknown, b: unknown, sortType: string, direction: SortDirection): number => {
    // Handle null/undefined values
    if (a === null || a === undefined) {
      if (b === null || b === undefined) return 0;
      return direction === 'asc' ? -1 : 1;
    }
    if (b === null || b === undefined) {
      return direction === 'asc' ? 1 : -1;
    }
    
    let comparison = 0;
    
    switch (sortType) {
      case 'number':
      case 'currency':
        comparison = Number(a) - Number(b);
        break;
      case 'date':
        const dateA = typeof a === 'string' || typeof a === 'number' || a instanceof Date ? new Date(a) : new Date(0);
        const dateB = typeof b === 'string' || typeof b === 'number' || b instanceof Date ? new Date(b) : new Date(0);
        comparison = dateA.getTime() - dateB.getTime();
        break;
      case 'boolean':
        comparison = getBooleanComparison(a, b);
        break;
      case 'string':
      default:
        comparison = getStringComparison(a, b);
        break;
    }
    
    return direction === 'asc' ? comparison : -comparison;
  }, []);
  
  // Filter functions
  const matchesFilter = useCallback((item: T, filter: FilterConfig): boolean => {
    const column = columns.find(col => col.key === filter.key);
    if (!column) return true;
    
    const value = getSortValue(item, column);
    const filterValue = filter.value;
    
    if (value === null || value === undefined || filterValue === null || filterValue === undefined || filterValue === '') return true;
    
    const stringValue = getStringValue(value).toLowerCase();
    const stringFilterValue = getStringValue(filterValue).toLowerCase();
    
    switch (filter.operator ?? 'contains') {
      case 'equals':
        return stringValue === stringFilterValue;
      case 'contains':
        return stringValue.includes(stringFilterValue);
      case 'startsWith':
        return stringValue.startsWith(stringFilterValue);
      case 'endsWith':
        return stringValue.endsWith(stringFilterValue);
      case 'gt':
        return Number(value) > Number(filterValue);
      case 'gte':
        return Number(value) >= Number(filterValue);
      case 'lt':
        return Number(value) < Number(filterValue);
      case 'lte':
        return Number(value) <= Number(filterValue);
      default:
        return true;
    }
  }, [columns, getSortValue]);
  
  
  // Helper functions
  const getBooleanComparison = (a: unknown, b: unknown): number => {
    if (a === b) return 0;
    return a ? 1 : -1;
  };

  const getStringComparison = (a: unknown, b: unknown): number => {
    let aStr: string;
    let bStr: string;
    
    if (typeof a === 'string') {
      aStr = a;
    } else if (a === null || a === undefined) {
      aStr = '';
    } else if (typeof a === 'number' || typeof a === 'boolean') {
      aStr = String(a);
    } else {
      aStr = '';
    }
    
    if (typeof b === 'string') {
      bStr = b;
    } else if (b === null || b === undefined) {
      bStr = '';
    } else if (typeof b === 'number' || typeof b === 'boolean') {
      bStr = String(b);
    } else {
      bStr = '';
    }
    
    return aStr.localeCompare(bStr, undefined, { numeric: true });
  };

  const getStringValue = (value: unknown): string => {
    if (typeof value === 'string') {
      return value;
    }
    if (value === null || value === undefined) {
      return '';
    }
    if (typeof value === 'number' || typeof value === 'boolean') {
      return String(value);
    }
    return '';
  };

  // Filter and sort data
  const filteredAndSortedData = useMemo(() => {
    let result = data ?? [];
    
    // Apply filters
    if (localFilters.length > 0) {
      result = result.filter(item => 
        localFilters.every(filter => matchesFilter(item, filter))
      );
    }
    
    // Apply sorting
    if (sortConfig) {
      const column = columns.find(col => col.key === sortConfig.key);
      if (column) {
        result = [...result].sort((a, b) => {
          if (column.customSort) {
            return column.customSort(a, b, sortConfig.direction);
          }
          
          const aValue = getSortValue(a, column);
          const bValue = getSortValue(b, column);
          const sortType = column.sortType ?? 'string';
          
          return compareSortValues(aValue, bValue, sortType, sortConfig.direction);
        });
      }
    }
    
    return result;
  }, [data, localFilters, sortConfig, columns, getSortValue, matchesFilter, compareSortValues]);
  
  // Pagination
  const paginatedData = useMemo(() => {
    if (!pagination) return filteredAndSortedData;
    
    const startIndex = (pagination.page - 1) * pagination.pageSize;
    const endIndex = startIndex + pagination.pageSize;
    return filteredAndSortedData.slice(startIndex, endIndex);
  }, [filteredAndSortedData, pagination]);
  
  // Update pagination total when filtered data changes
  const totalItems = filteredAndSortedData.length;
  
  // Handle sort
  const handleSort = (columnKey: string) => {
    const column = columns.find(col => col.key === columnKey);
    if (!column?.sortable) return;
    
    setSortConfig(prevConfig => {
      if (prevConfig?.key === columnKey) {
        if (prevConfig.direction === 'asc') {
          return { key: columnKey, direction: 'desc' };
        } else {
          return null; // Remove sort
        }
      } else {
        return { key: columnKey, direction: 'asc' };
      }
    });
  };
  
  // Get sort icon
  const getSortIcon = (columnKey: string) => {
    if (sortConfig?.key === columnKey) {
      return sortConfig.direction === 'asc' 
        ? <IconChevronUp size={14} /> 
        : <IconChevronDown size={14} />;
    }
    return <IconSelector size={14} opacity={0.5} />;
  };
  
  // Error state
  if (error && !isLoading) {
    return (
      <ErrorDisplay 
        error={error}
        title="Error loading data"
        variant="inline"
        onRetry={onRefresh}
        className={className}
      />
    );
  }

  // Prepare actions for the action menu
  const hasActions = onEdit ?? onDelete ?? customActions.length > 0;
  const actions: ActionDef<T>[] = [
    ...customActions,
    ...(onEdit ? [{
      label: 'Edit',
      icon: () => null, // Will be handled by TableActionMenu
      onClick: onEdit,
    }] : []),
    ...(onDelete ? [{
      label: 'Delete',
      icon: () => null, // Will be handled by TableActionMenu
      onClick: onDelete,
      color: 'red' as const,
    }] : []),
  ];

  // Removed unused filter functions
  
  // Get active filter count
  const activeFilterCount = localFilters.length;
  
  // Table rows
  const displayData = pagination ? paginatedData : filteredAndSortedData;
  const rows = displayData?.map((item, index) => (
    <Table.Tr key={typeof item.id === 'string' ? `${item.id}-${index}` : `item-${index}`}>
      {columns.map((column) => (
        <Table.Td key={column.key} style={{ width: column.width }}>
          {column.render(item)}
        </Table.Td>
      ))}
      {hasActions && (
        <Table.Td style={{ width: '60px' }}>
          <Group gap={0} justify="flex-end">
            <TableActionMenu
              item={item}
              actions={actions}
              deleteConfirmation={deleteConfirmation}
            />
          </Group>
        </Table.Td>
      )}
    </Table.Tr>
  ));

  return (
    <Paper withBorder={withBorder} radius={radius} className={className} id={id}>
      <Box pos="relative">
        <LoadingOverlay 
          visible={isLoading} 
          overlayProps={{ radius: 'sm', blur: 2 }}
          loaderProps={{ children: loadingMessage }}
        />
        
        {/* Refresh button */}
        {onRefresh && (
          <Box pos="absolute" top={16} right={16} style={{ zIndex: 1 }}>
            <ActionIcon 
              variant="subtle" 
              color="gray"
              onClick={onRefresh}
              disabled={isLoading}
            >
              <IconRefresh size={16} />
            </ActionIcon>
          </Box>
        )}
        
        <Table.ScrollContainer minWidth={minWidth}>
          <Table verticalSpacing="sm" horizontalSpacing="md">
            <Table.Thead>
              <Table.Tr>
                {columns.map((column) => (
                  <Table.Th key={column.key} style={{ width: column.width }}>
                    {column.sortable ? (
                      <UnstyledButton
                        onClick={() => handleSort(column.key)}
                        style={{ 
                          display: 'flex', 
                          alignItems: 'center', 
                          width: '100%',
                          justifyContent: 'space-between'
                        }}
                      >
                        <Text fw={500}>{column.label}</Text>
                        {getSortIcon(column.key)}
                      </UnstyledButton>
                    ) : (
                      <Text fw={500}>{column.label}</Text>
                    )}
                  </Table.Th>
                ))}
                {hasActions && (
                  <Table.Th style={{ width: '60px' }}>
                    <Text ta="right" fw={500}>Actions</Text>
                  </Table.Th>
                )}
              </Table.Tr>
            </Table.Thead>
            
            <Table.Tbody>
              {rows}
            </Table.Tbody>
          </Table>
        </Table.ScrollContainer>

        {/* Empty state */}
        {displayData?.length === 0 && !isLoading && (
          <Box p="xl" style={{ textAlign: 'center' }}>
            <Stack gap="xs" align="center">
              <Text c="dimmed" size="sm">
                {activeFilterCount > 0 ? 'No results match your filters' : emptyMessage}
              </Text>
              {onRefresh && (
                <ActionIcon 
                  variant="light" 
                  color="blue"
                  onClick={onRefresh}
                >
                  <IconRefresh size={16} />
                </ActionIcon>
              )}
            </Stack>
          </Box>
        )}
        
        {/* Pagination */}
        {pagination && totalItems > 0 && (
          <Box p="md" style={{ borderTop: '1px solid var(--mantine-color-gray-3)' }}>
            <TablePagination
              total={totalItems}
              page={pagination.page}
              pageSize={pagination.pageSize}
              onPageChange={pagination.onPageChange}
              onPageSizeChange={pagination.onPageSizeChange}
              pageSizeOptions={pagination.pageSizeOptions}
            />
          </Box>
        )}
      </Box>
    </Paper>
  );
}