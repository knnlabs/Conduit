#!/bin/bash
set -e

echo "=== Cleaning EF Core Migrations ==="

# Stop all containers
echo "Stopping Docker containers..."
docker-compose down -v

# Clean all build artifacts
echo "Cleaning build artifacts..."
find . -type d -name "bin" -o -type d -name "obj" | xargs rm -rf

# Clean NuGet cache for local packages
echo "Cleaning NuGet cache..."
dotnet nuget locals all --clear

# Clean solution
echo "Running dotnet clean..."
dotnet clean

# Restore packages
echo "Restoring packages..."
dotnet restore

# Build solution
echo "Building solution..."
dotnet build

# Rebuild Docker images
echo "Rebuilding Docker images..."
docker-compose build --no-cache

echo "=== Clean complete! ==="
echo "You can now run: docker-compose up -d"