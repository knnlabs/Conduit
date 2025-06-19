#!/bin/bash

# Auto-version script for Conduit .NET packages
# Usage: ./version-dotnet.sh [patch|minor|major]

set -e

VERSION_TYPE=${1:-patch}
CURRENT_BRANCH=$(git branch --show-current)

echo "🔄 Auto-versioning Conduit .NET packages..."
echo "📦 Version type: $VERSION_TYPE"
echo "🌿 Current branch: $CURRENT_BRANCH"

# Get current version from Directory.Build.props
CURRENT_VERSION=$(grep -oP '<Version>\K[^<]+' Directory.Build.props | head -1)
echo "📋 Current version: $CURRENT_VERSION"

# Calculate new version based on branch and type
if [[ "$CURRENT_BRANCH" == "dev" ]]; then
    # For dev: create prerelease with timestamp
    BUILD_NUMBER=$(date +%Y%m%d%H%M%S)
    if [[ "$CURRENT_VERSION" =~ -dev ]]; then
        # Already a dev version, increment
        BASE_VERSION=$(echo "$CURRENT_VERSION" | cut -d'-' -f1)
        NEW_VERSION="$BASE_VERSION-dev.$BUILD_NUMBER"
    else
        # Convert stable to dev version
        NEW_VERSION="$CURRENT_VERSION-dev.$BUILD_NUMBER"
    fi
    echo "📦 Creating dev prerelease version..."
elif [[ "$CURRENT_BRANCH" == "master" ]]; then
    # For master: semantic versioning
    echo "📦 Creating $VERSION_TYPE version..."
    
    # Parse current version and increment
    IFS='.' read -ra VERSION_PARTS <<< "${CURRENT_VERSION%%-*}"
    MAJOR=${VERSION_PARTS[0]}
    MINOR=${VERSION_PARTS[1]:-0}
    PATCH=${VERSION_PARTS[2]:-0}
    
    case $VERSION_TYPE in
        major)
            MAJOR=$((MAJOR + 1))
            MINOR=0
            PATCH=0
            ;;
        minor)
            MINOR=$((MINOR + 1))
            PATCH=0
            ;;
        patch)
            PATCH=$((PATCH + 1))
            ;;
        *)
            echo "❌ Invalid version type: $VERSION_TYPE"
            echo "   Valid options: patch, minor, major"
            exit 1
            ;;
    esac
    
    NEW_VERSION="$MAJOR.$MINOR.$PATCH"
else
    echo "⚠️  Warning: Not on dev or master branch, creating patch version..."
    # Parse current version and increment patch
    IFS='.' read -ra VERSION_PARTS <<< "${CURRENT_VERSION%%-*}"
    MAJOR=${VERSION_PARTS[0]}
    MINOR=${VERSION_PARTS[1]:-0}
    PATCH=${VERSION_PARTS[2]:-0}
    PATCH=$((PATCH + 1))
    NEW_VERSION="$MAJOR.$MINOR.$PATCH"
fi

echo "🎯 New version: $NEW_VERSION"

# Update version in Directory.Build.props
echo "🔄 Updating Directory.Build.props..."
# For assembly versions, use numeric-only version (strip prerelease suffix)
NUMERIC_VERSION=$(echo "$NEW_VERSION" | sed 's/-.*$//')

sed -i.bak "s|<Version>[^<]*</Version>|<Version>$NEW_VERSION</Version>|g" Directory.Build.props
sed -i.bak "s|<AssemblyVersion>[^<]*</AssemblyVersion>|<AssemblyVersion>$NUMERIC_VERSION</AssemblyVersion>|g" Directory.Build.props
sed -i.bak "s|<FileVersion>[^<]*</FileVersion>|<FileVersion>$NUMERIC_VERSION</FileVersion>|g" Directory.Build.props
sed -i.bak "s|<InformationalVersion>[^<]*</InformationalVersion>|<InformationalVersion>$NEW_VERSION</InformationalVersion>|g" Directory.Build.props

# Remove backup file
rm -f Directory.Build.props.bak

echo "✅ Version updated in Directory.Build.props"

# Build and test
echo "🔨 Building solution..."
dotnet build --configuration Release

echo "🧪 Running tests..."
dotnet test --configuration Release --no-build

# Create packages
echo "📦 Creating NuGet packages..."
dotnet pack ConduitLLM.Configuration/ConduitLLM.Configuration.csproj --configuration Release --no-build --output ./nupkgs
dotnet pack ConduitLLM.Core/ConduitLLM.Core.csproj --configuration Release --no-build --output ./nupkgs
dotnet pack ConduitLLM.Providers/ConduitLLM.Providers.csproj --configuration Release --no-build --output ./nupkgs

echo ""
echo "🎉 .NET packages versioned successfully!"
echo ""
echo "📦 Packages created:"
ls -la ./nupkgs/*.nupkg 2>/dev/null || echo "   (No packages found - check for build errors)"
echo ""
echo "📋 Next steps:"
echo "   1. Review the changes: git diff Directory.Build.props"
echo "   2. Commit: git add Directory.Build.props && git commit -m 'chore: bump .NET packages to v$NEW_VERSION'"
echo "   3. Push: git push origin $CURRENT_BRANCH"

if [[ "$CURRENT_BRANCH" == "master" ]]; then
    echo "   4. Publish to NuGet.org:"
    echo "      dotnet nuget push ./nupkgs/*.nupkg --source https://api.nuget.org/v3/index.json --api-key YOUR_API_KEY"
elif [[ "$CURRENT_BRANCH" == "dev" ]]; then
    echo "   4. Publish to GitHub Packages:"
    echo "      dotnet nuget add source --username USERNAME --password TOKEN --store-password-in-clear-text --name github 'https://nuget.pkg.github.com/OWNER/index.json'"
    echo "      dotnet nuget push ./nupkgs/*.nupkg --source github"
fi

echo ""
echo "💡 Installation commands:"
echo "   dotnet add package ConduitLLM.Core --version $NEW_VERSION"
echo "   dotnet add package ConduitLLM.Providers --version $NEW_VERSION"
echo "   dotnet add package ConduitLLM.Configuration --version $NEW_VERSION"