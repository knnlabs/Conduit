import { Stack, Skeleton, Group, Card, Table } from '@mantine/core';
import React from 'react';

interface CardSkeletonProps {
  count?: number;
  height?: number;
}

export function CardSkeleton({ count = 3, height = 120 }: CardSkeletonProps) {
  return (
    <Stack gap="md">
      {Array.from({ length: count }).map((_, index) => (
        <Card key={index} withBorder>
          <Stack gap="sm">
            <Group justify="space-between">
              <Skeleton height={20} width="30%" />
              <Skeleton height={20} width="20%" />
            </Group>
            <Skeleton height={height - 40} />
          </Stack>
        </Card>
      ))}
    </Stack>
  );
}

interface TableSkeletonProps {
  rows?: number;
  columns?: number;
}

export function TableSkeleton({ rows = 5, columns = 4 }: TableSkeletonProps) {
  return (
    <Table>
      <Table.Thead>
        <Table.Tr>
          {Array.from({ length: columns }).map((_, index) => (
            <Table.Th key={index}>
              <Skeleton height={16} width="80%" />
            </Table.Th>
          ))}
        </Table.Tr>
      </Table.Thead>
      <Table.Tbody>
        {Array.from({ length: rows }).map((_, rowIndex) => (
          <Table.Tr key={rowIndex}>
            {Array.from({ length: columns }).map((_, colIndex) => (
              <Table.Td key={colIndex}>
                <Skeleton height={16} width={colIndex === 0 ? '60%' : '40%'} />
              </Table.Td>
            ))}
          </Table.Tr>
        ))}
      </Table.Tbody>
    </Table>
  );
}

interface FormSkeletonProps {
  fields?: number;
}

export function FormSkeleton({ fields = 4 }: FormSkeletonProps) {
  return (
    <Stack gap="md">
      {Array.from({ length: fields }).map((_, index) => (
        <div key={index}>
          <Skeleton height={12} width="20%" mb={8} />
          <Skeleton height={36} />
        </div>
      ))}
      <Group mt="md">
        <Skeleton height={36} width={100} />
        <Skeleton height={36} width={100} />
      </Group>
    </Stack>
  );
}