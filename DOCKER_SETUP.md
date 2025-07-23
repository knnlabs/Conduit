# Docker Setup for Node.js WebUI

## Summary of Changes

### 1. WebUI Service Configuration

The `docker-compose.yml` has been updated with a new WebUI service configuration:

- **Build Context**: `./ConduitLLM.WebUI`
- **Port**: 3000 (changed from 5001)
- **Healthcheck**: Uses Node.js script instead of curl
- **Dependencies**: Requires both API and Admin services to be healthy

### 2. Environment Variables

The WebUI service uses the following environment variables:

#### Next.js Configuration
- `NODE_ENV=production`
- `PORT=3000`

#### Public URLs (Browser Access)
- `NEXT_PUBLIC_CONDUIT_CORE_API_URL=http://localhost:5000`
- `NEXT_PUBLIC_CONDUIT_ADMIN_API_URL=http://localhost:5002`

#### Internal URLs (Server-Side)
- `CONDUIT_API_BASE_URL=http://api:8080`
- `CONDUIT_ADMIN_API_BASE_URL=http://admin:8080`

#### SignalR External URLs
- `CONDUIT_API_EXTERNAL_URL=http://localhost:5000`
- `CONDUIT_ADMIN_API_EXTERNAL_URL=http://localhost:5002`

#### Authentication & Storage
- `CONDUIT_API_TO_API_BACKEND_AUTH_KEY=alpha`
- `SESSION_SECRET=your-session-secret-key-change-in-production`
- `REDIS_URL=redis://redis:6379`
- `REDIS_SESSION_PREFIX=conduit:session:`

### 3. CORS Configuration

The Admin API service has been updated to allow requests from the new WebUI port:
```yaml
AdminApi__AllowedOrigins__0: http://localhost:3000
```

### 4. Files Created

- `ConduitLLM.WebUI/Dockerfile` - Multi-stage Docker build for Next.js
- `ConduitLLM.WebUI/.dockerignore` - Excludes unnecessary files from Docker build
- Updated `ConduitLLM.WebUI/.env.example` with comprehensive environment variables

### 5. Running the Stack

To run the complete stack with the Node.js WebUI:

```bash
# From the project root directory
docker-compose up -d

# Access the services:
# - WebUI: http://localhost:3000
# - Core API: http://localhost:5000
# - Admin API: http://localhost:5002
# - RabbitMQ Management: http://localhost:15672 (user: conduit, pass: conduitpass)
```

### 6. Building Individual Services

To rebuild just the WebUI:
```bash
docker-compose build webui
docker-compose up -d webui
```

### 7. Development Notes

- The WebUI uses server-side API routes as proxies to handle authentication
- SignalR connections are made directly from the browser to the backend APIs
- Session storage uses Redis for distributed session management
- The WebUI container runs as a non-root user (nextjs:1001) for security

### 8. Production Considerations

For production deployment:
1. Change `SESSION_SECRET` to a secure random value
2. Update all `localhost` URLs to your actual domain
3. Enable HTTPS for all services
4. Consider using a reverse proxy (nginx) for the WebUI
5. Set appropriate CORS origins in the Admin API