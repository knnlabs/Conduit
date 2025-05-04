# XML Documentation Coverage Checker

This tool scans the Conduit solution to identify classes and methods that lack proper XML documentation.

## Features

- Scans C# source files across the entire solution
- Identifies completely undocumented types
- Identifies partially documented types (with undocumented methods)
- Generates a comprehensive report with statistics and recommendations
- Prioritizes documentation efforts based on importance and visibility

## Usage

Run the documentation checker using the provided script:

```bash
./check-documentation.sh
```

## Report Format

The report provides:

- Project-by-project breakdown of documentation coverage
- Lists of undocumented and partially documented types
- Overall documentation coverage statistics
- Prioritized recommendations for documentation improvements

## How It Works

The checker uses a simple regex-based approach to analyze C# files:

1. Scans the solution for C# files, excluding test files and generated code
2. Checks each file for class/interface definitions
3. Looks for XML documentation comments (`/// <summary>`) before each type and method
4. Compiles statistics on undocumented and partially documented code
5. Generates a detailed report with recommendations

## Limitations

This is a simple, regex-based tool rather than a full compiler-based analysis:

- It may miss some complex method definitions
- It doesn't parse actual XML content quality or completeness
- It can't detect deprecated or internal APIs that might not need documentation

For a more thorough analysis, consider using a dedicated static analysis tool like
[StyleCop](https://github.com/DotNetAnalyzers/StyleCopAnalyzers) or 
[DocumentationAnalyzers](https://github.com/DotNetAnalyzers/DocumentationAnalyzers).

## Documentation Standards

Refer to the XML Documentation Standards section in CLAUDE.md for the project's
documentation guidelines and requirements.