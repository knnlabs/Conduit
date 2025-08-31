#!/bin/bash
# Import provider configurations into Conduit database
# This includes providers, API keys, and model mappings

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Source common functions
source "$SCRIPT_DIR/lib/db-common.sh"
source "$SCRIPT_DIR/lib/provider-types.sh"

# Default settings
INPUT_FILE=""
DRY_RUN=false
FORCE=false
SKIP_BACKUP=false
VERBOSE=false
UPDATE_EXISTING=false
CLEAR_BEFORE_IMPORT=false

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -i|--input)
            INPUT_FILE="$2"
            shift 2
            ;;
        -d|--dry-run)
            DRY_RUN=true
            shift
            ;;
        -f|--force)
            FORCE=true
            shift
            ;;
        -u|--update)
            UPDATE_EXISTING=true
            shift
            ;;
        --clear)
            CLEAR_BEFORE_IMPORT=true
            shift
            ;;
        --skip-backup)
            SKIP_BACKUP=true
            shift
            ;;
        -v|--verbose)
            VERBOSE=true
            shift
            ;;
        -h|--help)
            cat << EOF
Usage: $0 -i INPUT_FILE [OPTIONS]

Import provider configurations into Conduit database from JSON file.

Required:
    -i, --input FILE       Input JSON file (or encrypted .enc file)

Options:
    -d, --dry-run          Simulate import without making changes
    -f, --force            Skip confirmation prompts
    -u, --update           Update existing providers (default: skip)
    --clear                Clear all existing providers before import
    --skip-backup          Skip creating backup before import
    -v, --verbose          Verbose output
    -h, --help             Show this help message

Examples:
    # Basic import
    $0 -i providers.json
    
    # Dry run to preview changes
    $0 -i providers.json --dry-run
    
    # Import encrypted file
    $0 -i providers.json.enc
    
    # Update existing providers
    $0 -i providers.json --update
    
    # Clear and replace all providers
    $0 -i providers.json --clear --force

Security Notice:
    - Backup is created by default before import
    - Use --dry-run to preview changes first
    - API keys are imported as-is from the export file

EOF
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            echo "Use -h for help"
            exit 1
            ;;
    esac
done

# Validate input file
if [ -z "$INPUT_FILE" ]; then
    echo -e "${RED}Error: Input file required${NC}"
    echo "Use -i to specify the input file"
    echo "Use -h for help"
    exit 1
fi

if [ ! -f "$INPUT_FILE" ]; then
    echo -e "${RED}Error: Input file not found: $INPUT_FILE${NC}"
    exit 1
fi

# Header
echo "=========================================="
echo "Conduit Provider Configuration Import"
echo "=========================================="
echo ""

# Check database connection
if ! check_db_connection; then
    exit 1
fi

# Check if tables exist
if ! check_tables_exist; then
    echo -e "${RED}Error: Required tables not found in database${NC}"
    echo "Please ensure Conduit is properly initialized"
    exit 1
fi

# Handle encrypted files
TEMP_JSON=""
if [[ "$INPUT_FILE" == *.enc ]]; then
    echo "Detected encrypted file. Decrypting..."
    TEMP_JSON=$(mktemp)
    
    if ! openssl enc -aes-256-cbc -d -in "$INPUT_FILE" -out "$TEMP_JSON" -pbkdf2; then
        echo -e "${RED}✗ Decryption failed${NC}"
        rm -f "$TEMP_JSON"
        exit 1
    fi
    
    echo -e "${GREEN}✓ File decrypted successfully${NC}"
    INPUT_FILE="$TEMP_JSON"
fi

# Validate JSON structure
echo -n "Validating JSON structure... "
if command -v jq &> /dev/null; then
    if ! jq empty "$INPUT_FILE" 2>/dev/null; then
        echo -e "${RED}✗ Invalid JSON${NC}"
        [ -n "$TEMP_JSON" ] && rm -f "$TEMP_JSON"
        exit 1
    fi
    echo -e "${GREEN}✓${NC}"
