/**
 * Model Type constants for the WebUI
 * Re-exports from the Admin SDK to maintain compatibility
 * @deprecated - Import directly from @knn_labs/conduit-admin-client instead
 */

import { 
  ModelType,
  MODEL_TYPE_DISPLAY,
  MODEL_TYPE_CAPABILITIES,
  ModelTypeUtils,
  type ModelTypeDisplayInfo,
  type ModelTypeCapabilities
} from '@knn_labs/conduit-admin-client';

// Re-export everything from the SDK for compatibility
export { ModelType };
export { MODEL_TYPE_DISPLAY };
export { MODEL_TYPE_CAPABILITIES };
export { ModelTypeUtils };
export type { ModelTypeDisplayInfo };
export type { ModelTypeCapabilities };

// Convenience function to get model type select options
export const getModelTypeSelectOptions = ModelTypeUtils.getSelectOptions;