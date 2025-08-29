'use client';

import { Table, Badge, ActionIcon, Group, Button, Text, Loader, Stack } from '@mantine/core';
import { IconEdit, IconTrash, IconPlus } from '@tabler/icons-react';

interface ProviderTypeAssociation {
  id: number;
  identifier: string;
  provider: string;
  isPrimary: boolean;
}

interface ProviderTypeListProps {
  associations: ProviderTypeAssociation[];
  loading: boolean;
  onAdd: () => void;
  onEdit: (association: ProviderTypeAssociation) => void;
  onDelete: (association: ProviderTypeAssociation) => void;
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
            <Table.Th>Actions</Table.Th>
          </Table.Tr>
        </Table.Thead>
        <Table.Tbody>
          {associations.length === 0 ? (
            <Table.Tr>
              <Table.Td colSpan={4}>
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
                    {association.provider}
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