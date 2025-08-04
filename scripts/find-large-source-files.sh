#!/bin/bash

# find-large-source-files.sh
# Finds source code files with more than 500 lines
# Usage: ./find-large-source-files.sh [filter]
# Filter options: dotnet, typescript (optional)

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Default line threshold
LINE_THRESHOLD=500

# Parse command line arguments
FILTER=""
if [ $# -eq 1 ]; then
    FILTER=$(echo "$1" | tr '[:upper:]' '[:lower:]')
fi

# Common directories to exclude
EXCLUDE_DIRS=(
    "*/node_modules/*"
    "*/.git/*"
    "*/bin/*"
    "*/obj/*"
    "*/dist/*"
    "*/build/*"
    "*/target/*"
    "*/.next/*"
    "*/coverage/*"
    "*/.nuget/*"
    "*/packages/*"
    "*/.vs/*"
    "*/.vscode/*"
    "*/.idea/*"
    "*/out/*"
    "*/publish/*"
    "*/Migrations/*"
    "*/generated/*"
    "*/*.Designer.cs"
    "*/*ModelSnapshot.cs"
    "*/package-lock.json"
    "*/TestResults/*"
    "*/openapi-*.json"
)

# Build exclude pattern for find command
EXCLUDE_PATTERN=""
for dir in "${EXCLUDE_DIRS[@]}"; do
    EXCLUDE_PATTERN="$EXCLUDE_PATTERN -not -path \"$dir\""
done

# Define file extensions based on filter
case "$FILTER" in
    "dotnet")
        echo -e "${BLUE}Searching for .NET source files with more than $LINE_THRESHOLD lines...${NC}"
        FILE_PATTERN="-name \"*.cs\" -o -name \"*.csproj\" -o -name \"*.sln\" -o -name \"*.cshtml\" -o -name \"*.razor\""
        ;;
    "typescript")
        echo -e "${BLUE}Searching for TypeScript/JavaScript files with more than $LINE_THRESHOLD lines...${NC}"
        FILE_PATTERN="-name \"*.ts\" -o -name \"*.tsx\" -o -name \"*.js\" -o -name \"*.jsx\" -o -name \"*.mjs\" -o -name \"*.cjs\""
        ;;
    "")
        echo -e "${BLUE}Searching for all source code files with more than $LINE_THRESHOLD lines...${NC}"
        FILE_PATTERN="-name \"*.cs\" -o -name \"*.ts\" -o -name \"*.tsx\" -o -name \"*.js\" -o -name \"*.jsx\" -o -name \"*.py\" -o -name \"*.java\" -o -name \"*.cpp\" -o -name \"*.c\" -o -name \"*.h\" -o -name \"*.hpp\" -o -name \"*.go\" -o -name \"*.rs\" -o -name \"*.vue\" -o -name \"*.svelte\" -o -name \"*.rb\" -o -name \"*.php\" -o -name \"*.swift\" -o -name \"*.kt\" -o -name \"*.scala\" -o -name \"*.r\" -o -name \"*.m\" -o -name \"*.mm\" -o -name \"*.sh\" -o -name \"*.bash\" -o -name \"*.zsh\" -o -name \"*.sql\" -o -name \"*.yaml\" -o -name \"*.yml\" -o -name \"*.json\" -o -name \"*.xml\" -o -name \"*.md\" -o -name \"*.mjs\" -o -name \"*.cjs\""
        ;;
    *)
        echo -e "${RED}Invalid filter: $FILTER${NC}"
        echo "Valid filters: dotnet, typescript"
        echo "Usage: $0 [filter]"
        exit 1
        ;;
esac

# Find files and count lines
echo -e "${YELLOW}Analyzing files...${NC}"
echo ""

# Create temporary file for results
TEMP_FILE=$(mktemp)

# Find files, count lines, and sort by line count
eval "find . -type f \( $FILE_PATTERN \) $EXCLUDE_PATTERN -exec wc -l {} + | awk '\$1 > $LINE_THRESHOLD {print \$1, \$2}' | sort -nr" > "$TEMP_FILE"

# Count total files found
TOTAL_FILES=$(wc -l < "$TEMP_FILE")

if [ "$TOTAL_FILES" -eq 0 ]; then
    echo -e "${YELLOW}No files found with more than $LINE_THRESHOLD lines.${NC}"
    rm "$TEMP_FILE"
    exit 0
fi

# Display results with formatting
echo -e "${GREEN}Found $TOTAL_FILES files with more than $LINE_THRESHOLD lines:${NC}"
echo ""
echo -e "${YELLOW}Lines   File${NC}"
echo "------  ----"

# Display files with color coding based on size
while IFS=' ' read -r lines file; do
    # Remove leading ./ from file paths
    file=${file#./}
    
    # Color code based on line count
    if [ "$lines" -gt 2000 ]; then
        echo -e "${RED}$(printf "%6d" "$lines")  $file${NC}"
    elif [ "$lines" -gt 1000 ]; then
        echo -e "${YELLOW}$(printf "%6d" "$lines")  $file${NC}"
    else
        echo -e "$(printf "%6d" "$lines")  $file"
    fi
done < "$TEMP_FILE"

# Calculate and display summary statistics
echo ""
echo -e "${BLUE}Summary:${NC}"
echo -e "Total files: ${GREEN}$TOTAL_FILES${NC}"

# Calculate total lines
TOTAL_LINES=$(awk '{sum += $1} END {print sum}' "$TEMP_FILE")
echo -e "Total lines: ${GREEN}$(printf "%'d" "$TOTAL_LINES")${NC}"

# Find largest file
LARGEST=$(head -n 1 "$TEMP_FILE")
if [ -n "$LARGEST" ]; then
    LARGEST_LINES=$(echo "$LARGEST" | awk '{print $1}')
    LARGEST_FILE=$(echo "$LARGEST" | awk '{print $2}')
    LARGEST_FILE=${LARGEST_FILE#./}
    echo -e "Largest file: ${YELLOW}$LARGEST_FILE${NC} ($(printf "%'d" "$LARGEST_LINES") lines)"
fi

# Average lines per file
if [ "$TOTAL_FILES" -gt 0 ]; then
    AVG_LINES=$((TOTAL_LINES / TOTAL_FILES))
    echo -e "Average lines per file: ${GREEN}$(printf "%'d" "$AVG_LINES")${NC}"
fi

# Show filter info
if [ -n "$FILTER" ]; then
    echo ""
    echo -e "${BLUE}Filter applied: ${YELLOW}$FILTER${NC}"
fi

# Clean up
rm "$TEMP_FILE"

# Legend
echo ""
echo -e "${BLUE}Color Legend:${NC}"
echo -e "  ${RED}■${NC} More than 2000 lines"
echo -e "  ${YELLOW}■${NC} 1001-2000 lines"
echo -e "  ■ 501-1000 lines"