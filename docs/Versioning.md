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
    <Version>3.0.0</Version>
    <AssemblyVersion>3.0.0</AssemblyVersion>
    <FileVersion>3.0.0</FileVersion>
    <InformationalVersion>3.0.0</InformationalVersion>
    <!-- Other properties -->
  </PropertyGroup>
</Project>
```

### Updating Versions

The whole product shares one version — the .NET services (via `Directory.Build.props`)
and both npm SDK packages. When preparing a new release:

1. Edit `Directory.Build.props` to set the new version number.
2. Set the same version in the two SDK manifests —
   `SDKs/Node/Common/package.json` and `SDKs/Node/Gateway/package.json` — then run `npm install --package-lock-only`
   in `SDKs/Node` to keep the lockfile in sync.
3. Commit the change ("Update version to X.Y.Z") and merge to `master`.
4. Cut the release by pushing a git tag — see [Release Channels](#release-channels).

The version in `Directory.Build.props` flows through to:
- Assembly version information
- Docker image tags
- WebAdmin version display

Published npm packages take their version from the git **tag** at release time (the
workflow stamps it onto both packages), so the `package.json` values above are
the development baseline — the tag is authoritative.

## Automated Version Checking

Conduit includes an automated version checking system that:

1. Reads the current version from assembly metadata
2. Periodically checks GitHub releases API to see if a newer version is available
3. Displays a notification in the WebAdmin when a new version is detected

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

Users can manually check for updates on the About page in the WebAdmin, which will show the current version and provide a button to check for updates.

## Release Channels

Releases are cut from `master` by pushing a version tag. The tag name selects the
channel — a tag is a **pre-release** if (and only if) its name contains a hyphen
(the SemVer rule):

| Tag | Channel | Docker | npm |
|---|---|---|---|
| `v3.0.0` | stable | `:3.0.0` + `:latest` | `3.0.0` `@latest` |
| `v3.0.0-beta.1` | beta | `:3.0.0-beta.1` + `:beta` | `3.0.0-beta.1` `@beta` |

- `:latest` / `@latest` always point at the newest **stable** release; a beta never
  moves them.
- `:beta` / `@beta` point at the newest **pre-release**.
- The exact `:X.Y.Z` image tag and exact npm version are immutable — pin to them for
  reproducibility.

Merges to `master` do **not** publish anything: CI builds the images only to validate
them. All publishing happens from `v*` tags.

## Docker Image Versioning

Images are published only from release tags (see [Release Channels](#release-channels)):
the exact semantic version (e.g. `:3.0.0`) plus the channel tag (`:latest` or `:beta`).
Older versions are retained in the container registry, so users can pin to a specific
version.

## Version Display

The current version is displayed in several places:

1. The About page in the WebAdmin
2. Startup logs
3. API responses include a version header
4. Docker image tags

## Best Practices

1. **Follow Semantic Versioning** principles when deciding which version component to increment
2. **Document changes** in the GitHub release notes
3. **Tag releases** in Git with the same version number as in `Directory.Build.props`
4. **Update the version** before merging to master for a release
