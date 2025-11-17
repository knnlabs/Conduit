'use client';

import { Group, Pagination, Select, Text } from '@mantine/core';

interface TablePaginationProps {
  total: number;
  page: number;
  pageSize: number;
  onPageChange: (page: number) => void;
  onPageSizeChange: (pageSize: number) => void;
  pageSizeOptions?: string[];
}

export function TablePagination({
  total,
  page,
  pageSize,
  onPageChange,
  onPageSizeChange,
  pageSizeOptions = ['10', '20', '50', '100'],
}: TablePaginationProps) {
  const totalPages = Math.ceil(total / pageSize);
  const start = (page - 1) * pageSize + 1;
  const end = Math.min(page * pageSize, total);

  return (
    <Group justify="space-between" mt="md">
      <Group gap="xs">
        <Text size="sm" c="dimmed">
          Showing {start} to {end} of {total} entries
        </Text>
      </Group>

      <Group gap="xs">
        <Text size="sm" c="dimmed">
          Rows per page:
        </Text>
        <Select
          size="xs"
          value={pageSize.toString()}
          onChange={(value) => {
            if (value) {
              onPageSizeChange(parseInt(value));
              onPageChange(1); // Reset to first page when changing page size
            }
          }}
          data={pageSizeOptions}
          w={70}
        />
      </Group>

      <Pagination
        value={page}
        onChange={onPageChange}
        total={totalPages}
        size="sm"
        radius="md"
        withEdges
      />
    </Group>
  );
}