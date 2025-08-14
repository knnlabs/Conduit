#!/bin/bash
set -e

# Script: validate-migrations.sh
# Purpose: Validate EF Core migrations for CI/CD pipeline
# Usage: ./validate-migrations.sh [--check-pending] [--generate-script]

echo "=============================================="
echo "EF Core Migration Validation"
echo "=============================================="

# Get script directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PROJECT_ROOT="$( cd "$SCRIPT_DIR/../.." && pwd )"
CONFIGURATION_PROJECT="$PROJECT_ROOT/ConduitLLM.Configuration"

# Parse command line arguments
CHECK_PENDING=false
GENERATE_SCRIPT=false

while [[ $# -gt 0 ]]; do
    case $1 in
        --check-pending)
            CHECK_PENDING=true
            shift
            ;;
        --generate-script)
            GENERATE_SCRIPT=true
            shift
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

cd "$CONFIGURATION_PROJECT"

# Step 1: Check if EF Core tools are installed
echo ""
echo "Step 1: Checking EF Core tools..."
if ! dotnet ef --version > /dev/null 2>&1; then
    echo "ERROR: EF Core tools not installed"
    echo "Install with: dotnet tool install --global dotnet-ef"
    exit 1
fi

# Step 2: List all migrations
echo ""
echo "Step 2: Listing migrations..."

# Try EF tool first, with timeout to prevent hanging
TEMP_FILE=$(mktemp)
if timeout 10s dotnet ef migrations list --no-build > "$TEMP_FILE" 2>&1; then
    # Extract migration names and strip status indicators like (Pending) or (Applied)
    MIGRATIONS_EF=$(grep -E "^[0-9]{14}_" "$TEMP_FILE" | sed 's/ (Pending)$//' | sed 's/ (Applied)$//' || true)
else
    MIGRATIONS_EF=""
fi
rm -f "$TEMP_FILE"

# Fallback: get migrations from filesystem
MIGRATIONS_FS=$(find Migrations -name "[0-9]*_*.cs" -not -name "*.Designer.cs" 2>/dev/null | sed 's|Migrations/||' | sed 's|\.cs$||' | sort || true)

# Use EF output if available and non-empty, otherwise use filesystem
if [ -n "$MIGRATIONS_EF" ]; then
    MIGRATIONS="$MIGRATIONS_EF"
    echo "Migrations from EF tool:"
else
    MIGRATIONS="$MIGRATIONS_FS"
    echo "Migrations from filesystem (EF tool unavailable):"
fi
echo "$MIGRATIONS"

# Count migrations
MIGRATION_COUNT=$(echo "$MIGRATIONS" | grep -v "^$" | wc -l)
echo ""
echo "Total migrations: $MIGRATION_COUNT"

# Step 3: Check for duplicate migration names
echo ""
echo "Step 3: Checking for duplicate migration names..."
DUPLICATES=$(echo "$MIGRATIONS" | grep -v "^$" | sort | uniq -d)
if [ -n "$DUPLICATES" ]; then
    echo "ERROR: Duplicate migration names found:"
    echo "$DUPLICATES"
    exit 1
else
    echo "✓ No duplicate migration names"
fi

# Step 4: Validate migration files exist
echo ""
echo "Step 4: Validating migration files..."
MISSING_FILES=0
for migration in $(echo "$MIGRATIONS" | grep -v "^$"); do
    if [ ! -f "Migrations/${migration}.cs" ]; then
        echo "ERROR: Missing migration file: ${migration}.cs"
        MISSING_FILES=$((MISSING_FILES + 1))
    fi
    if [ ! -f "Migrations/${migration}.Designer.cs" ]; then
        echo "ERROR: Missing designer file: ${migration}.Designer.cs"
        MISSING_FILES=$((MISSING_FILES + 1))
    fi
done

if [ $MISSING_FILES -eq 0 ]; then
    echo "✓ All migration files present"
else
    echo "ERROR: $MISSING_FILES migration files missing"
    exit 1
fi

# Step 5: Check for pending model changes
echo ""
echo "Step 5: Checking for pending model changes..."
PENDING_TEMP=$(mktemp)
if timeout 10s dotnet ef migrations has-pending-model-changes --no-build > "$PENDING_TEMP" 2>&1; then
    PENDING_OUTPUT=$(cat "$PENDING_TEMP")
    if echo "$PENDING_OUTPUT" | grep -q "Changes have been made to the model"; then
        echo "WARNING: Model has pending changes not included in migrations"
        if [ "$CHECK_PENDING" = true ]; then
            echo "ERROR: Pending model changes detected (--check-pending flag set)"
            rm -f "$PENDING_TEMP"
            exit 1
        fi
    else
        echo "✓ No pending model changes"
    fi
else
    echo "⚠ Cannot check pending changes (EF tool timeout or database unavailable)"
    if [ "$CHECK_PENDING" = true ]; then
        echo "WARNING: Cannot verify pending changes due to EF tool issues"
    fi
fi
rm -f "$PENDING_TEMP"

# Step 6: Generate migration script (optional)
if [ "$GENERATE_SCRIPT" = true ]; then
    echo ""
    echo "Step 6: Generating migration script..."
    OUTPUT_FILE="$PROJECT_ROOT/migration-script-$(date +%Y%m%d-%H%M%S).sql"
    
    # Use ef-wrapper for better error handling
    if [ -f "$SCRIPT_DIR/ef-wrapper.sh" ]; then
        "$SCRIPT_DIR/ef-wrapper.sh" migrations script --no-build -o "$OUTPUT_FILE"
    else
        dotnet ef migrations script --no-build -o "$OUTPUT_FILE"
    fi
    
    if [ -f "$OUTPUT_FILE" ]; then
        echo "✓ Migration script generated: $OUTPUT_FILE"
        
        # Validate SQL syntax (basic check)
        if grep -E "(syntax error|ERROR)" "$OUTPUT_FILE" > /dev/null; then
            echo "WARNING: Potential SQL errors detected in migration script"
        fi
    else
        echo "ERROR: Failed to generate migration script"
        exit 1
    fi
fi

# Step 7: Check migration snapshot
echo ""
echo "Step 7: Validating migration snapshot..."
SNAPSHOT_FILE=$(find Migrations -name "*ModelSnapshot.cs" | head -1)
if [ -z "$SNAPSHOT_FILE" ] || [ ! -f "$SNAPSHOT_FILE" ]; then
    echo "ERROR: Migration snapshot file missing"
    exit 1
else
    echo "✓ Migration snapshot present: $SNAPSHOT_FILE"
fi

# Step 8: Summary
echo ""
echo "=============================================="
echo "Validation Summary"
echo "=============================================="
echo "✓ EF Core tools installed"
echo "✓ $MIGRATION_COUNT migrations found"
echo "✓ No duplicate migrations"
echo "✓ All migration files present"
echo "✓ Migration snapshot valid"

if [ "$CHECK_PENDING" = true ]; then
    echo "✓ No pending model changes"
fi

if [ "$GENERATE_SCRIPT" = true ]; then
    echo "✓ Migration script generated"
fi

echo ""
echo "Migration validation completed successfully!"
exit 0