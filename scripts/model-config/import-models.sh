#!/bin/bash
# Import model configurations into Conduit via Admin API
# Handles relationship resolution by matching names

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Source common functions
source "$SCRIPT_DIR/lib/api-common.sh"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Default settings
INPUT_FILE=""
DRY_RUN=false
VERBOSE=false
SKIP_ASSOCIATIONS=false

# Tracking counters
AUTHORS_CREATED=0
AUTHORS_UPDATED=0
AUTHORS_FAILED=0
SERIES_CREATED=0
SERIES_UPDATED=0
SERIES_FAILED=0
MODELS_CREATED=0
MODELS_UPDATED=0
MODELS_FAILED=0
ASSOCS_CREATED=0
ASSOCS_UPDATED=0
ASSOCS_FAILED=0

# Failed items log
declare -a FAILED_ITEMS

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
        --skip-associations)
            SKIP_ASSOCIATIONS=true
            shift
            ;;
        -v|--verbose)
            VERBOSE=true
            shift
            ;;
        -h|--help)
            cat << EOF
Usage: $0 -i INPUT_FILE [OPTIONS]

Import model configurations into Conduit via Admin API.
Updates existing models based on name matching.

Required:
    -i, --input FILE       Input JSON file from export-models.sh

Options:
    -d, --dry-run          Preview changes without importing
    --skip-associations    Skip importing model associations
    -v, --verbose          Verbose output
    -h, --help             Show this help message

Examples:
    # Basic import
    $0 -i models.json
    
    # Dry run to preview
    $0 -i models.json --dry-run
    
    # Import without associations
    $0 -i models.json --skip-associations

Notes:
    - Import data is treated as source of truth
    - Existing models are updated when names match
    - Relationships are resolved by matching names
    - Failed items are reported at the end

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
    exit 1
fi

if [ ! -f "$INPUT_FILE" ]; then
    echo -e "${RED}Error: Input file not found: $INPUT_FILE${NC}"
    exit 1
fi

# Header
echo "=========================================="
echo "Conduit Model Configuration Import"
echo "=========================================="
echo ""

# Check API connection
if ! check_api_connection; then
    echo -e "${RED}Error: Cannot connect to Admin API${NC}"
    echo "Please ensure Conduit is running and accessible"
    exit 1
fi

# Get master key
MASTER_KEY=$(get_master_key)
if [ -z "$MASTER_KEY" ]; then
    echo -e "${RED}Error: Cannot retrieve master key${NC}"
    exit 1
fi

# Validate JSON structure
echo -n "Validating JSON structure... "
if command -v jq &> /dev/null; then
    if ! jq empty "$INPUT_FILE" 2>/dev/null; then
        echo -e "${RED}✗ Invalid JSON${NC}"
        exit 1
    fi
elif command -v python3 &> /dev/null; then
    if ! python3 -m json.tool "$INPUT_FILE" > /dev/null 2>&1; then
        echo -e "${RED}✗ Invalid JSON${NC}"
        exit 1
    fi
fi
echo -e "${GREEN}✓${NC}"

# Check export version
EXPORT_VERSION=$(parse_json_field "$(cat "$INPUT_FILE")" ".export_version" "unknown")
if [ "$EXPORT_VERSION" != "1.0" ]; then
    echo -e "${YELLOW}Warning: Unknown export version: $EXPORT_VERSION${NC}"
fi

# Get statistics from export file
echo ""
echo "Export file information:"
EXPORT_DATE=$(parse_json_field "$(cat "$INPUT_FILE")" ".export_date" "unknown")
echo "  Export date: $EXPORT_DATE"

AUTHOR_COUNT=$(parse_json_field "$(cat "$INPUT_FILE")" ".statistics.author_count" "0")
SERIES_COUNT=$(parse_json_field "$(cat "$INPUT_FILE")" ".statistics.series_count" "0")
MODEL_COUNT=$(parse_json_field "$(cat "$INPUT_FILE")" ".statistics.model_count" "0")
ASSOC_COUNT=$(parse_json_field "$(cat "$INPUT_FILE")" ".statistics.association_count" "0")

echo "  Model Authors: $AUTHOR_COUNT"
echo "  Model Series: $SERIES_COUNT"
echo "  Models: $MODEL_COUNT"
echo "  Associations: $ASSOC_COUNT"
echo ""

if [ "$DRY_RUN" = true ]; then
    echo -e "${BLUE}DRY RUN MODE - No changes will be made${NC}"
    echo ""
fi

# Phase 1: Import Model Authors
echo "Phase 1: Importing Model Authors..."
if command -v jq &> /dev/null; then
    AUTHORS=$(cat "$INPUT_FILE" | jq -c '.model_authors[]' 2>/dev/null)
