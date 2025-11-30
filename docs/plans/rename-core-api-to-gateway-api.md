# Plan: Rename "Core API" to "Gateway API"

## Overview

This plan covers renaming all references from "Core API" to "Gateway API" to eliminate confusion with the `ConduitLLM.Core` shared library and maintain consistency with the `ConduitLLM.Gateway` project name.

**Rationale**:
- "Core API" is ambiguous because `ConduitLLM.Core` is a shared library containing domain models and interfaces
- "Gateway API" accurately describes the service's role as an API gateway
- Maintains consistency with the `ConduitLLM.Gateway` project rename

## Impact Summary

| Category | Estimated Count | Description |
|----------|-----------------|-------------|
| Documentation | ~50+ | Markdown files with "Core API" references |
| Scripts | ~10 | Shell scripts with CORE_API variables |
| Docker configs | ~5 | Service names, environment variables |
| SDK packages | 3 | Package name, descriptions, generated types |
| OpenAPI specs | 2 | Filename and generated TypeScript |
| Environment vars | ~20 | CORE_API_* variable names |
| Code comments | ~50+ | Inline documentation |

**Total estimated references: ~413**

## Breaking Changes

### NPM Package Rename (BREAKING)
- Current: `@knn_labs/conduit-core`
- Proposed: `@knn_labs/conduit-gateway`
- **Impact**: All npm users will need to update their dependencies

### Environment Variable Rename
- `CORE_API_URL` → `GATEWAY_API_URL`
- `CORE_API_PORT` → `GATEWAY_API_PORT`
- `CONDUIT_API_BASE_URL` (keep as-is, already generic)

## Pre-Requisites

1. Ensure the `ConduitLLM.Http` → `ConduitLLM.Gateway` rename is complete and merged
2. Create a feature branch: `git checkout -b refactor/rename-core-api-to-gateway-api`
3. Plan npm deprecation strategy for old package name

---

## Phase 1: SDK Package Rename

### Step 1.1: Update SDK package.json files

**File: `SDKs/Node/Core/package.json`**
```json
// Change:
"name": "@knn_labs/conduit-core"
"description": "... Core API ..."

// To:
"name": "@knn_labs/conduit-gateway"
"description": "... Gateway API ..."
```

### Step 1.2: Rename SDK directory
```bash
git mv SDKs/Node/Core SDKs/Node/Gateway
```

### Step 1.3: Update SDK internal references
- Update imports in `SDKs/Node/Gateway/src/**/*.ts`
- Update references in `SDKs/Node/scripts/package.json`

### Step 1.4: Update WebAdmin SDK dependencies
**File: `WebAdmin/package.json`**
```json
// Change:
"@knn_labs/conduit-core": "..."

// To:
"@knn_labs/conduit-gateway": "..."
```

---

## Phase 2: OpenAPI and Generated Types

### Step 2.1: Rename OpenAPI spec file
```bash
git mv Services/ConduitLLM.Gateway/openapi-core.json Services/ConduitLLM.Gateway/openapi-gateway.json
git mv Services/ConduitLLM.Gateway/openapi-core.yaml Services/ConduitLLM.Gateway/openapi-gateway.yaml
```

### Step 2.2: Update generated TypeScript filename
**File: `SDKs/Node/scripts/package.json`**
```json
// Change all references:
"core-api.ts" → "gateway-api.ts"
"openapi-core.json" → "openapi-gateway.json"
```

### Step 2.3: Update SDK imports
All files importing from `generated/core-api.ts` need updating to `generated/gateway-api.ts`

---

## Phase 3: Docker Configuration

### Step 3.1: Update docker-compose service names (Optional)
**Decision needed**: Keep `api` service name or rename to `gateway`?

If renaming:
**Files: `docker-compose.yml`, `docker-compose.dev.yml`**
```yaml
# Change:
services:
  api:
    ...

# To:
services:
  gateway:
    ...
```

### Step 3.2: Update SDK codegen docker-compose
**File: `SDKs/Node/scripts/docker-compose.codegen.yml`**
```yaml
# Change:
core-api:
  ...
CORE_API_URL: http://core-api:5000

# To:
gateway-api:
  ...
GATEWAY_API_URL: http://gateway-api:5000
```

