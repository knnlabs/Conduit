'use client';

import {
  Stack,
  Title,
  Text,
  Group,
  Button,
  Card,
  Select,
  Textarea,
  NumberInput,
  SimpleGrid,
  Image,
  Badge,
  ActionIcon,
  LoadingOverlay,
  Alert,
  Paper,
  Tooltip,
  Switch,
  SegmentedControl,
  Slider,
  Grid,
  ThemeIcon,
} from '@mantine/core';
import {
  IconPhoto,
  IconDownload,
  IconTrash,
  IconRefresh,
  IconSettings,
  IconAlertCircle,
  IconZoomIn,
  IconCopy,
  IconBrush,
  IconPalette,
  IconDimensions,
} from '@tabler/icons-react';
import { useState } from 'react';
import { useDisclosure } from '@mantine/hooks';
import { notifications } from '@mantine/notifications';
import { useImageGeneration } from '@/lib/utils/sdkOptimizer';
import { useVirtualKeys } from '@/hooks/api/useAdminApi';
import { useImageGenerationStore } from '@/stores/useImageGenerationStore';
import { safeLog } from '@/lib/utils/logging';
import { formatters } from '@/lib/utils/formatters';

interface GeneratedImage {
  id: string;
  prompt: string;
  url: string;
  model: string;
  size: string;
  quality: string;
  style?: string;
  createdAt: Date;
  cost: number;
}

