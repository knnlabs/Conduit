'use client';

import { useState, useEffect } from 'react';
import Image from 'next/image';
import { 
  Card, 
  Text, 
  Button, 
  Group, 
  Center, 
  Stack, 
  Badge
} from '@mantine/core';
import { 
  IconDownload, 
  IconZoomIn, 
  IconDimensions, 
  IconFile 
} from '@tabler/icons-react';
import { useImageStore } from '../hooks/useImageStore';
import { GeneratedImage } from '../types';
import { 
  MediaGallery, 
  MediaCard,
  downloadMedia,
  formatFileSize as formatSize,
  ImageMetadataExtractor,
  MetadataCache
} from '@/app/components/media';
import { MediaGenerationStatus, type MediaMetadata } from '@/app/types/media';

export default function ImageGallery() {
  const { currentResults: results, status } = useImageStore();
  const [selectedImage, setSelectedImage] = useState<GeneratedImage | null>(null);
  const [metadataCache] = useState(() => new MetadataCache());
  const [imageMetadata, setImageMetadata] = useState<Record<string, MediaMetadata>>({});

  const handleDownload = async (image: GeneratedImage, index: number) => {
    const filename = `generated-image-${index + 1}.png`;
    await downloadMedia({
      url: image.url,
      b64_json: image.b64_json,
      filename,
      mimeType: 'image/png'
    });
  };

  const getImageSrc = (image: GeneratedImage): string => {
    if (image.url) {
      return image.url;
    } else if (image.b64_json) {
      return `data:image/png;base64,${image.b64_json}`;
    }
    console.warn('No image data available');
    return '';
  };

  const handleImageClick = (image: GeneratedImage) => {
    // Merge stored metadata with the image
    const imageKey = image.url ?? image.b64_json ?? '';
    const metadata = imageMetadata[imageKey];
    const enrichedImage: GeneratedImage = metadata ? {
      ...image,
      width: metadata.width,
      height: metadata.height,
      sizeBytes: metadata.sizeBytes ?? metadata.file_size_bytes,
      format: metadata.format
    } : image;
    setSelectedImage(enrichedImage);
  };

  const closeModal = () => {
    setSelectedImage(null);
  };

  // Extract metadata for all images when they change
  useEffect(() => {
    const extractAllMetadata = async () => {
      const extractor = new ImageMetadataExtractor();
      const metadataMap: Record<string, MediaMetadata> = {};
      
      for (const image of results) {
        const imageKey = image.url ?? image.b64_json ?? '';
        
        if (imageKey && !metadataCache.has(image)) {
          const metadata = await extractor.extract(image);
          metadataCache.set(image, metadata);
          metadataMap[imageKey] = metadata;
        } else if (imageKey) {
          const cached = metadataCache.get(image);
          if (cached) {
            metadataMap[imageKey] = cached;
          }
        }
      }
      
      if (Object.keys(metadataMap).length > 0) {
        setImageMetadata(prev => ({ ...prev, ...metadataMap }));
      }
    };

    void extractAllMetadata();
  }, [results, metadataCache]);

  const renderImageCard = (image: GeneratedImage, index: number) => {
    // Get metadata for this image
    const imageKey = image.url ?? image.b64_json ?? '';
    const metadata = imageMetadata[imageKey];
    
    return (
      <MediaCard 
        key={`image-${image.id ?? index}`}
        onClick={() => handleImageClick(image)}
      >
        <Card.Section>
          <div 
            style={{ 
              position: 'relative', 
              width: '100%', 
              height: '250px',
              cursor: 'pointer'
            }}
          >
            <Image
              src={getImageSrc(image)}
              alt={image.revised_prompt ?? `Generated image ${index + 1}`}
              fill
              style={{ objectFit: 'cover' }}
              loading="lazy"
              unoptimized={true}
              onError={(e) => {
                const failedUrl = getImageSrc(image);
                console.error('Image failed to load');
                console.error('Failed URL:', failedUrl);
                console.error('URL length:', failedUrl.length);
                console.error('Full URL:', JSON.stringify(failedUrl));
                // Log the event details separately to avoid React property contamination
                console.error('Error event:', {
                  type: e.type,
                  target: e.currentTarget?.src
                });
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
                if (icon instanceof HTMLElement) {
                  icon.style.opacity = '1';
                }
              }}
              onMouseLeave={(e) => {
                e.currentTarget.style.background = 'rgba(0, 0, 0, 0)';
                const icon = e.currentTarget.querySelector('svg');
                if (icon instanceof HTMLElement) {
                  icon.style.opacity = '0';
                }
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
          <Stack gap="xs">
            <Group justify="space-between">
              <div style={{ flex: 1 }}>
                <Text size="sm" fw={500}>Image {index + 1}</Text>
                {image.revised_prompt && (
                  <Text size="xs" c="dimmed" lineClamp={1} title={image.revised_prompt}>
                    {image.revised_prompt}
                  </Text>
                )}
              </div>
              <Button
                size="xs"
                variant="light"
                leftSection={<IconDownload size={14} />}
                onClick={(e) => {
                  e.stopPropagation();
                  void handleDownload(image, index);
                }}
              >
                Download
              </Button>
            </Group>
            {metadata && (metadata.width ?? metadata.height ?? metadata.sizeBytes ?? metadata.file_size_bytes) && (
              <Group gap="xs">
                {metadata.width && metadata.height && (
                  <Badge size="xs" variant="light" color="blue">
                    {metadata.width}×{metadata.height}
                  </Badge>
                )}
                {(metadata.sizeBytes ?? metadata.file_size_bytes) && (
                  <Badge size="xs" variant="light" color="green">
                    {formatSize(metadata.sizeBytes ?? metadata.file_size_bytes ?? 0)}
                  </Badge>
                )}
                {metadata.format && (
                  <Badge size="xs" variant="light" color="gray">
                    {metadata.format.toUpperCase()}
                  </Badge>
                )}
              </Group>
            )}
          </Stack>
        </Card.Section>
      </MediaCard>
    );
  };

  // Modal content for preview
  const modalContent = selectedImage && (
    <Stack>
      {/* Metadata badges */}
      <Group gap="xs">
        {selectedImage.width && selectedImage.height && (
          <Badge 
            leftSection={<IconDimensions size={14} />}
            variant="light"
            color="blue"
          >
            {selectedImage.width} × {selectedImage.height}px
          </Badge>
        )}
        {selectedImage.sizeBytes && (
          <Badge 
            leftSection={<IconFile size={14} />}
            variant="light"
            color="green"
          >
            {formatSize(selectedImage.sizeBytes)}
          </Badge>
        )}
        {selectedImage.format && (
          <Badge variant="light" color="gray">
            {selectedImage.format.toUpperCase()}
          </Badge>
        )}
      </Group>

      {/* Image display */}
      <div style={{ position: 'relative', width: '100%', height: '60vh' }}>
        <Image
          src={getImageSrc(selectedImage)}
          alt={selectedImage.revised_prompt ?? 'Generated image'}
          fill
          style={{ objectFit: 'contain' }}
          unoptimized={true}
        />
      </div>

      {/* Revised prompt if available */}
      {selectedImage.revised_prompt && (
        <div>
          <Text size="sm" fw={500} mb={4}>Revised Prompt:</Text>
          <Text size="sm" c="dimmed">
            {selectedImage.revised_prompt}
          </Text>
        </div>
      )}

      {/* Download button */}
      <Button
        leftSection={<IconDownload size={16} />}
        onClick={() => {
          const index = results.findIndex(img => 
            (img.url === selectedImage.url && img.b64_json === selectedImage.b64_json) ||
            img.id === selectedImage.id
          );
          void handleDownload(selectedImage, index);
        }}
      >
        Download Image
      </Button>
    </Stack>
  );

  // Show empty state if no results and not generating
  if (status === MediaGenerationStatus.Idle || (status !== MediaGenerationStatus.Generating && results.length === 0)) {
    return (
      <Center py="xl">
        <Text c="dimmed">
          Generated images will appear here. Enter a prompt and click &quot;Generate Images&quot; to get started.
        </Text>
      </Center>
    );
  }

  return (
    <MediaGallery
      items={results}
      renderCard={renderImageCard}
      cols={{ base: 1, sm: 2, md: 3, lg: 4 }}
      modalContent={modalContent}
      modalOpened={!!selectedImage}
      onModalClose={closeModal}
      modalTitle="Image Details"
    />
  );
}