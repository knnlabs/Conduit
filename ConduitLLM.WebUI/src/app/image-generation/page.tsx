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
  Grid,
  Badge,
  ActionIcon,
  Tooltip,
  Modal,
  NumberInput,
  Alert,
  Image,
  ScrollArea,
} from '@mantine/core';
import {
  IconPhoto,
  IconSettings,
  IconDownload,
  IconTrash,
  IconEye,
  IconCopy,
  IconAlertCircle,
} from '@tabler/icons-react';
import { useState, useRef } from 'react';
import { useDisclosure } from '@mantine/hooks';
import { useImageGeneration, useAvailableModels } from '@/hooks/api/useCoreApi';
import { useVirtualKeys } from '@/hooks/api/useAdminApi';
import { notifications } from '@mantine/notifications';
import { safeLog } from '@/lib/utils/logging';
import TaskProgressPanel from '@/components/realtime/TaskProgressPanel';

interface GeneratedImage {
  id: string;
  url: string;
  prompt: string;
  model: string;
  size: string;
  quality: string;
  style?: string;
  createdAt: Date;
}

interface ImageGenerationRequest {
  virtualKey: string;
  prompt: string;
  model: string;
  size: string;
  quality: string;
  style?: string;
  n: number;
}

export default function ImageGenerationPage() {
  const [prompt, setPrompt] = useState('');
  const [selectedVirtualKey, setSelectedVirtualKey] = useState('');
  const [selectedModel, setSelectedModel] = useState('dall-e-3');
  const [size, setSize] = useState('1024x1024');
  const [quality, setQuality] = useState('standard');
  const [style, setStyle] = useState('vivid');
  const [numberOfImages, setNumberOfImages] = useState(1);
  const [generatedImages, setGeneratedImages] = useState<GeneratedImage[]>([]);
  const [selectedImage, setSelectedImage] = useState<GeneratedImage | null>(null);
  const [settingsOpened, { open: openSettings, close: closeSettings }] = useDisclosure(false);
  const [previewOpened, { open: openPreview, close: closePreview }] = useDisclosure(false);
  const _fileInputRef = useRef<HTMLInputElement>(null);

  const { data: virtualKeys, isLoading: keysLoading } = useVirtualKeys();
  const { data: models, isLoading: modelsLoading } = useAvailableModels();
  const imageGeneration = useImageGeneration();

  // Filter models for image generation
  const imageModels = models?.filter((model: unknown) => 
    typeof model === 'object' && model !== null && 'id' in model &&
    typeof (model as { id: string }).id === 'string' && (
      (model as { id: string }).id.includes('dall-e') || 
      (model as { id: string }).id.includes('image') || 
      (model as { id: string }).id.includes('minimax') ||
      (model as { id: string }).id.includes('replicate')
    )
  ) || [];

  const handleGenerateImage = async () => {
    if (!prompt.trim() || !selectedVirtualKey || !selectedModel) {
      notifications.show({
        title: 'Configuration Required',
        message: 'Please enter a prompt and select a virtual key and model',
        color: 'orange',
      });
      return;
    }

    try {
      const request: ImageGenerationRequest = {
        virtualKey: selectedVirtualKey,
        prompt: prompt.trim(),
        model: selectedModel,
        size,
        quality,
        n: numberOfImages,
      };

      // Add style for DALL-E 3
      if (selectedModel === 'dall-e-3') {
        request.style = style;
      }

      const response = await imageGeneration.mutateAsync(request);

      if ((response as any).data && (response as any).data.length > 0) {
        const newImages: GeneratedImage[] = (response as any).data.map((imageData: unknown, index: number) => {
          if (typeof imageData === 'object' && imageData !== null) {
            const imgData = imageData as { url?: string; b64_json?: string };
            return {
              id: `img_${Date.now()}_${index}`,
              url: imgData.url || imgData.b64_json || '',
              prompt: prompt.trim(),
              model: selectedModel,
              size,
              quality,
              style: selectedModel === 'dall-e-3' ? style : undefined,
              createdAt: new Date(),
            };
          }
          return {
            id: `img_${Date.now()}_${index}`,
            url: '',
            prompt: prompt.trim(),
            model: selectedModel,
            size,
            quality,
            style: selectedModel === 'dall-e-3' ? style : undefined,
            createdAt: new Date(),
          };
        });

        setGeneratedImages(prev => [...newImages, ...prev]);
        
        safeLog('Image generation successful', {
          model: selectedModel,
          prompt: prompt.trim().slice(0, 50),
          count: newImages.length,
        });
      }
    } catch (error: unknown) {
      safeLog('Image generation failed', { error: error instanceof Error ? error.message : String(error) });
    }
  };

  const handleTaskCompleted = (task: unknown) => {
    if (typeof task === 'object' && task !== null &&
        'type' in task && 'status' in task && 'result' in task &&
        (task as { type: string }).type === 'image' &&
        (task as { status: string }).status === 'completed' &&
        (task as { result: unknown }).result) {
      const taskObj = task as any;
      const newImages: GeneratedImage[] = taskObj.result.data?.map((imageData: unknown, index: number) => {
        if (typeof imageData === 'object' && imageData !== null) {
          const imgData = imageData as { url?: string; b64_json?: string };
          return {
            id: `img_${taskObj.taskId}_${index}`,
            url: imgData.url || imgData.b64_json || '',
            prompt: taskObj.result.prompt || 'Generated image',
            model: taskObj.result.model || selectedModel,
            size: taskObj.result.size || size,
            quality: taskObj.result.quality || quality,
            style: taskObj.result.style,
            createdAt: new Date(),
          };
        }
        return {
          id: `img_${taskObj.taskId}_${index}`,
          url: '',
          prompt: taskObj.result.prompt || 'Generated image',
          model: taskObj.result.model || selectedModel,
          size: taskObj.result.size || size,
          quality: taskObj.result.quality || quality,
          style: taskObj.result.style,
          createdAt: new Date(),
        };
      }) || [];
      
      if (newImages.length > 0) {
        setGeneratedImages(prev => [...newImages, ...prev]);
      }
    }
  };

  const handleDownloadImage = async (image: GeneratedImage) => {
    try {
      const response = await fetch(image.url);
      const blob = await response.blob();
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `generated-image-${image.id}.png`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);

      notifications.show({
        title: 'Downloaded',
        message: 'Image downloaded successfully',
        color: 'green',
      });
    } catch (_error) {
      notifications.show({
        title: 'Download Failed',
        message: 'Failed to download image',
        color: 'red',
      });
    }
  };

  const handleDeleteImage = (imageId: string) => {
    setGeneratedImages(prev => prev.filter(img => img.id !== imageId));
    notifications.show({
      title: 'Deleted',
      message: 'Image removed from gallery',
      color: 'blue',
    });
  };

  const handleClearGallery = () => {
    setGeneratedImages([]);
    notifications.show({
      title: 'Gallery Cleared',
      message: 'All images have been removed',
      color: 'blue',
    });
  };

  const copyPromptToClipboard = async (prompt: string) => {
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

  const getSizeOptions = () => {
    if (selectedModel === 'dall-e-3') {
      return [
        { value: '1024x1024', label: '1024×1024 (Square)' },
        { value: '1792x1024', label: '1792×1024 (Landscape)' },
        { value: '1024x1792', label: '1024×1792 (Portrait)' },
      ];
    } else if (selectedModel === 'dall-e-2') {
      return [
        { value: '256x256', label: '256×256' },
        { value: '512x512', label: '512×512' },
        { value: '1024x1024', label: '1024×1024' },
      ];
    }
    return [
      { value: '1024x1024', label: '1024×1024 (Square)' },
      { value: '1792x1024', label: '1792×1024 (Landscape)' },
      { value: '1024x1792', label: '1024×1792 (Portrait)' },
    ];
  };

  const openImagePreview = (image: GeneratedImage) => {
    setSelectedImage(image);
    openPreview();
  };

  return (
    <Stack gap="md">
      <Group justify="space-between">
        <div>
          <Title order={1}>Image Generation</Title>
          <Text c="dimmed">Generate AI-powered images from text prompts</Text>
        </div>

        <Group>
          <Button
            variant="light"
            leftSection={<IconSettings size={16} />}
            onClick={openSettings}
          >
            Settings
          </Button>
          {generatedImages.length > 0 && (
            <Button
              variant="light"
              color="red"
              leftSection={<IconTrash size={16} />}
              onClick={handleClearGallery}
            >
              Clear Gallery
            </Button>
          )}
        </Group>
      </Group>

      <Grid>
        <Grid.Col span={{ base: 12, md: 6 }}>
          {/* Generation Panel */}
          <Card withBorder h="fit-content">
            <Stack gap="md">
              <Group justify="space-between">
                <Text fw={600} size="lg">Generate Images</Text>
                <Badge variant="light">{imageModels.length} models available</Badge>
              </Group>

              {!selectedVirtualKey && (
                <Alert icon={<IconAlertCircle size={16} />} color="orange">
                  Please select a virtual key in settings to start generating images.
                </Alert>
              )}

              <Textarea
                label="Prompt"
                placeholder="Describe the image you want to generate..."
                value={prompt}
                onChange={(event) => setPrompt(event.currentTarget.value)}
                minRows={3}
                maxRows={6}
                required
              />

              <Group grow>
                <Select
                  label="Model"
                  placeholder="Select a model"
                  data={imageModels.map((model: unknown) => {
                    if (typeof model === 'object' && model !== null && 'id' in model) {
                      return {
                        value: (model as { id: string }).id,
                        label: (model as { id: string }).id,
                      };
                    }
                    return { value: '', label: 'Invalid model' };
                  })}
                  value={selectedModel}
                  onChange={(value) => setSelectedModel(value || 'dall-e-3')}
                  disabled={!selectedVirtualKey || modelsLoading}
                />

                <Select
                  label="Size"
                  data={getSizeOptions()}
                  value={size}
                  onChange={(value) => setSize(value || '1024x1024')}
                />
              </Group>

              <Group grow>
                <Select
                  label="Quality"
                  data={[
                    { value: 'standard', label: 'Standard' },
                    { value: 'hd', label: 'HD (Premium)' },
                  ]}
                  value={quality}
                  onChange={(value) => setQuality(value || 'standard')}
                  disabled={selectedModel !== 'dall-e-3'}
                />

                {selectedModel === 'dall-e-3' && (
                  <Select
                    label="Style"
                    data={[
                      { value: 'vivid', label: 'Vivid' },
                      { value: 'natural', label: 'Natural' },
                    ]}
                    value={style}
                    onChange={(value) => setStyle(value || 'vivid')}
                  />
                )}
              </Group>

              <NumberInput
                label="Number of Images"
                description="Generate multiple variations"
                value={numberOfImages}
                onChange={(value) => setNumberOfImages(value as number)}
                min={1}
                max={selectedModel === 'dall-e-3' ? 1 : 10}
                disabled={selectedModel === 'dall-e-3'}
              />

              <Button
                leftSection={<IconPhoto size={16} />}
                onClick={handleGenerateImage}
                disabled={!prompt.trim() || !selectedVirtualKey || !selectedModel}
                loading={imageGeneration.isPending}
                size="lg"
              >
                Generate Images
              </Button>
            </Stack>
          </Card>
        </Grid.Col>

        <Grid.Col span={{ base: 12, md: 6 }}>
          {/* Real-time Task Progress */}
          {selectedVirtualKey && (
            <div style={{ marginBottom: '1rem' }}>
              <TaskProgressPanel 
                virtualKey={selectedVirtualKey}
                taskType="image"
                onTaskCompleted={handleTaskCompleted}
                maxHeight={200}
              />
            </div>
          )}
          
          {/* Image Gallery */}
          <Card withBorder h="600px">
            <Stack gap="md" h="100%">
              <Group justify="space-between">
                <Text fw={600} size="lg">Generated Images</Text>
                <Badge variant="light">{generatedImages.length} images</Badge>
              </Group>

              <ScrollArea flex={1}>
                {generatedImages.length === 0 ? (
                  <Alert icon={<IconPhoto size={16} />} variant="light">
                    <Text>No images generated yet. Create your first AI image above!</Text>
                  </Alert>
                ) : (
                  <Grid>
                    {generatedImages.map((image) => (
                      <Grid.Col key={image.id} span={6}>
                        <Card withBorder p="xs" style={{ position: 'relative' }}>
                          <div style={{ position: 'relative' }}>
                            <Image
                              src={image.url}
                              alt={image.prompt}
                              style={{ 
                                cursor: 'pointer',
                                borderRadius: '4px',
                              }}
                              onClick={() => openImagePreview(image)}
                            />
                            
                            <Group 
                              gap="xs" 
                              style={{ 
                                position: 'absolute',
                                top: '4px',
                                right: '4px',
                                background: 'rgba(0, 0, 0, 0.7)',
                                borderRadius: '4px',
                                padding: '2px',
                              }}
                            >
                              <Tooltip label="View full size">
                                <ActionIcon
                                  size="sm"
                                  variant="transparent"
                                  color="white"
                                  onClick={() => openImagePreview(image)}
                                >
                                  <IconEye size={12} />
                                </ActionIcon>
                              </Tooltip>
                              
                              <Tooltip label="Download">
                                <ActionIcon
                                  size="sm"
                                  variant="transparent"
                                  color="white"
                                  onClick={() => handleDownloadImage(image)}
                                >
                                  <IconDownload size={12} />
                                </ActionIcon>
                              </Tooltip>
                              
                              <Tooltip label="Copy prompt">
                                <ActionIcon
                                  size="sm"
                                  variant="transparent"
                                  color="white"
                                  onClick={() => copyPromptToClipboard(image.prompt)}
                                >
                                  <IconCopy size={12} />
                                </ActionIcon>
                              </Tooltip>
                              
                              <Tooltip label="Delete">
                                <ActionIcon
                                  size="sm"
                                  variant="transparent"
                                  color="red"
                                  onClick={() => handleDeleteImage(image.id)}
                                >
                                  <IconTrash size={12} />
                                </ActionIcon>
                              </Tooltip>
                            </Group>
                          </div>
                          
                          <Stack gap="xs" mt="xs">
                            <Text size="xs" c="dimmed" lineClamp={2}>
                              {image.prompt}
                            </Text>
                            <Group justify="space-between">
                              <Badge size="xs" variant="light">
                                {image.model}
                              </Badge>
                              <Text size="xs" c="dimmed">
                                {image.size}
                              </Text>
                            </Group>
                          </Stack>
                        </Card>
                      </Grid.Col>
                    ))}
                  </Grid>
                )}
              </ScrollArea>
            </Stack>
          </Card>
        </Grid.Col>
      </Grid>

      {/* Settings Modal */}
      <Modal
        opened={settingsOpened}
        onClose={closeSettings}
        title="Image Generation Settings"
        size="md"
      >
        <Stack gap="md">
          <Select
            label="Virtual Key"
            placeholder="Select a virtual key"
            data={virtualKeys?.map((key: unknown) => {
              if (typeof key === 'object' && key !== null && 'id' in key && 'keyName' in key) {
                return {
                  value: (key as { id: string }).id,
                  label: (key as { keyName: string }).keyName,
                };
              }
              return { value: '', label: 'Invalid key' };
            }) || []}
            value={selectedVirtualKey}
            onChange={(value) => setSelectedVirtualKey(value || '')}
            disabled={keysLoading}
          />

          <Alert icon={<IconAlertCircle size={16} />} variant="light">
            <Text size="sm">
              Image generation uses your virtual key credits. Costs vary by model and quality settings.
            </Text>
          </Alert>

          <Group justify="flex-end">
            <Button onClick={closeSettings}>
              Done
            </Button>
          </Group>
        </Stack>
      </Modal>

      {/* Image Preview Modal */}
      <Modal
        opened={previewOpened}
        onClose={closePreview}
        title="Image Preview"
        size="xl"
        centered
      >
        {selectedImage && (
          <Stack gap="md">
            <Image
              src={selectedImage.url}
              alt={selectedImage.prompt}
              style={{ maxHeight: '70vh', objectFit: 'contain' }}
            />
            
            <Card withBorder>
              <Stack gap="sm">
                <Group justify="space-between">
                  <Text fw={600}>Image Details</Text>
                  <Group gap="xs">
                    <ActionIcon
                      variant="light"
                      onClick={() => handleDownloadImage(selectedImage)}
                    >
                      <IconDownload size={16} />
                    </ActionIcon>
                    <ActionIcon
                      variant="light"
                      onClick={() => copyPromptToClipboard(selectedImage.prompt)}
                    >
                      <IconCopy size={16} />
                    </ActionIcon>
                  </Group>
                </Group>
                
                <Text size="sm">
                  <Text span fw={500}>Prompt:</Text> {selectedImage.prompt}
                </Text>
                
                <Group gap="md">
                  <Badge variant="light">{selectedImage.model}</Badge>
                  <Badge variant="light">{selectedImage.size}</Badge>
                  <Badge variant="light">{selectedImage.quality}</Badge>
                  {selectedImage.style && (
                    <Badge variant="light">{selectedImage.style}</Badge>
                  )}
                </Group>
                
                <Text size="xs" c="dimmed">
                  Generated on {selectedImage.createdAt.toLocaleString()}
                </Text>
              </Stack>
            </Card>
          </Stack>
        )}
      </Modal>
    </Stack>
  );
}