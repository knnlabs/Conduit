'use client';

import { useEffect, useState } from 'react';
import { 
  Container, 
  Paper, 
  Stack, 
  Grid,
  Title,
  Text,
  Center,
  Loader,
  Alert
} from '@mantine/core';
import { IconAlertCircle } from '@tabler/icons-react';
import { useChatStore } from '../hooks/useChatStore';
import { useModels } from '../hooks/useModels';
import { ChatSidebar } from './ChatSidebar';
import { ChatMessages } from './ChatMessages';
import { ChatInput } from './ChatInput';
import { ModelSelector } from './ModelSelector';
import { ChatSettings } from './ChatSettings';
import { ConversationStarters } from './ConversationStarters';
import { useChatCompletion } from '../hooks/useChatCompletion';
import { ChatMessage, ImageAttachment } from '../types';

export function ChatInterface() {
  const { data: models, isLoading: modelsLoading, error: modelsError } = useModels();
  const { 
    getActiveSession, 
    createSession, 
    addMessage, 
    updateMessage,
    activeSessionId 
  } = useChatStore();
  
  const [tokensPerSecond, setTokensPerSecond] = useState<number | null>(null);
  
  const activeSession = getActiveSession();
  
  const { sendMessage, isStreaming, streamingContent, stopStreaming } = useChatCompletion({
    onStreamEnd: (message) => {
      if (activeSession) {
        addMessage(activeSession.id, message);
      }
    },
    onTokensPerSecond: setTokensPerSecond,
  });

  useEffect(() => {
    if (!activeSession && models && models.length > 0) {
      createSession(models[0].id);
    }
  }, [activeSession, models, createSession]);

  const handleSendMessage = async (content: string, images?: ImageAttachment[]) => {
    if (!activeSession) return;
    
    const userMessage: Omit<ChatMessage, 'id' | 'timestamp'> = {
      role: 'user',
      content,
      images,
    };
    
    addMessage(activeSession.id, userMessage);
    
    const messages = [
      ...(activeSession.parameters.systemPrompt 
        ? [{ role: 'system', content: activeSession.parameters.systemPrompt }] 
        : []),
      ...activeSession.messages.map(msg => ({
        role: msg.role,
        content: msg.content,
        ...(msg.name && { name: msg.name }),
        ...(msg.functionCall && { function_call: msg.functionCall }),
        ...(msg.toolCalls && { tool_calls: msg.toolCalls }),
      })),
      { role: 'user', content },
    ];
    
    await sendMessage(messages, activeSession.model, {
      temperature: activeSession.parameters.temperature,
      maxTokens: activeSession.parameters.maxTokens,
      topP: activeSession.parameters.topP,
      frequencyPenalty: activeSession.parameters.frequencyPenalty,
      presencePenalty: activeSession.parameters.presencePenalty,
      responseFormat: activeSession.parameters.responseFormat === 'json_object' 
        ? { type: 'json_object' } 
        : undefined,
      seed: activeSession.parameters.seed,
      stop: activeSession.parameters.stop,
    });
  };

  const handleStarterClick = (prompt: string) => {
    handleSendMessage(prompt);
  };

  if (modelsLoading) {
    return (
      <Center h="100vh">
        <Loader size="lg" />
      </Center>
    );
  }

  if (modelsError) {
    return (
      <Container size="sm" mt="xl">
        <Alert icon={<IconAlertCircle size={16} />} color="red" title="Error loading models">
          Failed to load available models. Please try refreshing the page.
        </Alert>
      </Container>
    );
  }

  if (!models || models.length === 0) {
    return (
      <Container size="sm" mt="xl">
        <Alert icon={<IconAlertCircle size={16} />} color="yellow" title="No models available">
          No models are currently configured. Please configure model mappings first.
        </Alert>
      </Container>
    );
  }

  return (
    <Grid gutter={0} style={{ height: 'calc(100vh - 60px)' }}>
      <Grid.Col span={3} style={{ borderRight: '1px solid var(--mantine-color-gray-3)' }}>
        <ChatSidebar />
      </Grid.Col>
      
      <Grid.Col span={9}>
        <Stack h="100%" gap={0}>
          <Paper p="md" style={{ borderBottom: '1px solid var(--mantine-color-gray-3)' }}>
            <Grid align="center">
              <Grid.Col span={6}>
                <ModelSelector />
              </Grid.Col>
              <Grid.Col span={6}>
                <ChatSettings />
              </Grid.Col>
            </Grid>
          </Paper>
          
          <Stack style={{ flex: 1, overflow: 'hidden' }} p="md" gap="md">
            {activeSession && activeSession.messages.length === 0 ? (
              <ConversationStarters onStarterClick={handleStarterClick} />
            ) : (
              <ChatMessages 
                messages={activeSession?.messages || []} 
                streamingContent={isStreaming ? streamingContent : undefined}
                tokensPerSecond={tokensPerSecond}
              />
            )}
          </Stack>
          
          <Paper p="md" style={{ borderTop: '1px solid var(--mantine-color-gray-3)' }}>
            <ChatInput 
              onSendMessage={handleSendMessage}
              isStreaming={isStreaming}
              onStopStreaming={stopStreaming}
              disabled={!activeSession}
              model={models?.find(m => m.id === activeSession?.model)}
            />
          </Paper>
        </Stack>
      </Grid.Col>
    </Grid>
  );
}