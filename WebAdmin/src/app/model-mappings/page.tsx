'use client';

import { useState } from 'react';
import { Container, Title, Text, Button, Group, Stack } from '@mantine/core';
import { IconPlus, IconRefresh, IconFileImport } from '@tabler/icons-react';
import { ModelMappingsTable } from '@/components/modelmappings/ModelMappingsTableWithHooks';
import { CreateModelMappingModal } from '@/components/modelmappings/CreateModelMappingModal';
import { BulkMappingModal } from '@/components/modelmappings/BulkMappingModal';

export default function ModelMappingsPage() {
  const [refreshKey, setRefreshKey] = useState(0);
  const [createModalOpen, setCreateModalOpen] = useState(false);
  const [bulkModalOpen, setBulkModalOpen] = useState(false);

  const handleRefresh = () => {
    setRefreshKey(prev => prev + 1);
  };

  return (
    <Container size="xl">
      <Stack gap="md">
        <Group justify="space-between" align="flex-end">
          <div>
            <Title order={2}>Model Mappings</Title>
            <Text c="dimmed" size="sm" mt={4}>
              Configure how models are routed to different providers
            </Text>
          </div>
          <Group>
            <Button
              leftSection={<IconFileImport size={16} />}
              variant="light"
              onClick={() => setBulkModalOpen(true)}
            >
              Bulk Import
            </Button>
            <Button
              leftSection={<IconRefresh size={16} />}
              variant="subtle"
              onClick={handleRefresh}
            >
              Refresh
            </Button>
            <Button
              leftSection={<IconPlus size={16} />}
              onClick={() => setCreateModalOpen(true)}
            >
              Add Mapping
            </Button>
          </Group>
        </Group>

        <ModelMappingsTable 
          key={refreshKey} 
          onRefresh={handleRefresh}
        />
      </Stack>

      <CreateModelMappingModal
        isOpen={createModalOpen}
        onClose={() => setCreateModalOpen(false)}
        onSuccess={() => {
          setCreateModalOpen(false);
          handleRefresh();
        }}
      />
      
      <BulkMappingModal
        isOpen={bulkModalOpen}
        onClose={() => setBulkModalOpen(false)}
        onSuccess={() => {
          setBulkModalOpen(false);
          handleRefresh();
        }}
      />
    </Container>
  );
}