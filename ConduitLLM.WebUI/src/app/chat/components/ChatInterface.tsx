'use client';

import { useEffect, useState, useCallback } from 'react';
import { 
  Container, 
  Paper, 
  Stack, 
  Grid,
  Text,
  Center,
  Loader,
  Alert,
  Select,
  Group,
  Badge,
  ScrollArea,
  Collapse,
  ActionIcon
} from '@mantine/core';
import { IconAlertCircle, IconRobot, IconUser, IconBolt, IconClock, IconSettings, IconChevronDown, IconChevronUp } from '@tabler/icons-react';
import { v4 as uuidv4 } from 'uuid';
import { ContentHelpers, type TextContent, type ImageContent } from '@knn_labs/conduit-core-client';
import { ChatInput } from './ChatInput';
import { ChatMessages } from './ChatMessages';
import { ChatSettings } from './ChatSettings';
import { ImageAttachment, ChatParameters } from '../types';
import { StreamingPerformanceMetrics, UsageData, MessageMetrics, SSEEventType, MetricsEventData } from '../types/metrics';
import { parseSSEStream } from '../utils/sse-parser';
import { usePerformanceSettings } from '../hooks/usePerformanceSettings';
import { useChatStore } from '../hooks/useChatStore';

interface Message {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  images?: ImageAttachment[];
  timestamp: Date;
  metadata?: MessageMetrics;
}

