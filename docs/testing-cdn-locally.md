# Testing CDN Functionality Locally

## Option 1: MinIO (Best for Local Development)

MinIO provides S3-compatible storage that perfectly simulates CDN behavior locally.

### Setup Steps:

1. **Start MinIO:**
   ```bash
   docker-compose -f docker-compose.minio.yml up -d
   ```

2. **Configure bucket:**
   ```bash
   ./setup-minio-bucket.sh
   ```

3. **Update your `.env` file:**
   ```bash
   cp .env.minio .env.local
   # Edit .env.local to add your other required environment variables
   ```

4. **Start Conduit with MinIO:**
   ```bash
   # For docker-compose setup
   docker-compose up -d
   
   # For local development
   source .env.local
   dotnet run --project ConduitLLM.Http
   ```

5. **Access MinIO Console:**
   - URL: http://localhost:9001
   - Username: minioadmin
   - Password: minioadmin123

### Testing:
- Generated images will be accessible at: `http://localhost:9000/conduit-media/[image-key]`
- MinIO simulates CDN behavior with direct HTTP access to stored files

## Option 2: Cloudflare R2 (Free Tier)

R2 offers 10GB free storage and 1 million requests/month.

### Setup Steps:

1. **Create R2 bucket:**
   - Go to https://dash.cloudflare.com
   - Navigate to R2
   - Create bucket named `conduit-media`
   - Enable public access (R2.dev subdomain)

2. **Generate API credentials:**
   - Go to R2 > Manage R2 API tokens
   - Create token with Object Read & Write permissions

3. **Configure environment:**
   ```bash
   cp .env.cloudflare-r2 .env.local
   # Edit .env.local with your actual values
   ```

### Benefits:
- Real CDN testing with Cloudflare's edge network
- Free R2.dev subdomain for public access
- No credit card required for free tier

## Option 3: LocalStack (AWS S3 Simulation)

LocalStack provides local AWS service emulation.

```yaml
# Add to docker-compose.yml
localstack:
  image: localstack/localstack
  ports:
    - "4566:4566"
  environment:
    - SERVICES=s3
    - DEFAULT_REGION=us-east-1
```

## Option 4: ngrok for Public URL Testing

Use ngrok to expose your local MinIO to the internet:

```bash
# Expose MinIO
ngrok http 9000

# Update PUBLICBASEURL with ngrok URL
CONDUITLLM__STORAGE__S3__PUBLICBASEURL=https://[your-ngrok-subdomain].ngrok.io/conduit-media
```

## Testing Image Generation

Once configured, test with:

```bash
# Generate an image
curl -X POST http://localhost:3000/api/images/generate \
  -H "Content-Type: application/json" \
  -d '{
    "prompt": "A beautiful sunset over mountains",
    "model": "dall-e-3",
    "response_format": "url"
  }'
```

The response should include URLs pointing to your configured CDN/storage.

## Monitoring & Debugging

1. **Check storage logs:**
   ```bash
   docker logs conduit-minio
   ```

2. **Verify file upload:**
   - MinIO Console: http://localhost:9001
   - Check bucket contents

3. **Test direct access:**
   ```bash
   curl -I [returned-image-url]
   ```

## Production Considerations

When moving to production:
1. Use actual Cloudflare R2 or AWS S3
2. Configure proper CORS policies
3. Set up CDN caching rules
4. Implement proper access controls
5. Monitor storage costs and usage