else
    # Fall back to python if jq is not available
    if command -v python3 &> /dev/null; then
        if ! python3 -m json.tool "$INPUT_FILE" > /dev/null 2>&1; then
            echo -e "${RED}✗ Invalid JSON${NC}"
            [ -n "$TEMP_JSON" ] && rm -f "$TEMP_JSON"
            exit 1
        fi
        echo -e "${GREEN}✓${NC}"
    else
        echo -e "${YELLOW}⚠ Warning: Cannot validate JSON (jq and python3 not found)${NC}"
    fi
fi

# Function to parse JSON field using available tools
parse_json_field() {
    local file="$1"
    local field="$2"
    local default="${3:-unknown}"
    
    if command -v jq &> /dev/null; then
        jq -r "$field // \"$default\"" "$file" 2>/dev/null || echo "$default"
    elif command -v python3 &> /dev/null; then
        python3 -c "import json, sys; data = json.load(open('$file')); result = data; fields = '$field'.strip('.').split('.'); [result := result.get(f, None) for f in fields if result]; print(result if result else '$default')" 2>/dev/null || echo "$default"
    else
        echo "$default"
    fi
}

# Check export version
EXPORT_VERSION=$(parse_json_field "$INPUT_FILE" '.export_version' 'unknown')
if [ "$EXPORT_VERSION" != "1.0" ]; then
    echo -e "${YELLOW}Warning: Unknown export version: $EXPORT_VERSION${NC}"
    echo "This import script expects version 1.0"
    
    if [ "$FORCE" != true ]; then
        read -p "Continue anyway? (y/N): " -n 1 -r
        echo
        if [[ ! $REPLY =~ ^[Yy]$ ]]; then
            [ -n "$TEMP_JSON" ] && rm -f "$TEMP_JSON"
            exit 1
        fi
    fi
fi

# Get statistics from export file
echo ""
echo "Export file information:"
EXPORT_DATE=$(parse_json_field "$INPUT_FILE" '.export_date' 'unknown')
EXPORT_HOST=$(parse_json_field "$INPUT_FILE" '.export_host' 'unknown')
PROVIDER_COUNT=$(parse_json_field "$INPUT_FILE" '.statistics.provider_count' '0')
KEY_COUNT=$(parse_json_field "$INPUT_FILE" '.statistics.key_count' '0')
MAPPING_COUNT=$(parse_json_field "$INPUT_FILE" '.statistics.mapping_count' '0')

echo "  Export date: $EXPORT_DATE"
echo "  Export host: $EXPORT_HOST"
echo "  Providers: $PROVIDER_COUNT"
echo "  API Keys: $KEY_COUNT"
echo "  Model Mappings: $MAPPING_COUNT"
echo ""

# Get current database statistics
echo "Current database state:"
CURRENT_PROVIDERS=$(get_table_count "Providers")
CURRENT_KEYS=$(get_table_count "ProviderKeyCredentials")
CURRENT_MAPPINGS=$(get_table_count "ModelProviderMappings")

echo "  Providers: $CURRENT_PROVIDERS"
echo "  API Keys: $CURRENT_KEYS"
echo "  Model Mappings: $CURRENT_MAPPINGS"
echo ""

# Create backup unless skipped
if [ "$SKIP_BACKUP" != true ] && [ "$DRY_RUN" != true ]; then
    BACKUP_FILE="providers-backup-$(date +%Y%m%d_%H%M%S).sql"
    if create_backup "$BACKUP_FILE"; then
        echo "Backup saved to: $BACKUP_FILE"
    else
        echo -e "${RED}Warning: Backup failed${NC}"
        if [ "$FORCE" != true ]; then
            read -p "Continue without backup? (y/N): " -n 1 -r
            echo
            if [[ ! $REPLY =~ ^[Yy]$ ]]; then
                [ -n "$TEMP_JSON" ] && rm -f "$TEMP_JSON"
                exit 1
            fi
        fi
    fi
    echo ""
fi

# Confirm action
if [ "$DRY_RUN" = true ]; then
    echo -e "${BLUE}DRY RUN MODE - No changes will be made${NC}"
elif [ "$CLEAR_BEFORE_IMPORT" = true ]; then
    echo -e "${YELLOW}⚠ WARNING: This will CLEAR all existing providers before import!${NC}"
    
    if [ "$FORCE" != true ]; then
        read -p "Are you sure you want to clear and replace all providers? (y/N): " -n 1 -r
        echo
        if [[ ! $REPLY =~ ^[Yy]$ ]]; then
            [ -n "$TEMP_JSON" ] && rm -f "$TEMP_JSON"
            exit 1
        fi
    fi
