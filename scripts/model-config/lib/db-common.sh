#!/bin/bash
# Common database utilities for model configuration scripts
# Reuses functions from provider-config but adds model-specific ones

# Source the provider db-common for base functionality
PROVIDER_DB_COMMON="${SCRIPT_DIR:-$(dirname "${BASH_SOURCE[0]}")}/../../provider-config/lib/db-common.sh"
if [ -f "$PROVIDER_DB_COMMON" ]; then
    source "$PROVIDER_DB_COMMON"
else
    # Fallback - define essential functions if provider scripts don't exist
    
    # Database connection parameters
    DB_HOST="${DB_HOST:-postgres}"
    DB_PORT="${DB_PORT:-5432}"
    DB_NAME="${DB_NAME:-conduitdb}"
    DB_USER="${DB_USER:-conduit}"
    DB_PASSWORD="${DB_PASSWORD:-conduitpass}"
    POSTGRES_CONTAINER="${POSTGRES_CONTAINER:-conduit-postgres-1}"
    
    # Colors
    RED='\033[0;31m'
    GREEN='\033[0;32m'
    YELLOW='\033[1;33m'
    BLUE='\033[0;34m'
    NC='\033[0m'
    
    check_environment() {
        if [ -f /.dockerenv ] || [ -n "$DOCKER_CONTAINER" ]; then
            USE_DOCKER=false
        else
            USE_DOCKER=true
        fi
    }
    
    exec_psql() {
        local sql="$1"
        local db="${2:-$DB_NAME}"
        
        if [ "$USE_DOCKER" = true ]; then
            docker exec -i "$POSTGRES_CONTAINER" psql -U "$DB_USER" -d "$db" -t -c "$sql" 2>/dev/null
        else
            PGPASSWORD="$DB_PASSWORD" psql -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USER" -d "$db" -t -c "$sql" 2>/dev/null
        fi
    }
    
    exec_psql_json() {
        local sql="$1"
        local db="${2:-$DB_NAME}"
        
        if [ "$USE_DOCKER" = true ]; then
            docker exec -i "$POSTGRES_CONTAINER" psql -U "$DB_USER" -d "$db" -t -A -c "$sql" 2>/dev/null
        else
            PGPASSWORD="$DB_PASSWORD" psql -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USER" -d "$db" -t -A -c "$sql" 2>/dev/null
        fi
    }
    
    check_db_connection() {
        echo -n "Checking database connection... "
        local result=$(exec_psql "SELECT 1" 2>&1)
        if [ "$?" -eq 0 ]; then
            echo -e "${GREEN}✓ Connected${NC}"
            return 0
        else
            echo -e "${RED}✗ Failed${NC}"
            return 1
        fi
    }
    
    get_table_count() {
        local table="$1"
        local count=$(exec_psql "SELECT COUNT(*) FROM \"$table\";" | tr -d ' \r\n')
        echo "$count"
    }
    
    check_environment
fi

# Model-specific table checking
check_model_tables_exist() {
    local tables=("ModelAuthors" "ModelSeries" "Models" "ModelIdentifiers")
    local missing_tables=()
    
    for table in "${tables[@]}"; do
        local exists=$(exec_psql "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = '$table');" | tr -d ' \r\n')
        if [ "$exists" != "t" ]; then
            missing_tables+=("$table")
        fi
    done
    
    if [ ${#missing_tables[@]} -gt 0 ]; then
        echo -e "${YELLOW}Warning: Missing tables: ${missing_tables[*]}${NC}"
        return 1
    fi
    
    return 0
}

# Get model statistics
get_model_statistics() {
    local authors=$(get_table_count "ModelAuthors")
    local series=$(get_table_count "ModelSeries")
    local models=$(get_table_count "Models")
    local associations=$(get_table_count "ModelIdentifiers")
    
    echo "$authors:$series:$models:$associations"
}

# Export model data with relationships as names
export_model_authors() {
    local sql="SELECT row_to_json(a) FROM (
        SELECT 
            \"Name\" as name,
            \"Description\" as description,
            \"WebsiteUrl\" as website_url
        FROM \"ModelAuthors\"
        ORDER BY \"Name\"
    ) a;"
    
    exec_psql_json "$sql"
}

export_model_series() {
    local sql="SELECT row_to_json(s) FROM (
        SELECT 
            ma.\"Name\" as author_name,
            ms.\"Name\" as name,
            ms.\"Description\" as description,
            ms.\"TokenizerType\" as tokenizer_type,
            ms.\"Parameters\" as parameters
        FROM \"ModelSeries\" ms
        INNER JOIN \"ModelAuthors\" ma ON ms.\"AuthorId\" = ma.\"Id\"
        ORDER BY ma.\"Name\", ms.\"Name\"
    ) s;"
    
    exec_psql_json "$sql"
}

export_models() {
    local sql="SELECT row_to_json(m) FROM (
        SELECT 
            m.\"Name\" as name,
            ms.\"Name\" as series_name,
            ma.\"Name\" as series_author,
            m.\"Version\" as version,
            m.\"Description\" as description,
            m.\"ModelCardUrl\" as model_card_url,
            m.\"SupportsVision\" as supports_vision,
            m.\"SupportsImageGeneration\" as supports_image_generation,
            m.\"SupportsVideoGeneration\" as supports_video_generation,
            m.\"SupportsEmbeddings\" as supports_embeddings,
            m.\"SupportsChat\" as supports_chat,
            m.\"SupportsFunctionCalling\" as supports_function_calling,
            m.\"SupportsStreaming\" as supports_streaming,
            m.\"TokenizerType\" as tokenizer_type,
            m.\"MaxInputTokens\" as max_input_tokens,
            m.\"MaxOutputTokens\" as max_output_tokens,
            m.\"IsActive\" as is_active,
            m.\"ModelParameters\" as parameters
        FROM \"Models\" m
        INNER JOIN \"ModelSeries\" ms ON m.\"ModelSeriesId\" = ms.\"Id\"
        INNER JOIN \"ModelAuthors\" ma ON ms.\"AuthorId\" = ma.\"Id\"
        ORDER BY ma.\"Name\", ms.\"Name\", m.\"Name\"
    ) m;"
    
    exec_psql_json "$sql"
}

export_model_associations() {
    local sql="SELECT row_to_json(a) FROM (
        SELECT 
            m.\"Name\" as model_name,
            mi.\"Identifier\" as identifier,
            mi.\"Provider\" as provider,
            mi.\"IsEnabled\" as is_enabled,
            mi.\"MaxInputTokens\" as max_input_tokens,
            mi.\"MaxOutputTokens\" as max_output_tokens,
            mi.\"ProviderVariation\" as provider_variation,
            mi.\"QualityScore\" as quality_score,
            mi.\"SpeedScore\" as speed_score,
            mi.\"IsPrimary\" as is_primary,
            mi.\"Metadata\" as metadata
        FROM \"ModelIdentifiers\" mi
        INNER JOIN \"Models\" m ON mi.\"ModelId\" = m.\"Id\"
        ORDER BY m.\"Name\", mi.\"Provider\", mi.\"Identifier\"
    ) a;"
    
    exec_psql_json "$sql"
}