'use client';

import { Card, Image, Text, Group, Badge, Checkbox, ActionIcon, Stack } from '@mantine/core';
import { IconDownload, IconEye, IconRestore, IconTrash } from '@tabler/icons-react';
import { MediaRecord } from '../types';
import { getProviderColor } from '../utils/formatters';
import { formatters } from '@/lib/utils/formatters';

interface MediaCardProps {
  media: MediaRecord;
  selected: boolean;
  onSelect: (id: string) => void;
  onView: (media: MediaRecord) => void;
  onDelete: (id: string) => void;
  onRestore: (id: string) => void;
}

export default function MediaCard({ 
  media, 
  selected, 
  onSelect, 
  onView, 
  onDelete,
  onRestore,
}: MediaCardProps) {
  const mediaUrl = media.publicUrl ?? media.storageUrl;
  const isVideo = media.mediaType.toLowerCase() === 'video';
  const isImage = media.mediaType.toLowerCase() === 'image';
  const isDeleted = !!media.deletedAt;

  const handleDownload = () => {
    if (mediaUrl) {
      const link = document.createElement('a');
      link.href = mediaUrl;
      link.download = `${media.mediaType}-${media.id}`;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
    }
  };

  const renderPreview = () => {
    if (isDeleted) {
      return (
        <div style={{
          position: 'absolute',
          inset: 0,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          backgroundColor: '#f1f3f5',
        }}>
          <Text c="dimmed" fw={500}>Soft deleted</Text>
        </div>
      );
    }

    if (isImage) {
      return (
        <Image
          src={mediaUrl}
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
      );
    }
    if (mediaUrl) {
      return (
        <video
          src={mediaUrl}
          preload="metadata"
          muted
          playsInline
          style={{
            position: 'absolute',
            top: 0,
            left: 0,
            width: '100%',
            height: '100%',
            objectFit: 'cover',
            backgroundColor: '#f0f0f0'
          }}
        />
      );
    }
    return (
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
    );
  };

  return (
    <Card shadow="sm" radius="md" withBorder p={0} style={{ position: 'relative' }}>
      <div style={{ position: 'absolute', top: 8, left: 8, zIndex: 10 }}>
        <Checkbox
          checked={selected}
          onChange={() => onSelect(media.id)}
          disabled={isDeleted}
          styles={{ input: { backgroundColor: 'white' } }}
        />
      </div>

      <Card.Section
        style={{
          cursor: isDeleted ? 'default' : 'pointer',
          position: 'relative',
          paddingTop: '75%',
        }}
        onClick={isDeleted ? undefined : () => onView(media)}
      >
        {renderPreview()}
        {isVideo && (
          <Badge
            variant="filled"
            color="dark"
            style={{ position: 'absolute', top: 8, right: 8 }}
          >
            VIDEO
          </Badge>
        )}
        {isDeleted && (
          <Badge
            variant="filled"
            color="red"
            style={{ position: 'absolute', top: 8, right: 8 }}
          >
            DELETED
          </Badge>
        )}
      </Card.Section>

      <Stack gap="xs" p="md">
        <Group justify="space-between" wrap="nowrap">
          <Badge color={getProviderColor(media.provider)} size="sm">
            {media.provider ?? 'Unknown'}
          </Badge>
          <Text size="xs" c="dimmed">
            {formatters.fileSize(media.sizeBytes ?? 0)}
          </Text>
        </Group>

        {media.prompt ? (
          <Text size="sm" lineClamp={2}>
            {media.prompt}
          </Text>
        ) : null}

        <Group justify="space-between" align="center">
          <Text size="xs" c="dimmed">
            {formatters.date(media.createdAt)}
          </Text>
          <Group gap="xs">
            {!isDeleted && <ActionIcon
              size="sm"
              variant="light"
              onClick={(e) => {
                e.stopPropagation();
                handleDownload();
              }}
            >
              <IconDownload size={16} />
            </ActionIcon>}
            {!isDeleted && <ActionIcon
              size="sm"
              variant="light"
              onClick={(e) => {
                e.stopPropagation();
                onView(media);
              }}
            >
              <IconEye size={16} />
            </ActionIcon>}
            {isDeleted ? (
              <ActionIcon
                aria-label="Restore media"
                size="sm"
                variant="light"
                color="green"
                onClick={(e) => {
                  e.stopPropagation();
                  onRestore(media.id);
                }}
              >
                <IconRestore size={16} />
              </ActionIcon>
            ) : <ActionIcon
              size="sm"
              variant="light"
              color="red"
              onClick={(e) => {
                e.stopPropagation();
                onDelete(media.id);
              }}
            >
              <IconTrash size={16} />
            </ActionIcon>}
          </Group>
        </Group>

        {media.accessCount > 0 && (
          <Text size="xs" c="dimmed">
            Accessed {media.accessCount} times
          </Text>
        )}
        {media.deletedAt && (
          <Text size="xs" c="red">
            Deleted {formatters.date(media.deletedAt)}
          </Text>
        )}
      </Stack>
    </Card>
  );
}
