#!/bin/bash

# Wait for MinIO to be ready
echo "Waiting for MinIO to start..."
sleep 5

# Install mc (MinIO Client) if not already installed
if ! command -v mc &> /dev/null; then
    echo "Installing MinIO client..."
    curl -o mc https://dl.min.io/client/mc/release/linux-amd64/mc
    chmod +x mc
    sudo mv mc /usr/local/bin/
fi

# Configure mc with local MinIO
mc alias set local http://localhost:9000 minioadmin minioadmin123

# Create bucket
mc mb local/conduit-media --ignore-existing

# Set bucket policy to public (for CDN simulation)
mc anonymous set public local/conduit-media

echo "MinIO setup complete!"
echo "API URL: http://localhost:9000"
echo "Console URL: http://localhost:9001"
echo "Public URL format: http://localhost:9000/conduit-media/{object}"