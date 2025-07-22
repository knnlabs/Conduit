# @knn_labs/conduit-common

Shared types and utilities for Conduit SDK clients.

## Overview

This package contains common TypeScript types and utility functions used by both the Admin and Core SDK clients. It eliminates type duplication and ensures consistency across all Conduit client packages.

## Installation

This package is not published to npm and is used locally within the Conduit monorepo:

```json
{
  "dependencies": {
    "@knn_labs/conduit-common": "file:../Common"
  }
}
```

## Included Types

### Base Types
- `PaginatedResponse<T>` - Standard paginated response format
- `PagedResponse<T>` - Alternative paged response format
- `ErrorResponse` - Standard error response structure
- `ApiResponse<T>` - Generic API response wrapper
- `SortDirection` - Sort direction enum ('asc' | 'desc')
- `SortOptions` - Sorting configuration
- `FilterOptions` - Filtering and pagination options
- `DateRange` - Date range specification
- `HttpMethod` - HTTP method types
- `RequestOptions` - Request configuration options
- `Usage` - Token usage tracking
- `PerformanceMetrics` - Performance measurement data

### Pagination Types
- `PaginationParams` - Basic pagination parameters
- `SearchParams` - Search with pagination
- `TimeRangeParams` - Time-based filtering
- `BatchOperationParams` - Batch operation configuration

### Model Capabilities
- `ModelCapability` - Enum of all supported model capabilities
- `ModelCapabilityInfo` - Capability metadata
- `ModelCapabilities` - Model capability definition
- `ModelConstraints` - Model-specific constraints
- `getCapabilityDisplayName()` - Get human-readable capability names
- `getCapabilityCategory()` - Get capability category

## Usage

### Importing Types

```typescript
import type { 
  PaginatedResponse, 
  ErrorResponse, 
  ModelCapability 
} from '@knn_labs/conduit-common';
```

### Using Model Capabilities

```typescript
import { ModelCapability, getCapabilityDisplayName } from '@knn_labs/conduit-common';

// Check capability
if (capability === ModelCapability.CHAT) {
  console.log('Chat model');
}

// Get display name
const displayName = getCapabilityDisplayName(ModelCapability.IMAGE_GENERATION);
// Returns: "Image Generation"
```

## Development

To build the package:

```bash
npm run build
```

To watch for changes:

```bash
npm run dev
```

## Type Safety

When re-exporting types from this package, use `export type` to ensure proper TypeScript compilation:

```typescript
// Correct
export type { PaginatedResponse, ErrorResponse } from '@knn_labs/conduit-common';

// Also correct for type aliases and enums
export { type SortDirection, ModelCapability } from '@knn_labs/conduit-common';
```