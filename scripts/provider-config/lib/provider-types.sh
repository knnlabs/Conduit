#!/bin/bash
# Provider type enum validation and mapping

# Provider type enum values (from C# ProviderType enum)
declare -A PROVIDER_TYPES=(
    ["OpenAI"]=1
    ["Groq"]=2
    ["Replicate"]=3
    ["Fireworks"]=4
    ["OpenAICompatible"]=5
    ["MiniMax"]=6
    ["Ultravox"]=7
    ["ElevenLabs"]=8
    ["Cerebras"]=9
    ["SambaNova"]=10
)

# Reverse mapping for display
declare -A PROVIDER_TYPE_NAMES=(
    [1]="OpenAI"
    [2]="Groq"
    [3]="Replicate"
    [4]="Fireworks"
    [5]="OpenAICompatible"
    [6]="MiniMax"
    [7]="Ultravox"
    [8]="ElevenLabs"
    [9]="Cerebras"
    [10]="SambaNova"
)

# Validate provider type
# Usage: validate_provider_type "OpenAI" or validate_provider_type 1
validate_provider_type() {
    local type="$1"
    
    # Check if it's a number
    if [[ "$type" =~ ^[0-9]+$ ]]; then
        if [[ -n "${PROVIDER_TYPE_NAMES[$type]}" ]]; then
            return 0
        fi
    else
        # Check if it's a valid provider name
        if [[ -n "${PROVIDER_TYPES[$type]}" ]]; then
            return 0
        fi
    fi
    
    return 1
}

# Get provider type ID from name
# Usage: get_provider_type_id "OpenAI"
get_provider_type_id() {
    local name="$1"
    echo "${PROVIDER_TYPES[$name]}"
}

# Get provider type name from ID
# Usage: get_provider_type_name 1
get_provider_type_name() {
    local id="$1"
    echo "${PROVIDER_TYPE_NAMES[$id]}"
}

# List all provider types
list_provider_types() {
    echo "Available Provider Types:"
    for name in "${!PROVIDER_TYPES[@]}"; do
        echo "  $name (${PROVIDER_TYPES[$name]})"
    done | sort -t'(' -k2 -n
}

# Check if provider type supports certain features
provider_supports_base_url() {
    local type_id="$1"
    local type_name="${PROVIDER_TYPE_NAMES[$type_id]}"
    
    # Most providers support custom base URLs
    case "$type_name" in
        "OpenAI"|"OpenAICompatible"|"Groq"|"Cerebras"|"SambaNova")
            return 0
            ;;
        *)
            return 1
            ;;
    esac
}

# Check if provider type supports organization/project ID
provider_supports_organization() {
    local type_id="$1"
    local type_name="${PROVIDER_TYPE_NAMES[$type_id]}"
    
    case "$type_name" in
        "OpenAI")
            return 0
            ;;
        *)
            return 1
            ;;
    esac
}

# Validate provider configuration
validate_provider_config() {
    local provider_type="$1"
    local base_url="$2"
    local organization="$3"
    
    if ! validate_provider_type "$provider_type"; then
        echo "Invalid provider type: $provider_type"
        return 1
    fi
    
    # Get type ID if name was provided
    local type_id
    if [[ "$provider_type" =~ ^[0-9]+$ ]]; then
        type_id="$provider_type"
    else
        type_id="${PROVIDER_TYPES[$provider_type]}"
    fi
    
    # Validate base URL if provided
    if [ -n "$base_url" ] && ! provider_supports_base_url "$type_id"; then
        echo "Warning: Provider type ${PROVIDER_TYPE_NAMES[$type_id]} may not support custom base URLs"
    fi
    
    # Validate organization if provided
    if [ -n "$organization" ] && ! provider_supports_organization "$type_id"; then
        echo "Warning: Provider type ${PROVIDER_TYPE_NAMES[$type_id]} does not support organization IDs"
    fi
    
    return 0
}