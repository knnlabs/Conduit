'use client';

import { Table, Badge, ActionIcon, Group, Button, Text, Loader, Stack, Anchor } from '@mantine/core';
import { IconEdit, IconTrash, IconPlus, IconCoin } from '@tabler/icons-react';
import Link from 'next/link';
import type { NormalizedProviderTypeAssociation } from '@/lib/admin-api';

interface ProviderTypeListProps {
  associations: NormalizedProviderTypeAssociation[];
  loading: boolean;
  onAdd: () => void;
  onEdit: (association: NormalizedProviderTypeAssociation) => void;
  onDelete: (association: NormalizedProviderTypeAssociation) => void;
}

export function ProviderTypeList({ 
  associations, 
  loading, 
  onAdd, 
  onEdit, 
  onDelete 
}: ProviderTypeListProps) {
  
  if (loading) {
    return (
      <Stack align="center" py="xl">
        <Loader />
        <Text c="dimmed">Loading provider associations...</Text>
      </Stack>
    );
  }

  return (
    <Stack>
      <Group justify="space-between">
        <Text fw={500}>Provider Type Associations</Text>
        <Button 
          leftSection={<IconPlus size={16} />} 
          size="sm"
          onClick={onAdd}
        >
          Add Association
        </Button>
      </Group>

      <Table>
        <Table.Thead>
          <Table.Tr>
            <Table.Th>Identifier</Table.Th>
            <Table.Th>Provider Type</Table.Th>
            <Table.Th>Status</Table.Th>
            <Table.Th>Capability Overrides</Table.Th>
            <Table.Th>Cost</Table.Th>
            <Table.Th>Actions</Table.Th>
          </Table.Tr>
        </Table.Thead>
        <Table.Tbody>
          {associations.length === 0 ? (
            <Table.Tr>
              <Table.Td colSpan={6}>
                <Text ta="center" c="dimmed">
                  No provider associations configured
                </Text>
              </Table.Td>
            </Table.Tr>
          ) : (
            associations.map((association) => (
              <Table.Tr key={association.id}>
                <Table.Td>
                  <Text size="sm">{association.identifier}</Text>
                </Table.Td>
                <Table.Td>
                  <Badge variant="light">
                    {association.providerName ?? 'Unknown'}
                  </Badge>
                </Table.Td>
                <Table.Td>
                  {association.isPrimary && (
                    <Badge color="green" variant="light" size="sm">
                      Primary
                    </Badge>
                  )}
                </Table.Td>
                <Table.Td>
                  <Group gap={4}>
                    {association.inputModalities?.map(modality => (
                      <Badge key={`in-${modality}`} size="xs" variant="outline">in:{modality}</Badge>
                    ))}
                    {association.outputModalities?.map(modality => (
                      <Badge key={`out-${modality}`} size="xs" variant="outline">out:{modality}</Badge>
                    ))}
                    {association.operationalCapabilities && (
                      <Badge size="xs" color="orange" variant="light">operations</Badge>
                    )}
                    {!association.inputModalities && !association.outputModalities && !association.operationalCapabilities && (
                      <Text size="xs" c="dimmed">Inherited</Text>
                    )}
                  </Group>
                </Table.Td>
                <Table.Td>
                  {association.modelCostId ? (
                    <Anchor
                      component={Link}
                      href={`/model-costs?view=${association.modelCostId}`}
                      size="sm"
                      style={{ display: 'inline-flex', alignItems: 'center', gap: 4 }}
                    >
                      <IconCoin size={14} />
                      View Cost
                    </Anchor>
                  ) : (
                    <Text size="sm" c="dimmed">-</Text>
                  )}
                </Table.Td>
                <Table.Td>
                  <Group gap="xs">
                    <ActionIcon
                      variant="subtle"
                      onClick={() => onEdit(association)}
                    >
                      <IconEdit size={16} />
                    </ActionIcon>
                    <ActionIcon
                      variant="subtle"
                      color="red"
                      onClick={() => onDelete(association)}
                    >
                      <IconTrash size={16} />
                    </ActionIcon>
                  </Group>
                </Table.Td>
              </Table.Tr>
            ))
          )}
        </Table.Tbody>
      </Table>
    </Stack>
  );
}
