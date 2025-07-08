export const conduitQueryKeys = {
  all: ['conduit'] as const,
  
  // Models
  models: () => [...conduitQueryKeys.all, 'models'] as const,
  
  // Chat
  chat: () => [...conduitQueryKeys.all, 'chat'] as const,
  chatCompletions: () => [...conduitQueryKeys.chat(), 'completions'] as const,
  
  // Images
  images: () => [...conduitQueryKeys.all, 'images'] as const,
  imageGenerations: () => [...conduitQueryKeys.images(), 'generations'] as const,
  
  // Audio
  audio: () => [...conduitQueryKeys.all, 'audio'] as const,
  audioTranscriptions: () => [...conduitQueryKeys.audio(), 'transcriptions'] as const,
  audioTranslations: () => [...conduitQueryKeys.audio(), 'translations'] as const,
  audioSpeech: () => [...conduitQueryKeys.audio(), 'speech'] as const,
  
  // Videos
  videos: () => [...conduitQueryKeys.all, 'videos'] as const,
  videoGenerations: () => [...conduitQueryKeys.videos(), 'generations'] as const,
  
  // Embeddings
  embeddings: () => [...conduitQueryKeys.all, 'embeddings'] as const,
  
  // Moderations
  moderations: () => [...conduitQueryKeys.all, 'moderations'] as const,
} as const;