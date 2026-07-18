'use client';

import { Modal, Image, Stack, Group, Text, Badge, Button, CopyButton, Divider, Anchor } from '@mantine/core';
import { IconDownload, IconCopy, IconExternalLink } from '@tabler/icons-react';
import { MediaRecord } from '../types';
import { getProviderColor } from '../utils/formatters';
import { formatters } from '@/lib/utils/formatters';

interface MediaDetailModalProps {
  media: MediaRecord | null;
  opened: boolean;
  onClose: () => void;
  onDelete: (id: string) => void;
}

export default function MediaDetailModal({ 
  media, 
  opened, 
  onClose, 
  onDelete 
}: MediaDetailModalProps) {
  if (!media) return null;

  const mediaUrl = media.publicUrl ?? media.storageUrl;
  const isVideo = media.mediaType.toLowerCase() === 'video';
  const isImage = media.mediaType.toLowerCase() === 'image';

  const handleDownload = () => {
    if (mediaUrl) {
      window.open(mediaUrl, '_blank');
    }
  };

  return (
    <Modal
      opened={opened}
      onClose={onClose}
      title="Media Details"
      size="lg"
    >
      <Stack>
        {isImage && mediaUrl && (
          <Image
            src={mediaUrl}
            alt={media.prompt ?? 'Generated image'}
            radius="md"
            mah={400}
            fit="contain"
          />
        )}

        {isVideo && mediaUrl && (
          <video
            src={mediaUrl}
            controls
            style={{ width: '100%', maxHeight: 400, borderRadius: 8 }}
          />
        )}

        <Group justify="space-between" align="flex-start">
          <Stack gap="xs">
            <Group>
              <Badge color={getProviderColor(media.provider)}>
                {media.provider ?? 'Unknown'}
              </Badge>
              <Badge variant="light">
                {media.model ?? 'Unknown model'}
              </Badge>
              <Badge variant="light" color={isImage ? 'blue' : 'green'}>
                {media.mediaType.toUpperCase()}
              </Badge>
            </Group>
            
            {media.prompt && (
              <>
                <Text size="sm" fw={500}>Prompt:</Text>
                <Text size="sm" c="dimmed">{media.prompt}</Text>
              </>
            )}
          </Stack>

          <Stack gap="xs">
            <Button
              leftSection={<IconDownload size={16} />}
              variant="light"
              size="sm"
              onClick={handleDownload}
            >
              Download
            </Button>
            <CopyButton value={mediaUrl ?? ''}>
              {({ copied, copy }) => (
                <Button
                  leftSection={<IconCopy size={16} />}
                  variant="light"
                  size="sm"
                  color={copied ? 'green' : 'blue'}
                  onClick={copy}
                >
                  {copied ? 'Copied!' : 'Copy URL'}
                </Button>
              )}
            </CopyButton>
          </Stack>
        </Group>

        <Divider />

        <Stack gap="xs">
          <Group justify="space-between">
            <Text size="sm" c="dimmed">File Size:</Text>
            <Text size="sm">{formatters.fileSize(media.sizeBytes ?? 0)}</Text>
          </Group>
          <Group justify="space-between">
            <Text size="sm" c="dimmed">Created:</Text>
            <Text size="sm">{formatters.date(media.createdAt)}</Text>
          </Group>
          <Group justify="space-between">
            <Text size="sm" c="dimmed">Virtual Key ID:</Text>
            <Text size="sm">{media.virtualKeyId}</Text>
          </Group>
          {media.virtualKeyGroupId && (
            <Group justify="space-between">
              <Text size="sm" c="dimmed">Virtual Key Group ID:</Text>
              <Text size="sm">{media.virtualKeyGroupId}</Text>
            </Group>
          )}
          {media.virtualKeyGroupName && (
            <Group justify="space-between">
              <Text size="sm" c="dimmed">Virtual Key Group:</Text>
              <Text size="sm" fw={500}>{media.virtualKeyGroupName}</Text>
            </Group>
          )}
          <Group justify="space-between">
            <Text size="sm" c="dimmed">Access Count:</Text>
            <Text size="sm">{media.accessCount}</Text>
          </Group>
          {media.lastAccessedAt && (
            <Group justify="space-between">
              <Text size="sm" c="dimmed">Last Accessed:</Text>
              <Text size="sm">{formatters.date(media.lastAccessedAt)}</Text>
            </Group>
          )}
          {media.expiresAt && (
            <Group justify="space-between">
              <Text size="sm" c="dimmed">Expires:</Text>
              <Text size="sm" c="orange">{formatters.date(media.expiresAt)}</Text>
            </Group>
          )}
          <Group justify="space-between">
            <Text size="sm" c="dimmed">Storage Key:</Text>
            <Text size="xs" style={{ fontFamily: 'monospace', wordBreak: 'break-all' }}>
              {media.storageKey}
            </Text>
          </Group>
          {media.storageUrl && (
            <Group justify="space-between">
              <Text size="sm" c="dimmed">S3 URL:</Text>
              <Anchor 
                href={media.storageUrl} 
                target="_blank" 
                rel="noopener noreferrer"
                size="sm"
                style={{ wordBreak: 'break-all' }}
              >
                <Group gap="xs">
                  <Text size="xs" style={{ fontFamily: 'monospace' }}>
                    {media.storageUrl.length > 50 
                      ? `${media.storageUrl.substring(0, 30)}...${media.storageUrl.substring(media.storageUrl.length - 20)}`
                      : media.storageUrl
                    }
                  </Text>
                  <IconExternalLink size={12} />
                </Group>
              </Anchor>
            </Group>
          )}
        </Stack>

        <Divider />

        <Group justify="space-between">
          <Button variant="default" onClick={onClose}>
            Close
          </Button>
          <Button 
            color="red" 
            variant="light"
            onClick={() => {
              onDelete(media.id);
              onClose();
            }}
          >
            Delete Media
          </Button>
        </Group>
      </Stack>
    </Modal>
  );
}