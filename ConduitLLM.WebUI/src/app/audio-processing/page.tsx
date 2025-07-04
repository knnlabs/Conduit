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
  Alert,
  FileInput,
  ScrollArea,
  Tabs,
  NumberInput,
} from '@mantine/core';
import {
  IconMicrophone,
  IconSettings,
  IconDownload,
  IconTrash,
  IconUpload,
  IconVolume,
  IconFileText,
  IconCopy,
  IconAlertCircle,
  IconPlayerPlay,
  IconPlayerPause,
} from '@tabler/icons-react';
import { useState, useRef } from 'react';
import { useDisclosure } from '@mantine/hooks';
import { useAudioTranscription, useAudioSpeech, useAvailableModels } from '@/hooks/api/useCoreApi';
import { useVirtualKeys } from '@/hooks/api/useAdminApi';
import { notifications } from '@mantine/notifications';
import { safeLog } from '@/lib/utils/logging';
import TaskProgressPanel from '@/components/realtime/TaskProgressPanel';

interface TranscriptionResult {
  id: string;
  fileName: string;
  text: string;
  model: string;
  language?: string;
  duration?: number;
  createdAt: Date;
}

interface SpeechResult {
  id: string;
  text: string;
  audioUrl: string;
  model: string;
  voice: string;
  format: string;
  speed: number;
  createdAt: Date;
}

