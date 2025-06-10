# Docker Development Setup for WebUI

This guide explains how to use the development Docker setup for hot-reload development of the WebUI.

## Quick Start

```bash
# Start development environment with hot reload
docker-compose -f docker-compose.dev.yml up

# Or run in background
docker-compose -f docker-compose.dev.yml up -d
```

## Features

### Hot Reload
- Source code is mounted as volumes
- File changes are detected automatically
- No need to rebuild the container for code changes
- CSS and Razor file changes reload instantly

### Development Settings
- `ASPNETCORE_ENVIRONMENT=Development`
- Detailed error pages enabled
- Debug logging enabled for Blazor components
- All component lifecycle events logged

### Service URLs
- **WebUI**: http://localhost:5001
- **Admin API**: http://localhost:5002
- **Main API**: http://localhost:5000
- **PostgreSQL**: localhost:5432
- **Redis**: localhost:6379
- **Remote Debugger**: localhost:5051

## Debugging

### Visual Studio Code
Add this configuration to `.vscode/launch.json`:

```json
{
    "name": "Docker .NET Attach (WebUI)",
    "type": "coreclr",
    "request": "attach",
    "processId": "${command:pickRemoteProcess}",
    "pipeTransport": {
        "pipeProgram": "docker",
        "pipeArgs": ["exec", "-i", "conduit-webui-dev"],
        "debuggerPath": "/vsdbg/vsdbg",
        "pipeCwd": "${workspaceRoot}"
    },
    "sourceFileMap": {
        "/src": "${workspaceRoot}"
    }
}
```

### Visual Studio
1. Go to Debug → Attach to Process
2. Connection Type: Docker (Linux Container)
3. Select `conduit-webui-dev` container
4. Choose the dotnet process

## Viewing Logs

```bash
# View all logs
docker-compose -f docker-compose.dev.yml logs -f

# View only WebUI logs
docker-compose -f docker-compose.dev.yml logs -f webui-dev

# View with timestamps
docker-compose -f docker-compose.dev.yml logs -f -t webui-dev
```

## Making Changes

1. **Razor Components**: Changes reload automatically
2. **C# Code**: Changes trigger automatic recompilation
3. **CSS/JS**: Changes take effect on page refresh
4. **Static Files**: Changes immediate

## Troubleshooting

### Container won't start
```bash
# Clean rebuild
docker-compose -f docker-compose.dev.yml down
docker-compose -f docker-compose.dev.yml build --no-cache
docker-compose -f docker-compose.dev.yml up
```

### Hot reload not working
1. Check that `DOTNET_USE_POLLING_FILE_WATCHER=true` is set
2. Ensure volumes are mounted correctly:
   ```bash
   docker exec conduit-webui-dev ls -la /src/ConduitLLM.WebUI
   ```

### Database issues
```bash
# Reset database
docker-compose -f docker-compose.dev.yml down -v
docker-compose -f docker-compose.dev.yml up
```

### Port conflicts
If ports are already in use, modify the port mappings in `docker-compose.dev.yml`:
```yaml
ports:
  - "5001:8080"  # Change 5001 to another port
```

## Performance Tips

1. **Exclude node_modules**: Already handled in volume mounts
2. **Use .dockerignore**: Prevents copying unnecessary files
3. **Resource limits**: Add if needed:
   ```yaml
   deploy:
     resources:
       limits:
         cpus: '2'
         memory: 2G
   ```

## Integration with Production

The development setup uses the same service structure as production:
- Same environment variables
- Same service dependencies
- Same networking

To test production builds locally:
```bash
# Use regular docker-compose.yml
docker-compose up --build
```

## Custom Development Settings

Add to `appsettings.Development.json` in WebUI:
```json
{
  "DetailedErrors": true,
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore.Components": "Debug",
      "ConduitLLM": "Debug"
    }
  }
}
```

## Useful Commands

```bash
# View container details
docker ps

# Enter container shell
docker exec -it conduit-webui-dev /bin/bash

# View file changes in real-time
docker exec conduit-webui-dev find /src -name "*.cs" -o -name "*.razor" | \
  xargs docker exec conduit-webui-dev inotifywait -m

# Clean everything
docker-compose -f docker-compose.dev.yml down -v --remove-orphans
```