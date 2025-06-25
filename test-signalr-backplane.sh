#!/bin/bash

# SignalR Redis Backplane Test Script
# This script helps test the SignalR Redis backplane implementation

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${GREEN}SignalR Redis Backplane Test Script${NC}"
echo "===================================="

# Function to check if a service is running
check_service() {
    local name=$1
    local port=$2
    if nc -z localhost $port 2>/dev/null; then
        echo -e "${GREEN}✓${NC} $name is running on port $port"
        return 0
    else
        echo -e "${RED}✗${NC} $name is not running on port $port"
        return 1
    fi
}

# Function to start Redis if not running
start_redis() {
    echo -e "\n${YELLOW}Starting Redis...${NC}"
    docker run -d --name redis-signalr -p 6379:6379 redis:7-alpine || {
        echo "Redis container already exists, starting it..."
        docker start redis-signalr
    }
    sleep 2
}

# Function to monitor Redis
monitor_redis() {
    echo -e "\n${YELLOW}Monitoring Redis for SignalR messages (press Ctrl+C to stop)...${NC}"
    docker exec redis-signalr redis-cli MONITOR | grep --line-buffered "conduit_signalr"
}

# Function to check Redis SignalR activity
check_redis_signalr() {
    echo -e "\n${YELLOW}Checking Redis SignalR database...${NC}"
    local db_size=$(docker exec redis-signalr redis-cli -n 2 DBSIZE | awk '{print $2}')
    echo "SignalR database (db 2) has $db_size keys"
    
    echo -e "\n${YELLOW}SignalR channels:${NC}"
    docker exec redis-signalr redis-cli PUBSUB CHANNELS "conduit_signalr:*" || echo "No active channels"
}

# Function to test API endpoints
test_api_endpoints() {
    echo -e "\n${YELLOW}Testing API health endpoints...${NC}"
    for port in 5000 5001 5002; do
        if curl -s http://localhost:$port/health > /dev/null 2>&1; then
            echo -e "${GREEN}✓${NC} API on port $port is healthy"
        else
            echo -e "${RED}✗${NC} API on port $port is not responding"
        fi
    done
}

# Function to trigger a test event
trigger_test_event() {
    echo -e "\n${YELLOW}Triggering test event via Admin API...${NC}"
    
    # Create a test model mapping
    local model_name="test-model-$(date +%s)"
    local response=$(curl -s -X POST http://localhost:5000/api/admin/model-mappings \
        -H "Content-Type: application/json" \
        -H "Authorization: Bearer $ADMIN_KEY" \
        -d "{
            \"virtualModelName\": \"$model_name\",
            \"providerModelName\": \"gpt-3.5-turbo\",
            \"providerName\": \"openai\"
        }" 2>/dev/null)
    
    if [ $? -eq 0 ]; then
        echo -e "${GREEN}✓${NC} Successfully created model mapping: $model_name"
        echo "This should trigger SignalR messages across all instances"
    else
        echo -e "${RED}✗${NC} Failed to create model mapping"
        echo "Make sure the Admin API is running and ADMIN_KEY is set"
    fi
}

# Main menu
show_menu() {
    echo -e "\n${YELLOW}Select an option:${NC}"
    echo "1) Check services status"
    echo "2) Start Redis (if not running)"
    echo "3) Monitor Redis for SignalR messages"
    echo "4) Check Redis SignalR activity"
    echo "5) Test API health endpoints"
    echo "6) Trigger test event (requires Admin API)"
    echo "7) Run SignalR test client"
    echo "8) View API logs for SignalR"
    echo "9) Full automated test"
    echo "0) Exit"
}

# Function to run SignalR test client
run_test_client() {
    echo -e "\n${YELLOW}Building and running SignalR test client...${NC}"
    cd tools/SignalRBackplaneTest
    dotnet build
    dotnet run
    cd ../..
}

# Function to view API logs
view_api_logs() {
    echo -e "\n${YELLOW}Viewing API logs for SignalR (last 50 lines)...${NC}"
    if command -v docker &> /dev/null; then
        docker logs conduit_api-1_1 --tail 50 2>&1 | grep -E "(SignalR|conduit_signalr|Redis backplane)" || {
            echo "No docker container found. Checking local logs..."
        }
    fi
}

# Function to run full automated test
run_full_test() {
    echo -e "\n${GREEN}Running full automated test...${NC}"
    
    # 1. Check prerequisites
    echo -e "\n1. Checking prerequisites..."
    check_service "Redis" 6379 || start_redis
    check_service "PostgreSQL" 5432 || echo -e "${YELLOW}Warning: PostgreSQL not detected${NC}"
    
    # 2. Check API instances
    echo -e "\n2. Checking API instances..."
    test_api_endpoints
    
    # 3. Check Redis SignalR
    echo -e "\n3. Checking Redis SignalR configuration..."
    check_redis_signalr
    
    # 4. Monitor Redis in background
    echo -e "\n4. Starting Redis monitor in background..."
    docker exec redis-signalr redis-cli MONITOR | grep --line-buffered "conduit_signalr" > redis-monitor.log 2>&1 &
    MONITOR_PID=$!
    
    # 5. Trigger test event
    echo -e "\n5. Triggering test event..."
    trigger_test_event
    
    # 6. Wait and check results
    echo -e "\n6. Waiting for message propagation..."
    sleep 3
    
    # 7. Check monitor results
    echo -e "\n7. Checking Redis monitor results..."
    if [ -s redis-monitor.log ]; then
        echo -e "${GREEN}✓${NC} SignalR messages detected in Redis:"
        head -10 redis-monitor.log
    else
        echo -e "${RED}✗${NC} No SignalR messages detected in Redis"
    fi
    
    # Cleanup
    kill $MONITOR_PID 2>/dev/null || true
    rm -f redis-monitor.log
    
    echo -e "\n${GREEN}Full test completed!${NC}"
}

# Main loop
while true; do
    show_menu
    read -p "Enter choice: " choice
    
    case $choice in
        1) 
            check_service "Redis" 6379
            check_service "PostgreSQL" 5432
            check_service "Core API (1)" 5000
            check_service "Core API (2)" 5001
            check_service "Core API (3)" 5002
            ;;
        2) start_redis ;;
        3) monitor_redis ;;
        4) check_redis_signalr ;;
        5) test_api_endpoints ;;
        6) trigger_test_event ;;
        7) run_test_client ;;
        8) view_api_logs ;;
        9) run_full_test ;;
        0) echo "Exiting..."; exit 0 ;;
        *) echo -e "${RED}Invalid option${NC}" ;;
    esac
done