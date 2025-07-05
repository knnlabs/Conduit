'use client';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { notifications } from '@mantine/notifications';
import { BackendErrorHandler, type BackendError } from '@/lib/errors/BackendErrorHandler';
import { createCoreClient } from '@/lib/clients/conduit';
import { apiFetch } from '@/lib/utils/fetch-wrapper';
import type { ChatCompletionRequest, ImageGenerationRequest, ImageGenerationResponse } from '@knn_labs/conduit-core-client';

// Chat types
export interface ChatMessage {
  role: 'user' | 'assistant' | 'system';
  content: string;
}

// Query key factory for Core API
export const coreApiKeys = {
  all: ['core-api'] as const,
  chat: () => [...coreApiKeys.all, 'chat'] as const,
  images: () => [...coreApiKeys.all, 'images'] as const,
  videos: () => [...coreApiKeys.all, 'videos'] as const,
  audio: () => [...coreApiKeys.all, 'audio'] as const,
  imageHistory: (virtualKey: string) => [...coreApiKeys.images(), 'history', virtualKey] as const,
  videoHistory: (virtualKey: string) => [...coreApiKeys.videos(), 'history', virtualKey] as const,
  audioHistory: (virtualKey: string) => [...coreApiKeys.audio(), 'history', virtualKey] as const,
} as const;

// Chat Completions API
export function useChatCompletion() {
  return useMutation({
    mutationFn: async ({ virtualKey, ...body }: { virtualKey: string } & ChatCompletionRequest) => {
      try {
        const client = createCoreClient(virtualKey);
        // Handle the overloaded create method properly
        if (body.stream === true) {
          return await client.chat.completions.create({ ...body, stream: true });
        } else {
          return await client.chat.completions.create({ ...body, stream: false });
        }
      } catch (error: unknown) {
        throw BackendErrorHandler.classifyError(error);
      }
    },
    onError: (error: unknown) => {
      const classifiedError = (error as BackendError).type ? error : BackendErrorHandler.classifyError(error);
      notifications.show({
        title: 'Chat Error',
        message: BackendErrorHandler.getUserFriendlyMessage(classifiedError as BackendError),
        color: 'red',
      });
    },
    ...BackendErrorHandler.getRetryConfig(),
  });
}

export function useStreamingChatCompletion() {
  return useMutation({
    mutationFn: async ({ 
      virtualKey, 
      onChunk, 
      ...body 
    }: { 
      virtualKey: string; 
      onChunk: (chunk: unknown) => void;
    } & ChatCompletionRequest) => {
      try {
        const client = createCoreClient(virtualKey);
        const stream = await client.chat.completions.create({ ...body, stream: true });
        
        // Handle the streaming response
        for await (const chunk of stream) {
          onChunk(chunk);
        }
      } catch (error: unknown) {
        throw BackendErrorHandler.classifyError(error);
      }
    },
    onError: (error: unknown) => {
      const classifiedError = (error as BackendError).type ? error : BackendErrorHandler.classifyError(error);
      notifications.show({
        title: 'Streaming Chat Error',
        message: BackendErrorHandler.getUserFriendlyMessage(classifiedError as BackendError),
        color: 'red',
      });
    },
    ...BackendErrorHandler.getRetryConfig(),
  });
}

// Image Generation API
export function useImageGeneration() {
  const queryClient = useQueryClient();

  return useMutation<ImageGenerationResponse, unknown, { virtualKey: string } & ImageGenerationRequest>({
    mutationFn: async ({ virtualKey, ...body }) => {
      try {
        const client = createCoreClient(virtualKey);
        return await client.images.generate(body);
      } catch (error: unknown) {
        throw BackendErrorHandler.classifyError(error);
      }
    },
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: coreApiKeys.imageHistory(variables.virtualKey) });
      notifications.show({
        title: 'Image Generated',
        message: 'Image has been generated successfully',
        color: 'green',
      });
    },
    onError: (error: unknown) => {
      const classifiedError = (error as BackendError).type ? error : BackendErrorHandler.classifyError(error);
      notifications.show({
        title: 'Image Generation Error',
        message: BackendErrorHandler.getUserFriendlyMessage(classifiedError as BackendError),
        color: 'red',
      });
    },
    ...BackendErrorHandler.getRetryConfig(),
  });
}

