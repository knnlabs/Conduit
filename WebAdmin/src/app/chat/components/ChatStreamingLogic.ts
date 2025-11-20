import { useCallback, useRef, useMemo } from 'react';
import { v4 as uuidv4 } from 'uuid';
import {
  createToastErrorHandler,
  type ImageAttachment,
  type StreamingCallbacks,
  type StreamMessageOptions
} from '@knn_labs/conduit-core-client';
import type { FunctionConfigurationDto } from '@knn_labs/conduit-admin-client';
import { SDKChatStreamingAdapter } from '@/lib/client/sdkChatStreamingAdapter';
import {
  ChatParameters,
  ChatMessage,
  ChatErrorType
} from '../types';
import { notifications } from '@mantine/notifications';

interface ChatStreamingLogicParams {
  selectedModel: string | null;
  messages: ChatMessage[];
  setMessages: React.Dispatch<React.SetStateAction<ChatMessage[]>>;
  isLoading: boolean;
  setIsLoading: (value: boolean) => void;
  setStreamingContent: (value: string | ((prev: string) => string)) => void;
  setStreamingChannel?: (value: string | null) => void;
  setTokensPerSecond: (value: number | null) => void;
  setError: (error: Error | null) => void;
  getActiveSession: () => { parameters?: Partial<ChatParameters> } | null;
  performanceSettings: {
    trackPerformanceMetrics: boolean;
    showTokensPerSecond: boolean;
    useServerMetrics: boolean;
  };
  dynamicParameters?: Record<string, unknown>;
  sendHistoryEnabled?: boolean;
  functionConfigurationIds?: number[];
  availableFunctions?: FunctionConfigurationDto[];
}

