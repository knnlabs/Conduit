#!/bin/bash

# Health Monitoring Test Script
# This script validates the health monitoring and alert system

set -e

API_BASE_URL="${API_BASE_URL:-http://localhost:5000}"
ADMIN_TOKEN="${ADMIN_TOKEN:-your-admin-token}"

echo "================================================"
echo "Health Monitoring System Test Script"
echo "================================================"
echo "API Base URL: $API_BASE_URL"
echo ""

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Function to check HTTP response
check_response() {
    local response=$1
    local expected=$2
    local test_name=$3
    
    if [ "$response" -eq "$expected" ]; then
        echo -e "${GREEN}✓${NC} $test_name: HTTP $response"
    else
        echo -e "${RED}✗${NC} $test_name: Expected HTTP $expected, got $response"
        exit 1
    fi
}

# Function to test a scenario
test_scenario() {
    local scenario=$1
    local duration=$2
    local description=$3
    
    echo -e "\n${YELLOW}Testing:${NC} $description"
    
    # Start the scenario
    response=$(curl -s -o /dev/null -w "%{http_code}" -X POST \
        -H "Authorization: Bearer $ADMIN_TOKEN" \
        "$API_BASE_URL/api/test/health-monitoring/start/$scenario?durationSeconds=$duration")
    
    check_response "$response" "200" "Start $scenario"
    
    # Wait a bit
    sleep 3
    
    # Check if scenario is active
    active=$(curl -s -H "Authorization: Bearer $ADMIN_TOKEN" \
        "$API_BASE_URL/api/test/health-monitoring/active")
    
    if echo "$active" | grep -q "$scenario"; then
        echo -e "${GREEN}✓${NC} Scenario is active"
    else
        echo -e "${RED}✗${NC} Scenario is not in active list"
    fi
    
    # Stop the scenario
    response=$(curl -s -o /dev/null -w "%{http_code}" -X POST \
        -H "Authorization: Bearer $ADMIN_TOKEN" \
        "$API_BASE_URL/api/test/health-monitoring/stop/$scenario")
    
    check_response "$response" "200" "Stop $scenario"
    
    sleep 1
}

# Test 1: Health Check Endpoint
echo -e "\n${YELLOW}Test 1:${NC} Health Check Endpoint"
response=$(curl -s -o /dev/null -w "%{http_code}" "$API_BASE_URL/health")
check_response "$response" "200" "Health check endpoint"

# Test 2: Get Available Scenarios
echo -e "\n${YELLOW}Test 2:${NC} Get Available Test Scenarios"
scenarios=$(curl -s -H "Authorization: Bearer $ADMIN_TOKEN" \
    "$API_BASE_URL/api/test/health-monitoring/scenarios")

if echo "$scenarios" | grep -q "service-down"; then
    echo -e "${GREEN}✓${NC} Test scenarios endpoint working"
else
    echo -e "${RED}✗${NC} Failed to get test scenarios"
    exit 1
fi

# Test 3: Service Down Simulation
test_scenario "service-down" 5 "Service Down Alert (Critical)"

# Test 4: Performance Degradation
test_scenario "slow-response" 5 "Performance Degradation Alert (Warning)"

# Test 5: Security Threat
test_scenario "brute-force" 5 "Security Threat Detection"

# Test 6: Resource Exhaustion
test_scenario "high-cpu" 5 "High CPU Usage Alert"

# Test 7: Custom Alert
echo -e "\n${YELLOW}Test 7:${NC} Custom Alert Trigger"
response=$(curl -s -o /dev/null -w "%{http_code}" -X POST \
    -H "Authorization: Bearer $ADMIN_TOKEN" \
    -H "Content-Type: application/json" \
    -d '{
        "severity": "Info",
        "title": "Test Script Alert",
        "message": "This is a test alert from the validation script",
        "component": "TestScript",
        "suggestedActions": ["No action required", "This is just a test"]
    }' \
    "$API_BASE_URL/api/test/health-monitoring/alert")

