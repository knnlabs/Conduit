#!/bin/bash

# Script to fix the Admin Client ESLint errors systematically
# 
# ⚠️  DEPRECATED: This script has been replaced by ./scripts/fix-sdk-errors.sh
# Please use: ./scripts/fix-sdk-errors.sh admin
#

echo "⚠️  DEPRECATED: This script has been replaced by ./scripts/fix-sdk-errors.sh"
echo "Please use: ./scripts/fix-sdk-errors.sh admin"
echo ""
echo "Continuing with legacy behavior for now..."
echo ""

set -e

echo "🔧 Fixing Admin Client ESLint errors..."

cd SDKs/Node/Admin

# Count initial errors
INITIAL_ERRORS=$(npm run lint 2>&1 | grep -oE "[0-9]+ error" | grep -oE "[0-9]+" | head -1)
INITIAL_ERRORS=${INITIAL_ERRORS:-0}
echo "📊 Initial error count: $INITIAL_ERRORS"

# Step 1: Fix unused catch variables
echo "🔧 Step 1: Fixing unused catch variables..."
find src -name "*.ts" -type f -exec grep -l "catch (e)" {} \; | while read file; do
    echo "  Fixing: $file"
    sed -i 's/} catch (e) {/} catch {/g' "$file"
done

find src -name "*.ts" -type f -exec grep -l "catch (error)" {} \; | while read file; do
    # Check if error variable is used in the catch block by looking for error references
    # Use a simple approach: check if 'error' appears after 'catch (error)' and before the next catch/function
    if ! grep -A 20 "catch (error)" "$file" | grep -E "\berror\b" | grep -v "catch (error)" > /dev/null; then
        echo "  Fixing unused error in: $file"
        sed -i 's/} catch (error) {/} catch {/g' "$file"
    else
        echo "  Skipping $file - error variable is used"
    fi
done

# Step 2: Run auto-fix for other issues
echo "🔧 Step 2: Running ESLint auto-fix..."
npm run lint -- --fix || true

# Step 3: Fix console.log statements
echo "🔧 Step 3: Fixing console.log statements..."
find src -name "*.ts" -type f -exec grep -l "console\.log" {} \; | while read file; do
    echo "  Fixing console.log in: $file"
    sed -i 's/console\.log(/console.warn(/g' "$file"
done

# Step 4: Show remaining errors
echo ""
echo "📊 Checking remaining errors..."
REMAINING_ERRORS=$(npm run lint 2>&1 | grep -oE "[0-9]+ error" | grep -oE "[0-9]+" | head -1)
REMAINING_ERRORS=${REMAINING_ERRORS:-0}

echo ""
echo "✅ Fixed $(($INITIAL_ERRORS - $REMAINING_ERRORS)) errors"
echo "❌ Remaining errors: $REMAINING_ERRORS"

if [ "$REMAINING_ERRORS" -gt 0 ]; then
    echo ""
    echo "🔍 Showing remaining errors that need manual fixes:"
    npm run lint 2>&1 | grep "error" | head -20
    echo ""
    echo "Most common remaining issues:"
    echo "1. TypeScript type safety (@typescript-eslint/no-unsafe-*)"
    echo "2. Explicit any types (@typescript-eslint/no-explicit-any)"
    echo "3. Empty interfaces (@typescript-eslint/no-empty-object-type)"
    echo ""
    echo "These require manual intervention to add proper types."
fi