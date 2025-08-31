#!/bin/bash
# Export model configurations from Conduit database
# This includes model authors, series, models, and associations

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Source common functions
source "$SCRIPT_DIR/lib/db-common.sh"

# Default settings
OUTPUT_FILE=""
VERBOSE=false
INCLUDE_ASSOCIATIONS=true

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -o|--output)
            OUTPUT_FILE="$2"
            shift 2
            ;;
        --no-associations)
            INCLUDE_ASSOCIATIONS=false
            shift
            ;;
        -v|--verbose)
            VERBOSE=true
            shift
            ;;
        -h|--help)
            cat << EOF
Usage: $0 [OPTIONS]

Export model configurations from Conduit database to JSON file.
This includes model authors, series, models, and their provider associations.

Options:
    -o, --output FILE      Output file path (default: models-export-TIMESTAMP.json)
    --no-associations      Exclude model provider associations from export
    -v, --verbose          Verbose output
    -h, --help             Show this help message

Examples:
    # Basic export
    $0 -o models.json
    
    # Export without associations
    $0 -o models-minimal.json --no-associations

Notes:
    - The export contains model metadata only (no sensitive data)
    - Relationships are preserved using names instead of IDs
    - The export format is designed for easy import and merging

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
    OUTPUT_FILE="models-export-${TIMESTAMP}.json"
fi

# Header
echo "=========================================="
echo "Conduit Model Configuration Export"
echo "=========================================="
echo ""

# Check database connection
if ! check_db_connection; then
    exit 1
fi

# Check if tables exist
if ! check_model_tables_exist; then
    echo -e "${RED}Error: Required tables not found in database${NC}"
    echo "Please ensure Conduit is properly initialized with model tables"
    exit 1
fi

# Get statistics
echo "Checking data to export..."
STATS=$(get_model_statistics)
IFS=':' read -r AUTHOR_COUNT SERIES_COUNT MODEL_COUNT ASSOC_COUNT <<< "$STATS"

echo "  Model Authors: $AUTHOR_COUNT"
echo "  Model Series: $SERIES_COUNT"
echo "  Models: $MODEL_COUNT"
echo "  Model Associations: $ASSOC_COUNT"

if [ -z "$MODEL_COUNT" ] || [ "$MODEL_COUNT" -eq 0 ]; then
    echo -e "${YELLOW}Warning: No models found to export${NC}"
    echo "Consider importing a model dataset first"
    exit 0
fi

echo ""
echo "Starting export..."

# Create temporary file for JSON
TEMP_JSON=$(mktemp)

# Start JSON structure
cat > "$TEMP_JSON" << EOF
{
  "export_version": "1.0",
  "export_date": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
  "export_host": "$(hostname)",
  "statistics": {
    "author_count": $AUTHOR_COUNT,
    "series_count": $SERIES_COUNT,
    "model_count": $MODEL_COUNT,
    "association_count": $ASSOC_COUNT
  },
EOF

# Export Model Authors
echo -n "  Exporting model authors... "
echo '  "model_authors": [' >> "$TEMP_JSON"

AUTHORS=$(export_model_authors)
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
done <<< "$AUTHORS"
echo "" >> "$TEMP_JSON"
echo "  ]," >> "$TEMP_JSON"
echo -e "${GREEN}✓${NC} ($AUTHOR_COUNT authors)"

# Export Model Series
echo -n "  Exporting model series... "
echo '  "model_series": [' >> "$TEMP_JSON"

SERIES=$(export_model_series)
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
done <<< "$SERIES"
echo "" >> "$TEMP_JSON"
echo "  ]," >> "$TEMP_JSON"
echo -e "${GREEN}✓${NC} ($SERIES_COUNT series)"

# Export Models
echo -n "  Exporting models... "
echo '  "models": [' >> "$TEMP_JSON"

MODELS=$(export_models)
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
done <<< "$MODELS"
echo "" >> "$TEMP_JSON"

if [ "$INCLUDE_ASSOCIATIONS" = true ]; then
    echo "  ]," >> "$TEMP_JSON"
    echo -e "${GREEN}✓${NC} ($MODEL_COUNT models)"
    
    # Export Model Provider Type Associations
    echo -n "  Exporting model associations... "
    echo '  "model_associations": [' >> "$TEMP_JSON"
    
    ASSOCIATIONS=$(export_model_associations)
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
    done <<< "$ASSOCIATIONS"
    echo "" >> "$TEMP_JSON"
    echo "  ]" >> "$TEMP_JSON"
    echo -e "${GREEN}✓${NC} ($ASSOC_COUNT associations)"
else
    echo "  ]" >> "$TEMP_JSON"
    echo -e "${GREEN}✓${NC} ($MODEL_COUNT models)"
fi

# Close JSON
echo "}" >> "$TEMP_JSON"

# Move temp file to output
mv "$TEMP_JSON" "$OUTPUT_FILE"

# Pretty-print the JSON if jq is available
if command -v jq &> /dev/null; then
    jq '.' "$OUTPUT_FILE" > "${OUTPUT_FILE}.tmp" && mv "${OUTPUT_FILE}.tmp" "$OUTPUT_FILE"
fi

echo ""
echo -e "${GREEN}Export completed successfully!${NC}"
echo "Output file: $OUTPUT_FILE"
echo ""
echo "Export Summary:"
echo "  - Model authors exported: $AUTHOR_COUNT"
echo "  - Model series exported: $SERIES_COUNT"
echo "  - Models exported: $MODEL_COUNT"
if [ "$INCLUDE_ASSOCIATIONS" = true ]; then
    echo "  - Associations exported: $ASSOC_COUNT"
fi
echo ""
echo "This file can be imported into another Conduit instance using:"
echo "  ./scripts/model-config/import-models.sh -i $OUTPUT_FILE"
echo ""