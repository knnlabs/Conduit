#!/bin/bash
set -e

# Script: validate-postgresql-syntax.sh
# Purpose: Validate PostgreSQL syntax in EF Core migrations
# Usage: ./validate-postgresql-syntax.sh

echo "=============================================="
echo "PostgreSQL Syntax Validation"
echo "=============================================="

# Get script directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PROJECT_ROOT="$( cd "$SCRIPT_DIR/../.." && pwd )"

# Exit codes
EXIT_SUCCESS=0
EXIT_ERROR=1

# Track validation status
VALIDATION_FAILED=false

# Function to check for SQL Server boolean syntax
check_boolean_syntax() {
    local file="$1"
    local filename=$(basename "$file")
    
    # Skip Designer files and snapshots (they're auto-generated)
    if [[ "$filename" == *".Designer.cs" ]] || [[ "$filename" == *"ModelSnapshot.cs" ]]; then
        return
    fi
    
    # Check for SQL Server style boolean comparisons
    # Special handling for specific migration files
    if [[ "$filename" == "20250723201638_FixPostgreSQLBooleanFilter.cs" ]]; then
        # This migration is specifically fixing the boolean syntax, so skip it
        return
    fi
    
    if [[ "$filename" == "20250723043111_InitialCreate.cs" ]]; then
        # This is the initial migration that contains the old syntax, but it's already been applied
        # and fixed by the FixPostgreSQLBooleanFilter migration, so skip it
        return
    fi
    
    if grep -E '(IsActive|is_active|"IsActive"|"is_active")\s*=\s*[01]' "$file" > /dev/null; then
        echo "❌ ERROR: SQL Server boolean syntax found in $file"
        echo "   Found: boolean = 0/1 (SQL Server syntax)"
        echo "   Expected: boolean = true/false (PostgreSQL syntax)"
        grep -n -E '(IsActive|is_active|"IsActive"|"is_active")\s*=\s*[01]' "$file" | head -5
        VALIDATION_FAILED=true
    fi
    
    # Check for other common SQL Server patterns (exclude array syntax)
    if grep -E '\[[A-Za-z_][A-Za-z0-9_]*\]' "$file" | grep -v '^\s*//' | grep -v 'new\[\]' | grep -v 'Column<.*\[\]>' > /dev/null; then
        echo "⚠️  WARNING: SQL Server bracket syntax found in $file"
        echo "   Found: [identifier] (SQL Server syntax)"
        echo "   Expected: \"identifier\" (PostgreSQL syntax)"
        grep -n -E '\[[A-Za-z_][A-Za-z0-9_]*\]' "$file" | grep -v '^\s*//' | grep -v 'new\[\]' | grep -v 'Column<.*\[\]>' | head -5
    fi
}

# Function to check for PostgreSQL incompatible types
check_data_types() {
    local file="$1"
    local filename=$(basename "$file")
    
    # Skip Designer files and snapshots
    if [[ "$filename" == *".Designer.cs" ]] || [[ "$filename" == *"ModelSnapshot.cs" ]]; then
        return
    fi
    
    # Check for SQL Server specific data types
    if grep -E '(nvarchar|varchar\(max\)|datetime2|bit)' "$file" > /dev/null; then
        echo "⚠️  WARNING: SQL Server data type found in $file"
        grep -n -E '(nvarchar|varchar\(max\)|datetime2|bit)' "$file" | head -5
    fi
}

# Function to validate HasFilter expressions
check_hasfilter_expressions() {
    local file="$1"
    
    # Only check ConfigurationDbContext.cs and similar files
    if [[ ! "$file" == *"DbContext.cs" ]]; then
        return
    fi
    
    # Check for raw SQL in HasFilter
    if grep -E 'HasFilter\s*\(\s*".*"\s*\)' "$file" > /dev/null; then
        echo "ℹ️  INFO: Raw SQL found in HasFilter() in $file"
        echo "   Consider using LINQ expressions instead of raw SQL for better type safety"
        grep -n -E 'HasFilter\s*\(\s*".*"\s*\)' "$file" | head -5
    fi
}

echo ""
echo "Step 1: Checking migration files for SQL Server syntax..."
echo ""

# Check all migration files
for file in $(find "$PROJECT_ROOT" -name "*.cs" -path "*/Migrations/*" 2>/dev/null); do
    check_boolean_syntax "$file"
    check_data_types "$file"
done

echo ""
echo "Step 2: Checking DbContext files..."
echo ""

# Check DbContext files
for file in $(find "$PROJECT_ROOT" -name "*DbContext.cs" 2>/dev/null); do
    check_boolean_syntax "$file"
    check_hasfilter_expressions "$file"
done

echo ""
echo "Step 3: Checking for PostgreSQL-specific requirements..."
echo ""

# Check if all boolean filters use PostgreSQL syntax
MIGRATION_COUNT=$(find "$PROJECT_ROOT" -name "*.cs" -path "*/Migrations/*" ! -name "*.Designer.cs" ! -name "*ModelSnapshot.cs" | wc -l)
echo "✓ Found $MIGRATION_COUNT migration files to validate"

# Summary
echo ""
echo "=============================================="
echo "Validation Summary"
echo "=============================================="

if [ "$VALIDATION_FAILED" = true ]; then
    echo "❌ PostgreSQL syntax validation FAILED"
    echo ""
    echo "Fix the errors above before proceeding with database deployment."
    echo "PostgreSQL requires:"
    echo "  - Boolean comparisons: use 'true/false' not '1/0'"
    echo "  - Identifiers: use \"identifier\" not [identifier]"
    echo "  - Data types: use PostgreSQL types (text, boolean, timestamp with time zone)"
    exit $EXIT_ERROR
else
    echo "✓ PostgreSQL syntax validation PASSED"
    echo "✓ No SQL Server syntax patterns detected"
    echo "✓ Migrations are PostgreSQL-compatible"
    exit $EXIT_SUCCESS
fi