'use client';

import { useState, useEffect } from 'react';
import { Modal, Stack, Group, Text, Badge, Title, Divider, ScrollArea } from '@mantine/core';
import { CodeHighlight } from '@mantine/code-highlight';
import { withAdminClient } from '@/lib/client/adminClient';
import { notify } from '@/lib/notifications';
import { ParameterPreview } from '@/components/parameters/ParameterPreview';
import type { ModelSeriesDto, SeriesSimpleModelDto } from '@/lib/admin-api';


interface ViewModelSeriesModalProps {
  isOpen: boolean;
  series: ModelSeriesDto;
  onClose: () => void;
}

export function ViewModelSeriesModal({ isOpen, series, onClose }: ViewModelSeriesModalProps) {
  const [models, setModels] = useState<SeriesSimpleModelDto[]>([]);
  const [loading, setLoading] = useState(false);
  const parametersJson = series.parameters
    ? JSON.stringify(series.parameters, null, 2)
    : '';

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
      const data = await withAdminClient(client => client.modelSeries.getModels(series.id as number));
      setModels(data);
    } catch (error) {
      console.error('Failed to load models in series:', error);
      notify.error(error, 'Failed to load models in series');
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
              <ParameterPreview 
                parametersJson={parametersJson}
                context="chat"
                label="Preview UI Components"
                maxHeight={300}
              />
              <ScrollArea h={200}>
                <CodeHighlight
                  code={parametersJson}
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