elif [ "$FORCE" != true ]; then
    echo "This will import provider configurations into the database."
    read -p "Continue with import? (y/N): " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        [ -n "$TEMP_JSON" ] && rm -f "$TEMP_JSON"
        exit 1
    fi
fi

echo ""
echo "Starting import process..."

# Track statistics
PROVIDERS_IMPORTED=0
PROVIDERS_SKIPPED=0
PROVIDERS_UPDATED=0
KEYS_IMPORTED=0
KEYS_SKIPPED=0
MAPPINGS_IMPORTED=0
MAPPINGS_SKIPPED=0

# Clear existing data if requested
if [ "$CLEAR_BEFORE_IMPORT" = true ] && [ "$DRY_RUN" != true ]; then
    echo "Clearing existing data..."
    exec_psql "DELETE FROM \"ModelProviderMappings\";"
    exec_psql "DELETE FROM \"ProviderKeyCredentials\";"
    exec_psql "DELETE FROM \"Providers\";"
    echo -e "${GREEN}✓ Existing data cleared${NC}"
    echo ""
fi

# Import Providers
echo "Importing providers..."

# Function to extract JSON array items
extract_json_array() {
    local file="$1"
    local array_path="$2"
    
    if command -v jq &> /dev/null; then
        jq -c "$array_path[]" "$file" 2>/dev/null
    elif command -v python3 &> /dev/null; then
        python3 -c "
import json
data = json.load(open('$file'))
array = data
for part in '$array_path'.strip('.').split('.'):
    if part.endswith('[]'):
        part = part[:-2]
    array = array.get(part, []) if isinstance(array, dict) else []
for item in array:
    print(json.dumps(item))
" 2>/dev/null
    fi
}

# Function to parse JSON object field
parse_json_object_field() {
    local json="$1"
    local field="$2"
    local default="${3:-null}"
    
    if command -v jq &> /dev/null; then
        echo "$json" | jq -r ".$field // \"$default\"" 2>/dev/null || echo "$default"
    elif command -v python3 &> /dev/null; then
        echo "$json" | python3 -c "
import json, sys
data = json.loads(sys.stdin.read())
result = data.get('$field', '$default')
print(result if result is not None else '$default')
" 2>/dev/null || echo "$default"
    else
        echo "$default"
    fi
}

