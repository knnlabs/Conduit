# SDK React Query Module Resolution Issue

## Problem
When attempting to use the React Query exports from the Conduit SDKs (`@knn_labs/conduit-core-client/react-query` and `@knn_labs/conduit-admin-client/react-query`) in the WebUI, webpack fails to resolve the `@tanstack/react-query` dependency, even though it's installed in the WebUI's package.json.

## Error
```
Module not found: Can't resolve '@tanstack/react-query'
```

## Root Cause
The issue occurs when using local file dependencies (`file:../SDKs/Node/Core`) with tsup bundling. The bundled output references peer dependencies that webpack can't resolve properly in the Next.js build process.

## Current Workaround
The `ConduitProviders` component has been temporarily disabled, and the WebUI continues to use its existing API route hooks instead of the SDK React Query hooks.

## Potential Solutions

### 1. Publish SDKs to npm
Publishing the SDKs to npm would resolve the module resolution issues as npm handles peer dependencies correctly.

### 2. Bundle peer dependencies
Configure tsup to bundle React and React Query instead of marking them as external:
```typescript
// tsup.config.ts
export default defineConfig({
  // Remove 'react' and '@tanstack/react-query' from external
  external: [],
});
```

### 3. Use Next.js transpilePackages
Configure Next.js to transpile the SDK packages:
```javascript
// next.config.js
module.exports = {
  transpilePackages: [
    '@knn_labs/conduit-core-client',
    '@knn_labs/conduit-admin-client',
  ],
};
```

### 4. Webpack alias configuration
Add webpack aliases to resolve the peer dependencies:
```javascript
// next.config.js
module.exports = {
  webpack: (config) => {
    config.resolve.alias = {
      ...config.resolve.alias,
      '@tanstack/react-query': require.resolve('@tanstack/react-query'),
      'react': require.resolve('react'),
    };
    return config;
  },
};
```

## Files Affected
- `/ConduitLLM.WebUI/src/lib/providers/ConduitProviders.tsx` - Temporarily disabled
- `/ConduitLLM.WebUI/src/app/chat/page.tsx` - Reverted to use existing hooks
- All other pages that would use Core SDK hooks

## Next Steps
1. Try solution #3 (transpilePackages) first as it's the least invasive
2. If that doesn't work, try solution #4 (webpack aliases)
3. Consider publishing to npm if local development continues to have issues
4. Once resolved, re-enable `ConduitProviders` and migrate all pages to use SDK hooks