export function ChatInterface() {
  const [models, setModels] = useState<Array<{ value: string; label: string; supportsVision?: boolean }>>([]);
  const [selectedModel, setSelectedModel] = useState<string | null>(null);
  const [messages, setMessages] = useState<Message[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [modelsLoading, setModelsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [streamingContent, setStreamingContent] = useState('');
  const [tokensPerSecond, setTokensPerSecond] = useState<number | null>(null);
  const [showSettings, setShowSettings] = useState(false);
  
  const performanceSettings = usePerformanceSettings();
  const { 
    getActiveSession, 
    createSession, 
    setActiveSession,
    sessions,
    activeSessionId 
  } = useChatStore();

  // Fetch models on mount
  useEffect(() => {
    const fetchModels = async () => {
      try {
        const response = await fetch('/api/model-mappings');
        if (!response.ok) {
          throw new Error('Failed to fetch models');
        }
        const data = await response.json();
        const modelOptions = data.map((m: any) => ({
          value: m.modelId,
          label: `${m.modelId} (${m.providerId})`,
          supportsVision: m.supportsVision || false
        }));
        setModels(modelOptions);
        if (modelOptions.length > 0) {
          setSelectedModel(modelOptions[0].value);
        }
      } catch (err) {
        setError('Failed to load models');
        console.error(err);
      } finally {
        setModelsLoading(false);
      }
    };

    fetchModels();
  }, []);

  // Ensure we have an active session
  useEffect(() => {
    if (selectedModel && !activeSessionId) {
      createSession(selectedModel);
    }
  }, [selectedModel, activeSessionId, createSession]);

  const sendMessage = useCallback(async (inputMessage: string, images?: ImageAttachment[]) => {
    if (!inputMessage.trim() && (!images || images.length === 0)) return;
    if (!selectedModel || isLoading) return;

    const userMessage: Message = {
      id: uuidv4(),
      role: 'user',
      content: inputMessage.trim(),
      images,
      timestamp: new Date()
    };

    setMessages(prev => [...prev, userMessage]);
    setIsLoading(true);
    setStreamingContent('');
    setTokensPerSecond(null);

    try {
      // Convert message format for API
      const messageContent = buildMessageContent(inputMessage, images);
      
      // Get session parameters
      const activeSession = getActiveSession();
      const sessionParams = activeSession?.parameters || {} as Partial<ChatParameters>;
      
      // Build messages array with optional system prompt
      const allMessages = [...messages, userMessage];
      const apiMessages: Array<{ role: 'system' | 'user' | 'assistant'; content: string | Array<TextContent | ImageContent> }> = 
        allMessages.map(m => ({
          role: m.role,
          content: m.images && m.images.length > 0 
            ? buildMessageContent(m.content, m.images)
            : m.content
        }));
      
      // Prepend system prompt if it exists
      if (sessionParams.systemPrompt) {
        apiMessages.unshift({ role: 'system' as const, content: sessionParams.systemPrompt });
      }
      
      const requestBody = {
        messages: apiMessages,
        model: selectedModel,
        stream: sessionParams.stream ?? true,
        // Include all session parameters
        temperature: sessionParams.temperature,
        max_tokens: sessionParams.maxTokens,
        top_p: sessionParams.topP,
        frequency_penalty: sessionParams.frequencyPenalty,
        presence_penalty: sessionParams.presencePenalty,
        ...(sessionParams.responseFormat === 'json_object' && { response_format: { type: 'json_object' } }),
        ...(sessionParams.seed !== undefined && { seed: sessionParams.seed }),
        ...(sessionParams.stop && sessionParams.stop.length > 0 && { stop: sessionParams.stop })
      };
      
      console.log('Sending chat request:', {
        model: selectedModel,
        messageCount: requestBody.messages.length,
        lastMessage: requestBody.messages[requestBody.messages.length - 1]
      });
      
      const response = await fetch('/api/chat/completions', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(requestBody),
      });

      if (!response.ok) {
        const errorText = await response.text();
        console.error('Chat API error:', response.status, errorText);
        throw new Error(`Failed to get response: ${response.status} ${response.statusText}`);
      }

      // Handle response based on streaming mode
      if (sessionParams.stream ?? true) {
        // Handle streaming response
        const reader = response.body?.getReader();
        if (!reader) throw new Error('No response body reader');
        
        let fullContent = '';
        let startTime = Date.now();
        let finalMetrics: Partial<StreamingPerformanceMetrics & UsageData & MetricsEventData> = {};

        // Process SSE stream with proper event parsing
        for await (const event of parseSSEStream(reader)) {
        if (event.data === '[DONE]') {
          const endTime = Date.now();
          const duration = (endTime - startTime) / 1000;
          
          const metadata: MessageMetrics | undefined = performanceSettings.trackPerformanceMetrics
            ? {
                tokensUsed: finalMetrics.total_tokens || finalMetrics.completion_tokens || finalMetrics.tokens_generated || 0,
                tokensPerSecond: finalMetrics.tokens_per_second || finalMetrics.current_tokens_per_second || 0,
                latency: finalMetrics.total_latency_ms || duration * 1000,
              }
            : undefined;
          
          const assistantMessage: Message = {
            id: uuidv4(),
            role: 'assistant',
            content: fullContent,
            timestamp: new Date(),
            metadata
          };

          setMessages(prev => [...prev, assistantMessage]);
          setStreamingContent('');
          setTokensPerSecond(null);
          break;
        }

        switch (event.event) {
          case SSEEventType.Content:
            // Handle content chunks
            const delta = event.data?.choices?.[0]?.delta;
            
            if (delta?.content) {
              fullContent += delta.content;
              setStreamingContent(fullContent);
            }

            // Check for inline performance metrics (backward compatibility)
            if (event.data?.performance && performanceSettings.useServerMetrics) {
              Object.assign(finalMetrics, event.data.performance);
              if (event.data.performance.tokens_per_second && performanceSettings.showTokensPerSecond) {
                setTokensPerSecond(event.data.performance.tokens_per_second);
              }
            }
            
            // Check for usage data (OpenAI style)
            if (event.data?.usage) {
              Object.assign(finalMetrics, event.data.usage);
            }
            break;
            
          case SSEEventType.Metrics:
            // Handle live metrics updates
            if (performanceSettings.useServerMetrics) {
              Object.assign(finalMetrics, event.data);
              if (event.data.current_tokens_per_second && performanceSettings.showTokensPerSecond) {
                setTokensPerSecond(event.data.current_tokens_per_second);
              }
            }
            break;
            
          case SSEEventType.MetricsFinal:
            // Handle final metrics
            if (performanceSettings.useServerMetrics) {
              Object.assign(finalMetrics, event.data);
            }
            break;
            
          case SSEEventType.Error:
            // Handle error events
            console.error('SSE Error event:', event.data);
            break;
        }
      }
      } else {
        // Handle non-streaming response
        const data = await response.json();
        const assistantContent = data.choices?.[0]?.message?.content || '';
        const finishReason = data.choices?.[0]?.finish_reason;
        
        const metadata: MessageMetrics | undefined = performanceSettings.trackPerformanceMetrics && data.usage
          ? {
              tokensUsed: data.usage.total_tokens || 0,
              tokensPerSecond: 0, // Not applicable for non-streaming
              latency: Date.now() - Date.now(), // Would need to track request start time
            }
          : undefined;
        
        const assistantMessage: Message = {
          id: uuidv4(),
          role: 'assistant',
          content: assistantContent,
          timestamp: new Date(),
          metadata
        };
        
        setMessages(prev => [...prev, assistantMessage]);
      }
    } catch (err: any) {
      console.error('Chat error:', err);
      const errorMessage = err.message || 'Failed to send message';
      setError(errorMessage);
      
      // Show error as a system message in chat
      const errorMsg: Message = {
        id: uuidv4(),
        role: 'assistant',
        content: `Error: ${errorMessage}`,
        timestamp: new Date(),
        metadata: {
          tokensUsed: 0,
          tokensPerSecond: 0,
          latency: 0,
        }
      };
      setMessages(prev => [...prev, errorMsg]);
    } finally {
      setIsLoading(false);
      setStreamingContent('');
      setTokensPerSecond(null);
    }
  }, [selectedModel, messages, isLoading, getActiveSession]);

  const buildMessageContent = (text: string, images?: ImageAttachment[]) => {
    if (!images || images.length === 0) {
      return text;
    }

    const content: Array<TextContent | ImageContent> = [];
    
    if (text) {
      content.push(ContentHelpers.text(text));
    }

    images.forEach(img => {
      if (img.base64) {
        content.push(ContentHelpers.imageBase64(img.base64, img.mimeType));
      } else if (img.url) {
        content.push(ContentHelpers.imageUrl(img.url));
      }
    });

    return content;
  };

  const currentModel = models.find(m => m.value === selectedModel);

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
        <Alert icon={<IconAlertCircle size={16} />} color="red" title="Error">
          {error}
        </Alert>
      </Container>
    );
  }

  if (models.length === 0) {
    return (
      <Container size="sm" mt="xl">
        <Alert icon={<IconAlertCircle size={16} />} color="yellow" title="No models available">
          No models are currently configured. Please configure model mappings first.
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
                <Select
                  label="Model"
                  placeholder="Select a model"
                  value={selectedModel}
                  onChange={setSelectedModel}
                  data={models}
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

        <Paper p="md" withBorder style={{ flex: 1, overflow: 'hidden' }}>
          <ChatMessages 
            messages={messages}
            streamingContent={isLoading ? streamingContent : undefined}
            tokensPerSecond={performanceSettings.showTokensPerSecond ? tokensPerSecond : null}
          />
        </Paper>

        <Paper p="md" withBorder>
          <ChatInput
            onSendMessage={sendMessage}
            isStreaming={isLoading}
            onStopStreaming={() => {}}
            disabled={!selectedModel}
            model={currentModel ? {
              id: currentModel.value,
              providerId: '',
              displayName: currentModel.label,
              supportsVision: currentModel.supportsVision
            } : undefined}
          />
        </Paper>
      </Stack>
    </Container>
  );
}