#!/bin/bash

# Script to find and fix common test anti-patterns in the Conduit test suite
# Usage: ./fix-test-antipatterns.sh [--fix]

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Check if we should apply fixes
APPLY_FIXES=false
if [[ "$1" == "--fix" ]]; then
    APPLY_FIXES=true
    echo -e "${GREEN}Running in FIX mode - will apply changes${NC}"
else
    echo -e "${YELLOW}Running in CHECK mode - use --fix to apply changes${NC}"
fi

# Pattern 1: Find Assert.True with equality comparisons
echo -e "\n${YELLOW}=== Searching for Assert.True with equality comparisons ===${NC}"

# Find files with Assert.True(x == y) or Assert.True(x != y)
ASSERT_TRUE_FILES=$(grep -r "Assert\.True.*[!=]=" . --include="*.cs" -l 2>/dev/null || true)

if [[ -n "$ASSERT_TRUE_FILES" ]]; then
    echo -e "${RED}Found Assert.True anti-patterns in:${NC}"
    for file in $ASSERT_TRUE_FILES; do
        echo "  - $file"
        
        if [[ "$APPLY_FIXES" == true ]]; then
            # Replace Assert.True(x == y) with Assert.Equal(y, x)
            sed -i.bak -E 's/Assert\.True\(([^=]+) == ([^)]+)\)/Assert.Equal(\2, \1)/g' "$file"
            
            # Replace Assert.True(x != y) with Assert.NotEqual(y, x)
            sed -i.bak -E 's/Assert\.True\(([^!]+) != ([^)]+)\)/Assert.NotEqual(\2, \1)/g' "$file"
            
            echo -e "    ${GREEN}✓ Fixed${NC}"
        fi
    done
else
    echo -e "${GREEN}✓ No Assert.True anti-patterns found${NC}"
fi

# Pattern 2: Find tests without assertions
echo -e "\n${YELLOW}=== Searching for tests without assertions ===${NC}"

# Find test methods that don't contain any Assert statements
NO_ASSERT_FILES=""
for file in $(find . -name "*Tests.cs" -type f); do
    # Extract test methods and check for assertions
    awk '
    /\[Fact\]||\[Theory\]/ { 
        in_test = 1; 
        test_name = "";
        has_assert = 0;
        brace_count = 0;
    }
    in_test && /public.*Task|public.*void/ {
        match($0, /public.*?(Task|void)\s+(\w+)/, arr);
        test_name = arr[2];
    }
    in_test && /{/ { brace_count++ }
    in_test && /}/ { 
        brace_count--;
        if (brace_count == 0) {
            if (!has_assert && test_name != "") {
                print FILENAME ":" test_name;
            }
            in_test = 0;
        }
    }
    in_test && /Assert\.|Should|Verify|Throws/ { has_assert = 1 }
    ' "$file" >> /tmp/no_assert_tests.txt
done

if [[ -s /tmp/no_assert_tests.txt ]]; then
    echo -e "${RED}Found tests without assertions:${NC}"
    sort -u /tmp/no_assert_tests.txt | head -20
    TOTAL=$(wc -l < /tmp/no_assert_tests.txt)
    if [[ $TOTAL -gt 20 ]]; then
        echo "  ... and $((TOTAL - 20)) more"
    fi
    
    if [[ "$APPLY_FIXES" == true ]]; then
        echo -e "${YELLOW}Cannot auto-fix missing assertions - manual review required${NC}"
    fi
else
    echo -e "${GREEN}✓ All tests have assertions${NC}"
fi
rm -f /tmp/no_assert_tests.txt

# Pattern 3: Find exception swallowing
echo -e "\n${YELLOW}=== Searching for exception swallowing ===${NC}"

# Find catch blocks that don't rethrow or assert
SWALLOW_FILES=$(grep -r "catch.*{" . --include="*.cs" -A 5 | \
    grep -B 5 "^[[:space:]]*}" | \
    grep -l "catch" 2>/dev/null || true)

if [[ -n "$SWALLOW_FILES" ]]; then
    echo -e "${YELLOW}Found potential exception swallowing in:${NC}"
    for file in $SWALLOW_FILES; do
        # Check if the catch block actually swallows (no throw, assert, or log)
        if grep -A 10 "catch.*{" "$file" | grep -q "^[[:space:]]*}"; then
            echo "  - $file"
        fi
    done
else
    echo -e "${GREEN}✓ No exception swallowing found${NC}"
fi

# Pattern 4: Find tests with generic exception catching
echo -e "\n${YELLOW}=== Searching for generic exception catching ===${NC}"

GENERIC_CATCH=$(grep -r "catch\s*(\s*Exception\s" . --include="*Tests.cs" -l 2>/dev/null || true)

if [[ -n "$GENERIC_CATCH" ]]; then
    echo -e "${YELLOW}Found generic exception catching in tests:${NC}"
    for file in $GENERIC_CATCH; do
        echo "  - $file"
    done
    echo -e "${YELLOW}Consider using more specific exception types${NC}"
fi

# Pattern 5: Find Assert.True without messages
echo -e "\n${YELLOW}=== Searching for Assert.True without descriptive messages ===${NC}"

NO_MESSAGE_FILES=$(grep -r "Assert\.True([^,)]*)" . --include="*.cs" -l 2>/dev/null || true)

if [[ -n "$NO_MESSAGE_FILES" ]]; then
    echo -e "${YELLOW}Found Assert.True without messages in:${NC}"
    COUNT=0
    for file in $NO_MESSAGE_FILES; do
        if [[ $COUNT -lt 10 ]]; then
            echo "  - $file"
            ((COUNT++))
        fi
    done
    if [[ $(echo "$NO_MESSAGE_FILES" | wc -l) -gt 10 ]]; then
        echo "  ... and $(($(echo "$NO_MESSAGE_FILES" | wc -l) - 10)) more files"
    fi
fi

# Summary
echo -e "\n${GREEN}=== Summary ===${NC}"
echo "Run with --fix to automatically fix Assert.True patterns"
echo "Other issues require manual review and fixing"

# Cleanup backup files if fixes were applied
if [[ "$APPLY_FIXES" == true ]]; then
    find . -name "*.bak" -type f -delete
    echo -e "${GREEN}Fixes applied and backup files cleaned up${NC}"
fi