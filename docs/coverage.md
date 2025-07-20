# Code Coverage Dashboard and Metrics

This document explains the code coverage system implemented for Conduit LLM, including setup, usage, and metrics interpretation.

## 📊 Overview

Conduit uses comprehensive code coverage tracking to maintain code quality and ensure thorough testing of critical systems. Coverage is automatically collected during CI/CD and can be generated locally for development.

### Current Coverage Status

Coverage badges and current metrics are displayed in the main README.md. The system tracks:

- **Line Coverage**: Percentage of code lines executed during tests
- **Branch Coverage**: Percentage of code branches (if/else, switch cases) tested
- **Method Coverage**: Percentage of methods that are called during tests

## 🛠 Tools and Configuration

### Core Tools

1. **Coverlet**: Cross-platform .NET code coverage collector
   - Packages: `coverlet.collector`, `coverlet.msbuild`
   - Configuration: `coverlet.json`, `.runsettings`

2. **ReportGenerator**: Coverage report generator
   - Generates HTML, JSON, XML, and badge reports
   - Configured as local dotnet tool

3. **GitHub Actions**: Automated coverage collection and reporting
   - Runs on every PR and push to main branches
   - Enforces minimum coverage thresholds

### Configuration Files

#### `.runsettings`
Configures test execution and coverage collection:
- Specifies output formats (Cobertura, OpenCover, JSON)
- Defines exclusion patterns for generated code
- Enables source link support

#### `coverlet.json`
Coverlet-specific configuration:
- Assembly filters for inclusion/exclusion
- File-level exclusions
- Attribute-based exclusions
- Coverage thresholds

#### `.config/dotnet-tools.json`
Local tool manifest:
- ReportGenerator tool configuration
- Version pinning for reproducible builds

## 📈 Coverage Metrics

### Minimum Thresholds

| Category | Current Threshold | Target |
|----------|------------------|---------|
| **Overall** | 40% | 80% |
| **Core Services** | 40% | 80% |
| **HTTP API** | 40% | 75% |
| **Admin API** | 40% | 75% |

### Coverage Assessment

- 🟢 **Excellent** (≥80%): Production-ready coverage
- 🟡 **Good** (60-79%): Acceptable but improvement recommended
- 🟠 **Moderate** (40-59%): Below target, requires attention
- 🔴 **Low** (<40%): Critical gap, blocks PR merges

## 🚀 Usage

### Local Development

#### Quick Coverage Check
```bash
# Run tests with coverage and show summary
./scripts/coverage-dashboard.sh run
```

#### Generate Reports Only
```bash
# Generate reports from existing coverage data
./scripts/coverage-dashboard.sh report
```

#### View Summary
```bash
# Display coverage summary without running tests
./scripts/coverage-dashboard.sh summary
```

### Manual Coverage Collection

```bash
# Run tests with coverage
dotnet test --configuration Release \
  --collect:"XPlat Code Coverage" \
  --results-directory ./TestResults \
  --settings .runsettings

# Generate HTML reports
dotnet tool run reportgenerator \
  -reports:"./TestResults/**/coverage.cobertura.xml" \
  -targetdir:"./CoverageReport" \
  -reporttypes:"Html;HtmlSummary;Badges;JsonSummary"
```

### CI/CD Integration

Coverage is automatically collected in GitHub Actions:

1. **Test Execution**: Tests run with coverage collection
2. **Report Generation**: HTML and JSON reports created
3. **Threshold Enforcement**: PRs blocked if coverage drops below minimum
4. **Artifact Upload**: Reports uploaded with 30-day retention
5. **Summary Display**: Coverage metrics shown in PR summaries

## 📋 Reports and Artifacts

### Generated Reports

| Report Type | File | Purpose |
|-------------|------|---------|
| **HTML Report** | `CoverageReport/index.html` | Interactive coverage browser |
| **JSON Summary** | `CoverageReport/Summary.json` | Machine-readable metrics |
| **Badges** | `CoverageReport/badge_*.svg` | Coverage badges for README |
| **Text Summary** | `CoverageReport/Summary.txt` | Console-friendly summary |

### Accessing Reports