check_response "$response" "200" "Custom alert trigger"

# Test 8: Multiple Simultaneous Scenarios
echo -e "\n${YELLOW}Test 8:${NC} Multiple Simultaneous Scenarios"

# Start multiple scenarios
for scenario in "high-cpu" "memory-leak" "slow-response"; do
    curl -s -o /dev/null -X POST \
        -H "Authorization: Bearer $ADMIN_TOKEN" \
        "$API_BASE_URL/api/test/health-monitoring/start/$scenario?durationSeconds=10" &
done

wait
sleep 2

# Check active scenarios
active=$(curl -s -H "Authorization: Bearer $ADMIN_TOKEN" \
    "$API_BASE_URL/api/test/health-monitoring/active")

active_count=$(echo "$active" | jq '. | length')
if [ "$active_count" -eq 3 ]; then
    echo -e "${GREEN}✓${NC} All 3 scenarios are running simultaneously"
else
    echo -e "${RED}✗${NC} Expected 3 active scenarios, found $active_count"
fi

# Stop all scenarios
for scenario in "high-cpu" "memory-leak" "slow-response"; do
    curl -s -o /dev/null -X POST \
        -H "Authorization: Bearer $ADMIN_TOKEN" \
        "$API_BASE_URL/api/test/health-monitoring/stop/$scenario"
done

# Test 9: SignalR Connection (if available)
echo -e "\n${YELLOW}Test 9:${NC} SignalR Health Monitoring Hub"
if command -v wscat &> /dev/null; then
    # Test SignalR connection using wscat if available
    timeout 5 wscat -c "ws://localhost:5000/hubs/health-monitoring" &> /dev/null && \
        echo -e "${GREEN}✓${NC} SignalR hub is accessible" || \
        echo -e "${YELLOW}!${NC} SignalR hub connection test failed (may require auth)"
else
    echo -e "${YELLOW}!${NC} wscat not installed, skipping SignalR test"
fi

# Summary
echo -e "\n================================================"
echo -e "${GREEN}Health Monitoring System Tests Completed!${NC}"
echo -e "================================================"
echo ""
echo "Next steps:"
echo "1. Open the Health Monitoring dashboard in your browser"
echo "2. Run a longer test scenario to see real-time alerts"
echo "3. Check the Security Monitoring dashboard for threat detection"
echo ""
echo "To run a 60-second demo of all scenarios:"
echo "  ./test-health-monitoring.sh demo"

# Demo mode
if [ "$1" == "demo" ]; then
    echo -e "\n${YELLOW}Starting 60-second demo mode...${NC}"
    
    scenarios=("service-down" "high-cpu" "slow-response" "brute-force" "rate-limit-breach")
    
    for scenario in "${scenarios[@]}"; do
        echo "Starting $scenario..."
        curl -s -o /dev/null -X POST \
            -H "Authorization: Bearer $ADMIN_TOKEN" \
            "$API_BASE_URL/api/test/health-monitoring/start/$scenario?durationSeconds=60" &
    done
    
    echo -e "\n${GREEN}Demo scenarios started!${NC}"
    echo "Open the Health Monitoring dashboard to see real-time alerts"
    echo "Press Ctrl+C to stop the demo"
    
    # Wait for user interrupt
    trap 'echo -e "\n${YELLOW}Stopping demo...${NC}"; for s in "${scenarios[@]}"; do curl -s -o /dev/null -X POST -H "Authorization: Bearer $ADMIN_TOKEN" "$API_BASE_URL/api/test/health-monitoring/stop/$s"; done; exit' INT
    
    while true; do
        sleep 5
        active=$(curl -s -H "Authorization: Bearer $ADMIN_TOKEN" "$API_BASE_URL/api/test/health-monitoring/active")
        echo "Active scenarios: $active"
    done
fi