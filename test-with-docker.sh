#\!/bin/bash
# Start required services
docker compose up -d postgres redis admin

# Wait for services to be healthy
echo "Waiting for services to be ready..."
sleep 10

# Run tests
dotnet test

# Stop services
docker compose down