# Process each provider
extract_json_array "$INPUT_FILE" '.providers' | while IFS= read -r provider; do
    PROVIDER_ID=$(parse_json_object_field "$provider" 'Id')
    PROVIDER_TYPE=$(parse_json_object_field "$provider" 'ProviderType')
    PROVIDER_NAME=$(parse_json_object_field "$provider" 'ProviderName')
    BASE_URL=$(parse_json_object_field "$provider" 'BaseUrl' 'null')
    IS_ENABLED=$(parse_json_object_field "$provider" 'IsEnabled')
    CREATED_AT=$(parse_json_object_field "$provider" 'CreatedAt')
    UPDATED_AT=$(parse_json_object_field "$provider" 'UpdatedAt')
    
    # Validate provider type
    if ! validate_provider_type "$PROVIDER_TYPE"; then
        echo -e "  ${YELLOW}⚠ Skipping provider '$PROVIDER_NAME' - invalid type: $PROVIDER_TYPE${NC}"
        ((PROVIDERS_SKIPPED++))
        continue
    fi
    
    # Check if provider exists
    EXISTING=$(exec_psql "SELECT \"Id\" FROM \"Providers\" WHERE \"Id\" = $PROVIDER_ID;" | tr -d ' \r\n')
    
    if [ -n "$EXISTING" ]; then
        if [ "$UPDATE_EXISTING" = true ]; then
            if [ "$DRY_RUN" = true ]; then
                echo "  [DRY RUN] Would update provider: $PROVIDER_NAME (ID: $PROVIDER_ID)"
            else
                # Update existing provider
                SQL="UPDATE \"Providers\" SET 
                    \"ProviderType\" = $PROVIDER_TYPE,
                    \"ProviderName\" = '$PROVIDER_NAME',
                    \"BaseUrl\" = $([ "$BASE_URL" = "null" ] && echo "NULL" || echo "'$BASE_URL'"),
                    \"IsEnabled\" = $IS_ENABLED,
                    \"UpdatedAt\" = NOW()
                    WHERE \"Id\" = $PROVIDER_ID;"
                
                exec_psql "$SQL"
                echo -e "  ${GREEN}✓${NC} Updated: $PROVIDER_NAME (ID: $PROVIDER_ID)"
                ((PROVIDERS_UPDATED++))
            fi
        else
            echo "  ${YELLOW}⚠${NC} Skipped: $PROVIDER_NAME (ID: $PROVIDER_ID) - already exists"
            ((PROVIDERS_SKIPPED++))
        fi
    else
        if [ "$DRY_RUN" = true ]; then
            echo "  [DRY RUN] Would import provider: $PROVIDER_NAME (ID: $PROVIDER_ID)"
        else
            # Insert new provider with specific ID
            SQL="INSERT INTO \"Providers\" (
                \"Id\", \"ProviderType\", \"ProviderName\", \"BaseUrl\", 
                \"IsEnabled\", \"CreatedAt\", \"UpdatedAt\"
            ) VALUES (
                $PROVIDER_ID, $PROVIDER_TYPE, '$PROVIDER_NAME', 
                $([ "$BASE_URL" = "null" ] && echo "NULL" || echo "'$BASE_URL'"),
                $IS_ENABLED, '$CREATED_AT', '$UPDATED_AT'
            ) ON CONFLICT (\"Id\") DO NOTHING;"
            
            exec_psql "$SQL"
            echo -e "  ${GREEN}✓${NC} Imported: $PROVIDER_NAME (ID: $PROVIDER_ID)"
            ((PROVIDERS_IMPORTED++))
        fi
    fi
done

# Reset sequence for Providers table
if [ "$DRY_RUN" != true ]; then
    MAX_ID=$(exec_psql "SELECT COALESCE(MAX(\"Id\"), 0) FROM \"Providers\";" | tr -d ' \r\n')
    exec_psql "SELECT setval('\"Providers_Id_seq\"', GREATEST($MAX_ID, 1), true);" > /dev/null
fi

echo ""

# Import Provider Key Credentials
echo "Importing API keys..."

extract_json_array "$INPUT_FILE" '.provider_keys' | while IFS= read -r key; do
    KEY_ID=$(parse_json_object_field "$key" 'Id')
    PROVIDER_ID=$(parse_json_object_field "$key" 'ProviderId')
    ACCOUNT_GROUP=$(parse_json_object_field "$key" 'ProviderAccountGroup')
    API_KEY=$(parse_json_object_field "$key" 'ApiKey' 'null')
    BASE_URL=$(parse_json_object_field "$key" 'BaseUrl' 'null')
    ORGANIZATION=$(parse_json_object_field "$key" 'Organization' 'null')
    KEY_NAME=$(parse_json_object_field "$key" 'KeyName' 'null')
    IS_PRIMARY=$(parse_json_object_field "$key" 'IsPrimary')
    IS_ENABLED=$(parse_json_object_field "$key" 'IsEnabled')
    CREATED_AT=$(parse_json_object_field "$key" 'CreatedAt')
    UPDATED_AT=$(parse_json_object_field "$key" 'UpdatedAt')
    
    # Check if provider exists
    PROVIDER_EXISTS=$(exec_psql "SELECT \"Id\" FROM \"Providers\" WHERE \"Id\" = $PROVIDER_ID;" | tr -d ' \r\n')
    
    if [ -z "$PROVIDER_EXISTS" ]; then
        echo -e "  ${YELLOW}⚠ Skipping key - provider $PROVIDER_ID not found${NC}"
        ((KEYS_SKIPPED++))
        continue
    fi
    
    # Check if key exists
    EXISTING=$(exec_psql "SELECT \"Id\" FROM \"ProviderKeyCredentials\" WHERE \"Id\" = $KEY_ID;" | tr -d ' \r\n')
    
    if [ -n "$EXISTING" ]; then
        echo "  ${YELLOW}⚠${NC} Skipped: Key ID $KEY_ID - already exists"
        ((KEYS_SKIPPED++))
    else
        if [ "$DRY_RUN" = true ]; then
            echo "  [DRY RUN] Would import key: ${KEY_NAME:-Unnamed} (ID: $KEY_ID)"
        else
            # Handle primary key constraint
            if [ "$IS_PRIMARY" = "true" ]; then
                # Unset any existing primary keys for this provider
                exec_psql "UPDATE \"ProviderKeyCredentials\" SET \"IsPrimary\" = false WHERE \"ProviderId\" = $PROVIDER_ID;"
            fi
            
            # Insert new key with specific ID
            SQL="INSERT INTO \"ProviderKeyCredentials\" (
                \"Id\", \"ProviderId\", \"ProviderAccountGroup\", \"ApiKey\",
                \"BaseUrl\", \"Organization\", \"KeyName\", \"IsPrimary\",
                \"IsEnabled\", \"CreatedAt\", \"UpdatedAt\"
            ) VALUES (
                $KEY_ID, $PROVIDER_ID, $ACCOUNT_GROUP, 
                $([ "$API_KEY" = "null" ] && echo "NULL" || echo "'$API_KEY'"),
                $([ "$BASE_URL" = "null" ] && echo "NULL" || echo "'$BASE_URL'"),
                $([ "$ORGANIZATION" = "null" ] && echo "NULL" || echo "'$ORGANIZATION'"),
                $([ "$KEY_NAME" = "null" ] && echo "NULL" || echo "'$KEY_NAME'"),
                $IS_PRIMARY, $IS_ENABLED, '$CREATED_AT', '$UPDATED_AT'
            ) ON CONFLICT (\"Id\") DO NOTHING;"
            
            exec_psql "$SQL"
            echo -e "  ${GREEN}✓${NC} Imported: ${KEY_NAME:-API Key} (ID: $KEY_ID)"
            ((KEYS_IMPORTED++))
        fi
    fi
done

# Reset sequence for ProviderKeyCredentials table
if [ "$DRY_RUN" != true ]; then
    MAX_ID=$(exec_psql "SELECT COALESCE(MAX(\"Id\"), 0) FROM \"ProviderKeyCredentials\";" | tr -d ' \r\n')
    exec_psql "SELECT setval('\"ProviderKeyCredentials_Id_seq\"', GREATEST($MAX_ID, 1), true);" > /dev/null
fi

echo ""

# Import Model Provider Mappings (if present)
# Check if model_mappings exists in the file
HAS_MAPPINGS=$(parse_json_field "$INPUT_FILE" '.model_mappings' 'null')
if [ "$HAS_MAPPINGS" != "null" ] && [ "$HAS_MAPPINGS" != "[]" ]; then
    echo "Importing model mappings..."
    
    extract_json_array "$INPUT_FILE" '.model_mappings' | while IFS= read -r mapping; do
        MAPPING_ID=$(parse_json_object_field "$mapping" 'Id')
        MODEL_ALIAS=$(parse_json_object_field "$mapping" 'ModelAlias')
        PROVIDER_MODEL_ID=$(parse_json_object_field "$mapping" 'ProviderModelId')
        PROVIDER_ID=$(parse_json_object_field "$mapping" 'ProviderId')
        IS_ENABLED=$(parse_json_object_field "$mapping" 'IsEnabled')
        CREATED_AT=$(parse_json_object_field "$mapping" 'CreatedAt')
        UPDATED_AT=$(parse_json_object_field "$mapping" 'UpdatedAt')
        ASSOCIATION_ID=$(parse_json_object_field "$mapping" 'ModelProviderTypeAssociationId' 'null')
        
        # Check if provider exists
        PROVIDER_EXISTS=$(exec_psql "SELECT \"Id\" FROM \"Providers\" WHERE \"Id\" = $PROVIDER_ID;" | tr -d ' \r\n')
        
        if [ -z "$PROVIDER_EXISTS" ]; then
            echo -e "  ${YELLOW}⚠ Skipping mapping - provider $PROVIDER_ID not found${NC}"
            ((MAPPINGS_SKIPPED++))
            continue
        fi
        
        # Check if mapping exists
        EXISTING=$(exec_psql "SELECT \"Id\" FROM \"ModelProviderMappings\" WHERE \"Id\" = $MAPPING_ID;" | tr -d ' \r\n')
        
        if [ -n "$EXISTING" ]; then
            echo "  ${YELLOW}⚠${NC} Skipped: Mapping $MODEL_ALIAS - already exists"
            ((MAPPINGS_SKIPPED++))
        else
            if [ "$DRY_RUN" = true ]; then
                echo "  [DRY RUN] Would import mapping: $MODEL_ALIAS -> $PROVIDER_MODEL_ID"
            else
                # Note: ModelProviderTypeAssociationId might not exist in target system
                # We'll skip it if not valid
                if [ "$ASSOCIATION_ID" != "null" ]; then
                    ASSOC_EXISTS=$(exec_psql "SELECT \"Id\" FROM \"ModelProviderTypeAssociations\" WHERE \"Id\" = $ASSOCIATION_ID;" | tr -d ' \r\n')
                    if [ -z "$ASSOC_EXISTS" ]; then
                        echo -e "  ${YELLOW}⚠ Warning: ModelProviderTypeAssociation $ASSOCIATION_ID not found - importing without it${NC}"
                        ASSOCIATION_ID="null"
                    fi
                fi
                
                # Insert new mapping
                SQL="INSERT INTO \"ModelProviderMappings\" (
                    \"Id\", \"ModelAlias\", \"ProviderModelId\", \"ProviderId\",
                    \"IsEnabled\", \"CreatedAt\", \"UpdatedAt\"
                    $([ "$ASSOCIATION_ID" != "null" ] && echo ", \"ModelProviderTypeAssociationId\"" || echo "")
                ) VALUES (
                    $MAPPING_ID, '$MODEL_ALIAS', '$PROVIDER_MODEL_ID', $PROVIDER_ID,
                    $IS_ENABLED, '$CREATED_AT', '$UPDATED_AT'
                    $([ "$ASSOCIATION_ID" != "null" ] && echo ", $ASSOCIATION_ID" || echo "")
                ) ON CONFLICT (\"Id\") DO NOTHING;"
                
                exec_psql "$SQL"
                echo -e "  ${GREEN}✓${NC} Imported: $MODEL_ALIAS -> $PROVIDER_MODEL_ID"
                ((MAPPINGS_IMPORTED++))
            fi
        fi
    done
    
    # Reset sequence for ModelProviderMappings table
    if [ "$DRY_RUN" != true ]; then
        MAX_ID=$(exec_psql "SELECT COALESCE(MAX(\"Id\"), 0) FROM \"ModelProviderMappings\";" | tr -d ' \r\n')
        exec_psql "SELECT setval('\"ModelProviderMappings_Id_seq\"', GREATEST($MAX_ID, 1), true);" > /dev/null
    fi
    
    echo ""
fi

# Clean up temp file if it was created
[ -n "$TEMP_JSON" ] && rm -f "$TEMP_JSON"

# Summary
echo "=========================================="
echo "Import Summary"
echo "=========================================="

if [ "$DRY_RUN" = true ]; then
    echo -e "${BLUE}DRY RUN COMPLETED - No changes were made${NC}"
else
    echo -e "${GREEN}Import completed successfully!${NC}"
fi

echo ""
echo "Providers:"
echo "  Imported: $PROVIDERS_IMPORTED"
echo "  Updated: $PROVIDERS_UPDATED"
echo "  Skipped: $PROVIDERS_SKIPPED"

echo ""
echo "API Keys:"
echo "  Imported: $KEYS_IMPORTED"
echo "  Skipped: $KEYS_SKIPPED"

if [ "$MAPPING_COUNT" -gt 0 ]; then
    echo ""
    echo "Model Mappings:"
    echo "  Imported: $MAPPINGS_IMPORTED"
    echo "  Skipped: $MAPPINGS_SKIPPED"
fi

if [ "$DRY_RUN" != true ]; then
    echo ""
    echo "Next steps:"
    echo "  1. Restart Conduit services to load new configurations"
    echo "  2. Test provider connections through the Admin API"
    echo "  3. Verify model mappings are working correctly"
    
    # Suggest cache invalidation
    echo ""
    echo -e "${YELLOW}Note: Provider caches may need to be invalidated.${NC}"
    echo "Consider restarting services or triggering cache refresh through Admin API."
fi

echo ""