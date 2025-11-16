#!/bin/bash
# Script to wait for all services to be healthy

echo "Waiting for services to be healthy..."

# Function to check if a service is healthy
check_service() {
    local service=$1
    local container_name="conduit-${service}-1"
    
    # Check if container exists and is running
    if ! docker ps --format "table {{.Names}}" | grep -q "^${container_name}$"; then
        return 1
    fi
    
    # Check health status
    local health=$(docker inspect --format='{{.State.Health.Status}}' "$container_name" 2>/dev/null)
    
    if [ "$health" = "healthy" ]; then
        return 0
    else
        return 1
    fi
}

# Wait for each service
services=("postgres" "redis" "rabbitmq" "api" "admin" "webui")
max_attempts=60
attempt=0

while [ $attempt -lt $max_attempts ]; do
    all_healthy=true
    
    for service in "${services[@]}"; do
        if ! check_service "$service"; then
            all_healthy=false
            echo "  Waiting for $service..."
        fi
    done
    
    if [ "$all_healthy" = true ]; then
        echo "All services are healthy!"
        exit 0
    fi
    
    sleep 2
    ((attempt++))
done

echo "Error: Services did not become healthy within $(($max_attempts * 2)) seconds" >&2
exit 1