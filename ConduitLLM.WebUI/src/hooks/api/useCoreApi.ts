'use client';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { notifications } from '@mantine/notifications';
import { BackendErrorHandler } from '@/lib/errors/BackendErrorHandler';

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
    mutationFn: async ({ virtualKey, ...body }: { virtualKey: string; [key: string]: any }) => {
      const response = await fetch('/api/core/chat/completions', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ virtual_key: virtualKey, ...body }),
      });

      if (!response.ok) {
        const error = await response.json().catch(() => ({ error: 'Failed to create chat completion' }));
        throw new Error(error.error || 'Failed to create chat completion');
      }

      return response.json();
    },
    onError: (error: any) => {
      notifications.show({
        title: 'Chat Error',
        message: error.message || 'Failed to create chat completion',
        color: 'red',
      });
    },
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
      onChunk: (chunk: any) => void;
      [key: string]: any;
    }) => {
      const response = await fetch('/api/core/chat/completions', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ virtual_key: virtualKey, stream: true, ...body }),
      });

      if (!response.ok) {
        const error = await response.json().catch(() => ({ error: 'Failed to create streaming chat completion' }));
        throw new Error(error.error || 'Failed to create streaming chat completion');
      }

      if (!response.body) {
        throw new Error('No response body for streaming');
      }

      const reader = response.body.getReader();
      const decoder = new TextDecoder();

      try {
        while (true) {
          const { done, value } = await reader.read();
          if (done) break;

          const chunk = decoder.decode(value);
          const lines = chunk.split('\n');

          for (const line of lines) {
            if (line.startsWith('data: ')) {
              const data = line.slice(6);
              if (data === '[DONE]') {
                return;
              }
              try {
                const parsed = JSON.parse(data);
                onChunk(parsed);
              } catch {
                // Skip invalid JSON
              }
            }
          }
        }
      } finally {
        reader.releaseLock();
      }
    },
    onError: (error: any) => {
      notifications.show({
        title: 'Streaming Chat Error',
        message: error.message || 'Failed to create streaming chat completion',
        color: 'red',
      });
    },
  });
}

// Image Generation API
export function useImageGeneration() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ virtualKey, ...body }: { virtualKey: string; [key: string]: any }) => {
      const response = await fetch('/api/core/images/generations', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ virtual_key: virtualKey, ...body }),
      });

      if (!response.ok) {
        const error = await response.json().catch(() => ({ error: 'Failed to generate image' }));
        throw new Error(error.error || 'Failed to generate image');
      }

      return response.json();
    },
    onSuccess: (data, variables) => {
      queryClient.invalidateQueries({ queryKey: coreApiKeys.imageHistory(variables.virtualKey) });
      notifications.show({
        title: 'Image Generated',
        message: 'Image has been generated successfully',
        color: 'green',
      });
    },
    onError: (error: any) => {
      notifications.show({
        title: 'Image Generation Error',
        message: error.message || 'Failed to generate image',
        color: 'red',
      });
    },
  });
}

export function useImageHistory(virtualKey: string) {
  return useQuery({
    queryKey: coreApiKeys.imageHistory(virtualKey),
    queryFn: async () => {
      const response = await fetch(`/api/core/images/generations?virtual_key=${encodeURIComponent(virtualKey)}`, {
        method: 'GET',
        headers: {
          'Content-Type': 'application/json',
        },
      });

      if (!response.ok) {
        const error = await response.json().catch(() => ({ error: 'Failed to fetch image history' }));
        throw new Error(error.error || 'Failed to fetch image history');
      }

      return response.json();
    },
    enabled: !!virtualKey,
    staleTime: 30 * 1000,
  });
}

// Video Generation API
export function useVideoGeneration() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ virtualKey, ...body }: { virtualKey: string; [key: string]: any }) => {
      const response = await fetch('/api/core/videos/generations', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ virtual_key: virtualKey, ...body }),
      });

      if (!response.ok) {
        const error = await response.json().catch(() => ({ error: 'Failed to generate video' }));
        throw new Error(error.error || 'Failed to generate video');
      }

      return response.json();
    },
    onSuccess: (data, variables) => {
      queryClient.invalidateQueries({ queryKey: coreApiKeys.videoHistory(variables.virtualKey) });
      notifications.show({
        title: 'Video Generation Started',
        message: 'Video generation has been initiated successfully',
        color: 'green',
      });
    },
    onError: (error: any) => {
      notifications.show({
        title: 'Video Generation Error',
        message: error.message || 'Failed to generate video',
        color: 'red',
      });
    },
  });
}