export function useImageHistory(virtualKey: string) {
  return useQuery({
    queryKey: coreApiKeys.imageHistory(virtualKey),
    queryFn: async () => {
      try {
        const response = await apiFetch(`/api/core/images/generations?virtual_key=${encodeURIComponent(virtualKey)}`, {
          method: 'GET',
          headers: {
            'Content-Type': 'application/json',
          },
        });

        if (!response.ok) {
          const error = await response.json().catch(() => ({ error: 'Failed to fetch image history' }));
          const backendError = { status: response.status, message: error.error || 'Failed to fetch image history' };
          throw BackendErrorHandler.classifyError(backendError);
        }

        return response.json();
      } catch (error: unknown) {
        throw BackendErrorHandler.classifyError(error);
      }
    },
    enabled: !!virtualKey,
    staleTime: 30 * 1000,
    ...BackendErrorHandler.getRetryConfig(),
  });
}

// Video Generation API
export function useVideoGeneration() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ virtualKey, ...body }: { virtualKey: string; [key: string]: unknown }) => {
      try {
        const response = await apiFetch('/api/core/videos/generations', {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({ virtual_key: virtualKey, ...body }),
        });

        if (!response.ok) {
          const error = await response.json().catch(() => ({ error: 'Failed to generate video' }));
          const backendError = { status: response.status, message: error.error || 'Failed to generate video' };
          throw BackendErrorHandler.classifyError(backendError);
        }

        return response.json();
      } catch (error: unknown) {
        throw BackendErrorHandler.classifyError(error);
      }
    },
    onSuccess: (data: unknown, variables: { virtualKey: string }) => {
      queryClient.invalidateQueries({ queryKey: coreApiKeys.videoHistory(variables.virtualKey) });
      notifications.show({
        title: 'Video Generation Started',
        message: 'Video generation has been initiated successfully',
        color: 'green',
      });
    },
    onError: (error: unknown) => {
      const classifiedError = (error as BackendError).type ? error : BackendErrorHandler.classifyError(error);
      notifications.show({
        title: 'Video Generation Error',
        message: BackendErrorHandler.getUserFriendlyMessage(classifiedError as BackendError),
        color: 'red',
      });
    },
    ...BackendErrorHandler.getRetryConfig(),
  });
}

export function useVideoHistory(virtualKey: string) {
  return useQuery({
    queryKey: coreApiKeys.videoHistory(virtualKey),
    queryFn: async () => {
      try {
        const response = await apiFetch(`/api/core/videos/generations?virtual_key=${encodeURIComponent(virtualKey)}`, {
          method: 'GET',
          headers: {
            'Content-Type': 'application/json',
          },
        });

        if (!response.ok) {
          const error = await response.json().catch(() => ({ error: 'Failed to fetch video history' }));
          const backendError = { status: response.status, message: error.error || 'Failed to fetch video history' };
          throw BackendErrorHandler.classifyError(backendError);
        }

        return response.json();
      } catch (error: unknown) {
        throw BackendErrorHandler.classifyError(error);
      }
    },
    enabled: !!virtualKey,
    staleTime: 30 * 1000,
    ...BackendErrorHandler.getRetryConfig(),
  });
}

export function useVideoStatus(virtualKey: string, taskId: string) {
  return useQuery({
    queryKey: [...coreApiKeys.videos(), 'status', virtualKey, taskId],
    queryFn: async () => {
      try {
        const response = await apiFetch(`/api/core/videos/generations?virtual_key=${encodeURIComponent(virtualKey)}&task_id=${encodeURIComponent(taskId)}`, {
          method: 'GET',
          headers: {
            'Content-Type': 'application/json',
          },
        });

        if (!response.ok) {
          const error = await response.json().catch(() => ({ error: 'Failed to fetch video status' }));
          const backendError = { status: response.status, message: error.error || 'Failed to fetch video status' };
          throw BackendErrorHandler.classifyError(backendError);
        }

        return response.json();
      } catch (error: unknown) {
        throw BackendErrorHandler.classifyError(error);
      }
    },
    enabled: !!virtualKey && !!taskId,
    refetchInterval: 5000, // Poll every 5 seconds for status updates
    staleTime: 0, // Always fetch fresh status
    ...BackendErrorHandler.getRetryConfig(),
  });
}

