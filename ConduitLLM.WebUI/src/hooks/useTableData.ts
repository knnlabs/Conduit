'use client';

import { notifications } from '@mantine/notifications';
import type { UseMutationResult, UseQueryResult } from '@tanstack/react-query';

export interface UseTableDataOptions<T> {
  queryResult: UseQueryResult<T[], Error>;
  deleteMutation?: UseMutationResult<void, Error, string>;
  refreshMessage?: string;
  deleteSuccessMessage?: string;
  deleteErrorMessage?: string;
}

export interface UseTableDataReturn<T> {
  data: T[] | undefined;
  isLoading: boolean;
  error: Error | null;
  handleRefresh: () => void;
  handleDelete: (id: string) => Promise<void>;
  isDeleting: boolean;
}

export function useTableData<T>({
  queryResult,
  deleteMutation,
  refreshMessage = 'Data refreshed',
  deleteSuccessMessage = 'Item deleted successfully',
  deleteErrorMessage = 'Failed to delete item',
}: UseTableDataOptions<T>): UseTableDataReturn<T> {
  
  const { data, isLoading, error, refetch } = queryResult;
  
  const handleRefresh = () => {
    refetch();
    notifications.show({
      title: 'Refreshing',
      message: refreshMessage,
      color: 'blue',
    });
  };

  const handleDelete = async (id: string) => {
    if (!deleteMutation) {
      throw new Error('Delete mutation not provided');
    }

    try {
      await deleteMutation.mutateAsync(id);
      notifications.show({
        title: 'Success',
        message: deleteSuccessMessage,
        color: 'green',
      });
      // Refresh data after successful delete
      refetch();
    } catch (error) {
      notifications.show({
        title: 'Error',
        message: deleteErrorMessage,
        color: 'red',
      });
      throw error;
    }
  };

  return {
    data,
    isLoading,
    error,
    handleRefresh,
    handleDelete,
    isDeleting: deleteMutation?.isPending || false,
  };
}

// Common formatters for table cells
export const tableFormatters = {
  date: (date: string | Date) => {
    if (!date) return 'N/A';
    return new Date(date).toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  },

  dateOnly: (date: string | Date) => {
    if (!date) return 'N/A';
    return new Date(date).toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
    });
  },

  currency: (amount: number, currency = 'USD') => {
    if (amount === null || amount === undefined) return 'N/A';
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency,
      minimumFractionDigits: 2,
      maximumFractionDigits: 4,
    }).format(amount);
  },

  number: (num: number, decimals = 0) => {
    if (num === null || num === undefined) return 'N/A';
    return new Intl.NumberFormat('en-US', {
      minimumFractionDigits: decimals,
      maximumFractionDigits: decimals,
    }).format(num);
  },

  percentage: (value: number, decimals = 1) => {
    if (value === null || value === undefined) return 'N/A';
    return `${(value * 100).toFixed(decimals)}%`;
  },

  truncate: (text: string, maxLength = 50) => {
    if (!text) return 'N/A';
    if (text.length <= maxLength) return text;
    return `${text.substring(0, maxLength)}...`;
  },

  boolean: (value: boolean, trueText = 'Yes', falseText = 'No') => {
    return value ? trueText : falseText;
  },

  status: (isEnabled: boolean) => {
    return isEnabled ? 'Active' : 'Inactive';
  },

  statusColor: (isEnabled: boolean) => {
    return isEnabled ? 'green' : 'red';
  },

  priority: (priority: number) => {
    if (priority >= 90) return { text: 'Very High', color: 'red' };
    if (priority >= 70) return { text: 'High', color: 'orange' };
    if (priority >= 50) return { text: 'Medium', color: 'yellow' };
    if (priority >= 30) return { text: 'Low', color: 'blue' };
    return { text: 'Very Low', color: 'gray' };
  },
};