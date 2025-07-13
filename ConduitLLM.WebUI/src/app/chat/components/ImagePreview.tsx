'use client';

import { useState } from 'react';
import { Image, Modal, Group, Stack, Badge, Text } from '@mantine/core';
import { ImageAttachment } from '../types';

interface ImagePreviewProps {
  images: ImageAttachment[];
  compact?: boolean;
}

export function ImagePreview({ images, compact = false }: ImagePreviewProps) {
  const [selectedImage, setSelectedImage] = useState<ImageAttachment | null>(null);

  if (!images || images.length === 0) return null;

  const imageSize = compact ? 100 : 200;
  const maxImagesShown = compact ? 4 : 6;
  const displayedImages = images.slice(0, maxImagesShown);
  const remainingCount = images.length - maxImagesShown;

  return (
    <>
      <Group gap="xs" mt="xs">
        {displayedImages.map((image, index) => (
          <Stack key={index} gap="xs" align="center">
            <Image
              src={image.url}
              alt={image.name}
              width={imageSize}
              height={imageSize}
              fit="cover"
              radius="sm"
              style={{ cursor: 'pointer' }}
              onClick={() => setSelectedImage(image)}
            />
            {!compact && (
              <Text size="xs" truncate style={{ maxWidth: imageSize }}>
                {image.name}
              </Text>
            )}
          </Stack>
        ))}
        {remainingCount > 0 && (
          <Badge
            size={compact ? 'md' : 'lg'}
            radius="sm"
            variant="light"
            style={{ 
              width: imageSize, 
              height: imageSize,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              cursor: 'pointer'
            }}
            onClick={() => setSelectedImage(images[maxImagesShown])}
          >
            +{remainingCount} more
          </Badge>
        )}
      </Group>

      <Modal
        opened={selectedImage !== null}
        onClose={() => setSelectedImage(null)}
        size="xl"
        title={selectedImage?.name}
      >
        {selectedImage && (
          <Stack>
            <Image
              src={selectedImage.url}
              alt={selectedImage.name}
              fit="contain"
              style={{ maxHeight: '70vh' }}
            />
            <Group justify="space-between">
              <Text size="sm" c="dimmed">
                {selectedImage.name}
              </Text>
              <Badge variant="light">
                {(selectedImage.size / 1024).toFixed(1)} KB
              </Badge>
            </Group>
          </Stack>
        )}
      </Modal>
    </>
  );
}