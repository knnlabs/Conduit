import type { ModelCapabilitiesDto } from '@knn_labs/conduit-admin-client';

/**
 * Determines the primary type of a model based on its capabilities
 */
export function getModelPrimaryType(capabilities?: ModelCapabilitiesDto): string {
  if (!capabilities) return 'Unknown';
  
  // Priority order for determining primary type
  if (capabilities.supportsVideoGeneration) return 'Video';
  if (capabilities.supportsImageGeneration) return 'Image';
  if (capabilities.supportsRealtimeAudio || capabilities.supportsTextToSpeech || capabilities.supportsAudioTranscription) return 'Audio';
  if (capabilities.supportsEmbeddings) return 'Embedding';
  if (capabilities.supportsChat || capabilities.supportsVision) return 'Chat';
  
  return 'Unknown';
}

/**
 * Gets the badge color for a model type
 */
export function getModelTypeBadgeColor(type: string): string {
  switch (type) {
    case 'Chat': return 'blue';
    case 'Image': return 'purple';
    case 'Audio': return 'orange';
    case 'Video': return 'pink';
    case 'Embedding': return 'green';
    default: return 'gray';
  }
}

/**
 * Gets all capabilities of a model as an array of strings
 */
export function getModelCapabilityList(capabilities?: ModelCapabilitiesDto): string[] {
  if (!capabilities) return [];
  
  const capabilityList: string[] = [];
  
  if (capabilities.supportsChat) capabilityList.push('Chat');
  if (capabilities.supportsVision) capabilityList.push('Vision');
  if (capabilities.supportsFunctionCalling) capabilityList.push('Functions');
  if (capabilities.supportsStreaming) capabilityList.push('Streaming');
  if (capabilities.supportsAudioTranscription) capabilityList.push('Transcription');
  if (capabilities.supportsTextToSpeech) capabilityList.push('TTS');
  if (capabilities.supportsRealtimeAudio) capabilityList.push('Realtime Audio');
  if (capabilities.supportsImageGeneration) capabilityList.push('Image Gen');
  if (capabilities.supportsVideoGeneration) capabilityList.push('Video Gen');
  if (capabilities.supportsEmbeddings) capabilityList.push('Embeddings');
  
  return capabilityList;
}