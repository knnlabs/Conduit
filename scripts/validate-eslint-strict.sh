#!/bin/bash

# Strict ESLint validation wrapper script
# This script maintains backward compatibility by calling the unified script with --strict flag
# This is what CI/CD uses and the pre-push hook calls

# Get the directory of this script
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Call the unified validation script with --strict flag
exec "${SCRIPT_DIR}/validate-eslint.sh" --strict "$@"