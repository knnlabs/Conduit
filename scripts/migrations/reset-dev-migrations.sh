#!/bin/bash
set -e

# Script: reset-dev-migrations.sh
# Purpose: Reset Entity Framework Core migrations in development environment
# WARNING: This script will DELETE ALL DATA in the database!

echo "=============================================="
echo "EF Core Migration Reset Script (DEVELOPMENT)"
echo "=============================================="
echo ""
echo "WARNING: This script will:"
echo "  - Stop all Docker containers"
echo "  - Delete all database volumes"
echo "  - Clean all build artifacts"
echo "  - Rebuild the entire solution"
echo ""
read -p "Are you sure you want to continue? (yes/no): " confirm

if [ "$confirm" != "yes" ]; then
    echo "Operation cancelled."
    exit 0
fi

# Get script directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PROJECT_ROOT="$( cd "$SCRIPT_DIR/../.." && pwd )"

echo ""
echo "Working directory: $PROJECT_ROOT"
cd "$PROJECT_ROOT"

# Step 1: Stop all containers and remove volumes
echo ""
echo "Step 1: Stopping Docker containers and removing volumes..."
docker-compose down -v || true

# Step 2: Clean all build artifacts
echo ""
echo "Step 2: Cleaning build artifacts..."
find . -type d -name "bin" -o -type d -name "obj" | grep -E "(ConduitLLM\.|SDKs/)" | xargs rm -rf

# Step 3: Clear NuGet cache for local packages
echo ""
echo "Step 3: Clearing NuGet cache..."
dotnet nuget locals all --clear

# Step 4: Remove old migration files (if consolidating)
echo ""
echo "Step 4: Checking for migration consolidation..."
read -p "Do you want to remove existing migrations? (yes/no): " remove_migrations

if [ "$remove_migrations" == "yes" ]; then
    echo "Removing existing migrations..."
    rm -rf ConduitLLM.Configuration/Migrations/*
    
    echo ""
    echo "Creating new consolidated migration..."
    cd ConduitLLM.Configuration
    dotnet ef migrations add InitialCreate
    cd ..
fi

# Step 5: Build solution
echo ""
echo "Step 5: Building solution..."
dotnet build

# Step 6: Build Docker images
echo ""
echo "Step 6: Building Docker images..."
docker-compose build --no-cache

# Step 7: Start services
echo ""
echo "Step 7: Starting services..."
docker-compose up -d

# Wait for services to be healthy
echo ""
echo "Waiting for services to be healthy..."
sleep 30

# Step 8: Check migration status
echo ""
echo "Step 8: Checking migration status..."
curl -s http://localhost:5000/health/ready | jq '.checks[] | select(.name == "migrations")'

echo ""
echo "=============================================="
echo "Migration reset complete!"
echo "=============================================="
echo ""
echo "Services running at:"
echo "  - API: http://localhost:5000"
echo "  - Admin: http://localhost:5002"
echo "  - WebUI: http://localhost:3000"
echo ""
echo "Check logs with: docker-compose logs -f"