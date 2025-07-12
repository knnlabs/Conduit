'use client';

import { useState } from 'react';
import { Container, Title, Text, Button, Group, Stack } from '@mantine/core';
import { IconPlus, IconRefresh, IconWand, IconFileImport } from '@tabler/icons-react';
import { notifications } from '@mantine/notifications';
import { ModelMappingsTable } from '@/components/modelmappings/ModelMappingsTableWithHooks';
import { CreateModelMappingModal } from '@/components/modelmappings/CreateModelMappingModal';
import { BulkMappingModal } from '@/components/modelmappings/BulkMappingModal';
import { useAuthStore } from '@/stores/useAuthStore';
import { useDiscoverModels } from '@/hooks/useModelMappingsApi';
import { ProtectedRoute } from '@/components/auth/ProtectedRoute';

export default function ModelMappingsPage() {
  const [refreshKey, setRefreshKey] = useState(0);
  const [createModalOpen, setCreateModalOpen] = useState(false);
  const [bulkModalOpen, setBulkModalOpen] = useState(false);
  const { user } = useAuthStore();
  const isAdmin = true; // All authenticated users can manage model mappings
  const { discoverModels, isDiscovering } = useDiscoverModels();

  const handleRefresh = () => {
    setRefreshKey(prev => prev + 1);
  };

  const handleDiscoverModels = async () => {
    if (!isAdmin) {
      notifications.show({
        title: 'Permission Denied',
        message: 'Only administrators can discover models',
        color: 'red',
      });
      return;
    }

    try {
      await discoverModels(false, false);
      // Refresh the table to show any new mappings
      handleRefresh();
    } catch (error) {
      // Error notifications are handled by the hook
    }
  };

  return (
    <ProtectedRoute>
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
                leftSection={<IconWand size={16} />}
                variant="light"
                onClick={handleDiscoverModels}
                loading={isDiscovering}
                disabled={!isAdmin}
              >
                Discover Models
              </Button>
              <Button
                leftSection={<IconFileImport size={16} />}
                variant="light"
                onClick={() => setBulkModalOpen(true)}
                disabled={!isAdmin}
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
                disabled={!isAdmin}
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
    </ProtectedRoute>
  );
}