#!/bin/bash
# Export provider configurations from Conduit database
# This includes providers, API keys, and model mappings

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Source common functions
source "$SCRIPT_DIR/lib/db-common.sh"
source "$SCRIPT_DIR/lib/provider-types.sh"

# Default output file
OUTPUT_FILE=""
ENCRYPT=false
INCLUDE_MAPPINGS=true
VERBOSE=false

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -o|--output)
            OUTPUT_FILE="$2"
            shift 2
            ;;
        -e|--encrypt)
            ENCRYPT=true
            shift
            ;;
        --no-mappings)
            INCLUDE_MAPPINGS=false
            shift
            ;;
        -v|--verbose)
            VERBOSE=true
            shift
            ;;
        -h|--help)
            cat << EOF
Usage: $0 [OPTIONS]

Export provider configurations from Conduit database to JSON file.

Options:
    -o, --output FILE      Output file path (default: providers-export-TIMESTAMP.json)
    -e, --encrypt          Encrypt the output file with AES-256
    --no-mappings          Exclude model mappings from export
    -v, --verbose          Verbose output
    -h, --help             Show this help message

Examples:
    # Basic export
    $0 -o providers.json
    
    # Encrypted export
    $0 -o providers.json.enc -e
    
    # Export without model mappings
    $0 -o providers-minimal.json --no-mappings

Security Notice:
    The export file contains sensitive API keys. Please handle with care:
    - Use encryption (-e) when storing or transmitting
    - Set appropriate file permissions (chmod 600)
    - Delete exports after use if not needed

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

# Set default output file if not specified
if [ -z "$OUTPUT_FILE" ]; then
    TIMESTAMP=$(date +%Y%m%d_%H%M%S)
    OUTPUT_FILE="providers-export-${TIMESTAMP}.json"
    if [ "$ENCRYPT" = true ]; then
        OUTPUT_FILE="${OUTPUT_FILE}.enc"
    fi
fi

# Header
echo "=========================================="
echo "Conduit Provider Configuration Export"
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

# Get counts
echo "Checking data to export..."
PROVIDER_COUNT=$(get_table_count "Providers")
KEY_COUNT=$(get_table_count "ProviderKeyCredentials")
MAPPING_COUNT=$(get_table_count "ModelProviderMappings")

echo "  Providers: $PROVIDER_COUNT"
echo "  API Keys: $KEY_COUNT"
if [ "$INCLUDE_MAPPINGS" = true ]; then
    echo "  Model Mappings: $MAPPING_COUNT"
fi
echo ""

if [ "$PROVIDER_COUNT" -eq 0 ]; then
    echo -e "${YELLOW}Warning: No providers found to export${NC}"
    exit 0
fi

# Start building JSON
echo "Exporting data..."

# Create temporary file for JSON
TEMP_JSON=$(mktemp)

# Start JSON structure
cat > "$TEMP_JSON" << EOF
{
  "export_version": "1.0",
  "export_date": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
  "export_host": "$(hostname)",
  "statistics": {
    "provider_count": $PROVIDER_COUNT,
    "key_count": $KEY_COUNT,
    "mapping_count": $MAPPING_COUNT
  },
EOF

# Export Providers
echo -n "  Exporting providers... "
echo '  "providers": [' >> "$TEMP_JSON"

PROVIDER_SQL="SELECT row_to_json(p) FROM (
    SELECT 
        \"Id\",
        \"ProviderType\",
        \"ProviderName\",
        \"BaseUrl\",
        \"IsEnabled\",
        \"CreatedAt\",
        \"UpdatedAt\"
    FROM \"Providers\"
    ORDER BY \"Id\"
) p;"

PROVIDERS=$(exec_psql_json "$PROVIDER_SQL")
FIRST=true
while IFS= read -r line; do
    if [ -n "$line" ]; then
        if [ "$FIRST" = true ]; then
            FIRST=false
        else
            echo "," >> "$TEMP_JSON"
        fi
        echo -n "    $line" >> "$TEMP_JSON"
    fi
done <<< "$PROVIDERS"
echo "" >> "$TEMP_JSON"
echo "  ]," >> "$TEMP_JSON"
echo -e "${GREEN}✓${NC}"

# Export Provider Key Credentials
echo -n "  Exporting API keys... "
echo '  "provider_keys": [' >> "$TEMP_JSON"

