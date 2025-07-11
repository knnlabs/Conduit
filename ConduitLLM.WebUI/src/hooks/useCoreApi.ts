'use client';

import { useState, useCallback } from 'react';
import { notifications } from '@mantine/notifications';

interface ImageGenerationRequest {
  prompt: string;
  model?: string;
  size?: string;
  quality?: string;
  n?: number;
}

interface VideoGenerationRequest {
  prompt: string;
  model?: string;
  duration?: number;
  resolution?: string;
}

interface AudioTranscriptionRequest {
  file: File;
  model?: string;
  language?: string;
  prompt?: string;
}

interface TextToSpeechRequest {
  text: string;
  model?: string;
  voice?: string;
  speed?: number;
}

export function useCoreApi() {
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const generateImage = useCallback(async (data: ImageGenerationRequest): Promise<any> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const response = await fetch('/api/images/generate', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(data),
      });

      const result = await response.json();

      if (!response.ok) {
        throw new Error(result.error || 'Image generation failed');
      }

      notifications.show({
        title: 'Success',
        message: 'Image generated successfully',
        color: 'green',
      });

      return result;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Image generation failed';
      setError(message);
      notifications.show({
        title: 'Error',
        message,
        color: 'red',
      });
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const generateVideo = useCallback(async (data: VideoGenerationRequest): Promise<any> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const response = await fetch('/api/videos/generate', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(data),
      });

      const result = await response.json();

      if (!response.ok) {
        throw new Error(result.error || 'Video generation failed');
      }

      notifications.show({
        title: 'Success',
        message: 'Video generation started',
        color: 'green',
      });

      return result;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Video generation failed';
      setError(message);
      notifications.show({
        title: 'Error',
        message,
        color: 'red',
      });
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const transcribeAudio = useCallback(async (data: AudioTranscriptionRequest): Promise<any> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const formData = new FormData();
      formData.append('file', data.file);
      if (data.model) formData.append('model', data.model);
      if (data.language) formData.append('language', data.language);
      if (data.prompt) formData.append('prompt', data.prompt);

      const response = await fetch('/api/audio/transcribe', {
        method: 'POST',
        body: formData,
      });

      const result = await response.json();

      if (!response.ok) {
        throw new Error(result.error || 'Audio transcription failed');
      }

      notifications.show({
        title: 'Success',
        message: 'Audio transcribed successfully',
        color: 'green',
      });

      return result;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Audio transcription failed';
      setError(message);
      notifications.show({
        title: 'Error',
        message,
        color: 'red',
      });
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const generateSpeech = useCallback(async (data: TextToSpeechRequest): Promise<Blob> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const response = await fetch('/api/audio/speech', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(data),
      });

      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.error || 'Speech generation failed');
      }

      const blob = await response.blob();

      notifications.show({
        title: 'Success',
        message: 'Speech generated successfully',
        color: 'green',
      });

      return blob;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Speech generation failed';
      setError(message);
      notifications.show({
        title: 'Error',
        message,
        color: 'red',
      });
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const chatCompletion = useCallback(async (messages: any[], options?: any): Promise<any> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const response = await fetch('/api/chat/completions', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          messages,
          ...options,
        }),
      });

      const result = await response.json();

      if (!response.ok) {
        throw new Error(result.error || 'Chat completion failed');
      }

      return result;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Chat completion failed';
      setError(message);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  return {
    generateImage,
    generateVideo,
    transcribeAudio,
    generateSpeech,
    chatCompletion,
    isLoading,
    error,
  };
}