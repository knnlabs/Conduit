#!/bin/bash
# Common API utilities for model import operations

# Get master key for API authentication
get_master_key() {
    local key=$(grep -A20 "admin:" docker-compose.yml | grep "CONDUIT_API_TO_API_BACKEND_AUTH_KEY:" | head -1 | awk '{print $2}')
    if [ -z "$key" ]; then
        echo -e "${RED}Error: Could not find CONDUIT_API_TO_API_BACKEND_AUTH_KEY${NC}" >&2
        return 1
    fi
    echo "$key"
}

# Base URLs for APIs
ADMIN_API_URL="${ADMIN_API_URL:-http://localhost:5002}"

# Make API request with authentication
api_request() {
    local method="$1"
    local endpoint="$2"
    local data="$3"
    local master_key="$4"
    
    if [ -z "$master_key" ]; then
        master_key=$(get_master_key)
    fi
    
    local curl_args=(
        -s
        -X "$method"
        -H "X-Master-Key: $master_key"
        -H "Content-Type: application/json"
    )
    
    if [ -n "$data" ] && [ "$method" != "GET" ] && [ "$method" != "DELETE" ]; then
        curl_args+=(-d "$data")
    fi
    
    curl "${curl_args[@]}" "$ADMIN_API_URL$endpoint"
}

# Check API connectivity
check_api_connection() {
    echo -n "Checking Admin API connection... "
    
    local master_key=$(get_master_key)
    if [ -z "$master_key" ]; then
        echo -e "${RED}✗ Failed to get master key${NC}"
        return 1
    fi
    
    # Try to get model authors (should return at least empty array)
    local response=$(api_request "GET" "/api/ModelAuthor" "" "$master_key" 2>&1)
    
    if [ $? -eq 0 ] && [[ "$response" == *"["* ]] || [[ "$response" == *"[]"* ]]; then
        echo -e "${GREEN}✓ Connected${NC}"
        return 0
    else
        echo -e "${RED}✗ Failed${NC}"
        echo "Response: $response"
        return 1
    fi
}

# Parse JSON field using available tools
parse_json_field() {
    local json="$1"
    local field="$2"
    local default="${3:-}"
    
    if command -v jq &> /dev/null; then
        echo "$json" | jq -r "$field // \"$default\"" 2>/dev/null || echo "$default"
    elif command -v python3 &> /dev/null; then
        echo "$json" | python3 -c "
import json, sys
try:
    data = json.loads(sys.stdin.read())
    result = data
    for part in '$field'.strip('.').split('.'):
        if part.startswith('[') and part.endswith(']'):
            idx = int(part[1:-1])
            result = result[idx] if isinstance(result, list) and len(result) > idx else None
        else:
            result = result.get(part, None) if isinstance(result, dict) else None
    print(result if result is not None else '$default')
except:
    print('$default')
" 2>/dev/null || echo "$default"
    else
        echo "$default"
    fi
}

