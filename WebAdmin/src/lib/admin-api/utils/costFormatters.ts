/**
 * Cost formatting utilities for model pricing display.
 *
 * These encode Conduit's business rules for displaying costs across
 * different model types (chat, embedding, image, video, audio).
 */

import { ModelType } from '../models/modelType';

/**
 * Get the appropriate label describing the cost unit for a given model type.
 */
export function getCostTypeLabel(modelType: ModelType): string {
  switch (modelType) {
    case ModelType.Chat:
      return 'Input / Output (per million tokens)';
    case ModelType.Embedding:
      return 'Per million tokens';
    case ModelType.Image:
      return 'Per image';
    case ModelType.Video:
      return 'Per second';
    case ModelType.Audio:
      return 'Per minute';
    default:
      return 'Cost';
  }
}