export default function AudioProcessingPage() {
  const [activeTab, setActiveTab] = useState<string | null>('transcription');
  const [selectedVirtualKey, setSelectedVirtualKey] = useState('');
  
  // Transcription state
  const [audioFile, setAudioFile] = useState<File | null>(null);
  const [transcriptionModel, setTranscriptionModel] = useState('whisper-1');
  const [language, setLanguage] = useState('');
  const [responseFormat, setResponseFormat] = useState('json');
  const [temperature, setTemperature] = useState(0);
  const [transcriptionResults, setTranscriptionResults] = useState<TranscriptionResult[]>([]);
  
  // Speech state
  const [speechText, setSpeechText] = useState('');
  const [speechModel, setSpeechModel] = useState('tts-1');
  const [voice, setVoice] = useState('alloy');
  const [speechFormat, setSpeechFormat] = useState('mp3');
  const [speed, setSpeed] = useState(1.0);
  const [speechResults, setSpeechResults] = useState<SpeechResult[]>([]);
  
  const [settingsOpened, { open: openSettings, close: closeSettings }] = useDisclosure(false);
  const [isPlaying, setIsPlaying] = useState<string | null>(null);
  const audioPlayerRef = useRef<HTMLAudioElement>(null);

  const { data: virtualKeys, isLoading: keysLoading } = useVirtualKeys();
  const { data: models, isLoading: modelsLoading } = useAvailableModels();
  const transcription = useAudioTranscription();
  const speech = useAudioSpeech();

  // Filter models for audio processing
  const audioModels = models?.filter((model: unknown) => 
    typeof model === 'object' && model !== null && 'id' in model &&
    typeof (model as { id: string }).id === 'string' && (
      (model as { id: string }).id.includes('whisper') || 
      (model as { id: string }).id.includes('tts') ||
      (model as { id: string }).id.includes('audio') ||
      (model as { id: string }).id.includes('speech')
    )
  ) || [];

  const handleTranscribeAudio = async () => {
    if (!audioFile || !selectedVirtualKey) {
      notifications.show({
        title: 'File Required',
        message: 'Please select an audio file and virtual key',
        color: 'orange',
      });
      return;
    }

    try {
      const request = {
        virtualKey: selectedVirtualKey,
        file: audioFile,
        model: transcriptionModel,
        language: language || undefined,
        response_format: responseFormat as 'json' | 'text' | 'srt' | 'vtt' | 'verbose_json',
        temperature: temperature || undefined,
      };

      const response = await transcription.mutateAsync(request);

      const result: TranscriptionResult = {
        id: `trans_${Date.now()}`,
        fileName: audioFile.name,
        text: typeof response === 'string' ? response : String((response as Record<string, unknown>).text || 'No transcription generated'),
        model: transcriptionModel,
        language: language || undefined,
        createdAt: new Date(),
      };

      setTranscriptionResults(prev => [result, ...prev]);
      setAudioFile(null);

      safeLog('Audio transcription successful', {
        model: transcriptionModel,
        fileName: audioFile.name,
        textLength: result.text.length,
      });
    } catch (error: unknown) {
      safeLog('Audio transcription failed', { error: error instanceof Error ? error.message : String(error) });
    }
  };

  const handleTaskCompleted = (task: unknown) => {
    if (typeof task !== 'object' || task === null) return;
    const taskObj = task as { type?: string; status?: string; result?: unknown; taskId?: string; startedAt?: string };
    if (taskObj.type === 'audio_transcription' && taskObj.status === 'completed' && taskObj.result) {
      const resultObj = taskObj.result as { fileName?: string; text?: string; model?: string; language?: string };
      const result: TranscriptionResult = {
        id: taskObj.taskId || '',
        fileName: resultObj.fileName || 'Unknown file',
        text: resultObj.text || 'No transcription generated',
        model: resultObj.model || transcriptionModel,
        language: resultObj.language,
        createdAt: new Date(taskObj.startedAt || Date.now()),
      };
      
      setTranscriptionResults(prev => [result, ...prev]);
    } else if (taskObj.type === 'audio_speech' && taskObj.status === 'completed' && taskObj.result) {
      const resultObj = taskObj.result as { text?: string; audioUrl?: string; url?: string; model?: string; voice?: string; format?: string; speed?: number };
      const result: SpeechResult = {
        id: taskObj.taskId || '',
        text: resultObj.text || 'Generated speech',
        audioUrl: resultObj.audioUrl || resultObj.url || '',
        model: resultObj.model || speechModel,
        voice: resultObj.voice || voice,
        format: resultObj.format || speechFormat,
        speed: resultObj.speed || speed,
        createdAt: new Date(taskObj.startedAt || Date.now()),
      };
      
      setSpeechResults(prev => [result, ...prev]);
    }
  };

  const handleTextToSpeech = async () => {
    if (!speechText.trim() || !selectedVirtualKey) {
      notifications.show({
        title: 'Text Required',
        message: 'Please enter text and select a virtual key',
        color: 'orange',
      });
      return;
    }

    try {
      const request = {
        virtualKey: selectedVirtualKey,
        model: speechModel,
        input: speechText.trim(),
        voice,
        response_format: speechFormat as 'mp3' | 'opus' | 'aac' | 'flac',
        speed,
      };

      const response = await speech.mutateAsync(request);

      // Convert response to audio URL
      const audioUrl = URL.createObjectURL(response);

      const result: SpeechResult = {
        id: `speech_${Date.now()}`,
        text: speechText.trim(),
        audioUrl,
        model: speechModel,
        voice,
        format: speechFormat,
        speed,
        createdAt: new Date(),
      };

      setSpeechResults(prev => [result, ...prev]);
      setSpeechText('');

      safeLog('Text-to-speech successful', {
        model: speechModel,
        voice,
        textLength: speechText.trim().length,
      });
    } catch (error: unknown) {
      safeLog('Text-to-speech failed', { error: error instanceof Error ? error.message : String(error) });
    }
  };

  const handlePlayAudio = (audioUrl: string, id: string) => {
    if (audioPlayerRef.current) {
      if (isPlaying === id) {
        audioPlayerRef.current.pause();
        setIsPlaying(null);
      } else {
        audioPlayerRef.current.src = audioUrl;
        audioPlayerRef.current.play();
        setIsPlaying(id);
        
        audioPlayerRef.current.onended = () => {
          setIsPlaying(null);
        };
      }
    }
  };

  const handleDownloadAudio = async (audioUrl: string, fileName: string) => {
    try {
      const response = await fetch(audioUrl);
      const blob = await response.blob();
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = fileName;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);

      notifications.show({
        title: 'Downloaded',
        message: 'Audio file downloaded successfully',
        color: 'green',
      });
    } catch (_error) {
      notifications.show({
        title: 'Download Failed',
        message: 'Failed to download audio file',
        color: 'red',
      });
    }
  };

  const copyToClipboard = async (text: string) => {
    try {
      await navigator.clipboard.writeText(text);
      notifications.show({
        title: 'Copied',
        message: 'Text copied to clipboard',
        color: 'green',
      });
    } catch (_error) {
      notifications.show({
        title: 'Copy Failed',
        message: 'Failed to copy text',
        color: 'red',
      });
    }
  };

  const handleDeleteResult = (id: string, type: 'transcription' | 'speech') => {
    if (type === 'transcription') {
      setTranscriptionResults(prev => prev.filter(result => result.id !== id));
    } else {
      setSpeechResults(prev => prev.filter(result => result.id !== id));
    }
    notifications.show({
      title: 'Deleted',
      message: 'Result removed successfully',
      color: 'blue',
    });
  };

  const getLanguageOptions = () => [
    { value: '', label: 'Auto-detect' },
    { value: 'en', label: 'English' },
    { value: 'es', label: 'Spanish' },
    { value: 'fr', label: 'French' },
    { value: 'de', label: 'German' },
    { value: 'it', label: 'Italian' },
    { value: 'pt', label: 'Portuguese' },
    { value: 'ru', label: 'Russian' },
    { value: 'ja', label: 'Japanese' },
    { value: 'ko', label: 'Korean' },
    { value: 'zh', label: 'Chinese' },
  ];

  const getVoiceOptions = () => [
    { value: 'alloy', label: 'Alloy' },
    { value: 'echo', label: 'Echo' },
    { value: 'fable', label: 'Fable' },
    { value: 'onyx', label: 'Onyx' },
    { value: 'nova', label: 'Nova' },
    { value: 'shimmer', label: 'Shimmer' },
  ];

  return (
    <Stack gap="md">
      <audio ref={audioPlayerRef} style={{ display: 'none' }} />
      
      <Group justify="space-between">
        <div>
          <Title order={1}>Audio Processing</Title>
          <Text c="dimmed">Transcribe audio to text and convert text to speech</Text>
        </div>

        <Group>
          <Button
            variant="light"
            leftSection={<IconSettings size={16} />}
            onClick={openSettings}
          >
            Settings
          </Button>
        </Group>
      </Group>

      <Tabs value={activeTab} onChange={setActiveTab}>
        <Tabs.List>
          <Tabs.Tab value="transcription" leftSection={<IconFileText size={16} />}>
            Speech to Text
          </Tabs.Tab>
          <Tabs.Tab value="speech" leftSection={<IconVolume size={16} />}>
            Text to Speech
          </Tabs.Tab>
        </Tabs.List>

        {/* Speech to Text Tab */}
        <Tabs.Panel value="transcription">
          {/* Real-time Task Progress */}
          {selectedVirtualKey && (
            <div style={{ marginTop: '1rem', marginBottom: '1rem' }}>
              <TaskProgressPanel 
                virtualKey={selectedVirtualKey}
                taskType="audio_transcription"
                onTaskCompleted={handleTaskCompleted}
                maxHeight={150}
              />
            </div>
          )}
          
          <Grid mt="md">
            <Grid.Col span={{ base: 12, md: 6 }}>
              <Card withBorder h="fit-content">
                <Stack gap="md">
                  <Group justify="space-between">
                    <Text fw={600} size="lg">Upload Audio</Text>
                    <Badge variant="light">Transcription</Badge>
                  </Group>

                  {!selectedVirtualKey && (
                    <Alert icon={<IconAlertCircle size={16} />} color="orange">
                      Please select a virtual key in settings to start transcribing.
                    </Alert>
                  )}

                  <FileInput
                    label="Audio File"
                    placeholder="Select audio file"
                    value={audioFile}
                    onChange={setAudioFile}
                    accept="audio/*"
                    leftSection={<IconUpload size={16} />}
                  />

                  <Group grow>
                    <Select
                      label="Model"
                      data={audioModels
                        .filter((model: unknown) => 
                          typeof model === 'object' && model !== null && 'id' in model &&
                          typeof (model as { id: string }).id === 'string' &&
                          (model as { id: string }).id.includes('whisper')
                        )
                        .map((model: unknown) => ({
                          value: (model as { id: string }).id,
                          label: (model as { id: string }).id,
                        }))}
                      value={transcriptionModel}
                      onChange={(value) => setTranscriptionModel(value || 'whisper-1')}
                      disabled={!selectedVirtualKey || modelsLoading}
                    />

                    <Select
                      label="Language"
                      data={getLanguageOptions()}
                      value={language}
                      onChange={(value) => setLanguage(value || '')}
                    />
                  </Group>

                  <Group grow>
                    <Select
                      label="Response Format"
                      data={[
                        { value: 'json', label: 'JSON' },
                        { value: 'text', label: 'Text' },
                        { value: 'srt', label: 'SRT' },
                        { value: 'vtt', label: 'VTT' },
                        { value: 'verbose_json', label: 'Verbose JSON' },
                      ]}
                      value={responseFormat}
                      onChange={(value) => setResponseFormat(value || 'json')}
                    />

                    <NumberInput
                      label="Temperature"
                      description="0 = deterministic, 1 = creative"
                      value={temperature}
                      onChange={(value) => setTemperature(value as number)}
                      min={0}
                      max={1}
                      step={0.1}
                      decimalScale={1}
                    />
                  </Group>

                  <Button
                    leftSection={<IconMicrophone size={16} />}
                    onClick={handleTranscribeAudio}
                    disabled={!audioFile || !selectedVirtualKey}
                    loading={transcription.isPending}
                    size="lg"
                  >
                    Transcribe Audio
                  </Button>
                </Stack>
              </Card>
            </Grid.Col>

            <Grid.Col span={{ base: 12, md: 6 }}>
              <Card withBorder h="600px">
                <Stack gap="md" h="100%">
                  <Group justify="space-between">
                    <Text fw={600} size="lg">Transcription Results</Text>
                    <Badge variant="light">{transcriptionResults.length} results</Badge>
                  </Group>

                  <ScrollArea flex={1}>
                    {transcriptionResults.length === 0 ? (
                      <Alert icon={<IconMicrophone size={16} />} variant="light">
                        <Text>No transcriptions yet. Upload an audio file to get started!</Text>
                      </Alert>
                    ) : (
                      <Stack gap="md">
                        {transcriptionResults.map((result) => (
                          <Card key={result.id} withBorder p="md">
                            <Stack gap="sm">
                              <Group justify="space-between">
                                <Text fw={500} size="sm" truncate>
                                  {result.fileName}
                                </Text>
                                <Group gap="xs">
                                  <Tooltip label="Copy text">
                                    <ActionIcon
                                      size="sm"
                                      variant="light"
                                      onClick={() => copyToClipboard(result.text)}
                                    >
                                      <IconCopy size={14} />
                                    </ActionIcon>
                                  </Tooltip>
                                  <Tooltip label="Delete">
                                    <ActionIcon
                                      size="sm"
                                      variant="light"
                                      color="red"
                                      onClick={() => handleDeleteResult(result.id, 'transcription')}
                                    >
                                      <IconTrash size={14} />
                                    </ActionIcon>
                                  </Tooltip>
                                </Group>
                              </Group>

                              <Text size="sm" style={{ whiteSpace: 'pre-wrap' }}>
                                {result.text}
                              </Text>

                              <Group gap="xs">
                                <Badge size="xs" variant="light">
                                  {result.model}
                                </Badge>
                                {result.language && (
                                  <Badge size="xs" variant="light">
                                    {result.language}
                                  </Badge>
                                )}
                                <Text size="xs" c="dimmed">
                                  {result.createdAt.toLocaleString()}
                                </Text>
                              </Group>
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
        </Tabs.Panel>

        {/* Text to Speech Tab */}
        <Tabs.Panel value="speech">
          {/* Real-time Task Progress */}
          {selectedVirtualKey && (
            <div style={{ marginTop: '1rem', marginBottom: '1rem' }}>
              <TaskProgressPanel 
                virtualKey={selectedVirtualKey}
                taskType="audio_speech"
                onTaskCompleted={handleTaskCompleted}
                maxHeight={150}
              />
            </div>
          )}
          
          <Grid mt="md">
            <Grid.Col span={{ base: 12, md: 6 }}>
              <Card withBorder h="fit-content">
                <Stack gap="md">
                  <Group justify="space-between">
                    <Text fw={600} size="lg">Generate Speech</Text>
                    <Badge variant="light">Text-to-Speech</Badge>
                  </Group>

                  {!selectedVirtualKey && (
                    <Alert icon={<IconAlertCircle size={16} />} color="orange">
                      Please select a virtual key in settings to start generating speech.
                    </Alert>
                  )}

                  <Textarea
                    label="Text"
                    placeholder="Enter text to convert to speech..."
                    value={speechText}
                    onChange={(event) => setSpeechText(event.currentTarget.value)}
                    minRows={3}
                    maxRows={6}
                    required
                  />

                  <Group grow>
                    <Select
                      label="Model"
                      data={audioModels
                        .filter((model: unknown) => 
                          typeof model === 'object' && model !== null && 'id' in model &&
                          typeof (model as { id: string }).id === 'string' &&
                          (model as { id: string }).id.includes('tts')
                        )
                        .map((model: unknown) => ({
                          value: (model as { id: string }).id,
                          label: (model as { id: string }).id,
                        }))}
                      value={speechModel}
                      onChange={(value) => setSpeechModel(value || 'tts-1')}
                      disabled={!selectedVirtualKey || modelsLoading}
                    />

                    <Select
                      label="Voice"
                      data={getVoiceOptions()}
                      value={voice}
                      onChange={(value) => setVoice(value || 'alloy')}
                    />
                  </Group>

                  <Group grow>
                    <Select
                      label="Format"
                      data={[
                        { value: 'mp3', label: 'MP3' },
                        { value: 'opus', label: 'Opus' },
                        { value: 'aac', label: 'AAC' },
                        { value: 'flac', label: 'FLAC' },
                      ]}
                      value={speechFormat}
                      onChange={(value) => setSpeechFormat(value || 'mp3')}
                    />

                    <NumberInput
                      label="Speed"
                      description="0.25 to 4.0"
                      value={speed}
                      onChange={(value) => setSpeed(value as number)}
                      min={0.25}
                      max={4.0}
                      step={0.25}
                      decimalScale={2}
                    />
                  </Group>

                  <Button
                    leftSection={<IconVolume size={16} />}
                    onClick={handleTextToSpeech}
                    disabled={!speechText.trim() || !selectedVirtualKey}
                    loading={speech.isPending}
                    size="lg"
                  >
                    Generate Speech
                  </Button>
                </Stack>
              </Card>
            </Grid.Col>

            <Grid.Col span={{ base: 12, md: 6 }}>
              <Card withBorder h="600px">
                <Stack gap="md" h="100%">
                  <Group justify="space-between">
                    <Text fw={600} size="lg">Generated Speech</Text>
                    <Badge variant="light">{speechResults.length} results</Badge>
                  </Group>

                  <ScrollArea flex={1}>
                    {speechResults.length === 0 ? (
                      <Alert icon={<IconVolume size={16} />} variant="light">
                        <Text>No speech generated yet. Enter text above to get started!</Text>
                      </Alert>
                    ) : (
                      <Stack gap="md">
                        {speechResults.map((result) => (
                          <Card key={result.id} withBorder p="md">
                            <Stack gap="sm">
                              <Group justify="space-between">
                                <Text fw={500} size="sm">
                                  Generated Audio
                                </Text>
                                <Group gap="xs">
                                  <Tooltip label={isPlaying === result.id ? "Pause" : "Play"}>
                                    <ActionIcon
                                      size="sm"
                                      variant="light"
                                      onClick={() => handlePlayAudio(result.audioUrl, result.id)}
                                    >
                                      {isPlaying === result.id ? (
                                        <IconPlayerPause size={14} />
                                      ) : (
                                        <IconPlayerPlay size={14} />
                                      )}
                                    </ActionIcon>
                                  </Tooltip>
                                  <Tooltip label="Download">
                                    <ActionIcon
                                      size="sm"
                                      variant="light"
                                      onClick={() => 
                                        handleDownloadAudio(
                                          result.audioUrl, 
                                          `speech-${result.id}.${result.format}`
                                        )
                                      }
                                    >
                                      <IconDownload size={14} />
                                    </ActionIcon>
                                  </Tooltip>
                                  <Tooltip label="Copy text">
                                    <ActionIcon
                                      size="sm"
                                      variant="light"
                                      onClick={() => copyToClipboard(result.text)}
                                    >
                                      <IconCopy size={14} />
                                    </ActionIcon>
                                  </Tooltip>
                                  <Tooltip label="Delete">
                                    <ActionIcon
                                      size="sm"
                                      variant="light"
                                      color="red"
                                      onClick={() => handleDeleteResult(result.id, 'speech')}
                                    >
                                      <IconTrash size={14} />
                                    </ActionIcon>
                                  </Tooltip>
                                </Group>
                              </Group>

                              <Text size="sm" lineClamp={3}>
                                {result.text}
                              </Text>

                              <Group gap="xs">
                                <Badge size="xs" variant="light">
                                  {result.model}
                                </Badge>
                                <Badge size="xs" variant="light">
                                  {result.voice}
                                </Badge>
                                <Badge size="xs" variant="light">
                                  {result.format.toUpperCase()}
                                </Badge>
                                <Badge size="xs" variant="light">
                                  {result.speed}x
                                </Badge>
                                <Text size="xs" c="dimmed">
                                  {result.createdAt.toLocaleString()}
                                </Text>
                              </Group>
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
        </Tabs.Panel>
      </Tabs>

      {/* Settings Modal */}
      <Modal
        opened={settingsOpened}
        onClose={closeSettings}
        title="Audio Processing Settings"
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
              Audio processing operations consume virtual key credits. Transcription costs vary by duration, 
              and speech generation costs vary by text length.
            </Text>
          </Alert>

          <Group justify="flex-end">
            <Button onClick={closeSettings}>
              Done
            </Button>
          </Group>
        </Stack>
      </Modal>
    </Stack>
  );
}