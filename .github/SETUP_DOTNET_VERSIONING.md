# .NET Auto-Versioning Setup Guide

This guide explains how to set up fully automated versioning and publishing for Conduit .NET NuGet packages.

## 🚀 Quick Setup

### 1. NuGet.org API Key Setup (Optional)
1. Go to [nuget.org](https://www.nuget.org) and create an API key:
   - Login → Account Settings → API Keys → Create
   - Choose "Push new packages and package versions" scope
   - Add glob pattern: `ConduitLLM.*`
   - Copy the API key

2. Add the key to your GitHub repository secrets:
   - Go to your GitHub repo → Settings → Secrets and variables → Actions
   - Click "New repository secret"
   - Name: `NUGET_API_KEY`
   - Value: (paste your NuGet API key)

### 2. GitHub Packages Setup
GitHub Packages is automatically configured and doesn't require additional setup.

### 3. Repository Permissions
Ensure GitHub Actions has write permissions:
- Go to Settings → Actions → General
- Under "Workflow permissions", select "Read and write permissions"
- Check "Allow GitHub Actions to create and approve pull requests"

## 🎯 How It Works

### Automatic Triggers
The workflow automatically runs when:
- You push changes to `origin/dev` or `origin/master`
- Changes are detected in core library directories:
  - `ConduitLLM.Configuration/`
  - `ConduitLLM.Core/`
  - `ConduitLLM.Providers/`
  - `Directory.Build.props`

### Package Strategy

#### Packages Created
✅ **ConduitLLM.Configuration** - Data access and configuration  
✅ **ConduitLLM.Core** - Core interfaces and models  
✅ **ConduitLLM.Providers** - LLM provider implementations  

❌ **Applications not packaged**: Http, WebUI, Admin, Examples, Tests

#### Version Strategy

##### Dev Branch (`origin/dev`)
- **Versions**: `0.1.0-dev.20250619123456`, `0.1.0-dev.20250619124567`, etc.
- **Published to**: GitHub Packages only
- **Behavior**: Auto-increments with timestamp, no commits back to repo
- **Install**: 
  ```bash
  dotnet nuget add source https://nuget.pkg.github.com/OWNER/index.json -n github
  dotnet add package ConduitLLM.Core --version 0.1.0-dev.20250619123456 --source github
  ```

##### Master Branch (`origin/master`)
- **Versions**: `0.1.0` → `0.1.1` → `0.1.2` (semantic versioning)
- **Published to**: GitHub Packages + NuGet.org (if API key provided)
- **Behavior**: Commits version changes back to repo, creates GitHub releases
- **Install**: 
  ```bash
  dotnet add package ConduitLLM.Core --version 0.1.1
  ```

### Smart Version Detection
The workflow automatically detects version type from commit messages:
- **Major**: Contains "breaking" or "major" → `1.0.0` → `2.0.0`
- **Minor**: Contains "feat", "feature", or "minor" → `1.0.0` → `1.1.0`
- **Patch**: Everything else (default) → `1.0.0` → `1.0.1`

## 📝 Usage Examples

### Normal Development Flow
```bash
# 1. Make changes to library code
echo "// New feature" >> ConduitLLM.Core/NewFeature.cs

# 2. Commit and push to dev
git add .
git commit -m "feat: add new LLM provider support"
git push origin dev

# 3. GitHub Actions automatically:
#    - Detects Core library changes
#    - Creates version 0.1.0-dev.20250619123456
#    - Builds, tests, and packs all 3 libraries
#    - Publishes to GitHub Packages
```

### Feature Release to Production
```bash
# 1. Merge dev to master
git checkout master
git merge dev

# 2. Push to master
git push origin master

# 3. GitHub Actions automatically:
#    - Detects "feat" in commit message
#    - Creates version 0.2.0 (minor bump)
#    - Updates Directory.Build.props
#    - Commits version change back to master
#    - Publishes to GitHub Packages + NuGet.org
#    - Creates GitHub release
```

### Manual Versioning
You can also version manually using the provided script:
```bash
# Patch version (0.1.0 → 0.1.1)
./version-dotnet.sh patch

# Minor version (0.1.0 → 0.2.0)
./version-dotnet.sh minor

# Major version (0.1.0 → 1.0.0)
./version-dotnet.sh major
```

Or trigger GitHub Actions manually:
1. Go to Actions tab in GitHub
2. Select "Auto-Version and Publish .NET Packages"
3. Click "Run workflow"
4. Choose version type and options

## 🔍 Package Information

### ConduitLLM.Configuration
- **Purpose**: Data access, Entity Framework contexts, configurations
- **Dependencies**: EF Core, Redis, PostgreSQL, ASP.NET Core
- **Use Case**: When you need database access or configuration management

### ConduitLLM.Core  
- **Purpose**: Core interfaces, models, business logic
- **Dependencies**: AWS S3, Polly, TiktokenSharp, ConduitLLM.Configuration
- **Use Case**: When building applications that use Conduit's core functionality

### ConduitLLM.Providers
- **Purpose**: LLM provider implementations (OpenAI, Anthropic, Bedrock, etc.)
- **Dependencies**: AWS SDKs, Polly, ConduitLLM.Core, ConduitLLM.Configuration  
- **Use Case**: When you want to use specific LLM providers in your application

## 📦 Installation Examples

### Basic Usage
```bash
# Install core functionality
dotnet add package ConduitLLM.Core

# Add provider support  
dotnet add package ConduitLLM.Providers

# Add configuration support
dotnet add package ConduitLLM.Configuration
```

### Dev Versions
```bash
# Add GitHub Packages source
dotnet nuget add source https://nuget.pkg.github.com/knnlabs/index.json -n github

# Install dev versions
dotnet add package ConduitLLM.Core --version 0.1.0-dev.20250619123456 --source github
```

### In your .csproj
```xml
<PackageReference Include="ConduitLLM.Core" Version="0.1.1" />
<PackageReference Include="ConduitLLM.Providers" Version="0.1.1" />
```

## 🛠️ Troubleshooting

### Workflow Not Running
1. Check that changes are in the watched directories
2. Verify you're pushing to `dev` or `master` branch
3. Check GitHub Actions permissions in repository settings
4. Look for path exclusions (*.md files are ignored)

### Package Publishing Fails
1. **GitHub Packages**: Check repository permissions and GITHUB_TOKEN
2. **NuGet.org**: Verify NUGET_API_KEY secret is set correctly
3. **Package conflicts**: GitHub Actions uses `--skip-duplicate` to handle conflicts
4. **Version conflicts**: Check if version already exists on target feed

### Build Failures
1. Check .NET SDK version compatibility
2. Verify all dependencies are available
3. Run tests locally: `dotnet test`
4. Check for breaking changes in dependencies

### Manual Recovery
If something goes wrong, you can always:
```bash
# Reset to working state
git checkout master
git reset --hard origin/master

# Manually version and publish
./version-dotnet.sh patch
dotnet nuget push ./nupkgs/*.nupkg --source https://api.nuget.org/v3/index.json --api-key YOUR_KEY
```

## 🎉 Benefits

✅ **Automatic versioning** - based on branch and commit messages  
✅ **Multi-target publishing** - GitHub Packages + NuGet.org  
✅ **Smart change detection** - only versions when libraries change  
✅ **Semantic versioning** - proper SemVer compliance  
✅ **Release automation** - GitHub releases with changelogs  
✅ **Symbol packages** - debugging support with .snupkg files  
✅ **Dependency management** - proper package references  

Your .NET libraries are now ready for automated distribution! 📦🚀

## 📋 Package Dependency Graph

```
ConduitLLM.Configuration (foundational)
├── Entity Framework Core
├── Redis Caching
└── PostgreSQL Support

ConduitLLM.Core
├── ConduitLLM.Configuration
├── AWS S3 SDK
├── Polly (resilience)
└── TiktokenSharp (tokenization)

ConduitLLM.Providers  
├── ConduitLLM.Core
├── ConduitLLM.Configuration
├── AWS Bedrock SDK
├── AWS Transcribe SDK
└── AWS Polly SDK
```

This structure allows consumers to install only what they need while maintaining proper dependency resolution.