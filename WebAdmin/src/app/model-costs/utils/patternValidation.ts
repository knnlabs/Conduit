/**
 * Re-export model pattern utilities from @knn_labs/conduit-common.
 * All business logic now lives in the SDK for cross-project reuse.
 */
export {
  isValidModelPattern,
  isPatternMatch,
  getPatternExamples,
  validatePatternSyntax,
  normalizeModelPattern,
  getPatternSpecificity
} from '@knn_labs/conduit-common';
