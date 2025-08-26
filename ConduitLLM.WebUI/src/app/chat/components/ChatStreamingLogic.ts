import { useCallback, useRef, useMemo } from 'react';
import { v4 as uuidv4 } from 'uuid';
import { 
  createToastErrorHandler,
  ChatStreamingManager,
  type ImageAttachment,
  type StreamingCallbacks,
  type StreamMessageOptions
} from '@knn_labs/conduit-core-client';
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
  setTokensPerSecond,
  setError,
  getActiveSession,
  performanceSettings,
  dynamicParameters = {},
}: ChatStreamingLogicParams) {
  const streamingManagerRef = useRef<ChatStreamingManager | null>(null);
  
  // Create error handler with toast notifications
  const handleError = createToastErrorHandler(notifications.show);

  // Create streaming manager instance
  const streamingManager = useMemo(() => {
    return new ChatStreamingManager({
      apiEndpoint: '/api/chat/completions',
      timeoutMs: 300000, // 5 minutes
      trackPerformanceMetrics: performanceSettings.trackPerformanceMetrics,
      showTokensPerSecond: performanceSettings.showTokensPerSecond,
      useServerMetrics: performanceSettings.useServerMetrics,
      enableLogging: process.env.NODE_ENV === 'development'
    });
  }, [performanceSettings]);

  // Store reference for cleanup
  if (streamingManagerRef.current !== streamingManager) {
    streamingManagerRef.current = streamingManager;
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
        onContent: (content, totalContent) => {
          setStreamingContent(totalContent);
        },
        onTokensPerSecond: performanceSettings.showTokensPerSecond ? (tps) => {
          setTokensPerSecond(tps);
        } : undefined,
        onComplete: ({ content, metadata }) => {
          const assistantMessage: ChatMessage = {
            id: uuidv4(),
            role: 'assistant',
            content,
            timestamp: new Date(),
            metadata: metadata as ChatMessage['metadata'] // Convert SDK metadata to WebUI format
          };

          setMessages(prev => [...prev, assistantMessage]);
          setStreamingContent('');
          setTokensPerSecond(null);
        },
        onError: (error) => {
          console.error('Streaming error:', error);
          handleError(error, 'chat streaming');
          setError(error);
        },
        onAbort: () => {
          if (process.env.NODE_ENV === 'development') {
            console.warn('Chat streaming aborted');
          }
        }
      };

      // Use the streaming manager
      await streamingManager.streamMessage(inputMessage.trim(), streamingOptions, callbacks);
      
    } catch (err) {
      console.error('Chat error:', err);
      handleError(err, 'chat');
      setError(err as Error);
    } finally {
      setIsLoading(false);
      setStreamingContent('');
      setTokensPerSecond(null);
    }
  }, [selectedModel, messages, isLoading, getActiveSession, performanceSettings, handleError, setMessages, setIsLoading, setStreamingContent, setTokensPerSecond, setError, dynamicParameters, streamingManager]);

  const abortMessage = useCallback(() => {
    if (streamingManager.isStreaming()) {
      streamingManager.abort();
    }
  }, [streamingManager]);

  return {
    sendMessage,
    abortMessage,
    // Provide a ref-like interface for compatibility
    abortControllerRef: {
      current: streamingManager.isStreaming() ? { abort: abortMessage } : null
    }
  };
}

