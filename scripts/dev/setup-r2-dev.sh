#!/bin/bash
# Setup script for Cloudflare R2 development

set -e

echo "=== Cloudflare R2 Development Setup ==="
echo

# Check if .env exists
if [ ! -f .env ]; then
    echo "❌ No .env file found!"
    echo "📝 Creating .env from template..."
    cp .env.r2.development .env
    echo
    echo "⚠️  Please edit .env and add your R2 credentials:"
    echo "   1. Go to Cloudflare Dashboard → R2"
    echo "   2. Create a bucket called 'conduit-media-dev'"
    echo "   3. Create an API token with R2 read/write permissions"
    echo "   4. Add the credentials to .env"
    echo
    echo "Then run this script again."
    exit 1
fi

# Source the .env file
set -a
source .env
set +a

# Validate R2 configuration
if [ -z "$CONDUIT_S3_ENDPOINT" ] || [ "$CONDUIT_S3_ENDPOINT" == "https://<your-account-id>.r2.cloudflarestorage.com" ]; then
    echo "❌ R2 endpoint not configured in .env"
    echo "   Please add your R2 endpoint URL"
    exit 1
fi

if [ -z "$CONDUIT_S3_ACCESS_KEY" ] || [ "$CONDUIT_S3_ACCESS_KEY" == "<your-r2-access-key>" ]; then
    echo "❌ R2 access key not configured in .env"
    echo "   Please add your R2 credentials"
    exit 1
fi

echo "✅ R2 Configuration:"
echo "   Endpoint: $CONDUIT_S3_ENDPOINT"
echo "   Bucket: $CONDUIT_S3_BUCKET_NAME"
echo "   Public URL: $CONDUIT_S3_PUBLIC_BASE_URL"
echo

# Test R2 connectivity (optional)
echo "🔍 Testing R2 connectivity..."
if command -v aws &> /dev/null; then
    AWS_ACCESS_KEY_ID="$CONDUIT_S3_ACCESS_KEY" \
    AWS_SECRET_ACCESS_KEY="$CONDUIT_S3_SECRET_KEY" \
    aws s3 ls s3://$CONDUIT_S3_BUCKET_NAME --endpoint-url $CONDUIT_S3_ENDPOINT --region auto 2>/dev/null && \
    echo "✅ R2 connection successful!" || \
    echo "⚠️  Could not connect to R2 (this might be normal if bucket doesn't exist yet)"
else
    echo "ℹ️  AWS CLI not installed, skipping connectivity test"
fi

echo
echo "🚀 Starting development environment with R2..."
echo

# Start with R2 configuration
docker compose -f docker-compose.dev.yml up -d

echo
echo "✅ Development environment started with Cloudflare R2!"
echo
echo "📌 Services:"
echo "   - WebUI: http://localhost:3000"
echo "   - Core API: http://localhost:5000/swagger"
echo "   - Admin API: http://localhost:5002/swagger"
echo "   - Media Storage: Cloudflare R2"
echo
echo "🖼️  Generated images will be stored in R2 and served from:"
echo "   $CONDUIT_S3_PUBLIC_BASE_URL"
echo