'use client';

import { useEffect, useState, useCallback, useRef } from 'react';
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
import { v4 as uuidv4 } from 'uuid';
import { 
  createToastErrorHandler,
  shouldShowBalanceWarning
} from '@knn_labs/conduit-core-client';
import { ChatInput } from './ChatInput';
import { ChatMessages } from './ChatMessages';
import { ChatSettings } from './ChatSettings';
import { ErrorDisplay } from '@/components/common/ErrorDisplay';
import { 
  ImageAttachment, 
  ChatParameters, 
  ChatCompletionResponse, 
  ChatMessage,
  ChatCompletionRequest,
  ContentHelpers,
  type TextContent,
  type ImageContent,
  type MessageContent
} from '../types';
import { StreamingPerformanceMetrics, UsageData, SSEEventType, MetricsEventData } from '../types/metrics';
import { parseSSEStream } from '../utils/sse-parser';
import { usePerformanceSettings } from '../hooks/usePerformanceSettings';
import { useChatStore } from '../hooks/useChatStore';
import { useModels } from '../hooks/useModels';
import { notifications } from '@mantine/notifications';
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
  const abortControllerRef = useRef<AbortController | null>(null);
  
  // Create error handler with toast notifications
  const handleError = createToastErrorHandler(notifications.show);
  
  const performanceSettings = usePerformanceSettings();
  const { 
    getActiveSession, 
    createSession,
    activeSessionId 
  } = useChatStore();

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
  }, []);

  const sendMessage = useCallback(async (inputMessage: string, images?: ImageAttachment[]) => {
    if (!inputMessage.trim() && (!images || images.length === 0)) return;
    if (!selectedModel || isLoading) return;

    const userMessage: ChatMessage = {
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
    const startTime = Date.now();

    try {
      // Convert message format for API
      
      // Get session parameters
      const activeSession = getActiveSession();
      const sessionParams = activeSession?.parameters ?? {} as Partial<ChatParameters>;
      
      // Build messages array with optional system prompt
      const allMessages = [...messages, userMessage];
      const apiMessages: Array<{ role: 'system' | 'user' | 'assistant'; content: MessageContent }> = allMessages
        .filter(m => m.role !== 'function') // Filter out function messages for API
        .map(m => ({
          role: m.role as 'system' | 'user' | 'assistant',
          content: m.images && m.images.length > 0 
            ? buildMessageContent(m.content, m.images)
            : m.content
        }));
      
      // Prepend system prompt if it exists
      if (sessionParams.systemPrompt) {
        apiMessages.unshift({ role: 'system' as const, content: sessionParams.systemPrompt });
      }
      
      const requestBody: ChatCompletionRequest = {
        messages: apiMessages,
        model: selectedModel,
        stream: sessionParams.stream ?? true,
        // Include all session parameters
        temperature: sessionParams.temperature,
        max_tokens: sessionParams.maxTokens,
        top_p: sessionParams.topP,
        frequency_penalty: sessionParams.frequencyPenalty,
        presence_penalty: sessionParams.presencePenalty,
        ...(sessionParams.seed !== undefined && { seed: sessionParams.seed }),
        ...(sessionParams.stop && sessionParams.stop.length > 0 && { stop: sessionParams.stop }),
        ...(sessionParams.responseFormat === 'json_object' && { 
          response_format: { type: 'json_object' } 
        })
      };
      
      // Request logged for debugging
      if (process.env.NODE_ENV === 'development') {
        console.warn('Sending chat request:', {
          model: selectedModel,
          messageCount: requestBody.messages.length,
          lastMessage: requestBody.messages[requestBody.messages.length - 1]
        });
      }
      
      // Cancel any previous request
      if (abortControllerRef.current) {
        abortControllerRef.current.abort();
      }
      
      // Create new abort controller for this request
      const controller = new AbortController();
      abortControllerRef.current = controller;
      
      // Set a generous timeout for streaming responses (5 minutes)
      const timeoutId = setTimeout(() => {
        controller.abort();
        console.warn('Request timed out after 5 minutes');
      }, 300000);
      
      // Use the SDK through our API route (consistent with images/videos)
      const response = await fetch('/api/chat/completions', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(requestBody),
        signal: controller.signal,
      });
      
      if (process.env.NODE_ENV === 'development') {
        console.warn('Using SDK-backed chat completions API');
      }
      
      // Clear the timeout once we get a response
      clearTimeout(timeoutId);

      if (!response.ok) {
        // This will throw the appropriate ConduitError subclass (including InsufficientBalanceError for 402)
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
      }

      // Handle response based on streaming mode
      if (sessionParams.stream ?? true) {
        // Handle streaming response
        const reader = response.body?.getReader();
        if (!reader) throw new Error('No response body reader');
        
        let fullContent = '';
        const finalMetrics: Partial<StreamingPerformanceMetrics & UsageData> = {};

        // Process SSE stream with proper event parsing
        for await (const event of parseSSEStream(reader)) {
        if (event.data === '[DONE]') {
          const endTime = Date.now();
          const duration = (endTime - startTime) / 1000;
          
          // Calculate final metrics with proper fallbacks
          if (process.env.NODE_ENV === 'development') {
            console.warn('Final metrics collected:', finalMetrics);
          }
          const totalTokens = finalMetrics.total_tokens ?? finalMetrics.completion_tokens ?? 0;
          // Use completion_tokens_per_second for more accurate generation speed
          const tokensPerSec = (finalMetrics as StreamingPerformanceMetrics).completion_tokens_per_second 
            ?? (finalMetrics as StreamingPerformanceMetrics).tokens_per_second 
            ?? (totalTokens > 0 && duration > 0 ? totalTokens / duration : 0);
          const latencyMs = (finalMetrics as MetricsEventData).total_latency_ms ?? duration * 1000;
          
          const metadata = performanceSettings.trackPerformanceMetrics
            ? {
                tokensUsed: totalTokens,
                tokensPerSecond: tokensPerSec,
                latency: latencyMs,
                provider: finalMetrics.provider,
                model: finalMetrics.model ?? selectedModel,
                promptTokens: finalMetrics.prompt_tokens,
                completionTokens: finalMetrics.completion_tokens,
                timeToFirstToken: (finalMetrics as StreamingPerformanceMetrics).time_to_first_token_ms,
                streaming: true,
              }
            : { streaming: true };
          
          const assistantMessage: ChatMessage = {
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
          case SSEEventType.Content: {
            // Handle content chunks
            const contentData = event.data as { choices?: Array<{ delta?: { content?: string } }> };
            const delta = contentData?.choices?.[0]?.delta;
            
            if (delta?.content) {
              fullContent += delta.content;
              // Update streaming content incrementally for progressive rendering
              setStreamingContent(prev => prev + delta.content);
            }

            // Check for inline performance metrics (backward compatibility)
            const performanceData = event.data as { performance?: StreamingPerformanceMetrics };
            if (performanceData?.performance && performanceSettings.useServerMetrics) {
              Object.assign(finalMetrics, performanceData.performance);
              // Use completion_tokens_per_second for more accurate generation speed
              const tps = performanceData.performance.completion_tokens_per_second 
                ?? performanceData.performance.tokens_per_second;
              if (tps && performanceSettings.showTokensPerSecond) {
                setTokensPerSecond(tps);
              }
            }
            
            // Check for usage data (OpenAI style)
            const usageData = event.data as { usage?: UsageData };
            if (usageData?.usage) {
              Object.assign(finalMetrics, usageData.usage);
            }
            break;
          }
            
          case SSEEventType.Metrics: {
            // Handle live metrics updates
            if (performanceSettings.useServerMetrics) {
              const metricsData = event.data as MetricsEventData;
              if (process.env.NODE_ENV === 'development') {
                console.warn('Metrics event received:', metricsData);
              }
              Object.assign(finalMetrics, metricsData);
              // Use completion_tokens_per_second for more accurate generation speed
              const tps = metricsData.completion_tokens_per_second ?? metricsData.tokens_per_second;
              if (tps !== undefined && performanceSettings.showTokensPerSecond) {
                setTokensPerSecond(tps);
              }
            }
            break;
          }
            
          case SSEEventType.MetricsFinal: {
            // Handle final metrics
            if (performanceSettings.useServerMetrics) {
              const finalMetricsData = event.data as MetricsEventData;
              if (process.env.NODE_ENV === 'development') {
                console.warn('Final metrics event received:', finalMetricsData);
              }
              Object.assign(finalMetrics, finalMetricsData);
            }
            break;
          }
            
          case SSEEventType.Error: {
            // Handle error events
            console.warn('Received SSE error event:', event);
            const errorData = event.data as { error?: string; message?: string; statusCode?: number };
            console.warn('Parsed error data:', errorData);
            const rawError = errorData.error ?? errorData.message ?? 'Unknown error occurred';
            const streamError = new Error(`Stream error: ${rawError}`);
            
            // Add status code if available
            if (errorData.statusCode) {
              (streamError as Error & { status?: number }).status = errorData.statusCode;
            }
            
            // Use enhanced error handler for notifications
            handleError(streamError, 'process streaming response');
            setError(streamError);
            
            // Stop processing on error
            setStreamingContent('');
            setIsLoading(false);
            return;
          }
        }
      }
      } else {
        // Handle non-streaming response
        const data = await response.json() as ChatCompletionResponse;
        const assistantContent = data.choices?.[0]?.message?.content ?? '';
        
        const requestDuration = (Date.now() - startTime) / 1000;
        // Type assertion needed for response format compatibility
        const usage = data.usage as unknown as UsageData | undefined;
        const totalTokens = usage?.total_tokens ?? 0;
        
        const metadata = performanceSettings.trackPerformanceMetrics && data.usage
          ? {
              tokensUsed: totalTokens,
              tokensPerSecond: totalTokens > 0 && requestDuration > 0 ? totalTokens / requestDuration : 0,
              latency: requestDuration * 1000,
              model: data.model ?? selectedModel,
              promptTokens: usage?.prompt_tokens,
              completionTokens: usage?.completion_tokens,
              streaming: false,
            }
          : { streaming: false };
        
        const assistantMessage: ChatMessage = {
          id: uuidv4(),
          role: 'assistant',
          content: assistantContent,
          timestamp: new Date(),
          metadata
        };
        
        setMessages(prev => [...prev, assistantMessage]);
      }
    } catch (err) {
      // Check if the error is due to an abort (timeout or user cancellation)
      if (err instanceof Error && err.name === 'AbortError') {
        const errorMessage = 'Request timed out. The response took too long to generate. Please try a shorter prompt or simpler request.';
        
        const abortError = new Error(errorMessage);
        setError(abortError);
        
        // Show error in chat
        const errorMsg: ChatMessage = {
          id: uuidv4(),
          role: 'assistant',
          content: errorMessage,
          timestamp: new Date(),
          metadata: {
            tokensUsed: 0,
            tokensPerSecond: 0,
            latency: 0,
          }
        };
        setMessages(prev => [...prev, errorMsg]);
        return; // Early return for abort errors
      }
      
      // Use the enhanced error handler from SDK for notifications
      handleError(err, 'send chat message');
      const errorInstance = err instanceof Error ? err : new Error(String(err));
      setError(errorInstance);
      
      // Show error as a system message in chat
      const errorMsg: ChatMessage = {
        id: uuidv4(),
        role: 'assistant',
        content: `Error: ${errorInstance.message}`,
        timestamp: new Date(),
        metadata: {
          tokensUsed: 0,
          tokensPerSecond: 0,
          latency: 0,
        }
      };
      setMessages(prev => [...prev, errorMsg]);
      
      // Show special handling for balance errors
      if (shouldShowBalanceWarning(err)) {
        // Clear the loading state faster for balance errors since user needs to take action
        const balanceError = new Error('Please add credits to your account to continue chatting.');
        balanceError.name = 'InsufficientBalanceError';
        (balanceError as Error & { status?: number }).status = 402;
        setError(balanceError);
      }
    } finally {
      setIsLoading(false);
      setStreamingContent('');
      setTokensPerSecond(null);
      // Clean up abort controller reference
      if (abortControllerRef.current) {
        abortControllerRef.current = null;
      }
    }
  }, [selectedModel, messages, isLoading, getActiveSession, performanceSettings.showTokensPerSecond, performanceSettings.trackPerformanceMetrics, performanceSettings.useServerMetrics, handleError]);

  const buildMessageContent = (text: string, images?: ImageAttachment[]): MessageContent => {
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