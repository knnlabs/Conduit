#!/bin/bash

# Script to fix log injection vulnerabilities using inline sanitization
# This approach uses a pattern that CodeQL should recognize

set -e

echo "=== Fixing Log Injection Vulnerabilities with Inline Sanitization ==="
echo

# Function to sanitize a file
sanitize_file() {
    local file=$1
    echo "Processing: $file"
    
    # Create a temporary file
    temp_file="${file}.tmp"
    
    # Process the file with sed to wrap user input parameters
    # This regex finds _logger.Log* calls and wraps the parameters with sanitization
    
    # For simple parameter logging like: _logger.LogError(ex, "Message {Param}", param);
    # We need to wrap param with sanitization
    
    # Use perl for more complex regex processing
    perl -pe '
        # Match logger calls with parameters
        if (/_logger\.Log(Error|Warning|Information|Debug|Critical)\s*\([^)]*,\s*"[^"]*"\s*,\s*(.+)\)/) {
            my $params = $2;
            # Split parameters and wrap each with sanitization
            my @parts = split(/,\s*/, $params);
            my @sanitized = map {
                # Skip if already sanitized
                if ($_ =~ /LogSanitizer\.SanitizeObject/ || $_ =~ /\.SanitizeForLogging/) {
                    $_
                } else {
                    # Wrap with sanitization
                    "(string)ConduitLLM.Core.Extensions.SecureLoggingExtensions.SanitizeForLogging($_)"
                }
            } @parts;
            my $sanitized_params = join(", ", @sanitized);
            s/(_logger\.Log(?:Error|Warning|Information|Debug|Critical)\s*\([^)]*,\s*"[^"]*"\s*,\s*)(.+)(\))/$1$sanitized_params$3/;
        }
    ' "$file" > "$temp_file"
    
    # Check if changes were made
    if ! diff -q "$file" "$temp_file" > /dev/null; then
        mv "$temp_file" "$file"
        echo "  ✓ Updated"
    else
        rm "$temp_file"
        echo "  - No changes needed"
    fi
}

# Find all controllers and services that need fixing
echo "Finding files to fix..."

# Admin Controllers
for file in ./ConduitLLM.Admin/Controllers/*.cs; do
    if grep -q "_logger\.Log" "$file" && ! grep -q "LogSecure" "$file"; then
        sanitize_file "$file"
    fi
done

# Admin Services (if not already using secure logging)
for file in ./ConduitLLM.Admin/Services/*.cs; do
    if grep -q "_logger\.Log" "$file" && ! grep -q "LogSecure" "$file"; then
        sanitize_file "$file"
    fi
done

# Http Controllers
for file in ./ConduitLLM.Http/Controllers/*.cs; do
    if grep -q "_logger\.Log" "$file" && ! grep -q "LogSecure" "$file"; then
        sanitize_file "$file"
    fi
done

# WebUI Controllers (if not already using secure logging)
for file in ./ConduitLLM.WebUI/Controllers/*.cs; do
    if grep -q "_logger\.Log" "$file" && ! grep -q "LogSecure" "$file"; then
        sanitize_file "$file"
    fi
done

echo
echo "✅ Completed log injection fixes"
echo
echo "Next steps:"
echo "1. Run: dotnet build"
echo "2. Commit the changes"
echo "3. Push and check CodeQL results"