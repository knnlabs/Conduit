'use client';

import { useEffect, useState } from 'react';
import { 
  Container, 
  Paper, 
  Stack, 
  Center,
  Loader,
  Alert,
  Group,
  Badge,
  Collapse,
  ActionIcon
} from '@mantine/core';
import { IconAlertCircle, IconSettings, IconChevronUp } from '@tabler/icons-react';
import { ModelSelector } from './ModelSelector';
import { ChatInput } from './ChatInput';
import { ChatMessages } from './ChatMessages';
import { ChatSettings } from './ChatSettings';
import { ErrorDisplay } from '@/components/common/ErrorDisplay';
import { 
  ChatMessage,
} from '../types';
import { usePerformanceSettings } from '../hooks/usePerformanceSettings';
import { useChatStore } from '../hooks/useChatStore';
import { useModels } from '../hooks/useModels';
import { useChatStreamingLogic } from './ChatStreamingLogic';
import Link from 'next/link';

export function ChatInterface() {
  const { data: modelData, isLoading: modelsLoading } = useModels();
  const [selectedModel, setSelectedModel] = useState<string | null>(null);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<Error | null>(null);
  const [streamingContent, setStreamingContent] = useState('');
  const [tokensPerSecond, setTokensPerSecond] = useState<number | null>(null);
  const [showSettings, setShowSettings] = useState(false);
  
  const performanceSettings = usePerformanceSettings();
  const { 
    getActiveSession, 
    createSession,
    activeSessionId 
  } = useChatStore();

  // Use streaming logic hook
  const { sendMessage, abortControllerRef } = useChatStreamingLogic({
    selectedModel,
    messages,
    setMessages,
    isLoading,
    setIsLoading,
    setStreamingContent,
    setTokensPerSecond,
    setError,
    getActiveSession,
    performanceSettings,
  });

  // Set initial model when data loads
  useEffect(() => {
    if (modelData && modelData.length > 0 && !selectedModel) {
      setSelectedModel(modelData[0].id);
    }
  }, [modelData, selectedModel]);

  // Ensure we have an active session
  useEffect(() => {
    if (selectedModel && !activeSessionId) {
      createSession(selectedModel);
    }
  }, [selectedModel, activeSessionId, createSession]);

  // Cleanup on unmount - abort any pending requests
  useEffect(() => {
    return () => {
      if (abortControllerRef.current) {
        abortControllerRef.current.abort();
        abortControllerRef.current = null;
      }
    };
  }, [abortControllerRef]);


  const currentModel = modelData?.find(m => m.id === selectedModel);

  if (modelsLoading) {
    return (
      <Center h="100vh">
        <Loader size="lg" />
      </Center>
    );
  }

  if (error) {
    return (
      <Container size="sm" mt="xl">
        <ErrorDisplay 
          error={error}
          variant="card"
          showDetails={true}
          onRetry={() => {
            setError(null);
            setIsLoading(false);
          }}
          actions={[
            {
              label: 'Configure Providers',
              onClick: () => window.location.href = '/llm-providers',
              color: 'blue',
              variant: 'light',
            }
          ]}
        />
      </Container>
    );
  }

  if (!modelData || modelData.length === 0) {
    return (
      <Container size="sm" mt="xl">
        <Alert icon={<IconAlertCircle size={16} />} color="yellow" title="No models available">
          No models are currently configured. Please add model mappings first.<br />
          <Link href="/model-mappings">Add model mappings</Link>
        </Alert>
      </Container>
    );
  }

  return (
    <Container size="lg" py="md">
      <Stack h="calc(100vh - 100px)">
        <Paper p="md" withBorder>
          <Stack gap="md">
            <Group justify="space-between">
              <Group style={{ flex: 1 }}>
                <ModelSelector
                  value={selectedModel}
                  onChange={setSelectedModel}
                  modelData={modelData}
                  style={{ flex: 1, maxWidth: 400 }}
                />
                {currentModel?.supportsVision && (
                  <Badge variant="light" color="blue">
                    Vision Enabled
                  </Badge>
                )}
              </Group>
              <ActionIcon
                size="lg"
                variant="light"
                onClick={() => setShowSettings(!showSettings)}
                aria-label="Toggle advanced settings"
              >
                {showSettings ? <IconChevronUp size={20} /> : <IconSettings size={20} />}
              </ActionIcon>
            </Group>
            
            <Collapse in={showSettings}>
              <ChatSettings />
            </Collapse>
          </Stack>
        </Paper>

        <Paper p="md" withBorder style={{ flex: 1, display: 'flex', flexDirection: 'column', minHeight: 0 }}>
          <ChatMessages 
            messages={messages}
            streamingContent={isLoading ? streamingContent : undefined}
            tokensPerSecond={performanceSettings.showTokensPerSecond ? tokensPerSecond : null}
          />
        </Paper>

        <Paper p="md" withBorder>
          <ChatInput
            onSendMessage={(message, images) => void sendMessage(message, images)}
            isStreaming={isLoading}
            onStopStreaming={() => {}}
            disabled={!selectedModel}
            model={currentModel ? {
              id: currentModel.id,
              providerId: currentModel.providerId || '',
              displayName: currentModel.displayName,
              supportsVision: currentModel.supportsVision
            } : undefined}
          />
        </Paper>
      </Stack>
    </Container>
  );
}