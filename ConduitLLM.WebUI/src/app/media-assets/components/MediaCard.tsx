'use client';

import { Card, Image, Text, Group, Badge, Checkbox, ActionIcon, Stack } from '@mantine/core';
import { IconDownload, IconEye, IconTrash } from '@tabler/icons-react';
import { MediaRecord } from '../types';
import { formatBytes, formatDate, getProviderColor } from '../utils/formatters';

interface MediaCardProps {
  media: MediaRecord;
  selected: boolean;
  onSelect: (id: string) => void;
  onView: (media: MediaRecord) => void;
  onDelete: (id: string) => void;
}

export default function MediaCard({ 
  media, 
  selected, 
  onSelect, 
  onView, 
  onDelete 
}: MediaCardProps) {
  const getThumbnail = () => {
    if (media.mediaType === 'image' && media.publicUrl) {
      return media.publicUrl;
    }
    // For videos, we'd need a thumbnail service or use a placeholder
    return '/api/placeholder/400/300';
  };

  const handleDownload = () => {
    if (media.publicUrl) {
      const link = document.createElement('a');
      link.href = media.publicUrl;
      link.download = `${media.mediaType}-${media.id}`;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
    }
  };

  return (
    <Card shadow="sm" radius="md" withBorder p={0} style={{ position: 'relative' }}>
      <div style={{ position: 'absolute', top: 8, left: 8, zIndex: 10 }}>
        <Checkbox
          checked={selected}
          onChange={() => onSelect(media.id)}
          styles={{ input: { backgroundColor: 'white' } }}
        />
      </div>

      <Card.Section 
        style={{ cursor: 'pointer', position: 'relative', paddingTop: '75%' }}
        onClick={() => onView(media)}
      >
        {media.mediaType === 'image' ? (
          <Image
            src={getThumbnail()}
            alt={media.prompt ?? 'Generated media'}
            style={{ 
              position: 'absolute', 
              top: 0, 
              left: 0, 
              width: '100%', 
              height: '100%',
              objectFit: 'cover'
            }}
          />
        ) : (
          <div style={{
            position: 'absolute',
            top: 0,
            left: 0,
            width: '100%',
            height: '100%',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            backgroundColor: '#f0f0f0'
          }}>
            <Text size="xl">🎬</Text>
          </div>
        )}
        {media.mediaType === 'video' && (
          <Badge
            variant="filled"
            color="dark"
            style={{ position: 'absolute', top: 8, right: 8 }}
          >
            VIDEO
          </Badge>
        )}
      </Card.Section>

      <Stack gap="xs" p="md">
        <Group justify="space-between" wrap="nowrap">
          <Badge color={getProviderColor(media.provider)} size="sm">
            {media.provider ?? 'Unknown'}
          </Badge>
          <Text size="xs" c="dimmed">
            {formatBytes(media.sizeBytes ?? 0)}
          </Text>
        </Group>

        {media.prompt ? (
          <Text size="sm" lineClamp={2}>
            {media.prompt}
          </Text>
        ) : null}

        <Group justify="space-between" align="center">
          <Text size="xs" c="dimmed">
            {formatDate(media.createdAt)}
          </Text>
          <Group gap="xs">
            <ActionIcon
              size="sm"
              variant="light"
              onClick={(e) => {
                e.stopPropagation();
                handleDownload();
              }}
            >
              <IconDownload size={16} />
            </ActionIcon>
            <ActionIcon
              size="sm"
              variant="light"
              onClick={(e) => {
                e.stopPropagation();
                onView(media);
              }}
            >
              <IconEye size={16} />
            </ActionIcon>
            <ActionIcon
              size="sm"
              variant="light"
              color="red"
              onClick={(e) => {
                e.stopPropagation();
                onDelete(media.id);
              }}
            >
              <IconTrash size={16} />
            </ActionIcon>
          </Group>
        </Group>

        {media.accessCount > 0 && (
          <Text size="xs" c="dimmed">
            Accessed {media.accessCount} times
          </Text>
        )}
      </Stack>
    </Card>
  );
}