KEY_SQL="SELECT row_to_json(k) FROM (
    SELECT 
        \"Id\",
        \"ProviderId\",
        \"ProviderAccountGroup\",
        \"ApiKey\",
        \"BaseUrl\",
        \"Organization\",
        \"KeyName\",
        \"IsPrimary\",
        \"IsEnabled\",
        \"CreatedAt\",
        \"UpdatedAt\"
    FROM \"ProviderKeyCredentials\"
    ORDER BY \"ProviderId\", \"Id\"
) k;"

KEYS=$(exec_psql_json "$KEY_SQL")
FIRST=true
while IFS= read -r line; do
    if [ -n "$line" ]; then
        if [ "$FIRST" = true ]; then
            FIRST=false
        else
            echo "," >> "$TEMP_JSON"
        fi
        echo -n "    $line" >> "$TEMP_JSON"
    fi
done <<< "$KEYS"
echo "" >> "$TEMP_JSON"

if [ "$INCLUDE_MAPPINGS" = true ]; then
    echo "  ]," >> "$TEMP_JSON"
    
    # Export Model Provider Mappings
    echo -n "  Exporting model mappings... "
    echo '  "model_mappings": [' >> "$TEMP_JSON"
    
    MAPPING_SQL="SELECT row_to_json(m) FROM (
        SELECT 
            \"Id\",
            \"ModelAlias\",
            \"ProviderModelId\",
            \"ProviderId\",
            \"IsEnabled\",
            \"CreatedAt\",
            \"UpdatedAt\",
            \"ModelProviderTypeAssociationId\"
        FROM \"ModelProviderMappings\"
        ORDER BY \"ProviderId\", \"ModelAlias\"
    ) m;"
    
    MAPPINGS=$(exec_psql_json "$MAPPING_SQL")
    FIRST=true
    while IFS= read -r line; do
        if [ -n "$line" ]; then
            if [ "$FIRST" = true ]; then
                FIRST=false
            else
                echo "," >> "$TEMP_JSON"
            fi
            echo -n "    $line" >> "$TEMP_JSON"
        fi
    done <<< "$MAPPINGS"
    echo "" >> "$TEMP_JSON"
    echo "  ]" >> "$TEMP_JSON"
    echo -e "${GREEN}✓${NC}"
else
    echo "  ]" >> "$TEMP_JSON"
    echo -e "${GREEN}✓${NC}"
fi

# Close JSON
echo "}" >> "$TEMP_JSON"

# Handle encryption if requested
if [ "$ENCRYPT" = true ]; then
    echo ""
    echo "Encrypting export file..."
    echo -e "${YELLOW}You will be prompted for an encryption password${NC}"
    
    if openssl enc -aes-256-cbc -salt -in "$TEMP_JSON" -out "$OUTPUT_FILE" -pbkdf2; then
        echo -e "${GREEN}✓ File encrypted successfully${NC}"
        rm "$TEMP_JSON"
        
        # Set restrictive permissions
        chmod 600 "$OUTPUT_FILE"
        
        echo ""
        echo -e "${GREEN}Export completed successfully!${NC}"
        echo "Encrypted file: $OUTPUT_FILE"
        echo ""
        echo "To decrypt later, use:"
        echo "  openssl enc -aes-256-cbc -d -in $OUTPUT_FILE -out providers.json -pbkdf2"
    else
        echo -e "${RED}✗ Encryption failed${NC}"
        rm "$TEMP_JSON"
        exit 1
    fi
else
    # Move temp file to output
    mv "$TEMP_JSON" "$OUTPUT_FILE"
    
    # Set restrictive permissions for security
    chmod 600 "$OUTPUT_FILE"
    
    # Pretty-print the JSON
    if command -v jq &> /dev/null; then
        jq '.' "$OUTPUT_FILE" > "${OUTPUT_FILE}.tmp" && mv "${OUTPUT_FILE}.tmp" "$OUTPUT_FILE"
    fi
    
    echo ""
    echo -e "${GREEN}Export completed successfully!${NC}"
    echo "Output file: $OUTPUT_FILE"
    echo ""
    echo -e "${YELLOW}⚠ Security Warning:${NC}"
    echo "This file contains sensitive API keys. Please:"
    echo "  - Encrypt it for storage/transfer: $0 -e -o encrypted.json.enc"
    echo "  - Set restrictive permissions: chmod 600 $OUTPUT_FILE"
    echo "  - Delete after use if not needed: rm $OUTPUT_FILE"
fi

echo ""
echo "Export Summary:"
echo "  - Providers exported: $PROVIDER_COUNT"
echo "  - API keys exported: $KEY_COUNT"
if [ "$INCLUDE_MAPPINGS" = true ]; then
    echo "  - Model mappings exported: $MAPPING_COUNT"
fi
echo ""