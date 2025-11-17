import { useCallback, useRef, useMemo } from 'react';
import { v4 as uuidv4 } from 'uuid';
import {
  createToastErrorHandler,
  type ImageAttachment,
  type StreamingCallbacks,
  type StreamMessageOptions
} from '@knn_labs/conduit-core-client';
import { SDKChatStreamingAdapter } from '@/lib/client/sdkChatStreamingAdapter';
import { 
  ChatParameters, 
  ChatMessage
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
        messages: conversationHistory.slice(0, -1), // All except the current message
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
            metadata: metadata as ChatMessage['metadata'] // Convert SDK metadata to WebUI format
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

          // Create error message with partial content if available
          const partialContent = error.partialContent ?? '';

          // Only create an error message if there's partial content or we want to show the error
          if (partialContent || error.errorType) {
            const errorMessage: ChatMessage = {
              id: uuidv4(),
              role: 'assistant',
              content: partialContent || 'An error occurred during streaming.',
              timestamp: new Date(),
              error: {
                type: error.errorType ?? 'server_error',
                code: error.code,
                statusCode: error.statusCode,
                retryAfter: error.retryAfter,
                suggestions: error.suggestions,
                technical: error.technical ?? error.message,
                recoverable: error.recoverable ?? false
              }
            };

            setMessages(prev => [...prev, errorMessage]);
          }

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
  }, [selectedModel, messages, isLoading, getActiveSession, performanceSettings, handleError, setMessages, setIsLoading, setStreamingContent, setStreamingChannel, setTokensPerSecond, setError, dynamicParameters, streamingAdapter]);

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

