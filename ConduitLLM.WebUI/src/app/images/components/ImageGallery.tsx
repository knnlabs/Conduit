'use client';

import { useState } from 'react';
import Image from 'next/image';
import { Card, SimpleGrid, Text, Button, Group, Modal, Center } from '@mantine/core';
import { IconDownload, IconZoomIn } from '@tabler/icons-react';
import { useImageStore } from '../hooks/useImageStore';
import { GeneratedImage } from '../types';

export default function ImageGallery() {
  const { results, status } = useImageStore();
  const [selectedImage, setSelectedImage] = useState<GeneratedImage | null>(null);

  const handleDownload = async (image: GeneratedImage, index: number) => {
    try {
      let imageData: string;
      let filename: string;

      if (image.url) {
        // Download from URL
        const response = await fetch(image.url);
        const blob = await response.blob();
        imageData = URL.createObjectURL(blob);
        filename = `generated-image-${index + 1}.png`;
      } else if (image.b64Json) {
        // Convert base64 to blob
        const byteCharacters = atob(image.b64Json);
        const byteNumbers = new Array(byteCharacters.length);
        for (let i = 0; i < byteCharacters.length; i++) {
          byteNumbers[i] = byteCharacters.charCodeAt(i);
        }
        const byteArray = new Uint8Array(byteNumbers);
        const blob = new Blob([byteArray], { type: 'image/png' });
        imageData = URL.createObjectURL(blob);
        filename = `generated-image-${index + 1}.png`;
      } else {
        console.error('No image data available');
        return;
      }

      // Create download link
      const link = document.createElement('a');
      link.href = imageData;
      link.download = filename;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);

      // Clean up object URL if it was created from fetch
      if (image.url) {
        URL.revokeObjectURL(imageData);
      }
    } catch (error) {
      console.error('Download failed:', error);
    }
  };

  const getImageSrc = (image: GeneratedImage): string => {
    if (image.url) {
      console.warn('Image URL:', image.url);
      return image.url;
    } else if (image.b64Json) {
      console.warn('Using base64 image');
      return `data:image/png;base64,${image.b64Json}`;
    }
    console.warn('No image data available');
    return '';
  };

  const handleImageClick = (image: GeneratedImage) => {
    setSelectedImage(image);
  };

  const closeModal = () => {
    setSelectedImage(null);
  };

  if (status === 'idle' || (status !== 'generating' && results.length === 0)) {
    return (
      <Center py="xl">
        <Text c="dimmed">
          Generated images will appear here. Enter a prompt and click &quot;Generate Images&quot; to get started.
        </Text>
      </Center>
    );
  }

  return (
    <>
      <SimpleGrid cols={{ base: 1, sm: 2, md: 3, lg: 4 }} spacing="lg">
        {results.map((image, index) => (
          <Card 
            key={`image-${image.id ?? index}`} 
            p="sm" 
            radius="md" 
            withBorder
            style={{ overflow: 'hidden' }}
          >
            <Card.Section>
              <div 
                style={{ 
                  position: 'relative', 
                  width: '100%', 
                  height: '250px',
                  cursor: 'pointer'
                }}
                onClick={() => handleImageClick(image)}
              >
                <Image
                  src={getImageSrc(image)}
                  alt={image.revisedPrompt ?? `Generated image ${index + 1}`}
                  fill
                  style={{ objectFit: 'cover' }}
                  loading="lazy"
                  unoptimized={true}
                  onError={(e) => {
                    console.error('Image failed to load:', e);
                    console.error('Failed URL:', getImageSrc(image));
                  }}
                />
                <div 
                  style={{
                    position: 'absolute',
                    top: 0,
                    left: 0,
                    right: 0,
                    bottom: 0,
                    background: 'rgba(0, 0, 0, 0)',
                    transition: 'background 0.2s',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                  }}
                  onMouseEnter={(e) => {
                    e.currentTarget.style.background = 'rgba(0, 0, 0, 0.5)';
                    const icon = e.currentTarget.querySelector('svg');
                    if (icon) icon.style.opacity = '1';
                  }}
                  onMouseLeave={(e) => {
                    e.currentTarget.style.background = 'rgba(0, 0, 0, 0)';
                    const icon = e.currentTarget.querySelector('svg');
                    if (icon) icon.style.opacity = '0';
                  }}
                >
                  <IconZoomIn 
                    size={48} 
                    color="white" 
                    style={{ opacity: 0, transition: 'opacity 0.2s' }}
                  />
                </div>
              </div>
            </Card.Section>

            <Card.Section p="sm">
              <Group justify="space-between">
                <div style={{ flex: 1 }}>
                  <Text size="sm" fw={500}>Image {index + 1}</Text>
                  {image.revisedPrompt && (
                    <Text size="xs" c="dimmed" lineClamp={1} title={image.revisedPrompt}>
                      {image.revisedPrompt}
                    </Text>
                  )}
                </div>
                <Button
                  size="xs"
                  variant="light"
                  leftSection={<IconDownload size={14} />}
                  onClick={() => void handleDownload(image, index)}
                >
                  Download
                </Button>
              </Group>
            </Card.Section>
          </Card>
        ))}
      </SimpleGrid>

      {/* Image Modal */}
      <Modal
        opened={!!selectedImage}
        onClose={closeModal}
        size="xl"
        title={selectedImage?.revisedPrompt ? "Generated Image" : undefined}
        centered
      >
        {selectedImage && (
          <div style={{ position: 'relative', width: '100%', height: '70vh' }}>
            <Image
              src={getImageSrc(selectedImage)}
              alt={selectedImage.revisedPrompt ?? 'Generated image'}
              fill
              style={{ objectFit: 'contain' }}
              unoptimized={true}
            />
            {selectedImage.revisedPrompt && (
              <div style={{ marginTop: '1rem', position: 'absolute', bottom: 0, left: 0, right: 0 }}>
                <Text size="sm" c="dimmed">
                  <strong>Revised Prompt:</strong> {selectedImage.revisedPrompt}
                </Text>
              </div>
            )}
          </div>
        )}
      </Modal>
    </>
  );
}