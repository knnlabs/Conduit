// Optimize SDK imports by only importing what we need
// This helps reduce bundle size by tree-shaking unused exports

// Core SDK - Chat hooks
export { useChatCompletion } from '@knn_labs/conduit-core-client/react-query';
export { useChatCompletionStream } from '@knn_labs/conduit-core-client/react-query';

// Core SDK - Model hooks  
export { useModels } from '@knn_labs/conduit-core-client/react-query';

// Core SDK - Embeddings hooks
export { useEmbeddings } from '@knn_labs/conduit-core-client/react-query';

// Core SDK - Image hooks
export { useImageGeneration } from '@knn_labs/conduit-core-client/react-query';

// Core SDK - Audio hooks
export { useAudioTranscription } from '@knn_labs/conduit-core-client/react-query';
export { useAudioTranslation } from '@knn_labs/conduit-core-client/react-query';
export { useAudioSpeech } from '@knn_labs/conduit-core-client/react-query';

// Admin SDK - Provider hooks
export { useProviders } from '@knn_labs/conduit-admin-client/react-query';
export { useProvider } from '@knn_labs/conduit-admin-client/react-query';
export { useCreateProvider } from '@knn_labs/conduit-admin-client/react-query';
export { useUpdateProvider } from '@knn_labs/conduit-admin-client/react-query';
export { useDeleteProvider } from '@knn_labs/conduit-admin-client/react-query';
export { useDiscoverProviderModels } from '@knn_labs/conduit-admin-client/react-query';

// Admin SDK - Virtual Key hooks
export { useVirtualKeys } from '@knn_labs/conduit-admin-client/react-query';
export { useVirtualKey } from '@knn_labs/conduit-admin-client/react-query';
export { useCreateVirtualKey } from '@knn_labs/conduit-admin-client/react-query';
export { useUpdateVirtualKey } from '@knn_labs/conduit-admin-client/react-query';
export { useDeleteVirtualKey } from '@knn_labs/conduit-admin-client/react-query';

// Admin SDK - Model Mapping hooks
export { useModelMappings } from '@knn_labs/conduit-admin-client/react-query';
export { useModelMapping } from '@knn_labs/conduit-admin-client/react-query';
export { useCreateModelMapping } from '@knn_labs/conduit-admin-client/react-query';
export { useUpdateModelMapping } from '@knn_labs/conduit-admin-client/react-query';
export { useDeleteModelMapping } from '@knn_labs/conduit-admin-client/react-query';

// Re-export types we commonly use
export type {
  ChatCompletionRequest,
  ChatCompletionMessage,
  ChatCompletionResponse,
  Model,
} from '@knn_labs/conduit-core-client';