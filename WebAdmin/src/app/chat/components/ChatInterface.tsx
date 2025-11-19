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
import { TokenCounter } from './TokenCounter';
import { ErrorDisplay } from '@/components/common/ErrorDisplay';
import { 
  ChatMessage,
} from '../types';
import { usePerformanceSettings } from '../hooks/usePerformanceSettings';
import { useChatStore } from '../hooks/useChatStore';
import { useDiscoveryModels } from '../hooks/useDiscoveryModels';
import { ModelCapability } from '@knn_labs/conduit-core-client';
import { useChatStreamingLogic } from './ChatStreamingLogic';
import { DynamicParameters } from '@/components/parameters/DynamicParameters';
import { useParameterState } from '@/components/parameters/hooks/useParameterState';
import Link from 'next/link';
import { useAdminClient } from '@/lib/client/adminClient';
import type { FunctionConfigurationDto } from '@knn_labs/conduit-admin-client';

export function ChatInterface() {
  const { data: discoveryData, isLoading: modelsLoading } = useDiscoveryModels(ModelCapability.Chat); // Filter for chat-capable models only
  const [selectedModel, setSelectedModel] = useState<string | null>(null);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<Error | null>(null);
  const [streamingContent, setStreamingContent] = useState('');
  const [streamingChannel, setStreamingChannel] = useState<string | null>(null);
  const [tokensPerSecond, setTokensPerSecond] = useState<number | null>(null);
  const [showSettings, setShowSettings] = useState(false);
  const [showParameters] = useState(false);
  const [reasoningExpanded, setReasoningExpanded] = useState(true); // Default to expanded
  const [currentInputText, setCurrentInputText] = useState('');
  const [currentInputImages, setCurrentInputImages] = useState(0);
  const [sendHistoryEnabled, setSendHistoryEnabled] = useState(true); // Default to sending history
  const [selectedFunctionIds, setSelectedFunctionIds] = useState<number[]>([]);
  const [availableFunctions, setAvailableFunctions] = useState<FunctionConfigurationDto[]>([]);

  const performanceSettings = usePerformanceSettings();
  const { executeWithAdmin } = useAdminClient();
  const { 
    getActiveSession, 
    createSession,
    activeSessionId 
  } = useChatStore();

  // Load available functions on mount
  useEffect(() => {
    const loadFunctions = async () => {
      try {
        const functions = await executeWithAdmin(client =>
          client.functionConfigurations.list()
        );
        // Filter to only enabled functions
        setAvailableFunctions(functions.filter(f => f.isEnabled));
      } catch (err) {
        console.warn('Failed to load functions:', err);
        // Don't show error to user - function calling is optional
      }
    };
    void loadFunctions();
  }, [executeWithAdmin]);

  // Set initial model when data loads
  useEffect(() => {
    if (discoveryData?.data && discoveryData.data.length > 0 && !selectedModel) {
      setSelectedModel(discoveryData.data[0].id);
    }
  }, [discoveryData, selectedModel]);

  // Ensure we have an active session
  useEffect(() => {
    if (selectedModel && !activeSessionId) {
      createSession(selectedModel);
    }
  }, [selectedModel, activeSessionId, createSession]);

  const currentDiscoveryModel = discoveryData?.data?.find(m => m.id === selectedModel);
  
  // Use max_tokens from discovery API
  const maxContextTokens = currentDiscoveryModel?.max_tokens ?? 128000;
  
  // Use parameters from discovery model
  const modelParameters = currentDiscoveryModel?.parameters ?? '{}';
  const parameterState = useParameterState({
    parameters: modelParameters,
    persistKey: `chat-params-${selectedModel ?? 'default'}`,
  });

  // Use streaming logic hook
  const { sendMessage, abortControllerRef } = useChatStreamingLogic({
    selectedModel,
    messages,
    setMessages,
    isLoading,
    setIsLoading,
    setStreamingContent,
    setStreamingChannel,
    setTokensPerSecond,
    setError,
    getActiveSession,
    performanceSettings,
    dynamicParameters: parameterState.getSubmitValues(),
    sendHistoryEnabled,
    functionConfigurationIds: selectedFunctionIds.length > 0 ? selectedFunctionIds : undefined,
  });

  // Cleanup on unmount - abort any pending requests
  useEffect(() => {
    return () => {
      if (abortControllerRef.current) {
        abortControllerRef.current.abort();
        abortControllerRef.current = null;
      }
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []); // Empty dependency array - only run on unmount

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

  if (!discoveryData?.data || discoveryData.data.length === 0) {
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
    <Container size="lg" py="md" style={{ height: 'calc(100vh - 32px)', display: 'flex', flexDirection: 'column' }}>
      <Stack style={{ flex: 1, overflow: 'hidden' }}>
        <Paper p="md" withBorder style={{ flexShrink: 0 }}>
          <Stack gap="md">
            <Group justify="space-between">
              <Group style={{ flex: 1 }}>
                <ModelSelector
                  value={selectedModel}
                  onChange={setSelectedModel}
                  modelData={discoveryData?.data ?? []}
                  style={{ flex: 1, maxWidth: 400 }}
                />
                {currentDiscoveryModel?.capabilities?.vision && (
                  <Badge variant="light" color="blue">
                    Vision Enabled
                  </Badge>
                )}
                {currentDiscoveryModel && (
                  <TokenCounter
                    messages={messages}
                    maxTokens={maxContextTokens}
                    modelName={currentDiscoveryModel.display_name ?? currentDiscoveryModel.id}
                    compact={true}
                    showCost={false}
                    currentInputText={currentInputText}
                    currentInputImages={currentInputImages}
                  />
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
              <Stack gap="md">
                <ChatSettings
                  reasoningExpanded={reasoningExpanded}
                  onReasoningExpandedChange={setReasoningExpanded}
                  availableFunctions={availableFunctions}
                  selectedFunctionIds={selectedFunctionIds}
                  onFunctionIdsChange={setSelectedFunctionIds}
                />
                
                {/* Token Counter */}
                {currentDiscoveryModel && (
                  <TokenCounter
                    messages={messages}
                    maxTokens={maxContextTokens}
                    modelName={currentDiscoveryModel.display_name ?? currentDiscoveryModel.id}
                    compact={false}
                    showCost={false}
                    currentInputText={currentInputText}
                    currentInputImages={currentInputImages}
                  />
                )}
              </Stack>
            </Collapse>
            
            {/* Dynamic Parameters UI */}
            {currentDiscoveryModel?.parameters && currentDiscoveryModel.parameters !== '{}' && (
              <DynamicParameters
                parameters={currentDiscoveryModel.parameters}
                values={parameterState.values}
                onChange={parameterState.updateValues}
                context="chat"
                title="Model Parameters"
                collapsible={true}
                defaultExpanded={showParameters}
              />
            )}
          </Stack>
        </Paper>

        <Paper p="md" withBorder style={{ flex: 1, minHeight: 0, overflow: 'hidden', display: 'flex', flexDirection: 'column' }}>
          <ChatMessages 
            messages={messages}
            isLoading={isLoading}
            streamingContent={isLoading ? streamingContent : undefined}
            streamingChannel={isLoading ? streamingChannel : null}
            tokensPerSecond={performanceSettings.showTokensPerSecond ? tokensPerSecond : null}
            reasoningExpanded={reasoningExpanded}
          />
        </Paper>

        <Paper p="md" withBorder style={{ flexShrink: 0 }}>
          <ChatInput
            onSendMessage={(message, images) => {
              // Clear input state when message is sent
              setCurrentInputText('');
              setCurrentInputImages(0);
              void sendMessage(message, images);
            }}
            isStreaming={isLoading}
            onStopStreaming={() => {}}
            disabled={!selectedModel}
            model={currentDiscoveryModel ? {
              id: currentDiscoveryModel.id,
              providerId: '',
              displayName: currentDiscoveryModel.display_name ?? currentDiscoveryModel.id,
              supportsVision: currentDiscoveryModel.capabilities?.vision === true
            } : undefined}
            onInputChange={setCurrentInputText}
            onImagesChange={setCurrentInputImages}
            sendHistoryEnabled={sendHistoryEnabled}
            onToggleSendHistory={() => setSendHistoryEnabled(!sendHistoryEnabled)}
            onClearChat={() => {
              // Abort any ongoing streaming
              if (abortControllerRef.current) {
                abortControllerRef.current.abort();
              }
              // Clear all chat state
              setMessages([]);
              setStreamingContent('');
              setStreamingChannel(null);
              setTokensPerSecond(null);
              setError(null);
              setIsLoading(false);
              setCurrentInputText('');
              setCurrentInputImages(0);
            }}
          />
        </Paper>
      </Stack>
    </Container>
  );
}