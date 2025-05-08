# Versioning Guide for Conduit

This document explains the versioning system used in Conduit, how to update versions, and how the version check system works.

## Versioning Approach

Conduit uses [Semantic Versioning](https://semver.org/) with the format: `MAJOR.MINOR.PATCH`

- **MAJOR** version: Incremented for incompatible API changes
- **MINOR** version: Incremented for new features in a backward-compatible manner
- **PATCH** version: Incremented for backward-compatible bug fixes

## How Version Numbers Are Managed

### Central Version Configuration

All version numbers are centrally defined in the `Directory.Build.props` file in the root of the repository. This ensures that all projects use the same version number.

```xml
<Project>
  <PropertyGroup>
    <Version>1.0.0</Version>
    <AssemblyVersion>1.0.0</AssemblyVersion>
    <FileVersion>1.0.0</FileVersion>
    <InformationalVersion>1.0.0</InformationalVersion>
    <!-- Other properties -->
  </PropertyGroup>
</Project>
```

### Updating Versions

When preparing a new release:

1. Edit the `Directory.Build.props` file to increase the version numbers
2. Commit the change with a message like "Update version to X.Y.Z"
3. Create a new GitHub release with the same version

The version in `Directory.Build.props` flows through to:
- Assembly version information
- Docker image tags
- NuGet packages (if any)
- WebUI version display

## Automated Version Checking

Conduit includes an automated version checking system that:

1. Reads the current version from assembly metadata
2. Periodically checks GitHub releases API to see if a newer version is available
3. Displays a notification in the WebUI when a new version is detected

### Configuration

The version check system can be configured in the application settings:

```json
{
  "VersionCheck": {
    "Enabled": true,
    "IntervalHours": 24
  }
}
```

Or via environment variables:

```
CONDUIT_VERSION_CHECK_ENABLED=true
CONDUIT_VERSION_CHECK_INTERVAL_HOURS=24
```

### Manual Version Check

Users can manually check for updates on the About page in the WebUI, which will show the current version and provide a button to check for updates.

## Docker Image Versioning

When building Docker images through GitHub Actions:

1. Images are automatically tagged with:
   - The semantic version number (when building from a release tag)
   - The branch name (e.g., `master`, `dev`)
   - The commit SHA
   - `latest` tag for the master branch

2. Older versions are retained in the container registry, allowing users to pin to specific versions.

## Version Display

The current version is displayed in several places:

1. The About page in the WebUI
2. Startup logs
3. API responses include a version header
4. Docker image tags

## Best Practices

1. **Follow Semantic Versioning** principles when deciding which version component to increment
2. **Document changes** in the GitHub release notes
3. **Tag releases** in Git with the same version number as in `Directory.Build.props`
4. **Update the version** before merging to master for a release