export function useVideoHistory(virtualKey: string) {
  return useQuery({
    queryKey: coreApiKeys.videoHistory(virtualKey),
    queryFn: async () => {
      const response = await fetch(`/api/core/videos/generations?virtual_key=${encodeURIComponent(virtualKey)}`, {
        method: 'GET',
        headers: {
          'Content-Type': 'application/json',
        },
      });

      if (!response.ok) {
        const error = await response.json().catch(() => ({ error: 'Failed to fetch video history' }));
        throw new Error(error.error || 'Failed to fetch video history');
      }

      return response.json();
    },
    enabled: !!virtualKey,
    staleTime: 30 * 1000,
  });
}

export function useVideoStatus(virtualKey: string, taskId: string) {
  return useQuery({
    queryKey: [...coreApiKeys.videos(), 'status', virtualKey, taskId],
    queryFn: async () => {
      const response = await fetch(`/api/core/videos/generations?virtual_key=${encodeURIComponent(virtualKey)}&task_id=${encodeURIComponent(taskId)}`, {
        method: 'GET',
        headers: {
          'Content-Type': 'application/json',
        },
      });

      if (!response.ok) {
        const error = await response.json().catch(() => ({ error: 'Failed to fetch video status' }));
        throw new Error(error.error || 'Failed to fetch video status');
      }

      return response.json();
    },
    enabled: !!virtualKey && !!taskId,
    refetchInterval: 5000, // Poll every 5 seconds for status updates
    staleTime: 0, // Always fetch fresh status
  });
}

// Audio Transcription API
export function useAudioTranscription() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ virtualKey, file, ...options }: { virtualKey: string; file: File; [key: string]: any }) => {
      const formData = new FormData();
      formData.append('virtual_key', virtualKey);
      formData.append('file', file);
      
      // Add other options to form data
      Object.entries(options).forEach(([key, value]) => {
        if (value !== undefined && value !== null) {
          formData.append(key, String(value));
        }
      });

      const response = await fetch('/api/core/audio/transcriptions', {
        method: 'POST',
        body: formData,
      });

      if (!response.ok) {
        const error = await response.json().catch(() => ({ error: 'Failed to transcribe audio' }));
        throw new Error(error.error || 'Failed to transcribe audio');
      }

      return response.json();
    },
    onSuccess: (data, variables) => {
      queryClient.invalidateQueries({ queryKey: coreApiKeys.audioHistory(variables.virtualKey) });
      notifications.show({
        title: 'Audio Transcribed',
        message: 'Audio has been transcribed successfully',
        color: 'green',
      });
    },
    onError: (error: any) => {
      notifications.show({
        title: 'Transcription Error',
        message: error.message || 'Failed to transcribe audio',
        color: 'red',
      });
    },
  });
}

export function useAudioHistory(virtualKey: string) {
  return useQuery({
    queryKey: coreApiKeys.audioHistory(virtualKey),
    queryFn: async () => {
      const response = await fetch(`/api/core/audio/transcriptions?virtual_key=${encodeURIComponent(virtualKey)}`, {
        method: 'GET',
        headers: {
          'Content-Type': 'application/json',
        },
      });

      if (!response.ok) {
        const error = await response.json().catch(() => ({ error: 'Failed to fetch transcription history' }));
        throw new Error(error.error || 'Failed to fetch transcription history');
      }

      return response.json();
    },
    enabled: !!virtualKey,
    staleTime: 30 * 1000,
  });
}

// Available Models API
export function useAvailableModels() {
  return useQuery({
    queryKey: [...coreApiKeys.all, 'models'],
    queryFn: async () => {
      const response = await fetch('/api/core/models', {
        method: 'GET',
        headers: {
          'Content-Type': 'application/json',
        },
      });

      if (!response.ok) {
        const error = await response.json().catch(() => ({ error: 'Failed to fetch available models' }));
        throw new Error(error.error || 'Failed to fetch available models');
      }

      return response.json();
    },
    staleTime: 5 * 60 * 1000, // 5 minutes
  });
}

// Audio Speech API
export function useAudioSpeech() {
  return useMutation({
    mutationFn: async ({ virtualKey, ...body }: { virtualKey: string; [key: string]: any }) => {
      const response = await fetch('/api/core/audio/speech', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ virtual_key: virtualKey, ...body }),
      });

      if (!response.ok) {
        const error = await response.json().catch(() => ({ error: 'Failed to generate speech' }));
        throw new Error(error.error || 'Failed to generate speech');
      }

      return response.blob();
    },
    onError: (error: any) => {
      notifications.show({
        title: 'Speech Generation Error',
        message: error.message || 'Failed to generate speech',
        color: 'red',
      });
    },
  });
}