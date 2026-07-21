/**
 * Re-export model pattern utilities from the local shared utilities.
 * All business logic now lives in the SDK for cross-project reuse.
 */
export {
  isValidModelPattern,
  isPatternMatch,
  getPatternExamples,
  validatePatternSyntax,
  normalizeModelPattern,
  getPatternSpecificity
} from '@/lib/conduit-common';
