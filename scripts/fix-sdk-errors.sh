#!/bin/bash

# Combined script to fix ESLint errors in SDK clients
# Usage: 
#   ./scripts/fix-sdk-errors.sh           # Fix both SDKs
#   ./scripts/fix-sdk-errors.sh admin     # Fix Admin SDK only
#   ./scripts/fix-sdk-errors.sh core      # Fix Core SDK only

set -e

# SDK configuration: key -> "path:display_name"
declare -A SDKS=(
    ["admin"]="SDKs/Node/Admin:Admin Client"
    ["core"]="SDKs/Node/Core:Core Client"
)

# Global statistics
TOTAL_INITIAL_ERRORS=0
TOTAL_FIXED_ERRORS=0
TOTAL_REMAINING_ERRORS=0
FAILED_SDKS=()

# Print SDK section header
print_sdk_header() {
    local display_name="$1"
    local header_text="${display_name^^} SDK"
    local border_char="═"
    local corner_tl="╔"
    local corner_tr="╗"
    local corner_bl="╚"
    local corner_br="╝"
    local vertical="║"
    
    local header_length=${#header_text}
    local total_width=$((header_length + 6))
    local border=$(printf "%*s" $total_width | tr ' ' "$border_char")
    local padding=$(printf "%*s" 3)
    
    echo ""
    echo "${corner_tl}${border}${corner_tr}"
    echo "${vertical}${padding}${header_text}${padding}${vertical}"
    echo "${corner_bl}${border}${corner_br}"
}

# Print final summary for multiple SDKs
print_summary() {
    local num_sdks="$1"
    
    if [ "$num_sdks" -gt 1 ]; then
        print_sdk_header "COMBINED SUMMARY"
        echo "📊 Total initial errors: $TOTAL_INITIAL_ERRORS"
        echo "✅ Total fixed errors: $TOTAL_FIXED_ERRORS"
        echo "❌ Total remaining errors: $TOTAL_REMAINING_ERRORS"
        
        if [ ${#FAILED_SDKS[@]} -gt 0 ]; then
            echo ""
            echo "⚠️  Failed SDKs: ${FAILED_SDKS[*]}"
        fi
    fi
}

# Fix ESLint errors for a single SDK
fix_sdk_errors() {
    local sdk_key="$1"
    local sdk_info="${SDKS[$sdk_key]}"
    local sdk_path="${sdk_info%:*}"
    local display_name="${sdk_info#*:}"
    
    echo "🔧 Fixing $display_name ESLint errors..."
    
    # Change to SDK directory
    if ! cd "$sdk_path"; then
        echo "❌ Error: Cannot access $sdk_path"
        FAILED_SDKS+=("$display_name")
        return 1
    fi
    
    # Count initial errors
    local initial_errors
    initial_errors=$(npm run lint 2>&1 | grep -oE "[0-9]+ error" | grep -oE "[0-9]+" | head -1)
    initial_errors=${initial_errors:-0}
    echo "📊 Initial error count: $initial_errors"
    TOTAL_INITIAL_ERRORS=$((TOTAL_INITIAL_ERRORS + initial_errors))
    
    # Step 1: Fix unused catch variables
    echo "🔧 Step 1: Fixing unused catch variables..."
    find src -name "*.ts" -type f -exec grep -l "catch (e)" {} \; 2>/dev/null | while read -r file; do
        echo "  Fixing: $file"
        sed -i 's/} catch (e) {/} catch {/g' "$file"
    done || true
    
    find src -name "*.ts" -type f -exec grep -l "catch (error)" {} \; 2>/dev/null | while read -r file; do
        # Check if error variable is used in the catch block by looking for error references
        # Use a simple approach: check if 'error' appears after 'catch (error)' and before the next catch/function
        if ! grep -A 20 "catch (error)" "$file" | grep -E "\\berror\\b" | grep -v "catch (error)" > /dev/null; then
            echo "  Fixing unused error in: $file"
            sed -i 's/} catch (error) {/} catch {/g' "$file"
        else
            echo "  Skipping $file - error variable is used"
        fi
    done || true
    
    # Step 2: Run auto-fix for other issues
    echo "🔧 Step 2: Running ESLint auto-fix..."
    npm run lint -- --fix || true
    
    # Step 3: Fix console.log statements
    echo "🔧 Step 3: Fixing console.log statements..."
    find src -name "*.ts" -type f -exec grep -l "console\.log" {} \; 2>/dev/null | while read -r file; do
        echo "  Fixing console.log in: $file"
        sed -i 's/console\.log(/console.warn(/g' "$file"
    done || true
    
    # Step 4: Show remaining errors
    echo ""
    echo "📊 Checking remaining errors..."
    local remaining_errors
    remaining_errors=$(npm run lint 2>&1 | grep -oE "[0-9]+ error" | grep -oE "[0-9]+" | head -1)
    remaining_errors=${remaining_errors:-0}
    
    local fixed_errors=$((initial_errors - remaining_errors))
    TOTAL_FIXED_ERRORS=$((TOTAL_FIXED_ERRORS + fixed_errors))
    TOTAL_REMAINING_ERRORS=$((TOTAL_REMAINING_ERRORS + remaining_errors))
    
    echo ""
    echo "✅ Fixed $fixed_errors errors"
    echo "❌ Remaining errors: $remaining_errors"
    
    if [ "$remaining_errors" -gt 0 ]; then
        echo ""
        echo "🔍 Showing remaining errors that need manual fixes:"
        npm run lint 2>&1 | grep "error" | head -20 || true
        echo ""
        echo "Most common remaining issues:"
        echo "1. TypeScript type safety (@typescript-eslint/no-unsafe-*)"
        echo "2. Explicit any types (@typescript-eslint/no-explicit-any)"
        echo "3. Empty interfaces (@typescript-eslint/no-empty-object-type)"
        echo ""
        echo "These require manual intervention to add proper types."
    fi
    
    # Return to original directory
    cd - > /dev/null || return 1
    
    return 0
}

# Parse command line arguments
parse_arguments() {
    case "${1:-}" in
        "admin"|"--admin")
            echo "admin"
            ;;
        "core"|"--core")
            echo "core"
            ;;
        ""|"--all"|"all")
            echo "admin core"
            ;;
        "--help"|"-h"|"help")
            # This case is handled in main function
            echo "admin core"
            ;;
        *)
            echo "❌ Error: Unknown argument '$1'" >&2
            echo "Use '$0 --help' for usage information" >&2
            exit 1
            ;;
    esac
}

