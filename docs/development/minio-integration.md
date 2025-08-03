# MinIO Integration for Development

This document describes how to use MinIO S3-compatible storage in the Conduit development environment for testing media storage functionality.

## Overview

MinIO provides a local S3-compatible object storage server that runs in Docker. This allows you to test S3 integration locally without external dependencies or cloud costs.

## Usage

### Starting Development Environment

```bash
# Regular development (InMemory storage - default)
./scripts/start-dev.sh

# With MinIO S3 storage for testing
./scripts/start-dev.sh --with-minio

# Fast startup with MinIO (skip dependency checks)
./scripts/start-dev.sh --fast --with-minio

# Clean start with MinIO
./scripts/start-dev.sh --clean --with-minio
```

### Services Available with MinIO

When `--with-minio` is enabled, these additional services are available:

- **MinIO Console**: http://localhost:9001
  - Username: `minioadmin`
  - Password: `minioadmin123`
  - Web interface for managing buckets and files

- **MinIO API**: http://localhost:9000
  - S3-compatible REST API endpoint
  - Used by Conduit services for storage operations

- **Direct File Access**: http://localhost:9000/conduit-media/filename
  - CDN-like direct access to uploaded files

## Configuration

### Environment Variables

When `--with-minio` is used, these environment variables are automatically set:

```bash
CONDUIT_MEDIA_STORAGE_TYPE=S3
CONDUIT_S3_ENDPOINT=http://minio:9000
CONDUIT_S3_ACCESS_KEY=minioadmin
CONDUIT_S3_SECRET_KEY=minioadmin123
CONDUIT_S3_BUCKET_NAME=conduit-media
CONDUIT_S3_REGION=us-east-1
```

### Default Behavior

- **Without `--with-minio`**: Uses InMemory storage (no persistence)
- **With `--with-minio`**: Uses MinIO S3 storage (persisted in Docker volume)

## Testing Workflow

1. **Start with MinIO**:
   ```bash
   ./scripts/start-dev.sh --with-minio
   ```

2. **Access MinIO Console**:
   - Open http://localhost:9001
   - Login with `minioadmin` / `minioadmin123`
   - Create bucket `conduit-media` if not auto-created

3. **Test File Upload**:
   - Use Conduit WebUI to upload media files
   - Verify files appear in MinIO console
   - Test direct access via http://localhost:9000/conduit-media/filename

4. **CDN-like Testing**:
   - Files are accessible via direct URLs
   - No egress bandwidth costs (unlike real S3)
   - Simulates CDN behavior for development

## Benefits

✅ **Complete local testing** - No external dependencies  
✅ **S3 compatibility** - Test real S3 integration patterns  
✅ **Cost-free** - No cloud storage costs during development  
✅ **Offline development** - Works without internet  
✅ **Fast iteration** - No network latency to cloud storage  
✅ **Consistent environment** - All developers get identical behavior  

## Docker Configuration

### MinIO Service (docker-compose.dev.yml)

```yaml
minio:
  image: minio/minio:latest
  container_name: conduit-minio-dev
  ports:
    - "9000:9000"  # API port
    - "9001:9001"  # Console port
  environment:
    MINIO_ROOT_USER: minioadmin
    MINIO_ROOT_PASSWORD: minioadmin123
    MINIO_BROWSER_REDIRECT_URL: http://localhost:9001
  command: server /data --console-address ":9001"
  volumes:
    - minio_dev_data:/data
  healthcheck:
    test: ["CMD", "curl", "-f", "http://localhost:9000/minio/health/live"]
    interval: 30s
    timeout: 20s
    retries: 3
```

### Volume Persistence

- Data persists in `minio_dev_data` Docker volume
- Survives container restarts
- Cleaned with `./scripts/start-dev.sh --clean`

## Port Usage

- **9000**: MinIO S3 API
- **9001**: MinIO Web Console

Port conflicts are automatically detected by the start-dev.sh script.

## Comparison: Development Storage Options

| Feature | InMemory (default) | MinIO (--with-minio) |
|---------|-------------------|----------------------|
| **Persistence** | No | Yes (Docker volume) |
| **S3 Compatibility** | No | Full S3 API |
| **External Access** | No | Yes (direct URLs) |
| **Resource Usage** | Minimal | ~100MB RAM |
| **Setup Complexity** | None | Minimal |
| **Use Case** | Quick testing | S3 integration testing |

## Troubleshooting

### Port Conflicts
If ports 9000 or 9001 are in use:
```bash
# Check what's using the ports
lsof -i :9000
lsof -i :9001

# Or use different ports by modifying docker-compose.dev.yml
```

### MinIO Not Starting
```bash
# Check container logs
docker logs conduit-minio-dev

# Clean and restart
./scripts/start-dev.sh --clean --with-minio
```

### Bucket Creation Issues
- The `conduit-media` bucket should be auto-created
- If not, create manually via MinIO console at http://localhost:9001
- Set bucket policy to public read if needed for CDN testing

## Production Considerations

This MinIO setup is for **development only**. For production:

- Use **Cloudflare R2** (10x cheaper than Railway storage)
- See cost analysis in CLAUDE.md
- R2 provides free egress bandwidth
- Better for media-heavy LLM applications

## Related Documentation

- [Cloudflare R2 Configuration](../claude/media-storage-configuration.md)
- [Railway vs R2 Cost Analysis](../README.md#storage-costs)
- [Development Workflow](../../CLAUDE.md#development-workflow)