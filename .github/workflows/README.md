# GitHub Actions Workflows

This repository uses a simplified, industry-standard CI/CD pipeline.

## Active Workflows

### 1. CI (`ci.yml`)
**Triggers:** Push to `master` or `dev`, Pull requests to `master`

**What it does:**
- Validates code builds and tests pass
- Builds Docker images (pushes only from `master`)
- Publishes NPM packages with `next` tag (only from `master`)

**Artifacts produced from `master`:**
- Docker: `ghcr.io/knnlabs/conduit-{webui,http,admin}:latest`
- NPM: `@conduitllm/{admin,core}@next`

### 2. Release (`release.yml`)
**Triggers:** Push of tags matching `v*`

**What it does:**
- Creates GitHub Release with auto-generated notes
- Builds and pushes versioned Docker images
- Publishes versioned NPM packages

**Artifacts produced:**
- Docker: `ghcr.io/knnlabs/conduit-{webui,http,admin}:1.2.3`
- NPM: `@conduitllm/{admin,core}@1.2.3`
- GitHub Release with changelog

### 3. CodeQL (`codeql-analysis.yml`)
**Triggers:** Push to `master` or `dev`, Weekly schedule, Manual dispatch

**What it does:**
- Scans for security vulnerabilities
- Results appear in Security tab
- Non-blocking, informational only

## Release Process

1. **Continuous delivery from `master`:**
   - Every merge to `master` automatically updates `:latest` Docker images
   - NPM packages are published with `next` tag for early adopters

2. **Stable releases:**
   ```bash
   git tag v1.2.3
   git push origin v1.2.3
   ```
   This triggers the release workflow which creates versioned artifacts.

## Artifact Locations

- **Docker Images:** https://github.com/orgs/knnlabs/packages
- **NPM Packages:** https://www.npmjs.com/~knn_labs
- **Security Results:** https://github.com/knnlabs/Conduit/security/code-scanning

## Design Principles

1. **YAGNI (You Ain't Gonna Need It):** Only essential workflows
2. **DRY (Don't Repeat Yourself):** No duplicate logic across workflows
3. **Industry Standard:** Using official actions, no custom parsing
4. **Simple:** ~300 lines total vs previous 2,187 lines

## Required Secrets

- `GITHUB_TOKEN`: Automatically provided by GitHub Actions
- `NPM_TOKEN`: Required for NPM publishing (get from npmjs.com)

## Archived Workflows

Old workflows are archived in `.github/workflows/archive-2024-08/` for reference.
These were replaced due to:
- Overcomplexity (2,187 lines of YAML)
- Custom SARIF parsing that broke with format changes
- Duplicate logic across multiple workflows
- Manual security gating that failed silently