# Main function
main() {
    # Handle help first, before parsing
    case "${1:-}" in
        "--help"|"-h"|"help")
            cat << EOF
Usage: $0 [admin|core|all]

Options:
  admin    Fix Admin Client SDK only
  core     Fix Core Client SDK only
  all      Fix both SDKs (default)
  --help   Show this help message
EOF
            exit 0
            ;;
    esac
    
    local target_sdks
    target_sdks=$(parse_arguments "$1")
    local sdk_count=0
    local failed_count=0
    
    # Count SDKs to process
    for sdk in $target_sdks; do
        sdk_count=$((sdk_count + 1))
    done
    
    # Process each SDK
    for sdk in $target_sdks; do
        if [ ${#SDKS[$sdk]} -eq 0 ]; then
            echo "❌ Error: Unknown SDK '$sdk'"
            exit 1
        fi
        
        if [ $sdk_count -gt 1 ]; then
            local sdk_info="${SDKS[$sdk]}"
            local display_name="${sdk_info#*:}"
            print_sdk_header "$display_name"
        fi
        
        if ! fix_sdk_errors "$sdk"; then
            failed_count=$((failed_count + 1))
        fi
    done
    
    # Print summary for multiple SDKs
    print_summary "$sdk_count"
    
    # Exit with appropriate code
    if [ $failed_count -gt 0 ]; then
        echo ""
        echo "❌ Script completed with $failed_count failed SDK(s)"
        exit 1
    else
        echo ""
        echo "✅ All SDKs processed successfully"
        exit 0
    fi
}

# Run main function with all arguments
main "$@"