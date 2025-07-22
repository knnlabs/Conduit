# Release Process for @conduit/core

This document describes the release process for the Conduit Core Client library.

## Automated Release Process

The library is automatically published to NPM when changes are pushed to the `dev` or `master` branches.

### Development Releases (dev branch)

When changes are pushed to the `dev` branch:
1. The GitHub Action automatically runs tests and builds the package
2. Version is bumped as a prerelease (e.g., `0.1.1-dev.0`)
3. Package is published to NPM with the `dev` tag
4. Install with: `npm install @conduit/core@dev`

### Production Releases (master branch)

When changes are merged to the `master` branch:
1. The GitHub Action automatically runs tests and builds the package
2. Version is bumped as a patch release by default (e.g., `0.1.1`)
3. Package is published to NPM with the `latest` tag
4. A GitHub release is created
5. Install with: `npm install @conduit/core@latest`

## Manual Release Process

You can manually trigger a release using the GitHub Actions workflow dispatch:

1. Go to Actions → "Publish Core Client to NPM"
2. Click "Run workflow"
3. Select the branch and version type:
   - `patch`: Bug fixes (0.1.0 → 0.1.1)
   - `minor`: New features (0.1.0 → 0.2.0)
   - `major`: Breaking changes (0.1.0 → 1.0.0)
   - `prerelease`: Development version (0.1.0 → 0.1.1-dev.0)

## Version History

The library follows semantic versioning (semver):
- **Major**: Breaking API changes
- **Minor**: New features, backwards compatible
- **Patch**: Bug fixes, backwards compatible
- **Prerelease**: Development versions

## Pre-publish Checklist

Before releasing, ensure:
- [ ] All tests pass (`npm test`)
- [ ] No linting errors (`npm run lint`)
- [ ] TypeScript compiles (`npm run typecheck`)
- [ ] Build succeeds (`npm run build`)
- [ ] Documentation is up to date
- [ ] CHANGELOG is updated (if applicable)

## NPM Configuration

The package is published under the `@conduit` scope. Ensure you have:
1. NPM account with publish access to `@conduit` scope
2. `NPM_TOKEN` secret configured in GitHub repository settings

## Troubleshooting

### Build Failures
- Check the GitHub Actions logs for detailed error messages
- Ensure all dependencies are properly installed
- Verify TypeScript configuration

### Publishing Failures
- Verify NPM_TOKEN is valid and has publish permissions
- Check if the version already exists on NPM
- Ensure package.json has correct metadata

### Version Conflicts
- If a version already exists, the publish will fail
- Manually bump the version and retry
- For dev builds, the version auto-increments

## Rolling Back a Release

If a problematic version is published:
1. Mark the version as deprecated: `npm deprecate @conduit/core@<version> "Contains bugs"`
2. Publish a new patch version with the fix
3. Update documentation to skip the problematic version