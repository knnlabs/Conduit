# Media Storage Configuration

Conduit supports storing generated images and videos using either in-memory storage (for development) or S3-compatible storage (for production).

## Development (In-Memory Storage)

By default, Conduit uses in-memory storage for development. Generated media files are stored in memory and served directly by the API.

## Production (S3-Compatible Storage)

For production deployments, configure S3-compatible storage (AWS S3, Cloudflare R2, MinIO, etc.):

```bash
# Storage provider configuration
export CONDUITLLM__STORAGE__PROVIDER=S3

# S3 configuration
export CONDUITLLM__STORAGE__S3__SERVICEURL=https://your-s3-endpoint.com  # Optional for AWS S3
export CONDUITLLM__STORAGE__S3__ACCESSKEY=your-access-key
export CONDUITLLM__STORAGE__S3__SECRETKEY=your-secret-key
export CONDUITLLM__STORAGE__S3__BUCKETNAME=conduit-media
export CONDUITLLM__STORAGE__S3__REGION=auto  # Or specific region like us-east-1
export CONDUITLLM__STORAGE__S3__PUBLICBASEURL=https://cdn.yourdomain.com  # Optional CDN URL
```

## Cloudflare R2 Example

```bash
export CONDUITLLM__STORAGE__PROVIDER=S3
export CONDUITLLM__STORAGE__S3__SERVICEURL=https://<account-id>.r2.cloudflarestorage.com
export CONDUITLLM__STORAGE__S3__ACCESSKEY=<r2-access-key>
export CONDUITLLM__STORAGE__S3__SECRETKEY=<r2-secret-key>
export CONDUITLLM__STORAGE__S3__BUCKETNAME=conduit-media
export CONDUITLLM__STORAGE__S3__REGION=auto
```

## Docker SignalR Configuration

When running Conduit in Docker, the WebUI needs different URLs for server-side API calls vs client-side (browser) connections:

### WebUI Environment Variables

```bash
# Internal URLs for server-to-server communication (within Docker network)
CONDUIT_API_BASE_URL=http://api:8080
CONDUIT_ADMIN_API_BASE_URL=http://admin:8080

# External URLs for client-side browser access (SignalR/WebSocket connections)
CONDUIT_API_EXTERNAL_URL=http://localhost:5000
CONDUIT_ADMIN_API_EXTERNAL_URL=http://localhost:5002
```

**Important Notes:**
- `CONDUIT_API_BASE_URL` and `CONDUIT_ADMIN_API_BASE_URL` are used by the WebUI server for backend API calls
- `CONDUIT_API_EXTERNAL_URL` and `CONDUIT_ADMIN_API_EXTERNAL_URL` are passed to the browser for SignalR connections
- If external URLs are not set, the system falls back to internal URLs (which won't work for browser connections in Docker)
- For production deployments, set these to your actual domain names (e.g., `https://api.yourdomain.com`)

## Important: Media Lifecycle Management

**WARNING**: Generated media files (images/videos) are currently not cleaned up when virtual keys are deleted. This is a known limitation that will lead to:
- Ever-growing storage costs
- Orphaned media files in your CDN/S3 bucket
- No ability to track storage usage per virtual key

**Temporary Workarounds**:
1. Use S3 lifecycle policies to auto-delete old files
2. Manually clean up orphaned media periodically
3. Monitor your storage usage and costs

See `docs/TODO-Media-Lifecycle-Management.md` for the planned implementation to address this.

## Supported APIs

### Image Generation API
The image generation endpoint is available at:
```
POST /v1/images/generations
```

This endpoint follows OpenAI's image generation API format and supports providers like OpenAI (DALL-E), MiniMax, and Replicate.

#### Supported Image Generation Models
- **OpenAI**: `dall-e-2`, `dall-e-3`
- **MiniMax**: `minimax-image` (maps to `image-01`)
- **Replicate**: Various models via model name