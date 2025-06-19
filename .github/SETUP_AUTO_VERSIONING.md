# Auto-Versioning Setup Guide

This guide explains how to set up fully automated versioning for the Conduit Node.js client libraries.

## 🚀 Quick Setup

### 1. NPM Token Setup
1. Go to [npmjs.com](https://www.npmjs.com) and create an automation token:
   - Login → Account Settings → Access Tokens → Generate New Token
   - Choose "Automation" type
   - Copy the token

2. Add the token to your GitHub repository secrets:
   - Go to your GitHub repo → Settings → Secrets and variables → Actions
   - Click "New repository secret"
   - Name: `NPM_TOKEN`
   - Value: (paste your NPM token)

### 2. Repository Permissions
Ensure GitHub Actions has write permissions:
- Go to Settings → Actions → General
- Under "Workflow permissions", select "Read and write permissions"
- Check "Allow GitHub Actions to create and approve pull requests"

## 🎯 How It Works

### Automatic Triggers
The workflow automatically runs when:
- You push changes to `origin/dev` or `origin/master`
- Changes are detected in `Clients/Node/` directory
- Package.json changes are ignored (prevents infinite loops)

### Version Strategy

#### Dev Branch (`origin/dev`)
- **Versions**: `1.1.0-dev.1`, `1.1.0-dev.2`, `1.1.0-dev.3`, etc.
- **NPM Tag**: `@dev`
- **Behavior**: Auto-increments prerelease number, no commits back to repo
- **Install**: `npm install @knn_labs/conduit-core-client@dev`

#### Master Branch (`origin/master`)
- **Versions**: `1.1.0` → `1.1.1` → `1.1.2` (patch increments by default)
- **NPM Tag**: `@latest`
- **Behavior**: Commits version changes back to repo, publishes to NPM
- **Install**: `npm install @knn_labs/conduit-core-client@latest`

### Smart Version Detection
The workflow automatically detects version type from commit messages:
- **Major**: Contains "breaking" or "major"
- **Minor**: Contains "feat", "feature", or "minor" 
- **Patch**: Everything else (default)

## 📝 Usage Examples

### Normal Development Flow
```bash
# 1. Make changes to client code
echo "console.log('new feature');" >> Clients/Node/Core/src/index.ts

# 2. Commit and push to dev
git add .
git commit -m "feat: add new logging feature"
git push origin dev

# 3. GitHub Actions automatically:
#    - Detects Core client changes
#    - Creates version 0.2.0-dev.1
#    - Builds and tests
#    - Publishes to NPM with @dev tag
```

### Feature Release
```bash
# 1. Merge dev to master
git checkout master
git merge dev

# 2. Push to master
git push origin master

# 3. GitHub Actions automatically:
#    - Detects "feat" in commit message
#    - Creates version 0.3.0 (minor bump)
#    - Builds and tests
#    - Commits version change back to master
#    - Publishes to NPM with @latest tag
```

### Manual Control
You can also trigger versioning manually:
1. Go to Actions tab in GitHub
2. Select "Auto-Version and Publish Node Clients"
3. Click "Run workflow"
4. Choose version type and options

## 🔍 Monitoring

### Check Workflow Status
- Go to GitHub → Actions tab
- Look for "Auto-Version and Publish Node Clients" workflows
- Green checkmark = success, red X = failure

### NPM Package Status
```bash
# Check latest dev versions
npm view @knn_labs/conduit-core-client versions --json | jq '.[] | select(test("dev"))'

# Check latest stable versions  
npm view @knn_labs/conduit-core-client versions --json | jq '.[] | select(test("dev") | not)'
```

### Installation Commands
The workflow provides installation commands in its output:
- Dev: `npm install @knn_labs/conduit-core-client@dev`
- Stable: `npm install @knn_labs/conduit-core-client@latest`

## 🛠️ Troubleshooting

### Workflow Not Running
1. Check that changes are in `Clients/Node/` directory
2. Verify you're pushing to `dev` or `master` branch
3. Check GitHub Actions permissions in repository settings

### NPM Publishing Fails
1. Verify `NPM_TOKEN` secret is set correctly
2. Check if package name is available on NPM
3. Ensure you have publish permissions for the package

### Version Conflicts
The workflow includes retry logic, but if issues persist:
1. Check for merge conflicts in package.json
2. Manually resolve and push
3. Re-run the workflow if needed

### Manual Recovery
If something goes wrong, you can always:
```bash
# Reset to a working state
git checkout master
git reset --hard origin/master

# Manually version and publish
cd Clients/Node/Core
npm version patch
npm publish
```

## 🎉 Benefits

✅ **Zero manual intervention** - just push code and versions are handled  
✅ **Branch-aware** - dev gets prerelease versions, master gets stable  
✅ **Smart detection** - only versions when client code actually changes  
✅ **Conflict-free** - robust handling of concurrent pushes  
✅ **NPM integration** - automatic publishing with correct tags  
✅ **Rollback safe** - easy to recover from any issues  

Your Node.js clients will now stay perfectly synchronized with your development workflow! 🚀