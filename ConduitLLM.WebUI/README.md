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
- **Enterprise Security Features**:
  - IP address filtering (whitelist/blacklist with CIDR support)
  - Rate limiting to prevent DoS attacks
  - Failed login protection with automatic IP banning
  - Security headers (X-Frame-Options, CSP, HSTS, etc.)
  - Private/intranet IP detection and handling
  - Distributed security tracking with Redis
- **Security Dashboard** at `/security` for real-time monitoring

## Configuration

### Environment Variables
The WebUI can be configured using environment variables for port management, security, and other features:

#### Port Configuration
- `WebUIHttpPort` (default: **5001**): HTTP port for the WebUI
- `WebUIHttpsPort` (default: **5002**): HTTPS port for the WebUI

#### Authentication
- `CONDUIT_MASTER_KEY`: Master key for system administration
- `CONDUIT_WEBUI_AUTH_KEY`: Dedicated authentication key for WebUI access (recommended)

#### Security Configuration
- **IP Filtering**:
  - `CONDUIT_IP_FILTERING_ENABLED`: Enable/disable IP filtering (true/false)
  - `CONDUIT_IP_FILTER_MODE`: Filter mode ("permissive" or "restrictive")
  - `CONDUIT_IP_FILTER_ALLOW_PRIVATE`: Auto-allow private/intranet IPs (true/false)
  - `CONDUIT_IP_FILTER_WHITELIST`: Comma-separated allowed IPs/CIDRs
  - `CONDUIT_IP_FILTER_BLACKLIST`: Comma-separated blocked IPs/CIDRs

- **Rate Limiting**:
  - `CONDUIT_RATE_LIMITING_ENABLED`: Enable rate limiting (true/false)
  - `CONDUIT_RATE_LIMIT_MAX_REQUESTS`: Max requests per window (default: 100)
  - `CONDUIT_RATE_LIMIT_WINDOW_SECONDS`: Time window in seconds (default: 60)

- **Failed Login Protection**:
  - `CONDUIT_MAX_FAILED_ATTEMPTS`: Max failed logins before ban (default: 5)
  - `CONDUIT_IP_BAN_DURATION_MINUTES`: Ban duration in minutes (default: 30)
  - `CONDUIT_SECURITY_USE_DISTRIBUTED_TRACKING`: Use Redis for distributed tracking

- **Security Headers**:
  - `CONDUIT_SECURITY_HEADERS_X_FRAME_OPTIONS`: X-Frame-Options value (default: "DENY")
  - `CONDUIT_SECURITY_HEADERS_CSP`: Content Security Policy
  - `CONDUIT_SECURITY_HEADERS_HSTS_MAX_AGE`: HSTS max age in seconds

These are typically set via Docker environment variables or directly when running the application.

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

## Security

The WebUI includes comprehensive security features to protect your deployment:

### Authentication
- Uses either `CONDUIT_WEBUI_AUTH_KEY` (recommended) or `CONDUIT_MASTER_KEY` for access
- Session-based authentication with configurable timeout
- Automatic logout on inactivity

### IP Filtering
- Whitelist or blacklist specific IP addresses or CIDR subnets
- Automatic detection and handling of private/intranet IPs
- Can be configured via environment variables or Admin API

### Rate Limiting
- Protects against DoS attacks and API abuse
- Configurable per-IP request limits
- Sliding window algorithm with Redis support

### Security Dashboard
Access the security dashboard at `/security` (requires authentication) to:
- Monitor active IP filters
- View failed login attempts
- Manage banned IPs
- Check security configuration
- See real-time security metrics

### Best Practices
1. Always use HTTPS in production (handled by reverse proxy)
2. Set strong, unique values for `CONDUIT_WEBUI_AUTH_KEY`
3. Enable IP filtering in production environments
4. Configure appropriate rate limits for your use case
5. Monitor the security dashboard regularly
6. Use distributed tracking with Redis for multi-instance deployments

For detailed security documentation, see [Security Features](docs/SECURITY-FEATURES.md).

## Troubleshooting
- If ports are in use, adjust `WebUIHttpPort`/`WebUIHttpsPort` or stop existing processes using Docker commands (`docker compose down`).
- Logs are output to the console by default; adjust logging in `appsettings.json` as needed.
- When using Docker Compose, use `docker compose logs webui` to view container logs.

## License
See the root of the repository for license information.

---
For more information, see the main [Conduit](../Conduit.sln) solution or contact the maintainers.