else
    AUTHORS=$(cat "$INPUT_FILE" | python3 -c "
import json, sys
data = json.load(sys.stdin)
for author in data.get('model_authors', []):
    print(json.dumps(author))
" 2>/dev/null)
fi

declare -A AUTHOR_ID_MAP

while IFS= read -r author; do
    [ -z "$author" ] && continue
    
    name=$(parse_json_field "$author" ".name" "")
    description=$(parse_json_field "$author" ".description" "")
    website_url=$(parse_json_field "$author" ".website_url" "")
    
    if [ -z "$name" ]; then
        echo -e "  ${YELLOW}⚠ Skipping author with no name${NC}"
        ((AUTHORS_FAILED++))
        continue
    fi
    
    if [ "$DRY_RUN" = true ]; then
        echo "  [DRY RUN] Would import/update author: $name"
    else
        echo -n "  Processing author: $name... "
        author_id=$(get_or_create_author "$name" "$description" "$website_url" "$MASTER_KEY")
        
        if [ -n "$author_id" ]; then
            AUTHOR_ID_MAP["$name"]="$author_id"
            echo -e "${GREEN}✓${NC} (ID: $author_id)"
            ((AUTHORS_UPDATED++))
        else
            echo -e "${RED}✗ Failed${NC}"
            ((AUTHORS_FAILED++))
            FAILED_ITEMS+=("Author: $name")
        fi
    fi
done <<< "$AUTHORS"

echo "  Authors processed: Created/Updated=$AUTHORS_UPDATED, Failed=$AUTHORS_FAILED"
echo ""

# Phase 2: Import Model Series
echo "Phase 2: Importing Model Series..."
if command -v jq &> /dev/null; then
    SERIES=$(cat "$INPUT_FILE" | jq -c '.model_series[]' 2>/dev/null)
else
    SERIES=$(cat "$INPUT_FILE" | python3 -c "
import json, sys
data = json.load(sys.stdin)
for series in data.get('model_series', []):
    print(json.dumps(series))
" 2>/dev/null)
fi

declare -A SERIES_ID_MAP

while IFS= read -r series; do
    [ -z "$series" ] && continue
    
    author_name=$(parse_json_field "$series" ".author_name" "")
    name=$(parse_json_field "$series" ".name" "")
    description=$(parse_json_field "$series" ".description" "")
    tokenizer_type=$(parse_json_field "$series" ".tokenizer_type" "cl100k_base")
    parameters=$(parse_json_field "$series" ".parameters" "{}")
    
    if [ -z "$name" ] || [ -z "$author_name" ]; then
        echo -e "  ${YELLOW}⚠ Skipping series with missing name or author${NC}"
        ((SERIES_FAILED++))
        continue
    fi
    
    if [ "$DRY_RUN" = true ]; then
        echo "  [DRY RUN] Would import/update series: $name (Author: $author_name)"
    else
        # Get author ID from map
        author_id="${AUTHOR_ID_MAP[$author_name]}"
        if [ -z "$author_id" ]; then
            echo -e "  ${YELLOW}⚠ Skipping series '$name' - author '$author_name' not found${NC}"
            ((SERIES_FAILED++))
            FAILED_ITEMS+=("Series: $name (missing author: $author_name)")
            continue
        fi
        
        echo -n "  Processing series: $name... "
        series_id=$(get_or_create_series "$author_id" "$name" "$description" "$tokenizer_type" "$parameters" "$MASTER_KEY")
        
        if [ -n "$series_id" ]; then
            SERIES_ID_MAP["${author_name}:${name}"]="$series_id"
            echo -e "${GREEN}✓${NC} (ID: $series_id)"
            ((SERIES_UPDATED++))
        else
            echo -e "${RED}✗ Failed${NC}"
            ((SERIES_FAILED++))
            FAILED_ITEMS+=("Series: $name")
        fi
    fi
done <<< "$SERIES"

echo "  Series processed: Created/Updated=$SERIES_UPDATED, Failed=$SERIES_FAILED"
echo ""

# Phase 3: Import Models
echo "Phase 3: Importing Models..."
if command -v jq &> /dev/null; then
    MODELS=$(cat "$INPUT_FILE" | jq -c '.models[]' 2>/dev/null)
else
    MODELS=$(cat "$INPUT_FILE" | python3 -c "
import json, sys
data = json.load(sys.stdin)
for model in data.get('models', []):
    print(json.dumps(model))
" 2>/dev/null)
fi

declare -A MODEL_ID_MAP

while IFS= read -r model; do
    [ -z "$model" ] && continue
    
    name=$(parse_json_field "$model" ".name" "")
    series_name=$(parse_json_field "$model" ".series_name" "")
    series_author=$(parse_json_field "$model" ".series_author" "")
    
    if [ -z "$name" ] || [ -z "$series_name" ] || [ -z "$series_author" ]; then
        echo -e "  ${YELLOW}⚠ Skipping model with missing required fields${NC}"
        ((MODELS_FAILED++))
        continue
    fi
    
    if [ "$DRY_RUN" = true ]; then
        echo "  [DRY RUN] Would import/update model: $name"
    else
        # Get series ID from map
        series_key="${series_author}:${series_name}"
        series_id="${SERIES_ID_MAP[$series_key]}"
        if [ -z "$series_id" ]; then
            echo -e "  ${YELLOW}⚠ Skipping model '$name' - series '$series_name' not found${NC}"
            ((MODELS_FAILED++))
            FAILED_ITEMS+=("Model: $name (missing series: $series_name)")
            continue
        fi
        
        echo -n "  Processing model: $name... "
        model_id=$(get_or_create_model "$name" "$series_id" "$model" "$MASTER_KEY")
        
        if [ -n "$model_id" ]; then
            MODEL_ID_MAP["$name"]="$model_id"
            echo -e "${GREEN}✓${NC} (ID: $model_id)"
            ((MODELS_UPDATED++))
        else
            echo -e "${RED}✗ Failed${NC}"
            ((MODELS_FAILED++))
            FAILED_ITEMS+=("Model: $name")
        fi
    fi
done <<< "$MODELS"

echo "  Models processed: Created/Updated=$MODELS_UPDATED, Failed=$MODELS_FAILED"
echo ""

# Phase 4: Import Model Associations (if not skipped)
if [ "$SKIP_ASSOCIATIONS" != true ] && [ "$ASSOC_COUNT" -gt 0 ]; then
    echo "Phase 4: Importing Model Associations..."
    
    if command -v jq &> /dev/null; then
        ASSOCIATIONS=$(cat "$INPUT_FILE" | jq -c '.model_associations[]' 2>/dev/null)
    else
        ASSOCIATIONS=$(cat "$INPUT_FILE" | python3 -c "
import json, sys
data = json.load(sys.stdin)
for assoc in data.get('model_associations', []):
    print(json.dumps(assoc))
" 2>/dev/null)
    fi
    
    while IFS= read -r assoc; do
        [ -z "$assoc" ] && continue
        
        model_name=$(parse_json_field "$assoc" ".model_name" "")
        identifier=$(parse_json_field "$assoc" ".identifier" "")
        provider=$(parse_json_field "$assoc" ".provider" "")
        
        if [ -z "$model_name" ] || [ -z "$identifier" ]; then
            echo -e "  ${YELLOW}⚠ Skipping association with missing required fields${NC}"
            ((ASSOCS_FAILED++))
            continue
        fi
        
        if [ "$DRY_RUN" = true ]; then
            echo "  [DRY RUN] Would import/update association: $identifier (Model: $model_name)"
        else
            # Get model ID from map
            model_id="${MODEL_ID_MAP[$model_name]}"
            if [ -z "$model_id" ]; then
                echo -e "  ${YELLOW}⚠ Skipping association '$identifier' - model '$model_name' not found${NC}"
                ((ASSOCS_FAILED++))
                FAILED_ITEMS+=("Association: $identifier (missing model: $model_name)")
                continue
            fi
            
            echo -n "  Processing association: $identifier... "
            if create_or_update_association "$model_id" "$identifier" "$provider" "$assoc" "$MASTER_KEY"; then
                echo -e "${GREEN}✓${NC}"
                ((ASSOCS_UPDATED++))
            else
                echo -e "${RED}✗ Failed${NC}"
                ((ASSOCS_FAILED++))
                FAILED_ITEMS+=("Association: $identifier for model $model_name")
            fi
        fi
    done <<< "$ASSOCIATIONS"
    
    echo "  Associations processed: Created/Updated=$ASSOCS_UPDATED, Failed=$ASSOCS_FAILED"
    echo ""
fi

# Summary
echo "=========================================="
echo "Import Summary"
echo "=========================================="

if [ "$DRY_RUN" = true ]; then
    echo -e "${BLUE}DRY RUN COMPLETED - No changes were made${NC}"
else
    echo -e "${GREEN}Import completed!${NC}"
fi

echo ""
echo "Results:"
echo "  Model Authors: Updated=$AUTHORS_UPDATED, Failed=$AUTHORS_FAILED"
echo "  Model Series: Updated=$SERIES_UPDATED, Failed=$SERIES_FAILED"
echo "  Models: Updated=$MODELS_UPDATED, Failed=$MODELS_FAILED"
if [ "$SKIP_ASSOCIATIONS" != true ]; then
    echo "  Associations: Updated=$ASSOCS_UPDATED, Failed=$ASSOCS_FAILED"
fi

# Report failed items
if [ ${#FAILED_ITEMS[@]} -gt 0 ]; then
    echo ""
    echo -e "${YELLOW}Failed Items:${NC}"
    for item in "${FAILED_ITEMS[@]}"; do
        echo "  - $item"
    done
fi

if [ "$DRY_RUN" != true ]; then
    echo ""
    echo "Next steps:"
    echo "  1. Verify imported models in the Admin UI"
    echo "  2. Test model routing with the imported associations"
    echo "  3. Check logs for any cache invalidation events"
fi

echo ""