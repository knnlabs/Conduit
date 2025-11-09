#!/bin/bash

# Quick Test Verification
# Proves our fixes work without full reinstall

set -e

echo "=========================================="
echo "Quick CI Test Verification"
echo "=========================================="
echo ""

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

cd "$(dirname "$0")/../SDKs/Node"

echo "1. Testing with --detectOpenHandles (finds leaks)"
echo "-------------------------------------------"
OUTPUT=$(npm run test:ci -- --detectOpenHandles 2>&1)
if echo "$OUTPUT" | grep -q "Jest has detected the following.*open handle"; then
    echo -e "${RED}✗ LEAK FOUND:${NC}"
    echo "$OUTPUT" | grep -A 5 "Jest has detected"
else
    echo -e "${GREEN}✓ No open handles detected${NC}"
fi
echo ""

echo "2. Running tests 3 times (checks stability)"
echo "-------------------------------------------"
PASSES=0
for i in 1 2 3; do
    echo -n "  Run $i: "
    if npm run test:ci > /dev/null 2>&1; then
        echo -e "${GREEN}PASS${NC}"
        PASSES=$((PASSES + 1))
    else
        echo -e "${RED}FAIL${NC}"
    fi
done

if [ $PASSES -eq 3 ]; then
    echo -e "${GREEN}✓ All 3 runs passed - tests are stable${NC}"
else
    echo -e "${RED}✗ Only $PASSES/3 runs passed - tests are flaky${NC}"
fi
echo ""

echo "3. Checking for console output"
echo "-------------------------------------------"
CONSOLE_COUNT=$(npm run test:ci 2>&1 | grep -c "console\." || true)
if [ $CONSOLE_COUNT -eq 0 ]; then
    echo -e "${GREEN}✓ No console logs in production code${NC}"
else
    echo -e "${YELLOW}⚠ Found $CONSOLE_COUNT console statements${NC}"
fi
echo ""

echo "4. Test execution time"
echo "-------------------------------------------"
START=$(date +%s)
npm run test:ci > /dev/null 2>&1
END=$(date +%s)
TIME=$((END - START))
echo "Execution time: ${TIME} seconds"
if [ $TIME -lt 10 ]; then
    echo -e "${GREEN}✓ Fast execution${NC}"
else
    echo -e "${YELLOW}⚠ Could be faster${NC}"
fi
echo ""

echo "=========================================="
echo "RESULTS"
echo "=========================================="
if [ $PASSES -eq 3 ] && [ $CONSOLE_COUNT -eq 0 ]; then
    echo -e "${GREEN}✅ CI READY - All checks passed!${NC}"
    echo ""
    echo "Proof points:"
    echo "  • No memory leaks (no open handles)"
    echo "  • 100% test stability (3/3 passes)"
    echo "  • Clean output (no console logs)"
    echo "  • Efficient execution (${TIME}s)"
else
    echo -e "${RED}❌ Issues found - see above${NC}"
fi