# GitHub Actions Workflows

This repository uses a simplified, industry-standard CI/CD pipeline.

## Active Workflows

### 1. CI (`ci.yml`)
**Triggers:** Push to `master` or `dev`, Pull requests to `master`

**What it does:**
- Validates code builds and tests pass (.NET, SDK, WebAdmin lint/type-check)
- Builds all three Docker images for validation only — **never pushes** (this still
  catches Dockerfile / production-build breakage, notably WebAdmin's `next build`,
  which runs nowhere else in CI)

CI publishes nothing. Every Docker image and npm package is published from a `v*`
tag by the Release workflow.

### 2. Release (`release.yml`)
**Triggers:** Push of a tag matching `v*` (cut from `master`)

Two channels, decided by the tag name — a tag is a **pre-release** iff its name
contains a hyphen (SemVer rule):

| Tag | Channel | Docker | npm | GitHub Release |
|---|---|---|---|---|
| `v3.0.0` | stable | `:3.0.0` + `:latest` | `3.0.0` `@latest` | Latest |
| `v3.0.0-beta.1` | beta | `:3.0.0-beta.1` + `:beta` | `3.0.0-beta.1` `@beta` | Pre-release |

**What it does:**
- Creates a GitHub Release with auto-generated notes (pre-release for beta tags;
  only a stable tag becomes the repo's "Latest")
- Builds and pushes the three versioned Docker images plus the channel tag
  (`:latest` / `:beta`)
- Publishes the two npm packages — `@knn_labs/conduit-common` and
  `@knn_labs/conduit-gateway-client` — at the tag
  version on the channel dist-tag (`@latest` / `@beta`)

All three artifact types share one unified product version, driven by the tag.

### 3. CodeQL (`codeql-analysis.yml`)
**Triggers:** Push to `master` or `dev`, Weekly schedule, Manual dispatch

**What it does:**
- Scans for security vulnerabilities
- Results appear in Security tab
- Non-blocking, informational only

## Release Process

Releases are cut from `master` by pushing a version tag. The whole product — .NET /
Docker and all three npm packages — shares one version, so bump them together.

1. **Bump the version** in `Directory.Build.props` and in the three SDK
   `package.json` files (`SDKs/Node/{Common,Admin,Gateway}`) to the same number,
   commit, and merge to `master`.
2. **Stable release:**
   ```bash
   git tag v3.0.0
   git push origin v3.0.0
   ```
   Publishes Docker `:3.0.0` + `:latest` and npm `3.0.0` `@latest`.
3. **Beta release** — any pre-release suffix (a `-…`):
   ```bash
   git tag v3.0.0-beta.1
   git push origin v3.0.0-beta.1
   ```
   Publishes Docker `:3.0.0-beta.1` + `:beta` and npm `3.0.0-beta.1` `@beta`.
   A beta never moves `:latest` / `@latest`.

## Artifact Locations

- **Docker Images:** https://github.com/users/nickna/packages
- **NPM Packages:** https://www.npmjs.com/~knn_labs
- **Security Results:** https://github.com/nickna/Conduit/security/code-scanning

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
