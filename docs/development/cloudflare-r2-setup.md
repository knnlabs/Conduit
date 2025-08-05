# Cloudflare R2 Development Setup

This guide explains how to use Cloudflare R2 instead of MinIO for development.

## Why Use R2 for Development?

1. **Production Parity**: Test against actual R2 behavior
2. **No Docker Issues**: No more MinIO permission problems
3. **Real Public URLs**: R2 provides actual public URLs that work
4. **Better Performance**: CDN-backed delivery
5. **Cost Effective**: R2 free tier includes 10GB storage

## Prerequisites

1. Cloudflare account (free tier works)
2. R2 enabled in your Cloudflare dashboard

## Setup Steps

### 1. Create R2 Bucket

1. Go to [Cloudflare Dashboard](https://dash.cloudflare.com) → R2
2. Click "Create bucket"
3. Name it `conduit-media-dev`
4. Choose "Automatic" for location
5. After creation, go to Settings → Public Access
6. Enable "Allow public access"
7. Note the public URL (e.g., `https://pub-abc123.r2.dev`)

### 2. Create R2 API Token

1. In R2 dashboard, click "Manage R2 API Tokens"
2. Click "Create API token"
3. Configure:
   - Name: `conduit-dev`
   - Permissions: `Object Read & Write`
   - Bucket: `conduit-media-dev` (or all buckets)
   - TTL: Leave blank for permanent
4. Create and save the credentials:
   - Access Key ID
   - Secret Access Key
   - Account ID (in the endpoint URL)

### 3. Configure Local Environment

```bash
# Copy the R2 template
cp .env.r2.development .env

# Edit .env with your credentials
nano .env
```

Add your credentials:
```env
CONDUIT_S3_ENDPOINT=https://YOUR-ACCOUNT-ID.r2.cloudflarestorage.com
CONDUIT_S3_ACCESS_KEY=your-access-key-id
CONDUIT_S3_SECRET_KEY=your-secret-access-key
CONDUIT_S3_BUCKET_NAME=conduit-media-dev
CONDUIT_S3_PUBLIC_BASE_URL=https://pub-YOUR-HASH.r2.dev
```

### 4. Start Development with R2

```bash
# Use the R2 setup script
./scripts/setup-r2-dev.sh

# Or manually with docker compose
docker compose -f docker-compose.dev.yml -f docker-compose.r2.yml up
```

## How It Works

1. **Image Generation**: Images are uploaded to R2 instead of MinIO
2. **Public URLs**: R2 returns URLs like `https://pub-abc123.r2.dev/image/2025/...`
3. **No Proxying**: Browser directly accesses R2's CDN
4. **Automatic Detection**: The app detects R2 and optimizes settings

## Benefits of R2

| Feature | Cloudflare R2 |
|---------|---------------|
| Public URLs | Built-in with every bucket |
| Permissions | Simple, works immediately |
| Performance | Global CDN |
| Reliability | Cloud service, always available |
| Cost | Free tier: 10GB storage |

## Troubleshooting

### "Access Denied" Errors
- Ensure public access is enabled on the bucket
- Check API token has read/write permissions

### Images Not Loading
- Verify `CONDUIT_S3_PUBLIC_BASE_URL` matches your bucket's public URL
- Check CORS settings in R2 dashboard if needed

### Connection Errors
- Verify endpoint URL includes your account ID
- Check access key and secret are correct
- Ensure bucket name matches exactly

## Security Notes

For development only:
- R2 bucket is publicly readable (like a CDN)
- Use different buckets for dev/staging/prod
- Don't store sensitive data

For production:
- Use proper access controls
- Consider Cloudflare Access for protection
- Implement signed URLs if needed

## Cost Considerations

R2 Free Tier includes:
- 10 GB storage per month
- 1 million Class A operations
- 10 million Class B operations

This is more than enough for development use.


## Next Steps

1. Set up R2 for staging environment
2. Configure production R2 bucket
3. Set up Cloudflare Images for optimization (optional)