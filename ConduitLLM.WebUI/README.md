# ConduitLLM.WebUI

## Overview

**ConduitLLM.WebUI** is the web-based user interface for the ConduitLLM platform, part of the broader [Conduit](../Conduit.sln) solution. It provides an interactive frontend for users to interact with, manage, and visualize LLM (Large Language Model) operations and services hosted by ConduitLLM.

### How It Fits Into the Conduit Solution

The Conduit solution (`Conduit.sln`) is a modular .NET-based system for deploying, managing, and interacting with LLMs. Its key sub-projects include:

- **ConduitLLM.Http**: Exposes RESTful APIs for LLM operations and backend services.
- **ConduitLLM.WebUI**: (this project) Provides a modern web interface for users to interact with the backend, visualize data, and manage LLM tasks.
- **ConduitLLM.Configuration**: Centralizes configuration management for all sub-projects.

The WebUI communicates primarily with the Http project through HTTP/HTTPS endpoints, serving as the main user-facing component.

## Features
- User-friendly web interface for LLM operations
- Visualization of LLM tasks, status, and results
- Management of jobs, settings, and user access (if enabled)
- Secure communication with backend services

## Configuration

### Environment Variables
The WebUI can be configured using environment variables, especially for port and endpoint management:

- `WebUIHttpPort` (default: **5001**): HTTP port for the WebUI
- `WebUIHttpsPort` (default: **5002**): HTTPS port for the WebUI

These are typically set via Docker environment variables or directly when running the application. The application uses these to set the `ASPNETCORE_URLS` variable for Kestrel hosting.

#### Example (Docker Compose):
```yaml
services:
  webui:
    image: ghcr.io/knnlabs/conduit-webui:latest
    environment:
      - WebUIHttpPort=8080
      - WebUIHttpsPort=8443
    ports:
      - "8080:8080"
      - "8443:8443"
```

#### Example (Linux shell):
```sh
export WebUIHttpPort=8080
export WebUIHttpsPort=8443
dotnet run --project ConduitLLM.WebUI
```

### App Settings
Configuration files like `appsettings.json` and `appsettings.Development.json` provide additional options for customizing the WebUI (e.g., logging, API endpoints, feature toggles).

**Note:** The WebUI does not access databases directly. It communicates with the Admin API and HTTP API services for all data operations.

## Building and Running

### Prerequisites
- [.NET 7.0 SDK](https://dotnet.microsoft.com/en-us/download) or newer
- Node.js (if modifying frontend assets)

### Build and Run (Development)
```sh
dotnet build
export WebUIHttpPort=5001 # or your preferred port
export WebUIHttpsPort=5002 # or your preferred port
dotnet run
```

### Docker
The WebUI is Docker-ready. Ports can be mapped using environment variables as described above.

### Accessing the WebUI
Once running, access the interface at:
- `http://localhost:5001` (HTTP)
- `https://localhost:5002` (HTTPS)

Substitute with your configured ports if different.

## Project Structure
- `Controllers/` — API and page controllers
- `Components/` — Blazor/Razor UI components
- `Services/` — Application services and logic
- `wwwroot/` — Static web assets (JS, CSS, images)
- `appsettings.json` — Main configuration file

## Development Notes
- The WebUI is built with ASP.NET Core and Blazor for a modern, responsive experience.
- For local development, ensure backend services (`ConduitLLM.Http`) are running and accessible.
- Ports and endpoints can be freely reconfigured for local or containerized deployments.
- For advanced configuration, see the shared `ConduitLLM.Configuration` project.

## Troubleshooting
- If ports are in use, adjust `WebUIHttpPort`/`WebUIHttpsPort` or stop existing processes using Docker commands (`docker compose down`).
- Logs are output to the console by default; adjust logging in `appsettings.json` as needed.
- When using Docker Compose, use `docker compose logs webui` to view container logs.

## License
See the root of the repository for license information.

---
For more information, see the main [Conduit](../Conduit.sln) solution or contact the maintainers.
