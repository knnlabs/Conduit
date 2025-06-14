#!/bin/bash

# Script to fix all log injection vulnerabilities using inline sanitization
# This uses the S() function pattern that CodeQL should recognize

set -e

echo "=== Fixing All Log Injection Vulnerabilities ==="
echo

# Counter for fixed files
fixed_count=0

# Function to fix a single file
fix_file() {
    local file=$1
    local file_type=$2
    
    # Skip test files
    if [[ $file == *Test* ]] || [[ $file == *test* ]]; then
        return
    fi
    
    # Check if file has logging statements
    if ! grep -q "_logger\." "$file" 2>/dev/null; then
        return
    fi
    
    echo "Processing: $file"
    
    # Check if file already has the using static directive
    has_using_static=$(grep -c "using static ConduitLLM.Core.Extensions.LoggingSanitizer;" "$file" || echo 0)
    
    # Create temporary file
    temp_file="${file}.tmp"
    
    # Add using statements if needed
    if [ "$has_using_static" -eq 0 ]; then
        # Find the last using statement and add our imports after it
        awk '
        /^using / { 
            print
            if (!printed) {
                print "using ConduitLLM.Core.Extensions;"
                print ""
                print "using static ConduitLLM.Core.Extensions.LoggingSanitizer;"
                printed = 1
            }
            next
        }
        /^namespace/ && !printed {
            print "using ConduitLLM.Core.Extensions;"
            print ""
            print "using static ConduitLLM.Core.Extensions.LoggingSanitizer;"
            print ""
            printed = 1
        }
        { print }
        ' "$file" > "$temp_file"
        
        mv "$temp_file" "$file"
    fi
    
    # Now fix the logging statements
    # This is complex because we need to handle multi-line statements and various patterns
    
    python3 << 'PYTHON_SCRIPT' "$file"
import sys
import re

def sanitize_log_params(match):
    """Process a log statement and wrap parameters with S()"""
    full_match = match.group(0)
    method = match.group(1)
    params_part = match.group(2)
    
    # Split by comma but respect nested parentheses
    params = []
    current_param = ""
    paren_depth = 0
    in_string = False
    escape_next = False
    
    for char in params_part:
        if escape_next:
            current_param += char
            escape_next = False
            continue
            
        if char == '\\':
            escape_next = True
            current_param += char
            continue
            
        if char == '"' and paren_depth == 0:
            in_string = not in_string
            current_param += char
            continue
            
        if not in_string:
            if char == '(':
                paren_depth += 1
            elif char == ')':
                paren_depth -= 1
            elif char == ',' and paren_depth == 0:
                params.append(current_param.strip())
                current_param = ""
                continue
        
        current_param += char
    
    if current_param.strip():
        params.append(current_param.strip())
    
    # First parameter is usually the exception or the message
    if len(params) < 2:
        return full_match
    
    # Process parameters (skip the message template)
    processed_params = [params[0]]  # Keep first param (exception or message)
    
    for i, param in enumerate(params[1:], 1):
        param = param.strip()
        
        # Skip if already wrapped with S()
        if param.startswith('S(') and param.endswith(')'):
            processed_params.append(param)
            continue
        
        # Skip if it's a literal string
        if param.startswith('"') and param.endswith('"'):
            processed_params.append(param)
            continue
        
        # Skip if it's a number literal
        if param.isdigit() or re.match(r'^-?\d+\.?\d*$', param):
            processed_params.append(param)
            continue
        
        # Skip certain safe patterns
        if param in ['null', 'true', 'false', 'this', 'nameof', 'typeof']:
            processed_params.append(param)
            continue
        
        # Skip if it starts with nameof or typeof
        if param.startswith('nameof(') or param.startswith('typeof('):
            processed_params.append(param)
            continue
        
        # Wrap with S()
        processed_params.append(f'S({param})')
    
    return f'_logger.{method}({", ".join(processed_params)})'

def process_file(filepath):
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # Pattern to match logger calls
    # This handles single-line and multi-line logger calls
    pattern = re.compile(
        r'_logger\.(LogError|LogWarning|LogInformation|LogDebug|LogCritical|LogTrace)\s*\(([^;]+)\)',
        re.DOTALL
    )
    
    # Process all matches
    new_content = pattern.sub(sanitize_log_params, content)
    
    # Write back if changed
    if new_content != content:
        with open(filepath, 'w', encoding='utf-8') as f:
            f.write(new_content)
        return True
    return False

if __name__ == '__main__':
    filepath = sys.argv[1]
    if process_file(filepath):
        print(f"  ✓ Fixed logging statements")
    else:
        print(f"  - No changes needed")
PYTHON_SCRIPT
    
    ((fixed_count++))
}

# Process Admin project
echo "=== Processing Admin Project ==="
for file in ./ConduitLLM.Admin/Controllers/*.cs ./ConduitLLM.Admin/Services/*.cs; do
    fix_file "$file" "admin"
done

# Process Http project
echo -e "\n=== Processing Http Project ==="
for file in ./ConduitLLM.Http/Controllers/*.cs ./ConduitLLM.Http/Services/*.cs ./ConduitLLM.Http/Middleware/*.cs; do
    fix_file "$file" "http"
done

# Process WebUI project
echo -e "\n=== Processing WebUI Project ==="
for file in ./ConduitLLM.WebUI/Controllers/*.cs ./ConduitLLM.WebUI/Services/*.cs ./ConduitLLM.WebUI/Middleware/*.cs; do
    fix_file "$file" "webui"
done

# Process Core services that handle user input
echo -e "\n=== Processing Core Project ==="
for file in ./ConduitLLM.Core/Services/SecurityEventLogger.cs ./ConduitLLM.Core/Services/AudioAuditLogger.cs ./ConduitLLM.Core/Middleware/*.cs; do
    fix_file "$file" "core"
done

echo -e "\n=== Summary ==="
echo "Processed files to fix log injection vulnerabilities"
echo
echo "Next steps:"
echo "1. Review the changes: git diff"
echo "2. Build the solution: dotnet build"
echo "3. Run tests: dotnet test"
echo "4. Commit: git commit -am 'fix: resolve CodeQL log injection alerts with inline sanitization'"
echo "5. Push and verify CodeQL scan passes"