#### In GitHub Actions
1. Go to the Actions tab
2. Select a workflow run
3. Download the "coverage-report" artifact
4. Extract and open `index.html`

#### Locally
```bash
# Generate and open HTML report
./scripts/coverage-dashboard.sh run
# Report opens automatically in browser
```

## 🎯 Coverage Improvement Guide

### Identifying Gaps

1. **Review HTML Report**: Shows uncovered lines in red
2. **Check Critical Services**: Focus on Core, HTTP, and Admin modules
3. **Analyze Branch Coverage**: Ensure all code paths are tested
4. **Method Coverage**: Verify all public methods are called

### Adding Effective Tests

#### Unit Tests
- Test individual methods and classes
- Mock external dependencies
- Cover edge cases and error conditions
- Focus on business logic

#### Integration Tests
- Test service interactions
- Verify API endpoints
- Test database operations
- Cover authentication and authorization

#### Test Patterns
```csharp
[Fact]
public async Task MethodName_Condition_ExpectedResult()
{
    // Arrange
    var service = new ServiceUnderTest(mockDependency.Object);
    
    // Act
    var result = await service.MethodAsync(input);
    
    // Assert
    Assert.Equal(expectedValue, result);
    mockDependency.Verify(x => x.ExpectedCall(), Times.Once);
}
```

### Coverage Exclusions

The following are automatically excluded from coverage:
- Migration files (`**/Migrations/**`)
- Program.cs and Startup.cs
- Test files (`**/*Test*.cs`, `**/*Tests.cs`)
- Generated code (marked with appropriate attributes)
- View/Page files for web applications

## 🔧 Troubleshooting

### Common Issues

#### No Coverage Files Generated
```bash
# Check test output directory
ls -la ./TestResults/

# Verify .runsettings is being used
dotnet test --settings .runsettings --collect:"XPlat Code Coverage" --verbosity normal
```

#### ReportGenerator Not Found
```bash
# Restore local tools
dotnet tool restore

# Verify tool installation
dotnet tool list
```

#### Coverage Appears Low
1. Check exclusion patterns in `coverlet.json`
2. Verify test projects are running
3. Ensure assemblies are included in coverage collection
4. Review assembly filters in ReportGenerator command

### GitHub Actions Issues

#### Coverage Threshold Failures
- Review the coverage summary in the failed action
- Add tests for uncovered critical code
- Consider adjusting thresholds if temporary

#### Missing Coverage Reports
- Check if tests are passing
- Verify ReportGenerator tool restoration
- Check artifact upload status

## 📅 Coverage History and Trends

### Tracking Improvements

Coverage history is maintained in:
- GitHub Actions summaries (per-run metrics)
- ReportGenerator history directory (local trends)
- Weekly coverage reports (planned)

### Coverage Goals

| Timeline | Line Coverage Target |
|----------|----------------------|
| **Q1 2024** | 50% |
| **Q2 2024** | 65% |
| **Q3 2024** | 80% |

### Priority Areas

1. **ConduitLLM.Core**: Business logic and service layer
2. **ConduitLLM.Http**: API endpoints and controllers
3. **ConduitLLM.Admin**: Administrative functions
4. **Security Services**: Authentication and authorization
5. **Financial Services**: Billing and spend tracking

## 🤝 Contributing

### Before Submitting PRs

1. Run local coverage: `./scripts/coverage-dashboard.sh run`
2. Ensure coverage meets minimum thresholds
3. Add tests for new functionality
4. Review coverage report for your changes

### Writing Good Tests

- **Test Behavior, Not Implementation**: Focus on what the code does, not how
- **Use Descriptive Names**: Test names should explain the scenario
- **One Assertion Per Test**: Keep tests focused and clear
- **Cover Edge Cases**: Test boundary conditions and error scenarios
- **Mock External Dependencies**: Isolate units under test

## 📚 References

- [Coverlet Documentation](https://github.com/coverlet-coverage/coverlet)
- [ReportGenerator Documentation](https://danielpalme.github.io/ReportGenerator/)
- [.NET Testing Best Practices](https://docs.microsoft.com/en-us/dotnet/core/testing/best-practices)
- [GitHub Actions Documentation](https://docs.github.com/en/actions)