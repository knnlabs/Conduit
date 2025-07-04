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
  LoadingOverlay,
  ScrollArea,
  Progress,
  Center,
} from '@mantine/core';
import {
  IconVideo,
  IconSettings,
  IconDownload,
  IconTrash,
  IconRefresh,
  IconEye,
  IconCopy,
  IconCheck,
  IconAlertCircle,
  IconClock,
  IconUpload,
} from '@tabler/icons-react';
import { useState, useRef } from 'react';
import { useDisclosure } from '@mantine/hooks';
import { useVideoGeneration, useAvailableModels } from '@/hooks/api/useCoreApi';
import { useVirtualKeys } from '@/hooks/api/useAdminApi';
import { notifications } from '@mantine/notifications';
import { safeLog } from '@/lib/utils/logging';
import TaskProgressPanel from '@/components/realtime/TaskProgressPanel';

interface GeneratedVideo {
  id: string;
  url?: string;
  prompt: string;
  model: string;
  size: string;
  duration: number;
  fps?: number;
  status: 'pending' | 'processing' | 'completed' | 'failed';
  progress?: number;
  createdAt: Date;
  completedAt?: Date;
}

interface VideoGenerationRequest {
  virtualKey: string;
  prompt: string;
  model: string;
  size: string;
  duration: number;
  fps?: number;
}