# Get or create model author by name
get_or_create_author() {
    local name="$1"
    local description="$2"
    local website_url="$3"
    local master_key="$4"
    
    # First check if author exists
    local response=$(api_request "GET" "/api/ModelAuthor" "" "$master_key")
    local author_id=""
    
    # Look for existing author by name
    if command -v jq &> /dev/null; then
        author_id=$(echo "$response" | jq -r ".[] | select(.name == \"$name\") | .id" 2>/dev/null | head -1)
    elif command -v python3 &> /dev/null; then
        author_id=$(echo "$response" | python3 -c "
import json, sys
data = json.loads(sys.stdin.read())
for author in data:
    if author.get('name') == '$name':
        print(author.get('id'))
        break
" 2>/dev/null)
    fi
    
    if [ -n "$author_id" ]; then
        # Author exists, update it
        local update_data=$(cat <<EOF
{
    "name": "$name",
    "description": $([ -n "$description" ] && echo "\"$description\"" || echo "null"),
    "websiteUrl": $([ -n "$website_url" ] && echo "\"$website_url\"" || echo "null")
}
EOF
)
        api_request "PUT" "/api/ModelAuthor/$author_id" "$update_data" "$master_key" > /dev/null 2>&1
        echo "$author_id"
    else
        # Create new author
        local create_data=$(cat <<EOF
{
    "name": "$name",
    "description": $([ -n "$description" ] && echo "\"$description\"" || echo "null"),
    "websiteUrl": $([ -n "$website_url" ] && echo "\"$website_url\"" || echo "null")
}
EOF
)
        local response=$(api_request "POST" "/api/ModelAuthor" "$create_data" "$master_key")
        parse_json_field "$response" ".id" ""
    fi
}

# Get or create model series by name and author
get_or_create_series() {
    local author_id="$1"
    local name="$2"
    local description="$3"
    local tokenizer_type="$4"
    local parameters="$5"
    local master_key="$6"
    
    # First check if series exists
    local response=$(api_request "GET" "/api/ModelSeries" "" "$master_key")
    local series_id=""
    
    # Look for existing series by name and author
    if command -v jq &> /dev/null; then
        series_id=$(echo "$response" | jq -r ".[] | select(.name == \"$name\" and .authorId == $author_id) | .id" 2>/dev/null | head -1)
    elif command -v python3 &> /dev/null; then
        series_id=$(echo "$response" | python3 -c "
import json, sys
data = json.loads(sys.stdin.read())
for series in data:
    if series.get('name') == '$name' and series.get('authorId') == $author_id:
        print(series.get('id'))
        break
" 2>/dev/null)
    fi
    
    # Escape parameters JSON for embedding in JSON
    local escaped_params=$(echo "$parameters" | sed 's/\\/\\\\/g; s/"/\\"/g; s/\n/\\n/g; s/\r/\\r/g; s/\t/\\t/g')
    
    if [ -n "$series_id" ]; then
        # Series exists, update it
        local update_data=$(cat <<EOF
{
    "name": "$name",
    "authorId": $author_id,
    "description": $([ -n "$description" ] && echo "\"$description\"" || echo "null"),
    "tokenizerType": "$tokenizer_type",
    "parameters": "$escaped_params"
}
EOF
)
        api_request "PUT" "/api/ModelSeries/$series_id" "$update_data" "$master_key" > /dev/null 2>&1
        echo "$series_id"
    else
        # Create new series
        local create_data=$(cat <<EOF
{
    "name": "$name",
    "authorId": $author_id,
    "description": $([ -n "$description" ] && echo "\"$description\"" || echo "null"),
    "tokenizerType": "$tokenizer_type",
    "parameters": "$escaped_params"
}
EOF
)
        local response=$(api_request "POST" "/api/ModelSeries" "$create_data" "$master_key")
        parse_json_field "$response" ".id" ""
    fi
}

# Get or create model by name
get_or_create_model() {
    local name="$1"
    local series_id="$2"
    local model_json="$3"
    local master_key="$4"
    
    # First check if model exists
    local response=$(api_request "GET" "/api/Model" "" "$master_key")
    local model_id=""
    
    # Look for existing model by name
    if command -v jq &> /dev/null; then
        model_id=$(echo "$response" | jq -r ".[] | select(.name == \"$name\") | .id" 2>/dev/null | head -1)
    elif command -v python3 &> /dev/null; then
        model_id=$(echo "$response" | python3 -c "
import json, sys
data = json.loads(sys.stdin.read())
for model in data:
    if model.get('name') == '$name':
        print(model.get('id'))
        break
" 2>/dev/null)
    fi
    
    # Parse model fields
    local version=$(parse_json_field "$model_json" ".version" "")
    local description=$(parse_json_field "$model_json" ".description" "")
    local model_card_url=$(parse_json_field "$model_json" ".model_card_url" "")
    local supports_vision=$(parse_json_field "$model_json" ".supports_vision" "false")
    local supports_image_generation=$(parse_json_field "$model_json" ".supports_image_generation" "false")
    local supports_video_generation=$(parse_json_field "$model_json" ".supports_video_generation" "false")
    local supports_embeddings=$(parse_json_field "$model_json" ".supports_embeddings" "false")
    local supports_chat=$(parse_json_field "$model_json" ".supports_chat" "false")
    local supports_function_calling=$(parse_json_field "$model_json" ".supports_function_calling" "false")
    local supports_streaming=$(parse_json_field "$model_json" ".supports_streaming" "false")
    local tokenizer_type=$(parse_json_field "$model_json" ".tokenizer_type" "cl100k_base")
    local max_input_tokens=$(parse_json_field "$model_json" ".max_input_tokens" "null")
    local max_output_tokens=$(parse_json_field "$model_json" ".max_output_tokens" "null")
    local is_active=$(parse_json_field "$model_json" ".is_active" "true")
    local parameters=$(parse_json_field "$model_json" ".parameters" "{}")
    
    # Escape parameters JSON
    local escaped_params=$(echo "$parameters" | sed 's/\\/\\\\/g; s/"/\\"/g; s/\n/\\n/g; s/\r/\\r/g; s/\t/\\t/g')
    
    # Build model data
    local model_data=$(cat <<EOF
{
    "name": "$name",
    "modelSeriesId": $series_id,
    "version": $([ -n "$version" ] && echo "\"$version\"" || echo "null"),
    "description": $([ -n "$description" ] && echo "\"$description\"" || echo "null"),
    "modelCardUrl": $([ -n "$model_card_url" ] && echo "\"$model_card_url\"" || echo "null"),
    "supportsVision": $supports_vision,
    "supportsImageGeneration": $supports_image_generation,
    "supportsVideoGeneration": $supports_video_generation,
    "supportsEmbeddings": $supports_embeddings,
    "supportsChat": $supports_chat,
    "supportsFunctionCalling": $supports_function_calling,
    "supportsStreaming": $supports_streaming,
    "tokenizerType": "$tokenizer_type",
    "maxInputTokens": $max_input_tokens,
    "maxOutputTokens": $max_output_tokens,
    "isActive": $is_active,
    "modelParameters": "$escaped_params"
}
EOF
)
    
    if [ -n "$model_id" ]; then
        # Model exists, update it
        api_request "PUT" "/api/Model/$model_id" "$model_data" "$master_key" > /dev/null 2>&1
        echo "$model_id"
    else
        # Create new model
        local response=$(api_request "POST" "/api/Model" "$model_data" "$master_key")
        parse_json_field "$response" ".id" ""
    fi
}

# Create or update model association
create_or_update_association() {
    local model_id="$1"
    local identifier="$2"
    local provider="$3"
    local assoc_json="$4"
    local master_key="$5"
    
    # Parse association fields
    local is_enabled=$(parse_json_field "$assoc_json" ".is_enabled" "true")
    local max_input_tokens=$(parse_json_field "$assoc_json" ".max_input_tokens" "null")
    local max_output_tokens=$(parse_json_field "$assoc_json" ".max_output_tokens" "null")
    local provider_variation=$(parse_json_field "$assoc_json" ".provider_variation" "null")
    local quality_score=$(parse_json_field "$assoc_json" ".quality_score" "null")
    local speed_score=$(parse_json_field "$assoc_json" ".speed_score" "null")
    local is_primary=$(parse_json_field "$assoc_json" ".is_primary" "false")
    local metadata=$(parse_json_field "$assoc_json" ".metadata" "null")
    
    # Build association data
    local assoc_data=$(cat <<EOF
{
    "identifier": "$identifier",
    "provider": $([ -n "$provider" ] && [ "$provider" != "null" ] && echo "\"$provider\"" || echo "null"),
    "isEnabled": $is_enabled,
    "maxInputTokens": $max_input_tokens,
    "maxOutputTokens": $max_output_tokens,
    "providerVariation": $([ -n "$provider_variation" ] && [ "$provider_variation" != "null" ] && echo "\"$provider_variation\"" || echo "null"),
    "qualityScore": $quality_score,
    "speedScore": $speed_score,
    "isPrimary": $is_primary,
    "metadata": $([ -n "$metadata" ] && [ "$metadata" != "null" ] && echo "\"$metadata\"" || echo "null")
}
EOF
)
    
    # Try to create the association
    local response=$(api_request "POST" "/api/Model/$model_id/identifiers" "$assoc_data" "$master_key" 2>&1)
    
    # Check if it's a conflict (association already exists)
    if [[ "$response" == *"already exists"* ]] || [[ "$response" == *"conflict"* ]]; then
        # Need to update instead - first get existing associations
        local existing=$(api_request "GET" "/api/Model/$model_id/identifiers" "" "$master_key")
        local assoc_id=""
        
        # Find the existing association ID
        if command -v jq &> /dev/null; then
            if [ -n "$provider" ] && [ "$provider" != "null" ]; then
                assoc_id=$(echo "$existing" | jq -r ".[] | select(.identifier == \"$identifier\" and .provider == \"$provider\") | .id" 2>/dev/null | head -1)
            else
                assoc_id=$(echo "$existing" | jq -r ".[] | select(.identifier == \"$identifier\" and .provider == null) | .id" 2>/dev/null | head -1)
            fi
        fi
        
        if [ -n "$assoc_id" ]; then
            # Update the existing association
            api_request "PUT" "/api/Model/$model_id/identifiers/$assoc_id" "$assoc_data" "$master_key" > /dev/null 2>&1
            return 0
        else
            return 1
        fi
    fi
    
    return 0
}