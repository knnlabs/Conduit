'use client';

import {
  Stack,
  Title,
  Text,
  Card,
  Group,
  Button,
  Badge,
  SimpleGrid,
  ThemeIcon,
  Progress,
  Paper,
  Switch,
  NumberInput,
  Select,
  Slider,
  LoadingOverlay,
  Alert,
  Tabs,
  Table,
  ScrollArea,
  ActionIcon,
  Tooltip,
  Code,
  Divider,
} from '@mantine/core';
import {
  IconWaveSine,
  IconSettings,
  IconRefresh,
  IconDownload,
  IconMicrophone,
  IconVolume,
  IconActivity,
  IconClock,
  IconBolt,
  IconFilter,
  IconAdjustments,
  IconFileMusic,
  IconCircleCheck,
  IconAlertCircle,
  IconInfoCircle,
  IconPlayerPlay,
  IconPlayerPause,
  IconTestPipe,
} from '@tabler/icons-react';
import { useState, useEffect } from 'react';
import { notifications } from '@mantine/notifications';
import { formatters } from '@/lib/utils/formatters';

interface ProcessingConfig {
  id: string;
  name: string;
  category: 'transcription' | 'synthesis' | 'general';
  enabled: boolean;
  settings: Record<string, any>;
}

interface ProcessingQueue {
  id: string;
  type: 'transcription' | 'synthesis';
  status: 'pending' | 'processing' | 'completed' | 'failed';
  inputFile?: string;
  outputFile?: string;
  duration?: number;
  progress: number;
  provider: string;
  model: string;
  createdAt: string;
  startedAt?: string;
  completedAt?: string;
  error?: string;
}

interface ProcessingStats {
  totalProcessed: number;
  totalDuration: number;
  avgProcessingTime: number;
  successRate: number;
  queueLength: number;
  activeJobs: number;
}

interface AudioFormat {
  id: string;
  name: string;
  mimeType: string;
  extension: string;
  supported: boolean;
  bitrate?: number;
  sampleRate?: number;
  channels?: number;
}