export function useChatStreamingLogic({
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
  dynamicParameters = {},
  sendHistoryEnabled = true,
  functionConfigurationIds,
  availableFunctions = [],
}: ChatStreamingLogicParams) {
  const streamingAdapterRef = useRef<SDKChatStreamingAdapter | null>(null);
  
  // Create error handler with toast notifications
  const handleError = createToastErrorHandler(notifications.show);

  // Create streaming adapter that uses SDK directly
  const streamingAdapter = useMemo(() => {
    return new SDKChatStreamingAdapter({
      timeoutMs: 300000, // 5 minutes
      trackPerformanceMetrics: performanceSettings.trackPerformanceMetrics,
      showTokensPerSecond: performanceSettings.showTokensPerSecond,
      useServerMetrics: performanceSettings.useServerMetrics,
      enableLogging: process.env.NODE_ENV === 'development'
    });
  }, [performanceSettings]);

  // Store reference for cleanup
  if (streamingAdapterRef.current !== streamingAdapter) {
    // Dispose old adapter
    if (streamingAdapterRef.current) {
      streamingAdapterRef.current.dispose();
    }
    streamingAdapterRef.current = streamingAdapter;
  }

  const sendMessage = useCallback(async (inputMessage: string, images?: ImageAttachment[]) => {
    if (!inputMessage.trim() && (!images || images.length === 0)) return;
    if (!selectedModel || isLoading) return;

    // Build function metadata if functions are selected
    const functionMetadata = functionConfigurationIds && functionConfigurationIds.length > 0 ? {
      functionIds: functionConfigurationIds,
      functionNames: availableFunctions
        .filter(f => functionConfigurationIds.includes(f.id))
        .map(f => f.configurationName)
    } : undefined;

    const userMessage: ChatMessage = {
      id: uuidv4(),
      role: 'user',
      content: inputMessage.trim(),
      images,
      timestamp: new Date(),
      metadata: functionMetadata
    };

    setMessages(prev => [...prev, userMessage]);
    setIsLoading(true);
    setStreamingContent('');
    setTokensPerSecond(null);
    setError(null);

    try {
      // Get session parameters
      const activeSession = getActiveSession();
      const sessionParams = activeSession?.parameters ?? {} as Partial<ChatParameters>;
      
      // Build conversation history for streaming manager
      const allMessages = [...messages, userMessage];
      const conversationHistory = allMessages
        .filter(m => m.role !== 'function') // Filter out function messages for API
        .map(m => ({
          role: m.role as 'user' | 'assistant',
          content: m.content,
          images: m.images
        }));

      // Prepare streaming options
      const streamingOptions: StreamMessageOptions = {
        model: selectedModel,
        stream: true,
        messages: sendHistoryEnabled ? conversationHistory.slice(0, -1) : [], // Include history or send empty array
        images: images,
        systemPrompt: sessionParams.systemPrompt,
        temperature: sessionParams.temperature,
        maxTokens: sessionParams.maxTokens,
        topP: sessionParams.topP,
        frequencyPenalty: sessionParams.frequencyPenalty,
        presencePenalty: sessionParams.presencePenalty,
        seed: sessionParams.seed,
        stop: sessionParams.stop && sessionParams.stop.length > 0 ? sessionParams.stop : undefined,
        responseFormat: sessionParams.responseFormat === 'json_object' ? 'json_object' : undefined,
        functionConfigurationIds: functionConfigurationIds,
        dynamicParameters
      };

      // Create streaming callbacks
      const callbacks: StreamingCallbacks = {
        onStart: () => {
          if (process.env.NODE_ENV === 'development') {
            console.warn('Chat streaming started');
          }
        },
        onChunk: (chunk) => {
          // Track the channel for visual treatment
          const channel = chunk.choices?.[0]?.delta?.channel;
          if (setStreamingChannel && channel !== undefined) {
            setStreamingChannel(channel);
          }
        },
        onContent: (content, totalContent) => {
          setStreamingContent(totalContent);
        },
        onTokensPerSecond: performanceSettings.showTokensPerSecond ? (tps) => {
          setTokensPerSecond(tps);
        } : undefined,
        onComplete: ({ content, metadata }) => {
          // Note: For models like gpt-oss-120b, we may only receive reasoning content
          // which is still valid content that should be displayed
          let finalContent = content;
          if (!finalContent || finalContent.length === 0) {
            // Still create a message even if empty to show something happened
            finalContent = '[No response received]';
          }

          const assistantMessage: ChatMessage = {
            id: uuidv4(),
            role: 'assistant',
            content: finalContent,
            timestamp: new Date(),
            metadata: {
              ...metadata,
              toolCalls: undefined // Remove from metadata as we're adding to message root
            } as ChatMessage['metadata'],
            toolCalls: metadata?.toolCalls // Add tool calls to message root for UI display
          };

          setMessages(prev => [...prev, assistantMessage]);
          // Clear streaming state after message is added
          setStreamingContent('');
          setStreamingChannel?.(null);
          setTokensPerSecond(null);
          setIsLoading(false);
        },
        onError: (error) => {
          console.error('Streaming error:', error);
          handleError(error, 'chat streaming');
          setError(error);

          // Map StreamingError to ChatErrorType
          let errorType: ChatErrorType = 'server_error';
          if (error.status === 429 || error.code === 'rate_limit_exceeded') {
            errorType = 'rate_limit';
          } else if (error.status === 401 || error.status === 403) {
            errorType = 'auth_error';
          } else if (error.status !== undefined && error.status >= 400 && error.status < 500) {
            errorType = 'model_not_found';
          } else if (error.code === 'ECONNREFUSED' || error.code === 'ETIMEDOUT') {
            errorType = 'network_error';
          }

          const errorMessage: ChatMessage = {
            id: uuidv4(),
            role: 'assistant',
            content: error.context ?? 'An error occurred during streaming.',
            timestamp: new Date(),
            error: {
              type: errorType,
              code: error.code,
              statusCode: error.status,
              technical: error.message,
              recoverable: error.retryable ?? false
            }
          };

          setMessages(prev => [...prev, errorMessage]);

          // Clear streaming state
          setStreamingContent('');
          setStreamingChannel?.(null);
          setTokensPerSecond(null);
          setIsLoading(false);
        },
        onAbort: () => {
          if (process.env.NODE_ENV === 'development') {
            console.warn('Chat streaming aborted');
          }
          // Clean up state when aborted
          setStreamingContent('');
          setStreamingChannel?.(null);
          setTokensPerSecond(null);
          setIsLoading(false);
        }
      };

      // Use the streaming adapter
      await streamingAdapter.streamMessage(inputMessage.trim(), streamingOptions, callbacks);
      
    } catch (err) {
      console.error('Chat error:', err);
      handleError(err, 'chat');
      setError(err as Error);
    } finally {
      // Cleanup handled in callbacks
      setTokensPerSecond(null);
    }
  }, [selectedModel, messages, isLoading, getActiveSession, performanceSettings, handleError, setMessages, setIsLoading, setStreamingContent, setStreamingChannel, setTokensPerSecond, setError, dynamicParameters, streamingAdapter, sendHistoryEnabled, functionConfigurationIds, availableFunctions]);

  const abortMessage = useCallback(() => {
    if (streamingAdapterRef.current) {
      streamingAdapterRef.current.abort();
    }
  }, []);

  return {
    sendMessage,
    abortMessage,
    // Provide a ref-like interface for compatibility
    abortControllerRef: {
      current: streamingAdapterRef.current ? { abort: abortMessage } : null
    }
  };
}

