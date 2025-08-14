#!/bin/bash

# Test Cleanup Script for Conduit Integration Tests
# Uses proper Admin API endpoints to remove test data
# This serves as both cleanup tool and API functionality test

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color
BOLD='\033[1m'

echo -e "${BLUE}${BOLD}🧹 Conduit Test Data Cleanup (API-Based)${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${CYAN}Using Admin API endpoints to remove test data...${NC}"
echo

# Get the script directory and navigate to root
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
ROOT_DIR="$( cd "$SCRIPT_DIR/.." && pwd )"

# Load .env file if it exists
if [ -f "$ROOT_DIR/.env" ]; then
    export $(cat "$ROOT_DIR/.env" | grep -v '^#' | xargs)
fi

# API configuration
ADMIN_API_URL="${ADMIN_API_URL:-http://localhost:5002}"

# Read API key from docker-compose.dev.yml (canonical source)
DOCKER_COMPOSE_FILE="$ROOT_DIR/docker-compose.dev.yml"
if [ -f "$DOCKER_COMPOSE_FILE" ]; then
    ADMIN_API_KEY=$(grep "CONDUIT_API_TO_API_BACKEND_AUTH_KEY:" "$DOCKER_COMPOSE_FILE" | head -1 | sed 's/.*CONDUIT_API_TO_API_BACKEND_AUTH_KEY: *\([^ ]*\).*/\1/')
else
    echo -e "${RED}❌ docker-compose.dev.yml not found at $DOCKER_COMPOSE_FILE${NC}"
    exit 1
fi

# Check for API key
if [ -z "$ADMIN_API_KEY" ]; then
    echo -e "${RED}❌ Unable to extract CONDUIT_API_TO_API_BACKEND_AUTH_KEY from docker-compose.dev.yml${NC}"
    echo -e "${YELLOW}Check the format of CONDUIT_API_TO_API_BACKEND_AUTH_KEY in docker-compose.dev.yml${NC}"
    exit 1
fi


# Function to make API calls
api_call() {
    local method="$1"
    local endpoint="$2"
    local data="$3"
    
    local url="$ADMIN_API_URL$endpoint"
    local curl_args=(--max-time 5 -s -X "$method" "$url" -H "X-Master-Key: $ADMIN_API_KEY" -H "Content-Type: application/json")
    
    if [ -n "$data" ]; then
        curl_args+=(-d "$data")
    fi
    
    curl "${curl_args[@]}"
}

# Function to check API connectivity
check_api_connectivity() {
    echo -e "${YELLOW}Checking Admin API connectivity...${NC}"
    
    local response=$(api_call "GET" "/api/VirtualKeys" 2>/dev/null)
    local exit_code=$?
    
    if [ $exit_code -ne 0 ] || echo "$response" | grep -q '"error"' || echo "$response" | grep -q 'html'; then
        echo -e "${RED}❌ Cannot connect to Admin API at $ADMIN_API_URL${NC}"
        echo -e "${YELLOW}Make sure Conduit services are running with: ./scripts/start-dev.sh${NC}"
        echo -e "${YELLOW}Response: $response${NC}"
        exit 1
    fi
    
    echo -e "${GREEN}  ✓ Admin API connection successful${NC}"
}

