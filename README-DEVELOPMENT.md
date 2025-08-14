# Development Environment Setup

This document describes the canonical way to set up and work with the Conduit development environment.

## Quick Start

### Start Development Environment
```bash
# Start all services with proper permissions
./scripts/start-dev.sh

# With options
./scripts/start-dev.sh --build    # Force rebuild containers
./scripts/start-dev.sh --clean    # Clean existing containers first
./scripts/start-dev.sh --logs     # Show logs after startup
```

### Development Commands
```bash
# Build WebUI
./scripts/dev-workflow.sh build-webui

# Build SDKs
./scripts/dev-workflow.sh build-sdks
./scripts/dev-workflow.sh build-sdk admin

# Linting and Type Checking
./scripts/dev-workflow.sh lint-fix-webui
./scripts/dev-workflow.sh type-check-webui

# Shell Access
./scripts/dev-workflow.sh shell

# Show logs
./scripts/dev-workflow.sh logs
```

## Services Available

After starting the development environment:

- **Core API Swagger**: http://localhost:5000/swagger
- **Admin API Swagger**: http://localhost:5002/swagger  
- **WebUI**: http://localhost:3000
- **RabbitMQ Management**: http://localhost:15672 (conduit/conduitpass)

## Key Features

### Permission-Free Development
- Uses Docker user ID mapping instead of running as root
- No permission issues when Claude modifies files
- No need for `sudo` or permission fixes

### Hot Reloading
- WebUI automatically reloads on file changes
- Source code is mounted directly from host
- Build artifacts cached in Docker volumes for performance

### Development-Friendly APIs
- Swagger UI enabled on both APIs in development mode
- Detailed error messages and stack traces
- CORS configured for local development

## Architecture

The development setup uses:

1. **docker-compose.yml**: Base production configuration
2. **docker-compose.dev.yml**: Development overrides
3. **User ID Mapping**: Containers run as host user (1000:1000)
4. **Volume Mounts**: Source code mounted for hot reloading
5. **Named Volumes**: node_modules cached for performance

## Troubleshooting

### Container Won't Start
```bash
# Check container status
./scripts/dev-workflow.sh status

# View logs
./scripts/dev-workflow.sh logs

# Restart specific service
./scripts/dev-workflow.sh restart-webui
```

### Permission Issues (Legacy)
```bash
# Should not be needed with user mapping, but available if needed
./scripts/dev-workflow.sh fix-permissions
```

### Clean Start
```bash
# Remove all containers and volumes
./scripts/start-dev.sh --clean --build
```

## Development Workflow

1. **Start Environment**: `./scripts/start-dev.sh`
2. **Make Code Changes**: Edit files normally - no permission issues
3. **Build When Needed**: `./scripts/dev-workflow.sh build-webui`
4. **Lint/Test**: `./scripts/dev-workflow.sh lint-fix-webui`
5. **Shell Access**: `./scripts/dev-workflow.sh shell` for debugging

## Why This Approach?

This setup eliminates the common Docker development problem where:
- Container runs as root
- Creates root-owned files in mounted volumes  
- Host user (Claude) can't modify these files

Our solution:
- ✅ Containers run as host user ID
- ✅ No permission conflicts
- ✅ Convenient workflow scripts
- ✅ Follows Docker best practices
- ✅ No ongoing "fixes" needed

## Legacy vs New Approach

**Old Way (Problematic)**:
```bash
docker compose -f docker-compose.yml -f docker-compose.dev.yml up
# Files created as root, permission issues for Claude
```

**New Way (Recommended)**:
```bash
./scripts/start-dev.sh
# Files created as host user, no permission issues
```