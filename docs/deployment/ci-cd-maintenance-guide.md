# CI/CD Maintenance Guide

## Overview

This guide explains the simplified CI/CD approach for the Conduit project, designed to minimize maintenance and provide clear feedback.

## Key Principles

1. **Tests Block, Coverage Informs**: Only test failures block the build. Coverage is informational.
2. **Clear Error Messages**: When something fails, the reason is immediately obvious.
3. **Minimal Logs**: Show only what's necessary in the main output.
4. **Robust Parsing**: Use proper tools instead of fragile text parsing.

## The New Approach

### 1. Simplified Build Script (`scripts/ci-build-test.sh`)

This wrapper script handles the entire build and test process:

- **Builds** the solution
- **Runs** tests with minimal verbosity
- **Generates** coverage reports
- **Creates** clear summaries in GitHub UI
- **Exits** with proper status codes

**Key Features:**
- Captures test results from dotnet test output (reliable)
- Generates coverage without blocking on thresholds
- Provides visual indicators for coverage levels
- Handles errors gracefully

### 2. Informational Coverage (`scripts/check-coverage-info.sh`)

This script provides coverage insights without blocking:

- Shows coverage percentages with friendly feedback
- Suggests improvements without failing the build
- Handles missing data gracefully
- Always exits successfully

### 3. Simplified Workflow

The workflow now:
- Uses the wrapper script for all build/test operations
- Uploads artifacts only when needed (on failure or manual trigger)
- Provides clear summaries in GitHub's UI
- Minimizes console output

## Path Handling

All paths in the scripts use relative paths from the repository root:
- `./TestResults/` - Test output directory
- `./CoverageReport/` - Coverage report directory
- `./scripts/` - Script directory

This is correct for GitHub Actions, which sets the working directory to the repository root.

## Test Result Parsing

Instead of parsing XML with grep (fragile), we now:
1. Use dotnet test's console output for basic counts
2. Generate proper reports as artifacts for detailed analysis
3. Focus on pass/fail status rather than exact counts

## Coverage Thresholds

Coverage thresholds are now:
- **Informational only** - they don't block builds
- **Visual indicators** show coverage health:
  - 🟢 80%+ = Excellent
  - 🟡 60-79% = Good
  - 🟠 40-59% = Fair
  - 🔴 <40% = Needs improvement

## Debugging Failed Builds

When a build fails:

1. **Check the Summary** - GitHub UI shows a clear summary
2. **Look for Red Text** - Errors are highlighted
3. **Download Artifacts** - Detailed logs are available as artifacts
4. **Run Locally** - Use `./scripts/ci-build-test.sh` to reproduce

## Making Changes

### To Add New Tests
Just add them - the scripts will automatically include them.

### To Change Coverage Thresholds
Edit `scripts/ci-build-test.sh` and update these variables:
```bash
COVERAGE_THRESHOLD_WARNING=40  # Warn if below this
COVERAGE_THRESHOLD_INFO=60     # Info if below this
```

### To Change Build Configuration
Set the `BUILD_CONFIG` environment variable or edit the default in the script.

### To Add More Detail to Logs
Change verbosity in the wrapper script:
```bash
--logger "console;verbosity=minimal"  # Change to normal or detailed
```

## Maintenance Checklist

✅ **Scripts use relative paths** - No absolute path issues
✅ **Coverage is informational** - Won't block development
✅ **Test parsing is robust** - Uses console output, not XML parsing
✅ **Errors are clear** - Each failure has a descriptive message
✅ **Artifacts are available** - Detailed logs for debugging

## Integration with Existing Workflow

To integrate these changes into your existing workflow, replace the build-and-test job with the simplified version in `build-and-test-simplified.yml`.

## Future Improvements

1. **Trend Tracking**: Store coverage history to show trends
2. **Performance Metrics**: Track build/test execution time
3. **Selective Testing**: Only run affected tests on PRs
4. **Better Caching**: Cache test results for unchanged code

## Troubleshooting

### "No coverage data found"
- Ensure tests are actually running
- Check that `.runsettings` file exists
- Verify coverage collector is installed

### "Tests passed but build failed"
- Check for post-test steps that might fail
- Look for compilation warnings treated as errors
- Verify all required tools are installed

### "Can't reproduce locally"
- Ensure you're using the same .NET version
- Run `dotnet tool restore` first
- Check environment variables

## Questions?

If you encounter issues not covered here, the scripts are designed to be self-documenting. Read the comments in:
- `scripts/ci-build-test.sh`
- `scripts/check-coverage-info.sh`