import type { ModelDto } from '@/lib/admin-api';

/**
 * Determines the primary type of a model based on its capabilities
 */
export function getModelPrimaryType(model?: ModelDto): string {
  if (!model) return 'Unknown';
  
  // Priority order for determining primary type
  if (model.supportsVideoGeneration) return 'Video';
  if (model.supportsImageGeneration) return 'Image';
  if (model.supportsEmbeddings) return 'Embedding';
  if (model.supportsChat || model.supportsImageInput || model.supportsVideoInput) return 'Chat';
  
  return 'Unknown';
}

/**
 * Gets the badge color for a model type
 */
export function getModelTypeBadgeColor(type: string): string {
  switch (type) {
    case 'Chat': return 'blue';
    case 'Image': return 'purple';
    case 'Video': return 'pink';
    case 'Embedding': return 'green';
    default: return 'gray';
  }
}

/**
 * Gets all capabilities of a model as an array of strings
 */
export function getModelCapabilityList(model?: ModelDto): string[] {
  if (!model) return [];
  
  const capabilityList: string[] = [];
  
  if (model.supportsChat) capabilityList.push('Chat');
  if (model.supportsImageInput) capabilityList.push('Image Input');
  if (model.supportsVideoInput) capabilityList.push('Video Input');
  if (model.supportsAudioInput) capabilityList.push('Audio Input');
  if (model.supportsFileInput) capabilityList.push('File Input');
  if (model.supportsFunctionCalling) capabilityList.push('Functions');
  if (model.supportsStreaming) capabilityList.push('Streaming');
  if (model.supportsImageGeneration) capabilityList.push('Image Gen');
  if (model.supportsVideoGeneration) capabilityList.push('Video Gen');
  if (model.supportsEmbeddings) capabilityList.push('Embeddings');
  
  return capabilityList;
}
