# Media Storage Configuration

Conduit supports storing generated images and videos using either in-memory storage (for development) or S3-compatible storage (for production).

## Development (In-Memory Storage)

By default, Conduit uses in-memory storage for development. Generated media files are stored in memory and served directly by the API.

## Production (S3-Compatible Storage)

For production deployments, configure S3-compatible storage (AWS S3, Cloudflare R2, etc.):

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

# Optional advanced settings (with defaults shown)
# export CONDUITLLM__STORAGE__S3__FORCEPATHSTYLE=true  # Required for most S3-compatible services
# export CONDUITLLM__STORAGE__S3__AUTOCREATEBUCKET=true  # Auto-create bucket if it doesn't exist
# export CONDUITLLM__STORAGE__S3__AUTOCONFIGURECORS=true  # Auto-configure CORS for browser access
# export CONDUITLLM__STORAGE__S3__MAXFILESIZEBYTES=524288000  # 500MB max file size
# export CONDUITLLM__STORAGE__S3__MULTIPARTTHRESHOLDBYTES=104857600  # 100MB threshold for multipart
# export CONDUITLLM__STORAGE__S3__MULTIPARTCHUNKSIZEBYTES=10485760  # 10MB chunk size (optimal for R2)
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

**Cloudflare R2 Optimizations:**
- R2 is automatically detected when the ServiceURL contains "r2.cloudflarestorage.com"
- Optimized multipart upload settings are applied automatically for R2
- Default chunk size is 10MB (optimal for R2 performance)
- CORS is automatically configured for browser access

## R2 Public URL Configuration

For Cloudflare R2, you can use either:
1. **R2.dev subdomain** (free): `https://pub-[hash].r2.dev`
2. **Custom domain**: Configure a custom domain in Cloudflare and use it as `PUBLICBASEURL`

```bash
# Example with R2.dev subdomain
export CONDUITLLM__STORAGE__S3__PUBLICBASEURL=https://pub-abc123def456.r2.dev

# Example with custom domain
export CONDUITLLM__STORAGE__S3__PUBLICBASEURL=https://media.yourdomain.com
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
POST /v1/images/generations/async
```

These endpoints follow OpenAI's image generation API format and support various providers.

#### Supported Image Generation Models
Image generation models must be configured through the model mappings system. Common providers include:
- **OpenAI**: `dall-e-2`, `dall-e-3`
- **MiniMax**: `minimax-image` (maps to `image-01`)
- **Replicate**: Various models via model name
- **Other providers**: As configured in your model mappings

### Video Generation API
The video generation endpoint is available at:
```
POST /v1/videos/generations/async
```

**Note**: Video generation is async-only due to longer processing times. Use the task status endpoint to check progress:
```
GET /v1/videos/generations/tasks/{taskId}
```

#### Video Generation Features
- Async processing with progress tracking
- Task status monitoring
- Task cancellation support
- Multipart upload for large video files