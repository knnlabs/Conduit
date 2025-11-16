'use client';

import { useState, useEffect } from 'react';
import { Modal, Stack, Group, Text, Badge, Title, Divider, Anchor, ScrollArea } from '@mantine/core';
import { useAdminClient } from '@/lib/client/adminClient';
import { notifications } from '@mantine/notifications';
import type { ModelAuthorDto, SimpleModelSeriesDto } from '@knn_labs/conduit-admin-client';


interface ViewModelAuthorModalProps {
  isOpen: boolean;
  author: ModelAuthorDto;
  onClose: () => void;
}

export function ViewModelAuthorModal({ isOpen, author, onClose }: ViewModelAuthorModalProps) {
  const [series, setSeries] = useState<SimpleModelSeriesDto[]>([]);
  const [loading, setLoading] = useState(false);
  const { executeWithAdmin } = useAdminClient();

  useEffect(() => {
    if (isOpen && author?.id) {
      void loadSeries();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isOpen, author?.id]);

  const loadSeries = async () => {
    try {
      setLoading(true);
      if (!author.id) throw new Error('Author ID is required');
      const data = await executeWithAdmin(client => client.modelAuthors.getSeries(author.id as number));
      setSeries(data);
    } catch (error) {
      console.error('Failed to load author series:', error);
      notifications.show({
        title: 'Error',
        message: 'Failed to load author series',
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
      title={<Title order={3}>{author.name}</Title>}
      size="lg"
    >
      <Stack>
        <Group justify="space-between">
          <Text fw={500}>Author Name:</Text>
          <Text>{author.name}</Text>
        </Group>

        <Group justify="space-between">
          <Text fw={500}>Website:</Text>
          {author.websiteUrl ? (
            <Anchor href={author.websiteUrl} target="_blank" size="sm">
              {new URL(author.websiteUrl).hostname}
            </Anchor>
          ) : (
            <Text c="dimmed">-</Text>
          )}
        </Group>

        {/* isActive field doesn't exist in ModelAuthorDto */}

        <Divider />

        <Stack gap="xs">
          <Group justify="space-between">
            <Text fw={500}>Model Series:</Text>
            <Badge variant="light">
              {loading ? 'Loading...' : `${series.length} series`}
            </Badge>
          </Group>

          {series.length > 0 && (
            <ScrollArea h={200}>
              <Stack gap="xs">
                {series.map((s) => (
                  <Group key={s.id} justify="space-between" p="xs" style={{ borderLeft: '2px solid var(--mantine-color-gray-3)' }}>
                    <div>
                      <Text size="sm" fw={500}>{s.name}</Text>
                      {/* displayName field doesn't exist in ModelSeriesDto */}
                      {s.description && (
                        <Text size="xs" c="dimmed" lineClamp={1}>{s.description}</Text>
                      )}
                    </div>
                    <Group gap="xs">
                      <Badge size="xs" variant="light">
                        Series
                      </Badge>
                      {/* isActive field doesn't exist in ModelSeriesDto */}
                    </Group>
                  </Group>
                ))}
              </Stack>
            </ScrollArea>
          )}
        </Stack>

        <Divider />

        {/* createdAt and updatedAt fields don't exist in ModelAuthorDto */}
      </Stack>
    </Modal>
  );
}