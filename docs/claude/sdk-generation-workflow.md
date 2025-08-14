# SDK Generation Workflow

## CRITICAL: After ANY API endpoint changes

When you add, remove, or modify ANY API endpoints in Core API or Admin API, you MUST regenerate the SDK types.

### ⚠️ IMPORTANT: NEVER MANUALLY EDIT GENERATED FILES

**DO NOT** manually edit these files:
- `openapi-core.json`
- `openapi-admin.json`
- `core-api.ts` (generated)
- `admin-api.ts` (generated)

These files are **AUTOMATICALLY GENERATED** from the running services. Any manual edits will be lost!

### The Complete Process

1. **Make API changes** (add/remove/modify endpoints in Controllers)
2. **Rebuild and restart the services** to pick up the changes:
   ```bash
   # If using development environment
   docker compose down
   docker compose build --no-cache api admin
   docker compose up -d
   
   # Wait for services to be healthy
   docker compose ps
   ```
3. **Regenerate OpenAPI specs and TypeScript types**:
   ```bash
   # Option 1: Use the convenient wrapper from project root
   ./scripts/generate-api-clients.sh
   
   # Option 2: Run directly from SDK scripts directory
   cd SDKs/Node/scripts
   ./generate-openapi-from-build.sh
   ```

This script will:
- Check if development services are running (starts them if needed)
- Download OpenAPI specs from the RUNNING services (this is why rebuild is critical!)
- Generate TypeScript types for both Core and Admin SDKs
- Format the generated files

### What gets updated AUTOMATICALLY:
- `ConduitLLM.Http/openapi-core.json` - Core API OpenAPI spec (from Swagger)
- `ConduitLLM.Admin/openapi-admin.json` - Admin API OpenAPI spec (from Swagger)
- `SDKs/Node/Core/src/generated/core-api.ts` - Core SDK types
- `SDKs/Node/Admin/src/generated/admin-api.ts` - Admin SDK types

### Manual updates required:
- `SDKs/Node/Core/src/constants/endpoints.ts` - Update Core SDK endpoint constants
- `SDKs/Node/Admin/src/constants.ts` - Update Admin SDK endpoint constants
- Any client service classes that use removed/changed endpoints

### Common scenarios:

#### Moved endpoint from Core to Admin:
1. Remove from Core API controller
2. Add to Admin API controller  
3. Run `./generate-openapi-from-build.sh`
4. Update endpoint constants in both SDKs
5. Update any references in documentation

#### Added new endpoint:
1. Add to appropriate controller
2. Run `./generate-openapi-from-build.sh`
3. Add to endpoint constants
4. Add client methods if needed

### Alternative Scripts Available:

- `./scripts/generate-api-clients.sh` - Convenient wrapper for the main generation script
- `./scripts/fix-sdk-errors.sh` - Comprehensive script that fixes ESLint errors and builds SDKs
- `./scripts/dev-workflow.sh build-sdks` - Modern development workflow for building all SDKs

## Remember: This is a COMMON task we do HUNDREDS of times!