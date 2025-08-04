#!/bin/sh
# Initialize MinIO bucket with public access

# Wait for MinIO to be ready
until mc alias set local http://localhost:9000 minioadmin minioadmin123 >/dev/null 2>&1; do
    sleep 1
done

# Create bucket and set public access
mc mb local/conduit-media --ignore-existing >/dev/null 2>&1
mc anonymous set download local/conduit-media >/dev/null 2>&1

echo "MinIO bucket initialized with public access"