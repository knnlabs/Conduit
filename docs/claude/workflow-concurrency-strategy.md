# GitHub Actions Workflow Concurrency Strategy

**Last Updated**: 2025-01-23

## Overview

This document explains our GitHub Actions workflow concurrency strategy, specifically when and why we cancel in-progress workflow runs.

## Concurrency Configuration

All workflows use the `concurrency` feature to prevent multiple runs on the same branch:

```yaml
concurrency:
  group: ${{ github.workflow }}-${{ github.ref }}
  cancel-in-progress: <true|false>
```

## Workflows That SHOULD Be Cancelled

### 1. Build and Test (`build-and-release.yml`)
- **Cancel for**: Pull requests and `dev` branch pushes
- **Keep running for**: `master` branch pushes
- **Reason**: Faster feedback on development work

### 2. Documentation (`documentation.yml`)
- **Cancel for**: All scenarios
- **Reason**: Only the latest documentation matters

## Workflows That MUST NOT Be Cancelled

### 1. Release Workflows
- `release-orchestration.yml`
- `version-and-publish.yml` (Node packages)
- `dotnet-version-and-publish.yml` (.NET packages)

**Why not cancel:**
- Partial package publication to NPM/NuGet
- Version number inconsistencies
- Broken git tags and releases
- Failed artifact uploads

### 2. Database Migration Validation
- `migration-validation.yml`

**Why not cancel:**
- Must validate complete migration path
- Ensures schema integrity
- Prevents broken database states

### 3. Security Scanning
- `codeql-analysis.yml`

**Why not cancel:**
- Must complete full security analysis
- Ensures all vulnerabilities are detected
- Required for compliance

## Best Practices

1. **Development branches** (`dev`): Cancel old runs for faster iteration
2. **Production branches** (`master`): Never cancel to ensure stability
3. **Release processes**: Always complete to maintain consistency
4. **Security scans**: Always complete for comprehensive coverage

## Implementation Example

For a typical build workflow:

```yaml
# Cancel for PRs and dev, but not for master
concurrency:
  group: ${{ github.workflow }}-${{ github.ref }}
  cancel-in-progress: ${{ github.event_name == 'pull_request' || github.ref == 'refs/heads/dev' }}
```

## Edge Cases to Consider

1. **Force pushes**: New commits will cancel old runs (where enabled)
2. **Multiple PRs**: Each PR has its own concurrency group
3. **Manual runs**: Treated the same as automatic runs
4. **Scheduled runs**: Should not be cancelled (security scans)