export default function VideoGenerationPage() {
  const [prompt, setPrompt] = useState('');
  const [selectedVirtualKey, setSelectedVirtualKey] = useState('');
  const [selectedModel, setSelectedModel] = useState('minimax-video');
  const [size, setSize] = useState('1280x720');
  const [duration, setDuration] = useState(5);
  const [fps, setFps] = useState(24);
  const [generatedVideos, setGeneratedVideos] = useState<GeneratedVideo[]>([]);
  const [selectedVideo, setSelectedVideo] = useState<GeneratedVideo | null>(null);
  const [settingsOpened, { open: openSettings, close: closeSettings }] = useDisclosure(false);
  const [previewOpened, { open: openPreview, close: closePreview }] = useDisclosure(false);
  const videoRef = useRef<HTMLVideoElement>(null);

  const { data: virtualKeys, isLoading: keysLoading } = useVirtualKeys();
  const { data: models, isLoading: modelsLoading } = useAvailableModels();
  const videoGeneration = useVideoGeneration();

  // Filter models for video generation
  const videoModels = models?.filter((model: unknown) => 
    (model as { id: string }).id.includes('video') || 
    (model as { id: string }).id.includes('minimax') ||
    (model as { id: string }).id.includes('replicate') ||
    (model as { id: string }).id.includes('runway') ||
    (model as { id: string }).id.includes('stable-video')
  ) || [];

  const handleGenerateVideo = async () => {
    if (!prompt.trim() || !selectedVirtualKey || !selectedModel) {
      notifications.show({
        title: 'Configuration Required',
        message: 'Please enter a prompt and select a virtual key and model',
        color: 'orange',
      });
      return;
    }

    try {
      const request: VideoGenerationRequest = {
        virtualKey: selectedVirtualKey,
        prompt: prompt.trim(),
        model: selectedModel,
        size,
        duration,
        fps,
      };

      const response = await videoGeneration.mutateAsync(request);

      if (response.url || response.data) {
        // Video generation completed immediately
        const newVideo: GeneratedVideo = {
          id: `video_${Date.now()}`,
          prompt: prompt.trim(),
          model: selectedModel,
          size,
          duration,
          fps,
          status: 'completed',
          progress: 100,
          url: response.url || response.data?.[0]?.url,
          createdAt: new Date(),
          completedAt: new Date(),
        };

        setGeneratedVideos(prev => [newVideo, ...prev]);

        safeLog('Video generation successful', {
          model: selectedModel,
          prompt: prompt.trim().slice(0, 50),
          duration,
          size,
        });
      } else {
        // Video generation queued for async processing
        notifications.show({
          title: 'Video Queued',
          message: 'Video generation has been queued for processing. You will be notified when complete.',
          color: 'blue',
        });
      }
    } catch (error: unknown) {
      safeLog('Video generation failed', { error: (error as Error).message });
    }
  };

  const handleTaskCompleted = (task: unknown) => {
    if ((task as { type: string; status: string; result?: unknown }).type === 'video' && (task as { status: string }).status === 'completed' && (task as { result?: unknown }).result) {
      const newVideo: GeneratedVideo = {
        id: (task as { taskId: string }).taskId,
        prompt: ((task as { result: { prompt?: string } }).result.prompt) || 'Generated video',
        model: ((task as { result: { model?: string } }).result.model) || selectedModel,
        size: ((task as { result: { size?: string } }).result.size) || size,
        duration: ((task as { result: { duration?: number } }).result.duration) || duration,
        fps: ((task as { result: { fps?: number } }).result.fps) || fps,
        status: 'completed',
        progress: 100,
        url: (task as { result: { url: string } }).result.url,
        createdAt: new Date((task as { startedAt: string }).startedAt),
        completedAt: new Date((task as { completedAt: string }).completedAt),
      };
      
      setGeneratedVideos(prev => [newVideo, ...prev]);
    }
  };

  const handleDownloadVideo = async (video: GeneratedVideo) => {
    if (!video.url) {
      notifications.show({
        title: 'Not Available',
        message: 'Video is not ready for download yet',
        color: 'orange',
      });
      return;
    }

    try {
      const response = await fetch(video.url);
      const blob = await response.blob();
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `generated-video-${video.id}.mp4`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);

      notifications.show({
        title: 'Downloaded',
        message: 'Video downloaded successfully',
        color: 'green',
      });
    } catch (error) {
      notifications.show({
        title: 'Download Failed',
        message: 'Failed to download video',
        color: 'red',
      });
    }
  };

  const handleDeleteVideo = (videoId: string) => {
    setGeneratedVideos(prev => prev.filter(video => video.id !== videoId));
    notifications.show({
      title: 'Deleted',
      message: 'Video removed from gallery',
      color: 'blue',
    });
  };

  const handleClearGallery = () => {
    setGeneratedVideos([]);
    notifications.show({
      title: 'Gallery Cleared',
      message: 'All videos have been removed',
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
    } catch (error) {
      notifications.show({
        title: 'Copy Failed',
        message: 'Failed to copy prompt',
        color: 'red',
      });
    }
  };

  const getSizeOptions = () => {
    if (selectedModel.includes('minimax')) {
      return [
        { value: '720x480', label: '720×480 (4:3)' },
        { value: '1280x720', label: '1280×720 (16:9)' },
        { value: '1920x1080', label: '1920×1080 (16:9)' },
        { value: '720x1280', label: '720×1280 (9:16 Portrait)' },
        { value: '1080x1920', label: '1080×1920 (9:16 Portrait)' },
      ];
    }
    return [
      { value: '512x512', label: '512×512 (Square)' },
      { value: '768x512', label: '768×512 (3:2)' },
      { value: '512x768', label: '512×768 (2:3)' },
      { value: '1024x576', label: '1024×576 (16:9)' },
      { value: '576x1024', label: '576×1024 (9:16)' },
    ];
  };

  const openVideoPreview = (video: GeneratedVideo) => {
    setSelectedVideo(video);
    openPreview();
  };

  const getStatusColor = (status: GeneratedVideo['status']) => {
    switch (status) {
      case 'pending': return 'gray';
      case 'processing': return 'blue';
      case 'completed': return 'green';
      case 'failed': return 'red';
      default: return 'gray';
    }
  };

  const getStatusText = (status: GeneratedVideo['status']) => {
    switch (status) {
      case 'pending': return 'Queued';
      case 'processing': return 'Processing';
      case 'completed': return 'Completed';
      case 'failed': return 'Failed';
      default: return 'Unknown';
    }
  };

  return (
    <Stack gap="md">
      <Group justify="space-between">
        <div>
          <Title order={1}>Video Generation</Title>
          <Text c="dimmed">Generate AI-powered videos from text prompts</Text>
        </div>

        <Group>
          <Button
            variant="light"
            leftSection={<IconSettings size={16} />}
            onClick={openSettings}
          >
            Settings
          </Button>
          {generatedVideos.length > 0 && (
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
                <Text fw={600} size="lg">Generate Videos</Text>
                <Badge variant="light">{videoModels.length} models available</Badge>
              </Group>

              {!selectedVirtualKey && (
                <Alert icon={<IconAlertCircle size={16} />} color="orange">
                  Please select a virtual key in settings to start generating videos.
                </Alert>
              )}

              <Alert icon={<IconClock size={16} />} variant="light">
                <Text size="sm">
                  Video generation can take several minutes. You'll be notified when complete.
                </Text>
              </Alert>

              <Textarea
                label="Prompt"
                placeholder="Describe the video you want to generate..."
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
                  data={videoModels.map((model: unknown) => ({
                    value: (model as { id: string }).id,
                    label: (model as { id: string }).id,
                  }))}
                  value={selectedModel}
                  onChange={(value) => setSelectedModel(value || 'minimax-video')}
                  disabled={!selectedVirtualKey || modelsLoading}
                />

                <Select
                  label="Resolution"
                  data={getSizeOptions()}
                  value={size}
                  onChange={(value) => setSize(value || '1280x720')}
                />
              </Group>

              <Group grow>
                <NumberInput
                  label="Duration (seconds)"
                  description="Video length"
                  value={duration}
                  onChange={(value) => setDuration(value as number)}
                  min={1}
                  max={selectedModel.includes('minimax') ? 6 : 10}
                />

                <NumberInput
                  label="Frame Rate (FPS)"
                  description="Frames per second"
                  value={fps}
                  onChange={(value) => setFps(value as number)}
                  min={12}
                  max={60}
                />
              </Group>

              <Button
                leftSection={<IconVideo size={16} />}
                onClick={handleGenerateVideo}
                disabled={!prompt.trim() || !selectedVirtualKey || !selectedModel}
                loading={videoGeneration.isPending}
                size="lg"
              >
                Generate Video
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
                taskType="video"
                onTaskCompleted={handleTaskCompleted}
                maxHeight={200}
              />
            </div>
          )}
          
          {/* Video Gallery */}
          <Card withBorder h="600px">
            <Stack gap="md" h="100%">
              <Group justify="space-between">
                <Text fw={600} size="lg">Generated Videos</Text>
                <Badge variant="light">{generatedVideos.length} videos</Badge>
              </Group>

              <ScrollArea flex={1}>
                {generatedVideos.length === 0 ? (
                  <Alert icon={<IconVideo size={16} />} variant="light">
                    <Text>No videos generated yet. Create your first AI video above!</Text>
                  </Alert>
                ) : (
                  <Stack gap="md">
                    {generatedVideos.map((video) => (
                      <Card key={video.id} withBorder p="md">
                        <Stack gap="md">
                          <Group justify="space-between">
                            <Badge color={getStatusColor(video.status)} variant="light">
                              {getStatusText(video.status)}
                            </Badge>
                            
                            <Group gap="xs">
                              {video.status === 'completed' && video.url && (
                                <>
                                  <Tooltip label="Preview video">
                                    <ActionIcon
                                      size="sm"
                                      variant="light"
                                      onClick={() => openVideoPreview(video)}
                                    >
                                      <IconEye size={14} />
                                    </ActionIcon>
                                  </Tooltip>
                                  
                                  <Tooltip label="Download">
                                    <ActionIcon
                                      size="sm"
                                      variant="light"
                                      onClick={() => handleDownloadVideo(video)}
                                    >
                                      <IconDownload size={14} />
                                    </ActionIcon>
                                  </Tooltip>
                                </>
                              )}
                              
                              <Tooltip label="Copy prompt">
                                <ActionIcon
                                  size="sm"
                                  variant="light"
                                  onClick={() => copyPromptToClipboard(video.prompt)}
                                >
                                  <IconCopy size={14} />
                                </ActionIcon>
                              </Tooltip>
                              
                              <Tooltip label="Delete">
                                <ActionIcon
                                  size="sm"
                                  variant="light"
                                  color="red"
                                  onClick={() => handleDeleteVideo(video.id)}
                                >
                                  <IconTrash size={14} />
                                </ActionIcon>
                              </Tooltip>
                            </Group>
                          </Group>

                          {video.status === 'processing' && (
                            <div>
                              <Group justify="space-between" mb="xs">
                                <Text size="sm">Processing...</Text>
                                <Text size="sm">{Math.round(video.progress || 0)}%</Text>
                              </Group>
                              <Progress value={video.progress || 0} animated />
                            </div>
                          )}

                          {video.status === 'completed' && video.url && (
                            <div
                              style={{
                                position: 'relative',
                                backgroundColor: '#000',
                                borderRadius: '4px',
                                aspectRatio: '16/9',
                                cursor: 'pointer',
                              }}
                              onClick={() => openVideoPreview(video)}
                            >
                              <video
                                style={{
                                  width: '100%',
                                  height: '100%',
                                  objectFit: 'cover',
                                  borderRadius: '4px',
                                }}
                                muted
                              >
                                <source src={video.url} type="video/mp4" />
                              </video>
                              
                              <Center
                                style={{
                                  position: 'absolute',
                                  top: 0,
                                  left: 0,
                                  right: 0,
                                  bottom: 0,
                                  background: 'rgba(0, 0, 0, 0.3)',
                                  borderRadius: '4px',
                                }}
                              >
                                <ActionIcon size="xl" variant="filled" color="white">
                                  <IconVideo size={24} />
                                </ActionIcon>
                              </Center>
                            </div>
                          )}

                          <Stack gap="xs">
                            <Text size="sm" lineClamp={2}>
                              {video.prompt}
                            </Text>
                            
                            <Group justify="space-between">
                              <Group gap="xs">
                                <Badge size="xs" variant="light">
                                  {video.model}
                                </Badge>
                                <Badge size="xs" variant="light">
                                  {video.size}
                                </Badge>
                                <Badge size="xs" variant="light">
                                  {video.duration}s
                                </Badge>
                              </Group>
                              
                              <Text size="xs" c="dimmed">
                                {video.completedAt 
                                  ? video.completedAt.toLocaleString()
                                  : video.createdAt.toLocaleString()
                                }
                              </Text>
                            </Group>
                          </Stack>
                        </Stack>
                      </Card>
                    ))}
                  </Stack>
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
        title="Video Generation Settings"
        size="md"
      >
        <Stack gap="md">
          <Select
            label="Virtual Key"
            placeholder="Select a virtual key"
            data={virtualKeys?.map((key: unknown) => ({
              value: (key as { id: string }).id,
              label: (key as { keyName: string }).keyName,
            })) || []}
            value={selectedVirtualKey}
            onChange={(value) => setSelectedVirtualKey(value || '')}
            disabled={keysLoading}
          />

          <Alert icon={<IconAlertCircle size={16} />} variant="light">
            <Text size="sm">
              Video generation is resource-intensive and may cost more credits than other operations.
            </Text>
          </Alert>

          <Group justify="flex-end">
            <Button onClick={closeSettings}>
              Done
            </Button>
          </Group>
        </Stack>
      </Modal>

      {/* Video Preview Modal */}
      <Modal
        opened={previewOpened}
        onClose={closePreview}
        title="Video Preview"
        size="xl"
        centered
      >
        {selectedVideo && selectedVideo.url && (
          <Stack gap="md">
            <video
              ref={videoRef}
              controls
              style={{
                width: '100%',
                maxHeight: '70vh',
                backgroundColor: '#000',
                borderRadius: '4px',
              }}
            >
              <source src={selectedVideo.url} type="video/mp4" />
              Your browser does not support the video tag.
            </video>
            
            <Card withBorder>
              <Stack gap="sm">
                <Group justify="space-between">
                  <Text fw={600}>Video Details</Text>
                  <Group gap="xs">
                    <ActionIcon
                      variant="light"
                      onClick={() => handleDownloadVideo(selectedVideo)}
                    >
                      <IconDownload size={16} />
                    </ActionIcon>
                    <ActionIcon
                      variant="light"
                      onClick={() => copyPromptToClipboard(selectedVideo.prompt)}
                    >
                      <IconCopy size={16} />
                    </ActionIcon>
                  </Group>
                </Group>
                
                <Text size="sm">
                  <Text span fw={500}>Prompt:</Text> {selectedVideo.prompt}
                </Text>
                
                <Group gap="md">
                  <Badge variant="light">{selectedVideo.model}</Badge>
                  <Badge variant="light">{selectedVideo.size}</Badge>
                  <Badge variant="light">{selectedVideo.duration}s</Badge>
                  <Badge variant="light">{selectedVideo.fps} FPS</Badge>
                </Group>
                
                <Text size="xs" c="dimmed">
                  Generated on {selectedVideo.completedAt?.toLocaleString() || selectedVideo.createdAt.toLocaleString()}
                </Text>
              </Stack>
            </Card>
          </Stack>
        )}
      </Modal>
    </Stack>
  );
}