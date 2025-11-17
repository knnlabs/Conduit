'use client';

import { useState } from 'react';
import { Container, Title, Text, Button, Group, Stack, Tabs, Tooltip } from '@mantine/core';
import { IconPlus, IconRefresh, IconBrain, IconTags, IconUsers, IconTrash } from '@tabler/icons-react';
import { ModelsTable } from '@/components/models/ModelsTable';
import { ModelSeriesTable } from '@/components/models/ModelSeriesTable';
import { ModelAuthorsTable } from '@/components/models/ModelAuthorsTable';
import { CreateModelModal } from '@/components/models/CreateModelModal';
import { CreateModelSeriesModal } from '@/components/models/CreateModelSeriesModal';
import { CreateModelAuthorModal } from '@/components/models/CreateModelAuthorModal';
import { notifications } from '@mantine/notifications';
import { useAdminClient } from '@/lib/client/adminClient';

export default function ModelsPage() {
  const { executeWithAdmin } = useAdminClient();
  const [refreshKey, setRefreshKey] = useState(0);
  const [activeTab, setActiveTab] = useState<string | null>('models');
  const [createModelOpen, setCreateModelOpen] = useState(false);
  const [createSeriesOpen, setCreateSeriesOpen] = useState(false);
  const [createAuthorOpen, setCreateAuthorOpen] = useState(false);

  const handleRefresh = () => {
    setRefreshKey(prev => prev + 1);
  };

  const handleInvalidateCache = async () => {
    try {
      notifications.show({
        id: 'invalidating-cache',
        title: 'Invalidating Discovery Cache',
        message: 'Please wait...',
        loading: true,
        autoClose: false,
      });

      const result = await executeWithAdmin(client => 
        client.system.invalidateDiscoveryCache()
      );
      
      notifications.update({
        id: 'invalidating-cache',
        title: 'Cache Invalidated',
        message: (result as { message?: string })?.message ?? 'Discovery cache has been successfully cleared',
        color: 'green',
        loading: false,
        autoClose: 5000,
      });

      // Refresh the tables after cache invalidation
      handleRefresh();
    } catch (error) {
      console.error('Failed to invalidate cache:', error);
      notifications.update({
        id: 'invalidating-cache',
        title: 'Failed to Invalidate Cache',
        message: error instanceof Error ? error.message : 'An error occurred while invalidating the cache',
        color: 'red',
        loading: false,
        autoClose: 5000,
      });
    }
  };

  return (
    <Container size="xl">
      <Stack gap="md">
        <Group justify="space-between" align="flex-end">
          <div>
            <Title order={2}>Model Management</Title>
            <Text c="dimmed" size="sm" mt={4}>
              Configure AI models, series, and authors
            </Text>
          </div>
          <Group gap="xs">
            <Tooltip label="Clear the discovery cache to force reload of model parameters">
              <Button
                leftSection={<IconTrash size={16} />}
                variant="subtle"
                color="orange"
                onClick={() => void handleInvalidateCache()}
              >
                Clear Cache
              </Button>
            </Tooltip>
            <Button
              leftSection={<IconRefresh size={16} />}
              variant="subtle"
              onClick={handleRefresh}
            >
              Refresh
            </Button>
          </Group>
        </Group>

        <Tabs value={activeTab} onChange={setActiveTab}>
          <Tabs.List>
            <Tabs.Tab value="models" leftSection={<IconBrain size={16} />}>
              Models
            </Tabs.Tab>
            <Tabs.Tab value="series" leftSection={<IconTags size={16} />}>
              Model Series
            </Tabs.Tab>
            <Tabs.Tab value="authors" leftSection={<IconUsers size={16} />}>
              Authors
            </Tabs.Tab>
          </Tabs.List>

          <Tabs.Panel value="models" pt="md">
            <Stack gap="md">
              <Group justify="flex-end">
                <Button
                  leftSection={<IconPlus size={16} />}
                  onClick={() => setCreateModelOpen(true)}
                >
                  Add Model
                </Button>
              </Group>
              <ModelsTable 
                key={`models-${refreshKey}`}
                onRefresh={handleRefresh}
              />
            </Stack>
          </Tabs.Panel>

          <Tabs.Panel value="series" pt="md">
            <Stack gap="md">
              <Group justify="flex-end">
                <Button
                  leftSection={<IconPlus size={16} />}
                  onClick={() => setCreateSeriesOpen(true)}
                >
                  Add Series
                </Button>
              </Group>
              <ModelSeriesTable 
                key={`series-${refreshKey}`}
                onRefresh={handleRefresh}
              />
            </Stack>
          </Tabs.Panel>

          <Tabs.Panel value="authors" pt="md">
            <Stack gap="md">
              <Group justify="flex-end">
                <Button
                  leftSection={<IconPlus size={16} />}
                  onClick={() => setCreateAuthorOpen(true)}
                >
                  Add Author
                </Button>
              </Group>
              <ModelAuthorsTable 
                key={`authors-${refreshKey}`}
                onRefresh={handleRefresh}
              />
            </Stack>
          </Tabs.Panel>
        </Tabs>
      </Stack>

      <CreateModelModal
        isOpen={createModelOpen}
        onClose={() => setCreateModelOpen(false)}
        onSuccess={() => {
          setCreateModelOpen(false);
          handleRefresh();
        }}
      />

      <CreateModelSeriesModal
        isOpen={createSeriesOpen}
        onClose={() => setCreateSeriesOpen(false)}
        onSuccess={() => {
          setCreateSeriesOpen(false);
          handleRefresh();
        }}
      />

      <CreateModelAuthorModal
        isOpen={createAuthorOpen}
        onClose={() => setCreateAuthorOpen(false)}
        onSuccess={() => {
          setCreateAuthorOpen(false);
          handleRefresh();
        }}
      />
    </Container>
  );
}