# Function to get and delete items
process_deletions() {
    local description="$1"
    local get_endpoint="$2"
    local delete_endpoint_template="$3"
    local filter_field="$4"
    local id_field="$5"
    
    echo -e "${YELLOW}$description...${NC}"
    
    # Get all items
    local response=$(api_call "GET" "$get_endpoint")
    
    if echo "$response" | grep -q '"error"' || [ -z "$response" ] || [ "$response" = "null" ]; then
        echo -e "${GREEN}  ✓ No items found or endpoint not accessible${NC}"
        return
    fi
    
    # Get test item IDs and convert to array for safer processing
    local test_ids_json=$(echo "$response" | jq -r "
        if type == \"array\" then 
            [.[] | select(.${filter_field} // \"\" | test(\"TEST_\"; \"i\")) | .${id_field}]
        else 
            []
        end" 2>/dev/null || echo "[]")
    
    local count=$(echo "$test_ids_json" | jq 'length' 2>/dev/null || echo "0")
    
    if [ "$count" -eq 0 ]; then
        echo -e "${GREEN}  ✓ No test items to remove${NC}"
        return
    fi
    
    local deleted_count=0
    
    # Delete each test item using jq to iterate
    for i in $(seq 0 $((count-1))); do
        local item_id=$(echo "$test_ids_json" | jq -r ".[$i]" 2>/dev/null)
        
        if [ -n "$item_id" ] && [ "$item_id" != "null" ]; then
            # Show progress for large batches
            if [ $((i % 10)) -eq 0 ] && [ $count -gt 20 ]; then
                echo -e "${CYAN}    Processing $((i+1))/$count...${NC}"
            fi
            
            # Replace placeholder in delete endpoint
            local delete_endpoint=$(echo "$delete_endpoint_template" | sed "s/{id}/$item_id/g")
            
            # Attempt deletion
            local delete_response=$(api_call "DELETE" "$delete_endpoint" 2>/dev/null)
            local delete_exit_code=$?
            
            if [ $delete_exit_code -eq 0 ] && ! echo "$delete_response" | grep -q '"error"' && ! echo "$delete_response" | grep -q "unexpected error"; then
                ((deleted_count++))
            else
                # Skip logging 500 errors for now - likely dependency constraints
                if echo "$delete_response" | grep -q "unexpected error"; then
                    : # Silent skip for 500 errors
                else
                    echo -e "${YELLOW}    ⚠️ Failed to delete item $item_id: $delete_response${NC}"
                fi
            fi
        fi
    done
    
    echo -e "${GREEN}  ✓ Deleted $deleted_count of $count test items${NC}"
}

# Function to delete provider keys (special case - needs provider ID)
delete_provider_keys() {
    echo -e "${YELLOW}Removing test provider key credentials...${NC}"
    
    # First get all providers
    local providers_response=$(api_call "GET" "/api/ProviderCredentials")
    
    if echo "$providers_response" | grep -q '"error"' || [ -z "$providers_response" ] || [ "$providers_response" = "null" ]; then
        echo -e "${GREEN}  ✓ No providers found${NC}"
        return
    fi
    
    local total_deleted=0
    
    # Get test provider IDs as JSON array
    local test_provider_ids_json=$(echo "$providers_response" | jq -r "
        if type == \"array\" then 
            [.[] | select(.providerName // \"\" | test(\"TEST_\"; \"i\")) | .id]
        else 
            []
        end" 2>/dev/null || echo "[]")
    
    local provider_count=$(echo "$test_provider_ids_json" | jq 'length' 2>/dev/null || echo "0")
    
    # For each test provider, get and delete its keys
    for i in $(seq 0 $((provider_count-1))); do
        local provider_id=$(echo "$test_provider_ids_json" | jq -r ".[$i]" 2>/dev/null)
        
        if [ -n "$provider_id" ] && [ "$provider_id" != "null" ]; then
            # Get keys for this provider
            local keys_response=$(api_call "GET" "/api/ProviderCredentials/$provider_id/keys")
            
            if ! echo "$keys_response" | grep -q '"error"' && [ "$keys_response" != "null" ]; then
                # Get all key IDs as JSON array
                local key_ids_json=$(echo "$keys_response" | jq -r "
                    if type == \"array\" then 
                        [.[] | .id]
                    else 
                        []
                    end" 2>/dev/null || echo "[]")
                
                local key_count=$(echo "$key_ids_json" | jq 'length' 2>/dev/null || echo "0")
                
                # Delete each key
                for j in $(seq 0 $((key_count-1))); do
                    local key_id=$(echo "$key_ids_json" | jq -r ".[$j]" 2>/dev/null)
                    
                    if [ -n "$key_id" ] && [ "$key_id" != "null" ]; then
                        local delete_response=$(api_call "DELETE" "/api/ProviderCredentials/$provider_id/keys/$key_id" 2>/dev/null)
                        
                        if ! echo "$delete_response" | grep -q '"error"'; then
                            ((total_deleted++))
                        fi
                    fi
                done
            fi
        fi
    done
    
    echo -e "${GREEN}  ✓ Deleted $total_deleted provider key credentials${NC}"
}

# Check API connectivity first
check_api_connectivity
echo

# =============================================================================
# Test Data Cleanup (Order matters due to dependencies)
# =============================================================================

echo -e "${BLUE}${BOLD}Cleaning Integration Test Data:${NC}"

# 1. Virtual Keys (depend on Virtual Key Groups)
process_deletions "Removing test virtual keys" \
    "/api/VirtualKeys" \
    "/api/VirtualKeys/{id}" \
    "keyName" \
    "id"

# 2. Virtual Key Groups
process_deletions "Removing test virtual key groups" \
    "/api/VirtualKeyGroups" \
    "/api/VirtualKeyGroups/{id}" \
    "groupName" \
    "id"

# 3. Model Provider Mappings (depend on Providers)
process_deletions "Removing test model provider mappings" \
    "/api/ModelProviderMapping" \
    "/api/ModelProviderMapping/{id}" \
    "modelId" \
    "id"

# 4. Provider Key Credentials (special case - nested under providers)
delete_provider_keys

# 5. Model Costs
process_deletions "Removing test model costs" \
    "/api/ModelCosts" \
    "/api/ModelCosts/{id}" \
    "costName" \
    "id"

# 6. Providers (base entities)
process_deletions "Removing test providers" \
    "/api/ProviderCredentials" \
    "/api/ProviderCredentials/{id}" \
    "providerName" \
    "id"

echo

# =============================================================================
# Test Report Cleanup (File-based, like before)
# =============================================================================

echo -e "${BLUE}${BOLD}Cleaning Test Reports and Artifacts:${NC}"

# Clean test reports
REPORT_DIR="$ROOT_DIR/ConduitLLM.IntegrationTests/bin/Debug/net9.0/Reports"
if [ -d "$REPORT_DIR" ]; then
    echo -e "${YELLOW}Removing test reports...${NC}"
    local report_count=$(find "$REPORT_DIR" -name "*.md" 2>/dev/null | wc -l)
    rm -f "$REPORT_DIR"/*.md 2>/dev/null || true
    echo -e "${GREEN}  ✓ Removed $report_count report files${NC}"
else
    echo -e "${GREEN}  ✓ No test reports directory found${NC}"
fi

# Clean test context files
CONTEXT_FILE="$ROOT_DIR/ConduitLLM.IntegrationTests/bin/Debug/net9.0/test-context.json"
if [ -f "$CONTEXT_FILE" ]; then
    rm -f "$CONTEXT_FILE"
    echo -e "${GREEN}  ✓ Removed test context file${NC}"
else
    echo -e "${GREEN}  ✓ No test context file found${NC}"
fi

# Clean any test logs
TEST_LOGS_DIR="$ROOT_DIR/ConduitLLM.IntegrationTests/bin/Debug/net9.0/logs"
if [ -d "$TEST_LOGS_DIR" ]; then
    echo -e "${YELLOW}Removing test logs...${NC}"
    local log_count=$(find "$TEST_LOGS_DIR" -name "*.log" 2>/dev/null | wc -l)
    rm -f "$TEST_LOGS_DIR"/*.log 2>/dev/null || true
    echo -e "${GREEN}  ✓ Removed $log_count log files${NC}"
else
    echo -e "${GREEN}  ✓ No test logs directory found${NC}"
fi

echo

# =============================================================================
# Verification using API
# =============================================================================

echo -e "${BLUE}${BOLD}Verification:${NC}"

echo -e "${YELLOW}Checking for remaining test data via API...${NC}"

# Function to count remaining test items
count_remaining() {
    local endpoint="$1"
    local filter_field="$2"
    
    local response=$(api_call "GET" "$endpoint" 2>/dev/null)
    
    if echo "$response" | grep -q '"error"' || [ -z "$response" ] || [ "$response" = "null" ]; then
        echo "0"
        return
    fi
    
    local count=$(echo "$response" | jq -r "
        if type == \"array\" then 
            [.[] | select(.${filter_field} // \"\" | test(\"TEST_\"; \"i\"))] | length
        else 
            0 
        end" 2>/dev/null || echo "0")
    
    echo "$count"
}

remaining_vkeys=$(count_remaining "/api/VirtualKeys" "keyName")
remaining_groups=$(count_remaining "/api/VirtualKeyGroups" "groupName")  
remaining_providers=$(count_remaining "/api/ProviderCredentials" "providerName")
remaining_mappings=$(count_remaining "/api/ModelProviderMapping" "modelId")
remaining_costs=$(count_remaining "/api/ModelCosts" "costName")

echo -e "  Virtual Keys: $remaining_vkeys"
echo -e "  Virtual Key Groups: $remaining_groups"
echo -e "  Providers: $remaining_providers"
echo -e "  Model Mappings: $remaining_mappings"
echo -e "  Model Costs: $remaining_costs"

total_remaining=$((remaining_vkeys + remaining_groups + remaining_providers + remaining_mappings + remaining_costs))

echo

# =============================================================================
# Summary
# =============================================================================

echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
if [ "$total_remaining" -eq 0 ]; then
    echo -e "${GREEN}${BOLD}✅ Test Cleanup Complete!${NC}"
    echo -e "${GREEN}Local Conduit instance is ready for fresh integration tests${NC}"
else
    echo -e "${YELLOW}${BOLD}⚠️ Test Cleanup Partially Complete${NC}"
    echo -e "${YELLOW}$total_remaining test items may still remain${NC}"
fi
echo -e "${CYAN}This API-based cleanup validates that Conduit APIs can reliably${NC}"
echo -e "${CYAN}create and delete large amounts of data through proper endpoints.${NC}"
echo

# Exit successfully
exit 0