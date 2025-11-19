#!/bin/bash
# Test GitHub Actions workflows locally using 'act'
# Install act first: https://github.com/nektos/act

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

# Check if act is installed (system or local)
ACT_CMD=""
if command -v act &> /dev/null; then
    ACT_CMD="act"
elif [ -f "$PROJECT_ROOT/bin/act" ]; then
    ACT_CMD="$PROJECT_ROOT/bin/act"
    echo "ℹ️  Using local act binary: $PROJECT_ROOT/bin/act"
    echo ""
else
    echo "❌ 'act' is not installed"
    echo ""
    echo "Install with:"
    echo "  macOS:  brew install act"
    echo "  Linux:  curl https://raw.githubusercontent.com/nektos/act/master/install.sh | sudo bash"
    echo ""
    echo "The installer creates ./bin/act - you can either:"
    echo "  1. Use it directly: ./bin/act"
    echo "  2. Move to PATH: sudo mv ./bin/act /usr/local/bin/"
    echo "  3. Run this script (it will find ./bin/act automatically)"
    exit 1
fi

echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "Testing GitHub Actions Workflows with 'act'"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

cd "$PROJECT_ROOT"

# Show available workflows
echo "📋 Available workflows and jobs:"
echo ""
$ACT_CMD -l
echo ""

# Ask user what to test
echo "What would you like to test?"
echo ""
echo "  1. Validate job only (fastest - builds and tests)"
echo "  2. Full CI workflow (includes Docker builds - slow)"
echo "  3. Dry run (show what would execute)"
echo "  4. List workflows and exit"
echo ""
read -p "Enter choice [1-4]: " choice

case $choice in
    1)
        echo ""
        echo "🧪 Testing validate job..."
        echo ""
        echo "Note: This will:"
        echo "  - Start PostgreSQL and Redis containers"
        echo "  - Build .NET solution"
        echo "  - Run tests"
        echo "  - Build Node.js SDKs"
        echo "  - Type-check WebAdmin"
        echo ""
        read -p "Continue? [y/N]: " confirm
        if [[ $confirm == [yY] ]]; then
            # Use --container-architecture linux/amd64 for compatibility
            $ACT_CMD push -j validate \
                --container-architecture linux/amd64 \
                -P ubuntu-latest=catthehacker/ubuntu:act-latest
        fi
        ;;

    2)
        echo ""
        echo "🧪 Testing full CI workflow..."
        echo ""
        echo "⚠️  WARNING: This will:"
        echo "  - Run all validation tests"
        echo "  - Build 3 Docker images (webadmin, http, admin)"
        echo "  - Take 15-30 minutes"
        echo "  - Use significant disk space"
        echo ""
        read -p "Continue? [y/N]: " confirm
        if [[ $confirm == [yY] ]]; then
            $ACT_CMD push \
                --container-architecture linux/amd64 \
                -P ubuntu-latest=catthehacker/ubuntu:act-latest
        fi
        ;;

    3)
        echo ""
        echo "🔍 Dry run - showing what would execute..."
        echo ""
        $ACT_CMD push -n
        ;;

    4)
        echo ""
        echo "👋 Exiting"
        exit 0
        ;;

    *)
        echo ""
        echo "❌ Invalid choice"
        exit 1
        ;;
esac

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "Done!"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""
echo "Tips:"
echo "  - Use 'act -l' to list all workflows and jobs"
echo "  - Use 'act push -j <job-name>' to test specific jobs"
echo "  - Use 'act -n' for dry run"
echo "  - Use '--secret-file .env' to provide secrets"
echo ""
