# Docker Development Environment - Best Practices

## The Permission Problem

When developing with Docker and mounted volumes, a common issue is file permission conflicts between the host and container users. This happens because:

1. **Container User**: Docker images typically run as a specific user (e.g., `node` with UID 1001)
2. **Host User**: Your local user has a different UID (usually 1000 on Linux)
3. **File Ownership**: Files created by the container are owned by the container user, causing permission errors

## Standard Solutions

### 1. User ID Mapping (Recommended)
The most common and practical solution is to run the container with your host user's UID/GID:

```bash
# Run with your user ID
UID=$(id -u) GID=$(id -g) docker compose -f docker-compose.yml -f docker-compose.dev.yml up
```

### 2. Anonymous Volumes for node_modules
Always exclude `node_modules` from bind mounts using anonymous volumes:

```yaml
volumes:
  - ./src:/app/src  # Mount source code
  - /app/node_modules  # Exclude node_modules (anonymous volume)
```

This approach:
- Prevents permission conflicts
- Improves performance (native filesystem vs mounted)
- Avoids platform-specific binary issues

### 3. Development-Specific Overrides
Use a separate `docker-compose.dev.yml` file for development overrides:
- User ID mapping
- Volume mounts for hot reloading
- Development environment variables

## Why NOT Other Approaches

### Running as Root (Bad)
- Security risk
- Creates files owned by root on host
- Goes against Docker best practices

### chmod 777 (Bad)
- Major security vulnerability
- Not portable across environments
- Unprofessional solution

### Named Volumes (Limited)
- Good for data persistence
- Bad for development (no hot reloading)
- Still has permission issues

## Industry Standards

Major projects handle this similarly:
- **Next.js**: Uses user ID mapping in their examples
- **Node.js Docker Guide**: Recommends anonymous volumes for node_modules
- **Docker Documentation**: Suggests user namespaces or ID mapping

## Quick Start

```bash
# Stop any running containers
docker compose down

# Clean up
rm -rf ConduitLLM.WebUI/node_modules
rm -rf ConduitLLM.WebUI/.next

# Start with proper user mapping
UID=$(id -u) GID=$(id -g) docker compose -f docker-compose.yml -f docker-compose.dev.yml up
```

This is the standard, professional way to handle Docker development environments.