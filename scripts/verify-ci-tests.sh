#!/bin/bash

# Verify CI Tests Script
# This script proves that our test fixes work reliably in CI environment

set -e

echo "================================================"
echo "CI Test Verification Script"
echo "================================================"
echo ""

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Track results
TOTAL_RUNS=0
SUCCESSFUL_RUNS=0
FAILED_RUNS=0
LEAK_DETECTED=0

# Change to SDK directory
cd "$(dirname "$0")/../SDKs/Node"

echo "Step 1: Clean install dependencies"
echo "--------------------------------"
# Don't delete package-lock files, just node_modules
rm -rf node_modules
rm -rf Admin/node_modules
rm -rf Core/node_modules
rm -rf Common/node_modules
npm ci
echo -e "${GREEN}✓ Dependencies installed${NC}\n"

echo "Step 2: Build all packages"
echo "--------------------------------"
npm run build
echo -e "${GREEN}✓ Build successful${NC}\n"

echo "Step 3: Run tests with leak detection"
echo "--------------------------------"
echo "Running with --detectOpenHandles to check for leaks..."
if npm run test:ci -- --detectOpenHandles 2>&1 | tee test-output.log | grep -q "Jest has detected the following.*open handle"; then
    echo -e "${RED}✗ Memory leaks detected!${NC}"
    LEAK_DETECTED=1
    cat test-output.log | grep -A 10 "Jest has detected"
else
    echo -e "${GREEN}✓ No open handles detected${NC}"
fi
echo ""

echo "Step 4: Run tests multiple times to check stability"
echo "--------------------------------"
for i in {1..5}; do
    echo -n "Run $i/5: "
    TOTAL_RUNS=$((TOTAL_RUNS + 1))
    
    if npm run test:ci > /dev/null 2>&1; then
        echo -e "${GREEN}✓ PASS${NC}"
        SUCCESSFUL_RUNS=$((SUCCESSFUL_RUNS + 1))
    else
        echo -e "${RED}✗ FAIL${NC}"
        FAILED_RUNS=$((FAILED_RUNS + 1))
    fi
done
echo ""

echo "Step 5: Check test execution time"
echo "--------------------------------"
START_TIME=$(date +%s)
npm run test:ci > /dev/null 2>&1
END_TIME=$(date +%s)
EXECUTION_TIME=$((END_TIME - START_TIME))
echo "Test execution time: ${EXECUTION_TIME} seconds"

if [ $EXECUTION_TIME -lt 10 ]; then
    echo -e "${GREEN}✓ Tests run efficiently (under 10 seconds)${NC}"
elif [ $EXECUTION_TIME -lt 20 ]; then
    echo -e "${YELLOW}⚠ Tests take moderate time (10-20 seconds)${NC}"
else
    echo -e "${RED}✗ Tests are slow (over 20 seconds)${NC}"
fi
echo ""

echo "Step 6: Verify no console output in silent mode"
echo "--------------------------------"
OUTPUT=$(npm run test:ci 2>&1 | grep -E "console\.(log|error|warn)" | wc -l)
if [ $OUTPUT -eq 0 ]; then
    echo -e "${GREEN}✓ No console output in tests${NC}"
else
    echo -e "${RED}✗ Found $OUTPUT console outputs${NC}"
fi
echo ""

echo "Step 7: Check process cleanup"
echo "--------------------------------"
# Get process count before tests
BEFORE_PROCS=$(ps aux | grep -c "node\|jest" || true)

# Run tests
npm run test:ci > /dev/null 2>&1

# Wait a moment for processes to cleanup
sleep 2

# Get process count after tests
AFTER_PROCS=$(ps aux | grep -c "node\|jest" || true)

if [ "$AFTER_PROCS" -le "$BEFORE_PROCS" ]; then
    echo -e "${GREEN}✓ All processes cleaned up properly${NC}"
else
    echo -e "${RED}✗ Orphan processes detected${NC}"
fi
echo ""

echo "================================================"
echo "VERIFICATION REPORT"
echo "================================================"
echo ""
echo "Test Stability:"
echo "  Total runs: $TOTAL_RUNS"
echo "  Successful: $SUCCESSFUL_RUNS"
echo "  Failed: $FAILED_RUNS"
if [ $FAILED_RUNS -eq 0 ]; then
    echo -e "  Result: ${GREEN}✓ 100% SUCCESS RATE${NC}"
else
    echo -e "  Result: ${RED}✗ $(echo "scale=2; $FAILED_RUNS * 100 / $TOTAL_RUNS" | bc)% FAILURE RATE${NC}"
fi
echo ""

echo "Memory & Handles:"
if [ $LEAK_DETECTED -eq 0 ]; then
    echo -e "  ${GREEN}✓ No memory leaks detected${NC}"
else
    echo -e "  ${RED}✗ Memory leaks found${NC}"
fi
echo ""

echo "Performance:"
echo "  Execution time: ${EXECUTION_TIME}s"
if [ $EXECUTION_TIME -lt 10 ]; then
    echo -e "  ${GREEN}✓ Good performance${NC}"
else
    echo -e "  ${YELLOW}⚠ Could be optimized${NC}"
fi
echo ""

# Overall result
echo "Overall Status:"
if [ $FAILED_RUNS -eq 0 ] && [ $LEAK_DETECTED -eq 0 ] && [ $OUTPUT -eq 0 ]; then
    echo -e "${GREEN}✅ ALL CHECKS PASSED - CI READY!${NC}"
    exit 0
else
    echo -e "${RED}❌ SOME CHECKS FAILED - NEEDS ATTENTION${NC}"
    exit 1
fi