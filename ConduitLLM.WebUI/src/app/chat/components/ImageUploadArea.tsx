'use client';

import { useRef, useState } from 'react';
import {
  Box,
  Text,
  Group,
  Stack,
  Paper,
  Image,
  Grid,
  ActionIcon,
  Tooltip,
  Progress,
} from '@mantine/core';
import { IconX, IconPhoto, IconEdit } from '@tabler/icons-react';

interface ImageUploadAreaProps {
  images: string[];
  onImagesChange: (images: string[]) => void;
  maxImages?: number;
  maxSizeMB?: number;
  onEditImage?: (index: number) => void;
}

export function ImageUploadArea({
  images,
  onImagesChange,
  maxImages = 10,
  maxSizeMB = 10,
  onEditImage,
}: ImageUploadAreaProps) {
  const [isDragging, setIsDragging] = useState(false);
  const [uploadProgress, setUploadProgress] = useState<Record<string, number>>({});
  const fileInputRef = useRef<HTMLInputElement>(null);

  const handleFiles = async (files: FileList | null) => {
    if (!files) return;

    const newImages: string[] = [];
    const totalAllowed = maxImages - images.length;
    const filesToProcess = Array.from(files).slice(0, totalAllowed);

    for (const file of filesToProcess) {
      if (!file.type.startsWith('image/')) continue;
      if (file.size > maxSizeMB * 1024 * 1024) {
        console.warn(`File ${file.name} exceeds ${maxSizeMB}MB limit`);
        continue;
      }

      const id = `${file.name}-${Date.now()}`;
      setUploadProgress((prev) => ({ ...prev, [id]: 0 }));

      try {
        const base64 = await fileToBase64(file);
        newImages.push(base64);
        
        // Simulate upload progress
        for (let i = 0; i <= 100; i += 20) {
          setUploadProgress((prev) => ({ ...prev, [id]: i }));
          await new Promise((resolve) => setTimeout(resolve, 100));
        }
      } catch (error) {
        console.error('Error processing file:', error);
      } finally {
        setUploadProgress((prev) => {
          const { [id]: removed, ...rest } = prev;
          void removed; // Acknowledge unused destructured value
          return rest;
        });
      }
    }

    if (newImages.length > 0) {
      onImagesChange([...images, ...newImages]);
    }
  };

  const fileToBase64 = (file: File): Promise<string> => {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => resolve(reader.result as string);
      reader.onerror = reject;
      reader.readAsDataURL(file);
    });
  };

  const handleDragOver = (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(true);
  };

  const handleDragLeave = (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(false);
  };

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(false);
    void handleFiles(e.dataTransfer.files);
  };

  const handlePaste = async (e: React.ClipboardEvent) => {
    const items = Array.from(e.clipboardData.items);
    const imageItems = items.filter((item) => item.type.startsWith('image/'));

    if (imageItems.length === 0) return;

    const files: File[] = [];
    for (const item of imageItems) {
      const file = item.getAsFile();
      if (file) files.push(file);
    }

    if (files.length > 0) {
      const dataTransfer = new DataTransfer();
      files.forEach((file) => dataTransfer.items.add(file));
      void handleFiles(dataTransfer.files);
    }
  };

  const removeImage = (index: number) => {
    const filteredImages = images.filter((image, i) => {
      void image; // Acknowledge unused parameter
      return i !== index;
    });
    onImagesChange(filteredImages);
  };

  const hasImages = images.length > 0;
  const canAddMore = images.length < maxImages;
  const uploadingCount = Object.keys(uploadProgress).length;

  return (
    <Stack gap="xs">
      {hasImages && (
        <Grid gutter="xs">
          {images.map((image, index) => (
            <Grid.Col key={`image-${image.slice(0, 100)}`} span={{ base: 6, xs: 4, sm: 3 }}>
              <Paper
                p={4}
                withBorder
                style={{ position: 'relative', overflow: 'hidden' }}
              >
                <Image
                  src={image}
                  alt={`Upload ${index + 1}`}
                  height={80}
                  style={{ objectFit: 'cover' }}
                />
                <Group
                  style={{
                    position: 'absolute',
                    top: 4,
                    right: 4,
                    gap: 4,
                  }}
                >
                  {onEditImage && (
                    <Tooltip label="Edit image">
                      <ActionIcon
                        size="sm"
                        variant="filled"
                        color="blue"
                        onClick={() => onEditImage(index)}
                      >
                        <IconEdit size={14} />
                      </ActionIcon>
                    </Tooltip>
                  )}
                  <Tooltip label="Remove image">
                    <ActionIcon
                      size="sm"
                      variant="filled"
                      color="red"
                      onClick={() => removeImage(index)}
                    >
                      <IconX size={14} />
                    </ActionIcon>
                  </Tooltip>
                </Group>
              </Paper>
            </Grid.Col>
          ))}
        </Grid>
      )}

      {uploadingCount > 0 && (
        <Stack gap={4}>
          {Object.entries(uploadProgress).map(([id, progress]) => (
            <Box key={id}>
              <Text size="xs" c="dimmed">
                Uploading...
              </Text>
              <Progress value={progress} size="xs" />
            </Box>
          ))}
        </Stack>
      )}

      {canAddMore && (
        <Box
          onDragOver={handleDragOver}
          onDragLeave={handleDragLeave}
          onDrop={handleDrop}
          onPaste={(e) => void handlePaste(e)}
          style={{ position: 'relative' }}
        >
          <input
            ref={fileInputRef}
            type="file"
            multiple
            accept="image/*"
            onChange={(e) => void handleFiles(e.target.files)}
            style={{ display: 'none' }}
          />
          <Paper
            p="md"
            withBorder
            style={{
              borderStyle: 'dashed',
              cursor: 'pointer',
              backgroundColor: isDragging
                ? 'var(--mantine-color-blue-light)'
                : undefined,
              transition: 'background-color 200ms ease',
            }}
            onClick={() => void fileInputRef.current?.click()}
          >
            <Stack align="center" gap="xs">
              <IconPhoto
                size={32}
                style={{ color: 'var(--mantine-color-dimmed)' }}
              />
              <Text size="sm" c="dimmed" ta="center">
                {isDragging
                  ? 'Drop images here'
                  : 'Drag & drop images or click to select'}
              </Text>
              <Text size="xs" c="dimmed">
                Max {maxImages} images, {maxSizeMB}MB each
              </Text>
              {hasImages && (
                <Text size="xs" c="dimmed">
                  {images.length}/{maxImages} images added
                </Text>
              )}
            </Stack>
          </Paper>
        </Box>
      )}

      <Text size="xs" c="dimmed" ta="center">
        💡 Tip: You can also paste images from your clipboard (Ctrl/Cmd+V)
      </Text>
    </Stack>
  );
}