export default function AudioProcessingPage() {
  const [isLoading, setIsLoading] = useState(true);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [activeTab, setActiveTab] = useState<string | null>('settings');
  const [processingConfigs, setProcessingConfigs] = useState<ProcessingConfig[]>([]);
  const [processingQueue, setProcessingQueue] = useState<ProcessingQueue[]>([]);
  const [stats, setStats] = useState<ProcessingStats | null>(null);

  useEffect(() => {
    fetchProcessingData();
    const interval = setInterval(fetchProcessingData, 5000); // Refresh queue every 5 seconds
    return () => clearInterval(interval);
  }, []);

  const fetchProcessingData = async () => {
    try {
      // Mock data for development
      const mockConfigs: ProcessingConfig[] = [
        {
          id: 'noise-reduction',
          name: 'Noise Reduction',
          category: 'general',
          enabled: true,
          settings: {
            algorithm: 'spectral_subtraction',
            threshold: 0.3,
            sensitivity: 0.8,
            preserveVoice: true,
          },
        },
        {
          id: 'voice-enhancement',
          name: 'Voice Enhancement',
          category: 'general',
          enabled: true,
          settings: {
            clarity: 0.7,
            warmth: 0.5,
            bassBoost: 0.2,
            trebleBoost: 0.3,
          },
        },
        {
          id: 'auto-transcription',
          name: 'Automatic Transcription',
          category: 'transcription',
          enabled: true,
          settings: {
            language: 'auto',
            punctuation: true,
            profanityFilter: false,
            speakerDiarization: true,
            maxSpeakers: 5,
            confidence: 0.8,
          },
        },
        {
          id: 'tts-preprocessing',
          name: 'TTS Preprocessing',
          category: 'synthesis',
          enabled: true,
          settings: {
            normalizeText: true,
            expandAbbreviations: true,
            handleNumbers: 'spoken',
            emotionDetection: true,
            ssmlSupport: true,
          },
        },
        {
          id: 'format-conversion',
          name: 'Format Conversion',
          category: 'general',
          enabled: true,
          settings: {
            outputFormat: 'mp3',
            bitrate: 192,
            sampleRate: 44100,
            channels: 'stereo',
            compression: 0.8,
          },
        },
      ];

      const mockQueue: ProcessingQueue[] = [
        {
          id: 'job-1',
          type: 'transcription',
          status: 'processing',
          inputFile: 'meeting-recording.mp3',
          duration: 3600,
          progress: 65,
          provider: 'OpenAI',
          model: 'whisper-1',
          createdAt: '2024-01-10T12:20:00Z',
          startedAt: '2024-01-10T12:21:00Z',
        },
        {
          id: 'job-2',
          type: 'synthesis',
          status: 'processing',
          outputFile: 'announcement-audio.mp3',
          progress: 45,
          provider: 'ElevenLabs',
          model: 'eleven-multilingual-v2',
          createdAt: '2024-01-10T12:25:00Z',
          startedAt: '2024-01-10T12:26:00Z',
        },
        {
          id: 'job-3',
          type: 'transcription',
          status: 'pending',
          inputFile: 'podcast-episode.wav',
          duration: 5400,
          progress: 0,
          provider: 'Azure',
          model: 'speech-to-text',
          createdAt: '2024-01-10T12:28:00Z',
        },
        {
          id: 'job-4',
          type: 'synthesis',
          status: 'completed',
          outputFile: 'welcome-message.mp3',
          duration: 15,
          progress: 100,
          provider: 'OpenAI',
          model: 'tts-1-hd',
          createdAt: '2024-01-10T12:15:00Z',
          startedAt: '2024-01-10T12:15:30Z',
          completedAt: '2024-01-10T12:16:00Z',
        },
        {
          id: 'job-5',
          type: 'transcription',
          status: 'failed',
          inputFile: 'corrupted-audio.mp3',
          progress: 0,
          provider: 'Google',
          model: 'speech-to-text',
          createdAt: '2024-01-10T12:10:00Z',
          startedAt: '2024-01-10T12:10:30Z',
          error: 'Invalid audio format',
        },
      ];

      const mockStats: ProcessingStats = {
        totalProcessed: 1234,
        totalDuration: 432000, // seconds
        avgProcessingTime: 45, // seconds
        successRate: 96.5,
        queueLength: mockQueue.filter(j => j.status === 'pending').length,
        activeJobs: mockQueue.filter(j => j.status === 'processing').length,
      };

      setProcessingConfigs(mockConfigs);
      setProcessingQueue(mockQueue);
      setStats(mockStats);
    } catch (error) {
      console.error('Error fetching processing data:', error);
      notifications.show({
        title: 'Error',
        message: 'Failed to load processing configuration',
        color: 'red',
      });
    } finally {
      setIsLoading(false);
    }
  };

  const handleRefresh = async () => {
    setIsRefreshing(true);
    await fetchProcessingData();
    setIsRefreshing(false);
    notifications.show({
      title: 'Refreshed',
      message: 'Processing data updated',
      color: 'green',
    });
  };

  const handleConfigUpdate = async (configId: string, updates: Partial<ProcessingConfig>) => {
    try {
      // In production, this would call the API
      setProcessingConfigs(prev => 
        prev.map(config => 
          config.id === configId ? { ...config, ...updates } : config
        )
      );
      
      notifications.show({
        title: 'Updated',
        message: 'Processing configuration updated',
        color: 'green',
      });
    } catch (error) {
      notifications.show({
        title: 'Error',
        message: 'Failed to update configuration',
        color: 'red',
      });
    }
  };

  const handleCancelJob = async (jobId: string) => {
    try {
      // In production, this would call the API
      setProcessingQueue(prev => prev.filter(job => job.id !== jobId));
      
      notifications.show({
        title: 'Cancelled',
        message: 'Processing job cancelled',
        color: 'green',
      });
    } catch (error) {
      notifications.show({
        title: 'Error',
        message: 'Failed to cancel job',
        color: 'red',
      });
    }
  };

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'completed': return 'green';
      case 'processing': return 'blue';
      case 'pending': return 'gray';
      case 'failed': return 'red';
      default: return 'gray';
    }
  };

  const getStatusIcon = (status: string) => {
    switch (status) {
      case 'completed': return <IconCircleCheck size={16} />;
      case 'processing': return <IconActivity size={16} />;
      case 'pending': return <IconClock size={16} />;
      case 'failed': return <IconAlertCircle size={16} />;
      default: return null;
    }
  };

  const supportedFormats: AudioFormat[] = [
    { id: 'mp3', name: 'MP3', mimeType: 'audio/mpeg', extension: 'mp3', supported: true, bitrate: 192, sampleRate: 44100, channels: 2 },
    { id: 'wav', name: 'WAV', mimeType: 'audio/wav', extension: 'wav', supported: true, bitrate: 1411, sampleRate: 44100, channels: 2 },
    { id: 'flac', name: 'FLAC', mimeType: 'audio/flac', extension: 'flac', supported: true, bitrate: 900, sampleRate: 48000, channels: 2 },
    { id: 'ogg', name: 'OGG Vorbis', mimeType: 'audio/ogg', extension: 'ogg', supported: true, bitrate: 160, sampleRate: 44100, channels: 2 },
    { id: 'm4a', name: 'M4A/AAC', mimeType: 'audio/mp4', extension: 'm4a', supported: true, bitrate: 256, sampleRate: 44100, channels: 2 },
    { id: 'webm', name: 'WebM', mimeType: 'audio/webm', extension: 'webm', supported: true, bitrate: 128, sampleRate: 48000, channels: 2 },
  ];

  if (isLoading) {
    return (
      <Stack>
        <Card shadow="sm" p="md" radius="md" pos="relative" mih={200}>
          <LoadingOverlay visible={true} />
        </Card>
      </Stack>
    );
  }

  return (
    <Stack gap="xl">
      <Card shadow="sm" p="md" radius="md">
        <Group justify="space-between" align="center">
          <div>
            <Title order={2}>Audio Processing</Title>
            <Text size="sm" c="dimmed" mt={4}>
              Configure audio processing pipelines and monitor jobs
            </Text>
          </div>
          <Button
            variant="light"
            leftSection={<IconRefresh size={16} />}
            onClick={handleRefresh}
            loading={isRefreshing}
          >
            Refresh
          </Button>
        </Group>
      </Card>

      <SimpleGrid cols={{ base: 1, sm: 2, lg: 4 }} spacing="md">
        <Card padding="lg" radius="md" withBorder>
          <Group justify="space-between">
            <div>
              <Text size="sm" c="dimmed" fw={600} tt="uppercase">
                Total Processed
              </Text>
              <Text size="xl" fw={700} mt={4}>
                {stats?.totalProcessed.toLocaleString() || 0}
              </Text>
              <Text size="xs" c="dimmed" mt={4}>
                Audio files
              </Text>
            </div>
            <ThemeIcon color="blue" variant="light" size={48} radius="md">
              <IconFileMusic size={24} />
            </ThemeIcon>
          </Group>
        </Card>

        <Card padding="lg" radius="md" withBorder>
          <Group justify="space-between">
            <div>
              <Text size="sm" c="dimmed" fw={600} tt="uppercase">
                Processing Time
              </Text>
              <Text size="xl" fw={700} mt={4}>
                {stats?.avgProcessingTime || 0}s
              </Text>
              <Text size="xs" c="dimmed" mt={4}>
                Average per file
              </Text>
            </div>
            <ThemeIcon color="green" variant="light" size={48} radius="md">
              <IconBolt size={24} />
            </ThemeIcon>
          </Group>
        </Card>

        <Card padding="lg" radius="md" withBorder>
          <Group justify="space-between">
            <div>
              <Text size="sm" c="dimmed" fw={600} tt="uppercase">
                Success Rate
              </Text>
              <Text size="xl" fw={700} mt={4}>
                {stats?.successRate || 0}%
              </Text>
              <Text size="xs" c="dimmed" mt={4}>
                Last 7 days
              </Text>
            </div>
            <ThemeIcon color="teal" variant="light" size={48} radius="md">
              <IconCircleCheck size={24} />
            </ThemeIcon>
          </Group>
        </Card>

        <Card padding="lg" radius="md" withBorder>
          <Group justify="space-between">
            <div>
              <Text size="sm" c="dimmed" fw={600} tt="uppercase">
                Active Jobs
              </Text>
              <Text size="xl" fw={700} mt={4}>
                {stats?.activeJobs || 0} / {(stats?.activeJobs || 0) + (stats?.queueLength || 0)}
              </Text>
              <Text size="xs" c="dimmed" mt={4}>
                Processing / Total
              </Text>
            </div>
            <ThemeIcon color="orange" variant="light" size={48} radius="md">
              <IconActivity size={24} />
            </ThemeIcon>
          </Group>
        </Card>
      </SimpleGrid>

      <Tabs value={activeTab} onChange={setActiveTab}>
        <Tabs.List>
          <Tabs.Tab value="settings" leftSection={<IconSettings size={16} />}>
            Processing Settings
          </Tabs.Tab>
          <Tabs.Tab value="queue" leftSection={<IconActivity size={16} />}>
            Processing Queue
          </Tabs.Tab>
          <Tabs.Tab value="formats" leftSection={<IconWaveSine size={16} />}>
            Audio Formats
          </Tabs.Tab>
        </Tabs.List>

        <Tabs.Panel value="settings" pt="md">
          <Stack gap="md">
            {processingConfigs.map((config) => (
              <Card key={config.id} shadow="sm" p="md" radius="md" withBorder>
                <Group justify="space-between" mb="md">
                  <Group>
                    <Text fw={600}>{config.name}</Text>
                    <Badge color="blue" variant="light">
                      {config.category}
                    </Badge>
                  </Group>
                  <Switch
                    checked={config.enabled}
                    onChange={(e) => handleConfigUpdate(config.id, { enabled: e.currentTarget.checked })}
                  />
                </Group>

                {config.id === 'noise-reduction' && (
                  <Stack gap="sm">
                    <Select
                      label="Algorithm"
                      value={config.settings.algorithm}
                      onChange={(value) => handleConfigUpdate(config.id, { 
                        settings: { ...config.settings, algorithm: value } 
                      })}
                      data={[
                        { value: 'spectral_subtraction', label: 'Spectral Subtraction' },
                        { value: 'wiener_filter', label: 'Wiener Filter' },
                        { value: 'ai_enhanced', label: 'AI Enhanced' },
                      ]}
                      size="sm"
                    />
                    <div>
                      <Text size="sm" mb={4}>Noise Threshold</Text>
                      <Slider
                        value={config.settings.threshold}
                        onChange={(value) => handleConfigUpdate(config.id, { 
                          settings: { ...config.settings, threshold: value } 
                        })}
                        min={0}
                        max={1}
                        step={0.1}
                        marks={[
                          { value: 0, label: 'Low' },
                          { value: 0.5, label: 'Medium' },
                          { value: 1, label: 'High' },
                        ]}
                      />
                    </div>
                    <Switch
                      label="Preserve Voice"
                      checked={config.settings.preserveVoice}
                      onChange={(e) => handleConfigUpdate(config.id, { 
                        settings: { ...config.settings, preserveVoice: e.currentTarget.checked } 
                      })}
                      size="sm"
                    />
                  </Stack>
                )}

                {config.id === 'auto-transcription' && (
                  <Stack gap="sm">
                    <Select
                      label="Language Detection"
                      value={config.settings.language}
                      onChange={(value) => handleConfigUpdate(config.id, { 
                        settings: { ...config.settings, language: value } 
                      })}
                      data={[
                        { value: 'auto', label: 'Auto-detect' },
                        { value: 'en', label: 'English' },
                        { value: 'es', label: 'Spanish' },
                        { value: 'fr', label: 'French' },
                        { value: 'de', label: 'German' },
                        { value: 'zh', label: 'Chinese' },
                        { value: 'ja', label: 'Japanese' },
                      ]}
                      size="sm"
                    />
                    <Group grow>
                      <Switch
                        label="Punctuation"
                        checked={config.settings.punctuation}
                        onChange={(e) => handleConfigUpdate(config.id, { 
                          settings: { ...config.settings, punctuation: e.currentTarget.checked } 
                        })}
                        size="sm"
                      />
                      <Switch
                        label="Speaker Diarization"
                        checked={config.settings.speakerDiarization}
                        onChange={(e) => handleConfigUpdate(config.id, { 
                          settings: { ...config.settings, speakerDiarization: e.currentTarget.checked } 
                        })}
                        size="sm"
                      />
                    </Group>
                    <NumberInput
                      label="Max Speakers"
                      value={config.settings.maxSpeakers}
                      onChange={(value) => handleConfigUpdate(config.id, { 
                        settings: { ...config.settings, maxSpeakers: value } 
                      })}
                      min={1}
                      max={10}
                      size="sm"
                    />
                  </Stack>
                )}

                {config.id === 'format-conversion' && (
                  <SimpleGrid cols={2} spacing="sm">
                    <Select
                      label="Output Format"
                      value={config.settings.outputFormat}
                      onChange={(value) => handleConfigUpdate(config.id, { 
                        settings: { ...config.settings, outputFormat: value } 
                      })}
                      data={[
                        { value: 'mp3', label: 'MP3' },
                        { value: 'wav', label: 'WAV' },
                        { value: 'flac', label: 'FLAC' },
                        { value: 'ogg', label: 'OGG' },
                        { value: 'm4a', label: 'M4A' },
                      ]}
                      size="sm"
                    />
                    <NumberInput
                      label="Bitrate (kbps)"
                      value={config.settings.bitrate}
                      onChange={(value) => handleConfigUpdate(config.id, { 
                        settings: { ...config.settings, bitrate: value } 
                      })}
                      min={64}
                      max={320}
                      step={32}
                      size="sm"
                    />
                    <Select
                      label="Sample Rate"
                      value={String(config.settings.sampleRate)}
                      onChange={(value) => handleConfigUpdate(config.id, { 
                        settings: { ...config.settings, sampleRate: Number(value) } 
                      })}
                      data={[
                        { value: '22050', label: '22.05 kHz' },
                        { value: '44100', label: '44.1 kHz' },
                        { value: '48000', label: '48 kHz' },
                        { value: '96000', label: '96 kHz' },
                      ]}
                      size="sm"
                    />
                    <Select
                      label="Channels"
                      value={config.settings.channels}
                      onChange={(value) => handleConfigUpdate(config.id, { 
                        settings: { ...config.settings, channels: value } 
                      })}
                      data={[
                        { value: 'mono', label: 'Mono' },
                        { value: 'stereo', label: 'Stereo' },
                      ]}
                      size="sm"
                    />
                  </SimpleGrid>
                )}
              </Card>
            ))}
          </Stack>
        </Tabs.Panel>

        <Tabs.Panel value="queue" pt="md">
          <Card shadow="sm" p="md" radius="md" withBorder>
            <ScrollArea>
              <Table>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>File</Table.Th>
                    <Table.Th>Type</Table.Th>
                    <Table.Th>Provider</Table.Th>
                    <Table.Th>Progress</Table.Th>
                    <Table.Th>Status</Table.Th>
                    <Table.Th>Duration</Table.Th>
                    <Table.Th>Actions</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {processingQueue.map((job) => (
                    <Table.Tr key={job.id}>
                      <Table.Td>
                        <Text size="sm" fw={500}>
                          {job.inputFile || job.outputFile || 'Unknown'}
                        </Text>
                      </Table.Td>
                      <Table.Td>
                        <Badge
                          color={job.type === 'transcription' ? 'blue' : 'green'}
                          variant="light"
                        >
                          {job.type === 'transcription' ? 'STT' : 'TTS'}
                        </Badge>
                      </Table.Td>
                      <Table.Td>
                        <Text size="sm">{job.provider} - {job.model}</Text>
                      </Table.Td>
                      <Table.Td>
                        <Group gap="xs">
                          <Progress
                            value={job.progress}
                            size="sm"
                            w={100}
                            color={getStatusColor(job.status)}
                          />
                          <Text size="xs">{job.progress}%</Text>
                        </Group>
                      </Table.Td>
                      <Table.Td>
                        <Badge
                          leftSection={getStatusIcon(job.status)}
                          color={getStatusColor(job.status)}
                          variant="light"
                        >
                          {job.status}
                        </Badge>
                      </Table.Td>
                      <Table.Td>
                        <Text size="sm">
                          {job.duration ? formatters.duration(job.duration * 1000) : '-'}
                        </Text>
                      </Table.Td>
                      <Table.Td>
                        {(job.status === 'pending' || job.status === 'processing') && (
                          <Tooltip label="Cancel job">
                            <ActionIcon
                              variant="light"
                              color="red"
                              onClick={() => handleCancelJob(job.id)}
                            >
                              <IconPlayerPause size={16} />
                            </ActionIcon>
                          </Tooltip>
                        )}
                      </Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </ScrollArea>
          </Card>
        </Tabs.Panel>

        <Tabs.Panel value="formats" pt="md">
          <Card shadow="sm" p="md" radius="md" withBorder>
            <Title order={4} mb="md">Supported Audio Formats</Title>
            <ScrollArea>
              <Table>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Format</Table.Th>
                    <Table.Th>MIME Type</Table.Th>
                    <Table.Th>Extension</Table.Th>
                    <Table.Th>Bitrate</Table.Th>
                    <Table.Th>Sample Rate</Table.Th>
                    <Table.Th>Channels</Table.Th>
                    <Table.Th>Support</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {supportedFormats.map((format) => (
                    <Table.Tr key={format.id}>
                      <Table.Td>
                        <Text fw={500}>{format.name}</Text>
                      </Table.Td>
                      <Table.Td>
                        <Code>{format.mimeType}</Code>
                      </Table.Td>
                      <Table.Td>
                        <Code>.{format.extension}</Code>
                      </Table.Td>
                      <Table.Td>{format.bitrate} kbps</Table.Td>
                      <Table.Td>{(format.sampleRate || 0) / 1000} kHz</Table.Td>
                      <Table.Td>{format.channels === 2 ? 'Stereo' : 'Mono'}</Table.Td>
                      <Table.Td>
                        <Badge
                          color={format.supported ? 'green' : 'gray'}
                          variant="light"
                        >
                          {format.supported ? 'Supported' : 'Not Supported'}
                        </Badge>
                      </Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </ScrollArea>

            <Divider my="md" />

            <Alert
              icon={<IconInfoCircle size={16} />}
              title="Audio Processing Information"
              color="blue"
            >
              <Text size="sm">
                All audio files are automatically normalized and optimized during processing. 
                Large files are chunked for efficient processing. Maximum file size: 1GB. 
                Maximum duration: 4 hours for transcription, 30 minutes for synthesis.
              </Text>
            </Alert>
          </Card>
        </Tabs.Panel>
      </Tabs>

      <LoadingOverlay visible={isRefreshing} />
    </Stack>
  );
}