#!/bin/bash

# Auto-version script for Conduit Node.js SDKs
# Usage: ./scripts/auto-version.sh [core|admin|both] [patch|minor|major]

set -e

CLIENT=${1:-both}
VERSION_TYPE=${2:-patch}
CURRENT_BRANCH=$(git branch --show-current)

echo "🔄 Auto-versioning Conduit Node.js SDKs..."
echo "📂 Client: $CLIENT"
echo "📦 Version type: $VERSION_TYPE"
echo "🌿 Current branch: $CURRENT_BRANCH"

# Function to version a client
version_client() {
    local client_path=$1
    local client_name=$2
    
    echo "🔨 Processing $client_name client..."
    
    cd "$client_path"
    
    # Determine version command based on branch
    if [[ "$CURRENT_BRANCH" == "dev" ]]; then
        echo "📦 Creating dev prerelease version..."
        npm run version:dev
    elif [[ "$CURRENT_BRANCH" == "master" ]]; then
        echo "📦 Creating $VERSION_TYPE version..."
        npm run "version:$VERSION_TYPE"
    else
        echo "⚠️  Warning: Not on dev or master branch, creating patch version..."
        npm run version:patch
    fi
    
    # Get new version
    NEW_VERSION=$(node -p "require('./package.json').version")
    echo "✅ $client_name client versioned to $NEW_VERSION"
    
    # Build and test
    echo "🔨 Building $client_name client..."
    npm run build
    
    echo "🧪 Testing $client_name client..."
    npm test
    
    echo "✅ $client_name client ready!"
    
    cd - > /dev/null
}

# Version Core client
if [[ "$CLIENT" == "core" || "$CLIENT" == "both" ]]; then
    version_client "SDKs/Node/Core" "Core"
fi

# Version Admin client
if [[ "$CLIENT" == "admin" || "$CLIENT" == "both" ]]; then
    version_client "SDKs/Node/Admin" "Admin"
fi

echo ""
echo "🎉 Auto-versioning complete!"
echo ""
echo "📋 Next steps:"
if [[ "$CURRENT_BRANCH" == "dev" ]]; then
    echo "   • Commit and push to dev branch"
    echo "   • Dev versions will be available as @latest-dev"
elif [[ "$CURRENT_BRANCH" == "master" ]]; then
    echo "   • Commit and push to master branch"
    echo "   • Production versions will be available as @latest"
    echo "   • Consider publishing to NPM with: npm publish"
else
    echo "   • Review changes and commit"
    echo "   • Consider switching to dev or master branch"
fi