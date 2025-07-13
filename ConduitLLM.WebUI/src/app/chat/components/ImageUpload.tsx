'use client';

import { useRef, useState } from 'react';
import React from 'react';
import { 
  Button, 
  Group, 
  Text, 
  Stack, 
  Paper, 
  CloseButton,
  Image,
  Badge,
  ActionIcon,
  Tooltip
} from '@mantine/core';
import { IconUpload, IconX, IconPhoto } from '@tabler/icons-react';
import { notifications } from '@mantine/notifications';
import { ImageAttachment } from '../types';

interface ImageUploadProps {
  onImagesChange: (images: ImageAttachment[]) => void;
  images: ImageAttachment[];
  maxImages?: number;
  maxSizeInMB?: number;
  disabled?: boolean;
}

export function ImageUpload({ 
  onImagesChange, 
  images, 
  maxImages = 10,
  maxSizeInMB = 20,
  disabled = false 
}: ImageUploadProps) {
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [isProcessing, setIsProcessing] = useState(false);

  const handleFileSelect = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const files = event.target.files;
    if (!files || files.length === 0) return;

    setIsProcessing(true);
    const newImages: ImageAttachment[] = [];
    const maxSizeInBytes = maxSizeInMB * 1024 * 1024;

    for (let i = 0; i < files.length; i++) {
      const file = files[i];
      
      // Check if we've reached max images
      if (images.length + newImages.length >= maxImages) {
        notifications.show({
          title: 'Max images reached',
          message: `You can only upload up to ${maxImages} images`,
          color: 'yellow',
        });
        break;
      }

      // Validate file type
      if (!file.type.startsWith('image/')) {
        notifications.show({
          title: 'Invalid file type',
          message: `${file.name} is not an image`,
          color: 'red',
        });
        continue;
      }

      // Check file size
      if (file.size > maxSizeInBytes) {
        notifications.show({
          title: 'File too large',
          message: `${file.name} exceeds ${maxSizeInMB}MB limit`,
          color: 'red',
        });
        continue;
      }

      try {
        // Convert to base64
        const base64 = await fileToBase64(file);
        
        // Create object URL for preview
        const url = URL.createObjectURL(file);
        
        newImages.push({
          url,
          base64: base64.split(',')[1], // Remove data:image/...;base64, prefix
          mimeType: file.type,
          size: file.size,
          name: file.name,
        });
      } catch (error) {
        console.error('Error processing image:', error);
        notifications.show({
          title: 'Error processing image',
          message: `Failed to process ${file.name}`,
          color: 'red',
        });
      }
    }

    if (newImages.length > 0) {
      onImagesChange([...images, ...newImages]);
    }

    // Reset input
    if (fileInputRef.current) {
      fileInputRef.current.value = '';
    }
    setIsProcessing(false);
  };

  const handlePaste = async (event: ClipboardEvent) => {
    const items = event.clipboardData?.items;
    if (!items) return;

    for (let i = 0; i < items.length; i++) {
      const item = items[i];
      if (item.type.startsWith('image/')) {
        const file = item.getAsFile();
        if (file) {
          // Create a synthetic event
          const syntheticEvent = {
            target: {
              files: [file]
            }
          } as unknown as React.ChangeEvent<HTMLInputElement>;
          
          await handleFileSelect(syntheticEvent);
        }
      }
    }
  };

  const removeImage = (index: number) => {
    const newImages = [...images];
    // Revoke object URL to free memory
    if (newImages[index].url.startsWith('blob:')) {
      URL.revokeObjectURL(newImages[index].url);
    }
    newImages.splice(index, 1);
    onImagesChange(newImages);
  };

  const fileToBase64 = (file: File): Promise<string> => {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.readAsDataURL(file);
      reader.onload = () => resolve(reader.result as string);
      reader.onerror = error => reject(error);
    });
  };

  // Add paste event listener
  React.useEffect(() => {
    document.addEventListener('paste', handlePaste);
    return () => document.removeEventListener('paste', handlePaste);
  }, [images]);

  // Clean up object URLs when component unmounts
  React.useEffect(() => {
    return () => {
      images.forEach(img => {
        if (img.url.startsWith('blob:')) {
          URL.revokeObjectURL(img.url);
        }
      });
    };
  }, []);

  return (
    <Stack gap="xs">
      <input
        ref={fileInputRef}
        type="file"
        accept="image/*"
        multiple
        style={{ display: 'none' }}
        onChange={handleFileSelect}
        disabled={disabled || isProcessing}
      />
      
      {images.length > 0 && (
        <Group gap="xs">
          {images.map((image, index) => (
            <Paper
              key={index}
              p="xs"
              withBorder
              radius="md"
              style={{ position: 'relative' }}
            >
              <CloseButton
                size="sm"
                style={{
                  position: 'absolute',
                  top: -8,
                  right: -8,
                  zIndex: 1,
                }}
                onClick={() => removeImage(index)}
                disabled={disabled}
              />
              <Stack gap="xs" align="center">
                <Image
                  src={image.url}
                  alt={image.name}
                  width={100}
                  height={100}
                  fit="cover"
                  radius="sm"
                />
                <Text size="xs" truncate style={{ maxWidth: 100 }}>
                  {image.name}
                </Text>
                <Badge size="xs" variant="light">
                  {(image.size / 1024).toFixed(1)} KB
                </Badge>
              </Stack>
            </Paper>
          ))}
        </Group>
      )}
      
      <Group gap="xs">
        <Tooltip label="Click to upload or paste images">
          <Button
            leftSection={<IconPhoto size={20} />}
            variant="subtle"
            size="sm"
            onClick={() => fileInputRef.current?.click()}
            disabled={disabled || isProcessing || images.length >= maxImages}
            loading={isProcessing}
          >
            {images.length > 0 ? `Add More (${images.length}/${maxImages})` : 'Add Images'}
          </Button>
        </Tooltip>
        
        {images.length > 0 && (
          <Button
            size="sm"
            variant="subtle"
            color="red"
            onClick={() => {
              images.forEach(img => {
                if (img.url.startsWith('blob:')) {
                  URL.revokeObjectURL(img.url);
                }
              });
              onImagesChange([]);
            }}
            disabled={disabled}
          >
            Clear All
          </Button>
        )}
      </Group>
      
      <Text size="xs" c="dimmed">
        Drag & drop, click to upload, or paste images • Max {maxSizeInMB}MB per image
      </Text>
    </Stack>
  );
}