export default function ImageGeneration() {
  const [prompt, setPrompt] = useState('');
  const [negativePrompt, setNegativePrompt] = useState('');
  const [selectedModel, setSelectedModel] = useState('dall-e-3');
  const [selectedSize, setSelectedSize] = useState('1024x1024');
  const [selectedQuality, setSelectedQuality] = useState('standard');
  const [selectedStyle, setSelectedStyle] = useState('vivid');
  const [numberOfImages, setNumberOfImages] = useState(1);
  const [seed, setSeed] = useState<number | undefined>();
  const [isGenerating, setIsGenerating] = useState(false);
  const [settingsOpened, { open: openSettings, close: closeSettings }] = useDisclosure(false);
  const [selectedImage, setSelectedImage] = useState<GeneratedImage | null>(null);

  const {
    images,
    selectedVirtualKey,
    totalCost,
    addImage,
    removeImage,
    clearImages,
    setSelectedVirtualKey: setStoreVirtualKey,
  } = useImageGenerationStore();

  const { data: virtualKeys, isLoading: keysLoading, error: keysError } = useVirtualKeys();
  const createImage = useImageGeneration();

  const modelOptions = [
    { value: 'dall-e-3', label: 'DALL-E 3' },
    { value: 'dall-e-2', label: 'DALL-E 2' },
    { value: 'stable-diffusion-xl', label: 'Stable Diffusion XL' },
    { value: 'midjourney', label: 'Midjourney (via Replicate)' },
  ];

  const sizeOptions = {
    'dall-e-3': [
      { value: '1024x1024', label: '1024x1024 (Square)' },
      { value: '1792x1024', label: '1792x1024 (Landscape)' },
      { value: '1024x1792', label: '1024x1792 (Portrait)' },
    ],
    'dall-e-2': [
      { value: '256x256', label: '256x256' },
      { value: '512x512', label: '512x512' },
      { value: '1024x1024', label: '1024x1024' },
    ],
    'stable-diffusion-xl': [
      { value: '1024x1024', label: '1024x1024' },
      { value: '768x1024', label: '768x1024' },
      { value: '1024x768', label: '1024x768' },
    ],
    'midjourney': [
      { value: '1024x1024', label: '1024x1024' },
      { value: '1456x816', label: '1456x816' },
      { value: '816x1456', label: '816x1456' },
    ],
  };

  const handleGenerate = async () => {
    if (!prompt.trim()) {
      notifications.show({
        title: 'Prompt Required',
        message: 'Please enter a prompt to generate images',
        color: 'orange',
      });
      return;
    }

    if (!selectedVirtualKey) {
      notifications.show({
        title: 'Virtual Key Required',
        message: 'Please select a virtual key before generating images',
        color: 'orange',
      });
      return;
    }

    setIsGenerating(true);

    try {
      // Simulate API call with proper SDK usage
      const response = await createImage.mutateAsync({
        prompt: prompt.trim(),
        model: selectedModel,
        n: numberOfImages,
        size: selectedSize as any, // Type assertion for size options
        quality: selectedQuality as 'standard' | 'hd',
        style: selectedStyle as 'vivid' | 'natural',
      });

      // Process response and add images
      if ('data' in response && response.data) {
        response.data.forEach((imageData: any, index: number) => {
          const newImage: GeneratedImage = {
            id: `${Date.now()}-${index}`,
            prompt: prompt.trim(),
            url: imageData.url || '',
            model: selectedModel,
            size: selectedSize,
            quality: selectedQuality,
            style: selectedStyle,
            createdAt: new Date(),
            cost: calculateCost(selectedModel, selectedSize, selectedQuality),
          };
          addImage(newImage);
        });

        notifications.show({
          title: 'Images Generated',
          message: `Successfully generated ${response.data.length} image(s)`,
          color: 'green',
        });

        safeLog('Images generated successfully', { 
          model: selectedModel, 
          count: response.data.length 
        });
      }
    } catch (error: unknown) {
      notifications.show({
        title: 'Generation Failed',
        message: error instanceof Error ? error.message : 'Failed to generate images',
        color: 'red',
      });
      
      safeLog('Image generation failed', { 
        error: error instanceof Error ? error.message : String(error) 
      });
    } finally {
      setIsGenerating(false);
    }
  };

  const calculateCost = (model: string, size: string, quality: string): number => {
    // Mock cost calculation - in real app, this would come from API
    const costs: Record<string, Record<string, number>> = {
      'dall-e-3': {
        'standard-1024x1024': 0.04,
        'standard-1792x1024': 0.08,
        'standard-1024x1792': 0.08,
        'hd-1024x1024': 0.08,
        'hd-1792x1024': 0.12,
        'hd-1024x1792': 0.12,
      },
      'dall-e-2': {
        '256x256': 0.016,
        '512x512': 0.018,
        '1024x1024': 0.02,
      },
      'stable-diffusion-xl': {
        '1024x1024': 0.002,
        '768x1024': 0.002,
        '1024x768': 0.002,
      },
      'midjourney': {
        '1024x1024': 0.01,
        '1456x816': 0.01,
        '816x1456': 0.01,
      },
    };

    const key = model === 'dall-e-3' ? `${quality}-${size}` : size;
    return costs[model]?.[key] || 0.01;
  };

  const handleDownload = async (image: GeneratedImage) => {
    try {
      const response = await fetch(image.url);
      const blob = await response.blob();
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `generated-${image.id}.png`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      window.URL.revokeObjectURL(url);

      notifications.show({
        title: 'Downloaded',
        message: 'Image downloaded successfully',
        color: 'green',
      });
    } catch (error) {
      notifications.show({
        title: 'Download Failed',
        message: 'Failed to download image',
        color: 'red',
      });
    }
  };

  const handleCopyPrompt = async (prompt: string) => {
    try {
      await navigator.clipboard.writeText(prompt);
      notifications.show({
        title: 'Copied',
        message: 'Prompt copied to clipboard',
        color: 'green',
      });
    } catch (_error) {
      notifications.show({
        title: 'Copy Failed',
        message: 'Failed to copy prompt',
        color: 'red',
      });
    }
  };

  if (keysError) {
    return (
      <Stack gap="md">
        <div>
          <Title order={1}>Image Generation</Title>
          <Text c="dimmed">Create images with AI models</Text>
        </div>
        <Alert icon={<IconAlertCircle size={16} />} color="red">
          Failed to load virtual keys. Please try again.
        </Alert>
      </Stack>
    );
  }

  return (
    <Stack gap="xl">
      <Group justify="space-between">
        <div>
          <Title order={1}>Image Generation</Title>
          <Text c="dimmed">Create images with AI models</Text>
        </div>

        <Group>
          <Badge variant="light" size="lg">
            Total Cost: {formatters.currency(totalCost)}
          </Badge>
          <Button
            variant="light"
            leftSection={<IconSettings size={16} />}
            onClick={openSettings}
          >
            Settings
          </Button>
          {images.length > 0 && (
            <Button
              variant="light"
              color="red"
              leftSection={<IconTrash size={16} />}
              onClick={clearImages}
            >
              Clear All
            </Button>
          )}
        </Group>
      </Group>

      <Grid>
        <Grid.Col span={{ base: 12, md: 4 }}>
          <Card withBorder>
            <Stack gap="md">
              <Select
                label="Virtual Key"
                placeholder="Select a virtual key"
                data={virtualKeys?.map((key: any) => ({
                  value: key.id,
                  label: key.keyName,
                })) || []}
                value={selectedVirtualKey}
                onChange={(value) => setStoreVirtualKey(value || '')}
                disabled={keysLoading}
                required
              />

              <Select
                label="Model"
                data={modelOptions}
                value={selectedModel}
                onChange={(value) => {
                  setSelectedModel(value || 'dall-e-3');
                  // Reset size to first available option for new model
                  const sizes = sizeOptions[value as keyof typeof sizeOptions] || sizeOptions['dall-e-3'];
                  setSelectedSize(sizes[0].value);
                }}
              />

              <Textarea
                label="Prompt"
                placeholder="Describe the image you want to generate..."
                value={prompt}
                onChange={(event) => setPrompt(event.currentTarget.value)}
                minRows={3}
                maxRows={6}
                required
              />

              {selectedModel === 'stable-diffusion-xl' && (
                <Textarea
                  label="Negative Prompt"
                  placeholder="What to avoid in the image..."
                  value={negativePrompt}
                  onChange={(event) => setNegativePrompt(event.currentTarget.value)}
                  minRows={2}
                  maxRows={4}
                />
              )}

              <Select
                label="Size"
                data={sizeOptions[selectedModel as keyof typeof sizeOptions] || sizeOptions['dall-e-3']}
                value={selectedSize}
                onChange={(value) => setSelectedSize(value || '1024x1024')}
              />

              {selectedModel === 'dall-e-3' && (
                <>
                  <div>
                    <Text size="sm" fw={500} mb={5}>Quality</Text>
                    <SegmentedControl
                      data={[
                        { label: 'Standard', value: 'standard' },
                        { label: 'HD', value: 'hd' },
                      ]}
                      value={selectedQuality}
                      onChange={setSelectedQuality}
                    />
                  </div>

                  <div>
                    <Text size="sm" fw={500} mb={5}>Style</Text>
                    <SegmentedControl
                      data={[
                        { label: 'Vivid', value: 'vivid' },
                        { label: 'Natural', value: 'natural' },
                      ]}
                      value={selectedStyle}
                      onChange={setSelectedStyle}
                    />
                  </div>
                </>
              )}

              <NumberInput
                label="Number of Images"
                value={numberOfImages}
                onChange={(value) => setNumberOfImages(typeof value === 'number' ? value : 1)}
                min={1}
                max={selectedModel === 'dall-e-3' ? 1 : 10}
                disabled={selectedModel === 'dall-e-3'}
              />

              {selectedModel === 'stable-diffusion-xl' && (
                <NumberInput
                  label="Seed (Optional)"
                  placeholder="Random seed for reproducibility"
                  value={seed}
                  onChange={(value) => setSeed(typeof value === 'number' ? value : undefined)}
                  min={0}
                />
              )}

              <Button
                fullWidth
                leftSection={<IconPhoto size={16} />}
                onClick={handleGenerate}
                loading={isGenerating}
                disabled={!prompt.trim() || !selectedVirtualKey}
              >
                Generate Image{numberOfImages > 1 ? 's' : ''}
              </Button>
            </Stack>
          </Card>
        </Grid.Col>

        <Grid.Col span={{ base: 12, md: 8 }}>
          {images.length > 0 ? (
            <SimpleGrid cols={{ base: 1, sm: 2 }} spacing="md">
              {images.map((image) => (
                <Card key={image.id} withBorder p="xs">
                  <Card.Section>
                    <div style={{ position: 'relative' }}>
                      <Image
                        src={image.url}
                        alt={image.prompt}
                        h={300}
                        fit="cover"
                        style={{ cursor: 'pointer' }}
                        onClick={() => setSelectedImage(image)}
                      />
                      <Group
                        gap="xs"
                        style={{
                          position: 'absolute',
                          top: 8,
                          right: 8,
                        }}
                      >
                        <Tooltip label="View full size">
                          <ActionIcon
                            variant="filled"
                            color="blue"
                            onClick={() => setSelectedImage(image)}
                          >
                            <IconZoomIn size={16} />
                          </ActionIcon>
                        </Tooltip>
                        <Tooltip label="Download">
                          <ActionIcon
                            variant="filled"
                            color="green"
                            onClick={() => handleDownload(image)}
                          >
                            <IconDownload size={16} />
                          </ActionIcon>
                        </Tooltip>
                        <Tooltip label="Delete">
                          <ActionIcon
                            variant="filled"
                            color="red"
                            onClick={() => removeImage(image.id)}
                          >
                            <IconTrash size={16} />
                          </ActionIcon>
                        </Tooltip>
                      </Group>
                    </div>
                  </Card.Section>
                  <Stack gap="xs" mt="sm">
                    <Group justify="space-between" gap="xs">
                      <Badge variant="light" size="sm">
                        {image.model}
                      </Badge>
                      <Badge variant="light" size="sm" color="green">
                        {formatters.currency(image.cost)}
                      </Badge>
                    </Group>
                    <Text size="sm" lineClamp={2}>
                      {image.prompt}
                    </Text>
                    <Group gap="xs" justify="space-between">
                      <Text size="xs" c="dimmed">
                        {image.size} • {image.quality}
                        {image.style && ` • ${image.style}`}
                      </Text>
                      <ActionIcon
                        size="xs"
                        variant="subtle"
                        onClick={() => handleCopyPrompt(image.prompt)}
                      >
                        <IconCopy size={12} />
                      </ActionIcon>
                    </Group>
                  </Stack>
                </Card>
              ))}
            </SimpleGrid>
          ) : (
            <Paper withBorder p="xl" radius="md" style={{ textAlign: 'center' }}>
              <Stack align="center" gap="md">
                <ThemeIcon size={60} variant="light" color="gray">
                  <IconPhoto size={30} />
                </ThemeIcon>
                <div>
                  <Text size="lg" fw={500}>No images generated yet</Text>
                  <Text size="sm" c="dimmed" mt={4}>
                    Enter a prompt and click generate to create your first image
                  </Text>
                </div>
              </Stack>
            </Paper>
          )}
        </Grid.Col>
      </Grid>

      {/* Full size image modal */}
      {selectedImage && (
        <div
          style={{
            position: 'fixed',
            top: 0,
            left: 0,
            right: 0,
            bottom: 0,
            backgroundColor: 'rgba(0, 0, 0, 0.8)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            zIndex: 1000,
            cursor: 'pointer',
          }}
          onClick={() => setSelectedImage(null)}
        >
          <div
            style={{
              maxWidth: '90vw',
              maxHeight: '90vh',
              position: 'relative',
            }}
            onClick={(e) => e.stopPropagation()}
          >
            <Image
              src={selectedImage.url}
              alt={selectedImage.prompt}
              fit="contain"
              style={{ maxWidth: '100%', maxHeight: '90vh' }}
            />
            <Group
              gap="xs"
              style={{
                position: 'absolute',
                top: 16,
                right: 16,
              }}
            >
              <ActionIcon
                variant="filled"
                color="green"
                size="lg"
                onClick={() => handleDownload(selectedImage)}
              >
                <IconDownload size={20} />
              </ActionIcon>
              <ActionIcon
                variant="filled"
                color="gray"
                size="lg"
                onClick={() => setSelectedImage(null)}
              >
                <IconPhoto size={20} />
              </ActionIcon>
            </Group>
          </div>
        </div>
      )}

      <LoadingOverlay visible={isGenerating} />
    </Stack>
  );
}