// Audio Transcription API
export function useAudioTranscription() {
  const queryClient = useQueryClient();

  return useMutation<unknown, Error, { virtualKey: string; file: File; [key: string]: unknown }>({
    mutationFn: async ({ virtualKey, file, ...options }) => {
      try {
        const formData = new FormData();
        formData.append('virtual_key', virtualKey);
        formData.append('file', file);
        
        // Add other options to form data
        Object.entries(options).forEach(([key, value]) => {
          if (value !== undefined && value !== null) {
            formData.append(key, String(value));
          }
        });

        const response = await apiFetch('/api/core/audio/transcriptions', {
          method: 'POST',
          body: formData,
        });

        if (!response.ok) {
          const error = await response.json().catch(() => ({ error: 'Failed to transcribe audio' }));
          const backendError = { status: response.status, message: error.error || 'Failed to transcribe audio' };
          throw BackendErrorHandler.classifyError(backendError);
        }

        return response.json();
      } catch (error: unknown) {
        throw BackendErrorHandler.classifyError(error);
      }
    },
    onSuccess: (data: unknown, variables: { virtualKey: string }) => {
      queryClient.invalidateQueries({ queryKey: coreApiKeys.audioHistory(variables.virtualKey) });
      notifications.show({
        title: 'Audio Transcribed',
        message: 'Audio has been transcribed successfully',
        color: 'green',
      });
    },
    onError: (error: unknown) => {
      const classifiedError = (error as BackendError).type ? error : BackendErrorHandler.classifyError(error);
      notifications.show({
        title: 'Transcription Error',
        message: BackendErrorHandler.getUserFriendlyMessage(classifiedError as BackendError),
        color: 'red',
      });
    },
    ...BackendErrorHandler.getRetryConfig(),
  });
}

export function useAudioHistory(virtualKey: string) {
  return useQuery({
    queryKey: coreApiKeys.audioHistory(virtualKey),
    queryFn: async () => {
      try {
        const response = await apiFetch(`/api/core/audio/transcriptions?virtual_key=${encodeURIComponent(virtualKey)}`, {
          method: 'GET',
          headers: {
            'Content-Type': 'application/json',
          },
        });

        if (!response.ok) {
          const error = await response.json().catch(() => ({ error: 'Failed to fetch transcription history' }));
          const backendError = { status: response.status, message: error.error || 'Failed to fetch transcription history' };
          throw BackendErrorHandler.classifyError(backendError);
        }

        return response.json();
      } catch (error: unknown) {
        throw BackendErrorHandler.classifyError(error);
      }
    },
    enabled: !!virtualKey,
    staleTime: 30 * 1000,
    ...BackendErrorHandler.getRetryConfig(),
  });
}

// Available Models API
export function useAvailableModels() {
  return useQuery({
    queryKey: [...coreApiKeys.all, 'models'],
    queryFn: async () => {
      try {
        const response = await apiFetch('/api/core/models', {
          method: 'GET',
          headers: {
            'Content-Type': 'application/json',
          },
        });

        if (!response.ok) {
          const error = await response.json().catch(() => ({ error: 'Failed to fetch available models' }));
          const backendError = { status: response.status, message: error.error || 'Failed to fetch available models' };
          throw BackendErrorHandler.classifyError(backendError);
        }

        const data = await response.json();
        // Ensure the response has the expected structure
        if (data && Array.isArray(data.data)) {
          return data.data;
        }
        // Or if it's directly an array
        if (Array.isArray(data)) {
          return data;
        }
        return [];
      } catch (error: unknown) {
        throw BackendErrorHandler.classifyError(error);
      }
    },
    staleTime: 5 * 60 * 1000, // 5 minutes
    ...BackendErrorHandler.getRetryConfig(),
  });
}

// Audio Speech API
export function useAudioSpeech() {
  return useMutation({
    mutationFn: async ({ virtualKey, ...body }: { virtualKey: string; [key: string]: unknown }) => {
      try {
        const response = await apiFetch('/api/core/audio/speech', {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({ virtual_key: virtualKey, ...body }),
        });

        if (!response.ok) {
          const error = await response.json().catch(() => ({ error: 'Failed to generate speech' }));
          const backendError = { status: response.status, message: error.error || 'Failed to generate speech' };
          throw BackendErrorHandler.classifyError(backendError);
        }

        return response.blob();
      } catch (error: unknown) {
        throw BackendErrorHandler.classifyError(error);
      }
    },
    onError: (error: unknown) => {
      const classifiedError = (error as BackendError).type ? error : BackendErrorHandler.classifyError(error);
      notifications.show({
        title: 'Speech Generation Error',
        message: BackendErrorHandler.getUserFriendlyMessage(classifiedError as BackendError),
        color: 'red',
      });
    },
    ...BackendErrorHandler.getRetryConfig(),
  });
}