---

## Phase 4: Environment Variables

### Step 4.1: Update shell scripts
**Files to update:**
- `SDKs/Node/scripts/generate-openapi-from-build.sh`
  - `CORE_API_PORT` → `GATEWAY_API_PORT`
  - All "Core API" log messages → "Gateway API"

### Step 4.2: Update documentation for env vars
Document the new environment variable names and provide migration guidance.

---

## Phase 5: Documentation Updates

### Step 5.1: Bulk find/replace in markdown
```bash
find /path/to/Conduit -name "*.md" -exec sed -i 's/Core API/Gateway API/g' {} \;
```

### Step 5.2: Key documentation files to review manually
- `README.md`
- `CLAUDE.md`
- `docs/api-guides/core/` → rename to `docs/api-guides/gateway/`
- `docs/development/API-PATTERNS-BEST-PRACTICES.md`
- All files in `docs/api-guides/`

### Step 5.3: Update CLAUDE.md service descriptions
```markdown
# Change:
- 📚 **Core API Swagger**: http://localhost:5000/swagger

# To:
- 📚 **Gateway API Swagger**: http://localhost:5000/swagger
```

---

## Phase 6: Code Comments and Log Messages

### Step 6.1: Update C# log messages
Search and replace in `Services/ConduitLLM.Gateway/**/*.cs`:
```csharp
// Change:
Console.WriteLine("[Conduit API] ...");
// or
Console.WriteLine("[Core API] ...");

// To:
Console.WriteLine("[Gateway API] ...");
```

### Step 6.2: Update inline documentation
Review and update XML doc comments that reference "Core API"

---

## Phase 7: WebAdmin Updates

### Step 7.1: Update client creation functions
**File: `WebAdmin/src/lib/api/clients.ts`** (or similar)
- Update function names if they reference "core"
- Update comments

### Step 7.2: Update UI labels (if any)
Search WebAdmin for any user-facing "Core API" text

---

## Phase 8: Verification

### Step 8.1: Search for remaining references
```bash
grep -ri "core.api\|core api\|coreapi\|CORE_API" \
  --include="*.cs" --include="*.ts" --include="*.tsx" \
  --include="*.json" --include="*.yml" --include="*.sh" \
  --include="*.md" | grep -v node_modules | grep -v bin | grep -v obj
```

### Step 8.2: Build verification
```bash
dotnet build
cd WebAdmin && npm run lint && npm run type-check
cd SDKs/Node && npm run build
```

### Step 8.3: Test SDK generation
```bash
cd SDKs/Node/scripts
./generate-openapi-from-build.sh
```

---

## Phase 9: NPM Package Migration

### Step 9.1: Deprecate old package
```bash
cd SDKs/Node/Core  # before rename
npm deprecate @knn_labs/conduit-core "This package has been renamed to @knn_labs/conduit-gateway"
```

### Step 9.2: Publish new package
```bash
cd SDKs/Node/Gateway
npm publish --access public
```

### Step 9.3: Update release workflow
**File: `.github/workflows/release.yml`**
Update all references to Core SDK paths

---

## Rollback Plan

If issues arise:
```bash
git checkout dev
git branch -D refactor/rename-core-api-to-gateway-api
```

For npm: The old package can remain available (deprecated) indefinitely.

---

## Migration Guide for Users

### NPM Package Migration
```bash
# Remove old package
npm uninstall @knn_labs/conduit-core

# Install new package
npm install @knn_labs/conduit-gateway
```

### Code Updates
```typescript
// Before
import { ConduitCoreClient } from '@knn_labs/conduit-core';

// After
import { ConduitGatewayClient } from '@knn_labs/conduit-gateway';
```

### Environment Variables
```bash
# Before
CORE_API_URL=http://localhost:5000

# After
GATEWAY_API_URL=http://localhost:5000
```

---

## Notes

- Consider keeping backward compatibility aliases for environment variables during a transition period
- The docker-compose service name `api` is already generic and may not need changing
- Coordinate with any external documentation or wikis
- Update any CI/CD pipelines that reference the old names
