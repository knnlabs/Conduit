#!/bin/bash

# This script compiles and runs the XML documentation coverage checker

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CHECKER_DIR="$SCRIPT_DIR/tools/XmlDocumentationChecker"
OUTPUT_DIR="$SCRIPT_DIR/out/tools"

echo "Compiling XML documentation coverage checker..."
dotnet build "$CHECKER_DIR" -o "$OUTPUT_DIR"

echo "Running documentation coverage check..."
cd "$SCRIPT_DIR"
dotnet run --project "$CHECKER_DIR" -- "$SCRIPT_DIR"

echo "Documentation coverage check completed."