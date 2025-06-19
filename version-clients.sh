#!/bin/bash

# Simple version wrapper for Conduit Node.js clients
# This script automatically detects the current branch and versions accordingly

set -e

echo "🔄 Auto-versioning Conduit Node.js clients..."

CURRENT_BRANCH=$(git branch --show-current)
echo "🌿 Current branch: $CURRENT_BRANCH"

if [[ "$CURRENT_BRANCH" == "dev" ]]; then
    echo "📦 Creating dev versions with timestamps..."
elif [[ "$CURRENT_BRANCH" == "master" ]]; then
    echo "📦 Creating production patch versions..."
else
    echo "⚠️  Warning: Not on dev or master branch, will create patch versions..."
fi

echo ""

# Version Core client
echo "🔨 Versioning Core client..."
cd Clients/Node/Core
npm run version:auto
npm run build
NEW_VERSION=$(node -p "require('./package.json').version")
echo "✅ Core client versioned to $NEW_VERSION"
cd - > /dev/null

echo ""

# Version Admin client  
echo "🔨 Versioning Admin client..."
cd Clients/Node/Admin
npm run version:auto
npm run build
NEW_VERSION=$(node -p "require('./package.json').version")
echo "✅ Admin client versioned to $NEW_VERSION"
cd - > /dev/null

echo ""
echo "🎉 Both clients versioned successfully!"
echo ""
echo "📋 Next steps:"
echo "   1. Review the changes: git diff"
echo "   2. Commit: git add . && git commit -m 'chore: bump client versions'"
echo "   3. Push: git push origin $CURRENT_BRANCH"

if [[ "$CURRENT_BRANCH" == "master" ]]; then
    echo "   4. Publish to NPM:"
    echo "      cd Clients/Node/Core && npm publish"
    echo "      cd Clients/Node/Admin && npm publish"
fi