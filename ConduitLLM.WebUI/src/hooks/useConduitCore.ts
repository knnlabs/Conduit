'use client';

import {
  useChatCompletion as useSDKChatCompletion,
  useChatCompletionStream as useSDKStreamingChatCompletion,
  useImageGeneration as useSDKImageGeneration,
  useVideoGeneration as useSDKVideoGeneration,
  useAudioTranscription as useSDKAudioTranscription,
  useAudioSpeech as useSDKAudioSpeech,
  useModels as useSDKModels,
} from '@knn_labs/conduit-core-client/react-query';
import type { 
  ChatCompletionRequest,
  ChatCompletionMessage,
  ImageGenerationRequest,
  VideoGenerationRequest,
  AudioTranscriptionRequest,
  TextToSpeechRequest,
  TranscriptionModel,
  TextToSpeechModel,
  Voice,
} from '@knn_labs/conduit-core-client';

// Re-export the SDK hooks with WebUI-specific wrappers if needed
export function useChatCompletion() {
  return useSDKChatCompletion();
}

export function useStreamingChatCompletion() {
  return useSDKStreamingChatCompletion();
}

export function useImageGeneration() {
  return useSDKImageGeneration();
}

export function useVideoGeneration() {
  return useSDKVideoGeneration();
}

export function useAudioTranscription() {
  return useSDKAudioTranscription();
}

export function useAudioSpeech() {
  return useSDKAudioSpeech();
}

export function useAvailableModels() {
  return useSDKModels();
}

// Re-export types for convenience
export type {
  ChatCompletionRequest,
  ChatCompletionMessage,
  ImageGenerationRequest,
  VideoGenerationRequest,
  AudioTranscriptionRequest,
  TextToSpeechRequest,
  TranscriptionModel,
  TextToSpeechModel,
  Voice,
};