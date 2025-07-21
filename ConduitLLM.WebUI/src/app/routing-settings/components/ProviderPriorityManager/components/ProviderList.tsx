'use client';

import {
  Card,
  Stack,
  Group,
  Text,
  Table,
  Badge,
} from '@mantine/core';
import { ProviderRow } from './ProviderRow';

interface ProviderDisplay {
  providerId: string;
  providerName: string;
  priority: number;
  weight?: number;
  isEnabled: boolean;
  statistics: {
    usagePercentage: number;
    successRate: number;
    avgResponseTime: number;
  };
  type: 'primary' | 'backup' | 'special';
}

interface ProviderListProps {
  providers: ProviderDisplay[];
  originalProviders: ProviderDisplay[];
  onProviderUpdate: (index: number, updates: Partial<ProviderDisplay>) => void;
  isLoading: boolean;
}

export function ProviderList({ 
  providers, 
  originalProviders, 
  onProviderUpdate, 
  isLoading 
}: ProviderListProps) {
  const getProviderOriginalIndex = (provider: ProviderDisplay) => {
    return originalProviders.findIndex(p => p.providerId === provider.providerId);
  };

  return (
    <Card shadow="sm" radius="md" withBorder>
      <Stack gap={0}>
        {/* Header */}
        <Group p="md" pb="sm">
          <Text fw={500} size="lg">Provider Priority List</Text>
          <Badge variant="light" size="sm">
            {providers.length} providers
          </Badge>
        </Group>

        {/* Table Header */}
        <Table.ScrollContainer minWidth={800}>
          <Table striped highlightOnHover>
            <Table.Thead>
              <Table.Tr>
                <Table.Th style={{ width: '80px' }}>Priority</Table.Th>
                <Table.Th>Provider</Table.Th>
                <Table.Th style={{ width: '100px' }}>Type</Table.Th>
                <Table.Th style={{ width: '100px' }}>Status</Table.Th>
                <Table.Th style={{ width: '80px' }}>Usage %</Table.Th>
                <Table.Th style={{ width: '100px' }}>Success Rate</Table.Th>
                <Table.Th style={{ width: '120px' }}>Avg Response</Table.Th>
                <Table.Th style={{ width: '80px' }}>Weight</Table.Th>
                <Table.Th style={{ width: '80px' }}>Actions</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {providers.map((provider) => {
                const originalIndex = getProviderOriginalIndex(provider);
                return (
                  <ProviderRow
                    key={provider.providerId}
                    provider={provider}
                    index={originalIndex}
                    onUpdate={onProviderUpdate}
                    isLoading={isLoading}
                    allProviders={originalProviders}
                  />
                );
              })}
            </Table.Tbody>
          </Table>
        </Table.ScrollContainer>

        {providers.length === 0 && (
          <Group justify="center" p="xl">
            <Text c="dimmed">No providers to display</Text>
          </Group>
        )}
      </Stack>
    </Card>
  );
}