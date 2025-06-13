#!/bin/bash

# Deploy documentation to GitHub Pages

set -e

echo "Deploying documentation to GitHub Pages..."

# Save current branch
CURRENT_BRANCH=$(git branch --show-current)

# Build the documentation
cd website
npm run build
cd ..

# Create a temporary directory for the build
TEMP_DIR=$(mktemp -d)
cp -r website/build/* $TEMP_DIR/

# Switch to gh-pages branch
git checkout gh-pages

# Clear old files (keep .git and other hidden files)
find . -maxdepth 1 ! -name '.git' ! -name '.gitignore' ! -name '.nojekyll' ! -name '.' -exec rm -rf {} \;

# Copy new build files
cp -r $TEMP_DIR/* .

# Add all changes
git add -A

# Commit
git commit -m "docs: deploy documentation update $(date +'%Y-%m-%d %H:%M:%S')"

# Push to GitHub
git push origin gh-pages

# Switch back to original branch
git checkout $CURRENT_BRANCH

# Clean up
rm -rf $TEMP_DIR

echo "Documentation deployed successfully!"
echo "Visit https://knnlabs.github.io/Conduit/ to see the updated documentation."