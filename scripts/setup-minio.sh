#!/bin/bash
# Setup MinIO for development - makes bucket publicly readable

# Wait for MinIO to be ready
until docker exec conduit-minio-dev mc alias set minio http://localhost:9000 minioadmin minioadmin123 &>/dev/null; do
    sleep 1
done

# Create bucket if it doesn't exist
docker exec conduit-minio-dev mc mb minio/conduit-media --ignore-existing &>/dev/null

# Set bucket to allow public downloads
docker exec conduit-minio-dev mc anonymous set download minio/conduit-media &>/dev/null