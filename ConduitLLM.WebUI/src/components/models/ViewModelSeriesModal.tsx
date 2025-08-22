'use client';

import { useState, useEffect } from 'react';
import { Modal, Stack, Group, Text, Badge, Title, Divider, ScrollArea } from '@mantine/core';
import { CodeHighlight } from '@mantine/code-highlight';
import { useAdminClient } from '@/lib/client/adminClient';
import { notifications } from '@mantine/notifications';
import type { ModelSeriesDto, SeriesSimpleModelDto } from '@knn_labs/conduit-admin-client';


interface ViewModelSeriesModalProps {
  isOpen: boolean;
  series: ModelSeriesDto;
  onClose: () => void;
}

export function ViewModelSeriesModal({ isOpen, series, onClose }: ViewModelSeriesModalProps) {
  const [models, setModels] = useState<SeriesSimpleModelDto[]>([]);
  const [loading, setLoading] = useState(false);
  const { executeWithAdmin } = useAdminClient();

  useEffect(() => {
    if (isOpen && series?.id) {
      void loadModels();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isOpen, series?.id]);

  const loadModels = async () => {
    try {
      setLoading(true);
      if (!series.id) throw new Error('Series ID is required');
      const data = await executeWithAdmin(client => client.modelSeries.getModels(series.id as number));
      setModels(data);
    } catch (error) {
      console.error('Failed to load models in series:', error);
      notifications.show({
        title: 'Error',
        message: 'Failed to load models in series',
        color: 'red',
      });
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      opened={isOpen}
      onClose={onClose}
      title={<Title order={3}>{series.name}</Title>}
      size="lg"
    >
      <Stack>
        <Group justify="space-between">
          <Text fw={500}>Series Name:</Text>
          <Text>{series.name}</Text>
        </Group>

        {/* displayName field doesn't exist in ModelSeriesDto */}

        <Group justify="space-between">
          <Text fw={500}>Author:</Text>
          <Text>{series.authorName ?? '-'}</Text>
        </Group>

        {/* isActive field doesn't exist in ModelSeriesDto */}

        {series.description && (
          <>
            <Divider />
            <Stack gap="xs">
              <Text fw={500}>Description:</Text>
              <Text size="sm">{series.description}</Text>
            </Stack>
          </>
        )}

        {series.parameters && (
          <>
            <Divider />
            <Stack gap="xs">
              <Text fw={500}>UI Parameters:</Text>
              <ScrollArea h={200}>
                <CodeHighlight
                  code={(() => {
                    try {
                      return JSON.stringify(JSON.parse(series.parameters), null, 2);
                    } catch {
                      return series.parameters;
                    }
                  })()}
                  language="json"
                  withCopyButton={false}
                />
              </ScrollArea>
            </Stack>
          </>
        )}

        <Divider />

        <Stack gap="xs">
          <Group justify="space-between">
            <Text fw={500}>Models in Series:</Text>
            <Badge variant="light">
              {loading ? 'Loading...' : `${models.length} models`}
            </Badge>
          </Group>

          {models.length > 0 && (
            <ScrollArea h={150}>
              <Stack gap="xs">
                {models.map((model) => (
                  <Group key={model.id} justify="space-between" p="xs" style={{ borderLeft: '2px solid var(--mantine-color-gray-3)' }}>
                    <div>
                      <Text size="sm" fw={500}>{model.name}</Text>
                      {/* displayName field doesn't exist in ModelDto */}
                    </div>
                    <Group gap="xs">
                      {model.isActive !== undefined && (
                        <Badge size="xs" color={model.isActive ? 'green' : 'gray'} variant="light">
                          {model.isActive ? 'Active' : 'Inactive'}
                        </Badge>
                      )}
                    </Group>
                  </Group>
                ))}
              </Stack>
            </ScrollArea>
          )}
        </Stack>

        <Divider />

        {/* createdAt and updatedAt fields don't exist in ModelSeriesDto */}
      </Stack>
    </Modal>
  );
}