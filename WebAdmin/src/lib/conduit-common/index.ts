/**
 * the local shared utilities - Shared types for Conduit SDK clients
 */

// Base types
export * from "./types/base";

// Capability types
export * from "./types/capabilities";

// Error types and utilities
export * from "./errors";

// HTTP types and utilities
export * from "./http";

// Client configuration types
export * from "./client";

// Value-level export of the HttpError class (the client barrel re-exports it type-only)
export { HttpError } from "./client/types";

// Formatting utilities
export * from "./formatting";

// Validation utilities (type guards, model patterns, form validators)
export * from "./validation";
