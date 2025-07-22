#!/bin/bash
# Script to fix out-of-sync package-lock.json files

echo "🔧 Fixing package-lock.json files"
echo "================================="
echo ""
echo "This will regenerate package-lock.json files to match package.json"
echo ""

# Admin SDK
echo "📦 Updating Admin SDK package-lock.json..."
cd SDKs/Node/Admin
rm -f package-lock.json
npm install
echo "✅ Admin SDK done"
echo ""

# Core SDK
echo "📦 Updating Core SDK package-lock.json..."
cd ../Core
rm -f package-lock.json
npm install
echo "✅ Core SDK done"
echo ""

# WebUI
echo "📦 Updating WebUI package-lock.json..."
cd ../../../ConduitLLM.WebUI
rm -f package-lock.json
npm install
echo "✅ WebUI done"
echo ""

# Return to root
cd ..

echo "🎉 All package-lock.json files have been regenerated!"
echo ""
echo "Please review and commit these changes:"
echo "  git add SDKs/Node/Admin/package-lock.json"
echo "  git add SDKs/Node/Core/package-lock.json"
echo "  git add ConduitLLM.WebUI/package-lock.json"
echo "  git commit -m 'fix: regenerate package-lock.json files'"
echo ""
echo "After committing, the optimized Dockerfiles using 'npm ci' will work properly."