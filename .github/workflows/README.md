# GitHub Actions Workflows

## Active Workflows

### build-and-release.yml
**Primary CI/CD workflow** that:
1. Runs CodeQL security analysis first
2. Blocks Docker image builds if high-severity security issues are found
3. Runs build and tests
4. Builds and publishes Docker images only if all checks pass

Triggers on:
- Push to `master` or `dev` branches
- Pull requests to `master` or `dev`
- Release publications

### codeql-analysis.yml
**Scheduled security scans** that:
- Runs weekly CodeQL analysis
- Can be triggered manually
- Uploads results to GitHub Security tab

## Deprecated Workflows

### docker-release.yml
- **Status**: DEPRECATED
- **Replacement**: Use `build-and-release.yml`
- Now only runs on manual trigger

### codeql-local.yml
- **Status**: DEPRECATED
- **Replacement**: Use `build-and-release.yml`
- Now only runs on manual trigger

### publish-docker.yml
- **Status**: DEPRECATED
- Already marked as deprecated in favor of docker-release.yml

## Workflow Dependencies

The `build-and-release.yml` workflow ensures that:
1. **Security First**: CodeQL must pass before any images are built
2. **Quality Gates**: Tests must pass before images are published
3. **No Vulnerabilities**: High-severity security issues block the entire pipeline

## Migration Notes

All automated builds now go through `build-and-release.yml` which provides:
- Consolidated security scanning
- Proper dependency ordering
- Fail-fast on security issues
- Single source of truth for CI/CD