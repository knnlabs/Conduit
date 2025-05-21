# Legacy Code Removal Instructions

This document provides instructions for removing legacy direct database access code from the WebUI project, scheduled for October 2025.

## Overview

The direct database access mode in WebUI has been deprecated since May 2025 and is scheduled for removal in November 2025. This document outlines the steps to execute the removal.

## Pre-removal Checklist

Before executing the removal, ensure:

1. All users have been notified of the upcoming change
2. The migration guide has been published
3. The deprecation warnings have been in place for at least 3 months
4. All users have updated to the Admin API architecture

## Removal Instructions

### 1. Create Removal Branch

```bash
git checkout -b feature/remove-legacy-db-access
```

### 2. Run Automated Removal Script

The `remove-deprecated-code.sh` script will automatically remove all deprecated code and make necessary adjustments to the codebase:

```bash
./remove-deprecated-code.sh
```

The script performs the following actions:

- Removes all deprecated service implementations
- Removes DbContext registration extensions
- Removes deprecation warning components
- Updates Program.cs to remove conditional logic
- Updates AdminApiOptions to remove legacy mode
- Updates background services to remove conditional logic
- Removes Entity Framework dependencies
- Creates backups of all modified files

### 3. Update Docker Compose File

Replace the existing docker-compose.yml with the version without legacy environment variables:

```bash
cp docker-compose.yml.removed-legacy docker-compose.yml
```

### 4. Build and Test

```bash
dotnet build
dotnet test
```

### 5. Manual Verification

After the automated removal, perform these manual verification steps:

- Verify the WebUI builds successfully without database dependencies
- Check that all adapters are properly registered
- Verify that background services work correctly
- Test all functionality in the WebUI

### 6. Update Documentation

Update the following documentation files:

- README.md - Remove all references to legacy mode
- docs/Environment-Variables.md - Remove deprecated variables
- docs/Getting-Started.md - Update setup instructions

### 7. Create Pull Request

Create a pull request for review with a detailed description of the changes.

## Troubleshooting

If you encounter issues during the removal process:

1. Check the backups created by the removal script in the `deprecated-code-backup-*` directory
2. Review the error messages for specific files or dependencies that might be missing
3. Check for any remaining references to direct database access

## Rollback Procedure

If necessary, you can roll back the changes:

```bash
git checkout master
git branch -D feature/remove-legacy-db-access
```

## Post-Removal Tasks

After the removal is complete:

1. Tag the release as v2025.11.0
2. Publish updated documentation
3. Announce the release to users
4. Monitor for any issues reported by users

## Timeline

- **October 1, 2025**: Create removal branch
- **October 1-10, 2025**: Execute removal and testing
- **October 15, 2025**: Create pull request
- **October 25, 2025**: Review and finalize
- **November 1, 2025**: Release v2025.11.0