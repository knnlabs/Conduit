// Type augmentation for admin client
// This file adds type information for methods that exist in the SDK
// but may not have proper TypeScript declarations exported

import '@knn_labs/conduit-admin-client';

declare module '@knn_labs/conduit-admin-client' {
  // The SDK should already have all these services properly typed
  // This file can be removed once we verify the SDK exports are working correctly
}

// Note: This file is kept as a placeholder in case we need to augment types in the future