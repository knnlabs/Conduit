# Release Strategy for Conduit Admin Client

## Automated Publishing

This package uses GitHub Actions for automated publishing to NPM with different strategies for dev and production releases.

## Version Strategy

### Development Releases (`dev` branch)
- **Trigger**: Every push to `dev` branch
- **Version**: Prerelease with `-dev` suffix (e.g., `1.0.1-dev.0`, `1.0.1-dev.1`)
- **NPM Tag**: `dev`
- **Installation**: `npm install @conduit/admin-client@dev`

### Production Releases (`master` branch)
- **Trigger**: Push to `master` branch OR manual workflow dispatch
- **Version**: Semantic versioning (patch/minor/major)
- **NPM Tag**: `latest`
- **Installation**: `npm install @conduit/admin-client@latest`

## Manual Publishing

### Prerequisites
1. Set up NPM_TOKEN in GitHub repository secrets
2. Ensure you have publish permissions for `@conduit/admin-client`

### Local Development Publishing
```bash
# For dev releases
npm run publish:dev

# For stable releases
npm run publish:stable
```

### Manual Version Updates
```bash
# Patch version (1.0.0 -> 1.0.1)
npm run version:patch

# Minor version (1.0.0 -> 1.1.0)
npm run version:minor

# Major version (1.0.0 -> 2.0.0)
npm run version:major

# Dev prerelease (1.0.0 -> 1.0.1-dev.0)
npm run version:dev
```

## GitHub Secrets Required

Add these secrets in your GitHub repository settings:

1. **NPM_TOKEN**
   - Get from: https://www.npmjs.com/settings/[username]/tokens
   - Type: Automation token (recommended) or Publish token
   - Scopes: Read and write

## Workflow Features

- ✅ Automated testing before publishing
- ✅ Linting checks
- ✅ Build verification
- ✅ Automatic version bumping
- ✅ Git tagging
- ✅ GitHub releases (for production)
- ✅ NPM package publishing
- ✅ Support for manual workflow dispatch

## Package Access

### Latest Stable Version
```bash
npm install @conduit/admin-client
# or specifically
npm install @conduit/admin-client@latest
```

### Development Version
```bash
npm install @conduit/admin-client@dev
```

### Specific Version
```bash
npm install @conduit/admin-client@1.2.3
```

## Monitoring

- **NPM Package**: https://www.npmjs.com/package/@conduit/admin-client
- **GitHub Releases**: https://github.com/knnlabs/Conduit/releases
- **Workflow Status**: Actions tab in GitHub repository

## Troubleshooting

### Common Issues

1. **NPM_TOKEN expired**: Update the token in GitHub secrets
2. **Build failures**: Check the Actions logs for specific errors
3. **Version conflicts**: Ensure version in package.json is unique
4. **Permission denied**: Verify NPM token has correct scopes

### Debug Workflow
Use workflow dispatch with different version types to test publishing manually.