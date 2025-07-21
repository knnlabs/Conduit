# Mutation Testing with Stryker.NET

This guide explains how to use Stryker.NET for mutation testing in the ConduitLLM project.

## Installation

Install Stryker.NET as a global tool:

```bash
dotnet tool install -g dotnet-stryker
```

## Running Mutation Tests

### Quick Start

From the test project directory (`ConduitLLM.Tests`):

```bash
# Test a specific file
dotnet-stryker --project "ConduitLLM.Core.csproj" --mutate "**/CostCalculationService.cs"

# Test with specific configuration
dotnet-stryker --config-file stryker-simple.json
```

### Configuration Files

We have several configuration files for different testing scenarios:

1. **stryker-simple.json** - For testing individual services
2. **stryker-config.json** - For full project mutation testing

### Command Line Options

Common options:
- `--project` - Specify which project to mutate
- `--mutate` - Glob pattern for files to mutate
- `--mutation-level` - Basic, Standard, Advanced, or Complete
- `--reporter` - Progress, Html, ClearText, etc.
- `--threshold-break` - Fail if mutation score is below this

## Understanding Results

### Mutation Score

The mutation score indicates test quality:
- **90-100%**: Excellent test coverage and quality
- **80-90%**: Good test coverage
- **70-80%**: Acceptable, but room for improvement
- **< 70%**: Tests need significant improvement

### Types of Mutations

Stryker.NET applies various mutations:

1. **Arithmetic Operators**: `+` → `-`, `*` → `/`
2. **Comparison Operators**: `>` → `>=`, `==` → `!=`
3. **Boolean Literals**: `true` → `false`
4. **Conditional Operators**: `&&` → `||`
5. **String Literals**: `"value"` → `""`
6. **Method Calls**: Removes method calls
7. **Return Values**: Changes return values

### Reading Reports

After running, check the HTML report:
```bash
# Reports are generated in:
StrykerOutput/<timestamp>/reports/mutation-report.html
```

The report shows:
- **Killed Mutations**: Tests caught the change (good)
- **Survived Mutations**: Tests missed the change (needs improvement)
- **Timeout**: Tests took too long (possible infinite loop)
- **No Coverage**: Code not covered by tests

## Best Practices

### 1. Start Small
Test individual files or services first:
```bash
dotnet-stryker --mutate "**/LoggingSanitizer.cs"
```

### 2. Focus on Critical Code
Prioritize mutation testing for:
- Business logic services
- Security-related code
- Cost calculation services
- Data validation logic

### 3. Incremental Improvement
- Run mutation tests regularly
- Fix survived mutations incrementally
- Add tests for uncovered scenarios

### 4. Performance Optimization
- Use `--concurrency` to control parallel execution
- Use `coverage-analysis: "perTest"` for faster runs
- Exclude generated code and migrations

## Example Configurations

### Testing a Single Service

```json
{
  "stryker-config": {
    "project": "ConduitLLM.Core.csproj",
    "mutate": ["**/CostCalculationService.cs"],
    "mutation-level": "Complete",
    "thresholds": {
      "high": 90,
      "low": 80,
      "break": 70
    }
  }
}
```

### Testing Security Code

```json
{
  "stryker-config": {
    "project": "ConduitLLM.Core.csproj",
    "mutate": [
      "**/LoggingSanitizer.cs",
      "**/AudioEncryptionService.cs",
      "**/CorrelationContext.cs"
    ],
    "mutation-level": "Complete",
    "thresholds": {
      "high": 95,
      "low": 90,
      "break": 85
    }
  }
}
```

## Troubleshooting

### Common Issues

1. **"No project references found"**
   - Run from the test project directory
   - Ensure project references are correct

2. **"Test project contains more than one project reference"**
   - Specify the project to mutate with `--project`

3. **Timeouts**
   - Reduce concurrency with `--concurrency 2`
   - Use `--mutate` to test smaller portions

4. **Out of Memory**
   - Test smaller files or directories
   - Close other applications
   - Use `--concurrency 1`

### Performance Tips

1. **Use Coverage Analysis**
   ```json
   "coverage-analysis": "perTest"
   ```

2. **Exclude Unnecessary Files**
   ```json
   "mutate": [
     "!**/Program.cs",
     "!**/Migrations/**",
     "!**/*Extensions.cs"
   ]
   ```

3. **Run in CI/CD**
   - Use `--reporter Json` for machine-readable output
   - Set appropriate break thresholds
   - Cache mutation testing results

## Integration with CI/CD

Add to your build pipeline:

```yaml
- name: Run Mutation Tests
  run: |
    dotnet tool install -g dotnet-stryker
    cd ConduitLLM.Tests
    dotnet-stryker --config-file stryker-ci.json --reporter Json --reporter Html
    
- name: Upload Mutation Report
  uses: actions/upload-artifact@v3
  with:
    name: mutation-report
    path: '**/mutation-report.html'
```

## Current Status

As of the latest run, mutation testing has been set up for:
- LoggingSanitizer (security-critical)
- CostCalculationService (business-critical)
- AudioCostCalculationService (business-critical)

Next targets for mutation testing:
- CorrelationContext
- CancellableTaskRegistry
- AudioEncryptionService