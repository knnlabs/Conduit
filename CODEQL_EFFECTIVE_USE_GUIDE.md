# CodeQL Effective Use Guide

## Key Learnings from 8 Hours of Pain

### 1. Understanding GitHub CodeQL Reports

When GitHub shows:
- **Security Alerts: 7 high** - These are ACTUAL security vulnerabilities
- **Other Alerts: 8 errors, 231 warnings, 768 notes** - These are CODE QUALITY issues, NOT security issues

The "errors" and "warnings" are often:
- Catch of generic exceptions (741 instances in our case!)
- Disposable objects not disposed
- Unused assignments
- Code quality issues

### 2. Running CodeQL Locally

#### Quick Security Check
```bash
# Create database
.codeql/codeql/codeql database create codeql-db --language=csharp

# Run ONLY security queries
.codeql/codeql/codeql database analyze codeql-db \
  --format=sarif-latest \
  --output=security.sarif \
  codeql/csharp-queries:codeql-suites/csharp-security-extended.qls
```

#### Full GitHub-Style Analysis (Security + Quality)
```bash
# This is what GitHub Actions runs with "queries: security-and-quality"
.codeql/codeql/codeql database analyze codeql-db \
  --format=sarif-latest \
  --output=all-alerts.sarif \
  codeql/csharp-queries:codeql-suites/csharp-security-and-quality.qls
```

### 3. Understanding Query Suites

- **csharp-security-extended.qls** - Only security vulnerabilities
- **csharp-security-and-quality.qls** - Security + code quality (what GitHub uses)
- **csharp-code-scanning.qls** - Basic security scanning

### 4. Checking Specific Vulnerabilities

```bash
# Check ONLY log injection
.codeql/codeql/codeql database analyze codeql-db \
  --format=csv \
  --output=log-injection.csv \
  ".codeql/codeql/qlpacks/codeql/csharp-queries/*/Security Features/CWE-117/LogForging.ql"
```

### 5. What CodeQL Recognizes for Log Injection

**WORKS:**
- `.Replace(Environment.NewLine, "")` - Inline replacement
- String interpolation with sanitized values

**DOESN'T WORK:**
- Custom sanitizer wrapper functions like `S(value)`
- Extension methods for sanitization

### 6. Reading Results

```bash
# Count alerts
cat results.sarif | jq '.runs[0].results | length'

# Group by type
cat results.sarif | jq '.runs[0].results[] | .ruleId' | sort | uniq -c | sort -nr
```

### 7. GitHub Actions vs Local

GitHub Actions may show MORE alerts because:
1. It runs on PR diffs, not just current code
2. It may include alerts from base branch
3. The UI aggregates multiple analyses

### 8. The Truth About Our Situation

- Log injection fixes: ✅ SUCCESSFUL (0 alerts)
- Actual security issues: 7 high severity (unrelated to logging)
- Code quality issues: 1000+ (mostly generic exception catching)

The massive alert count was NEVER about log injection - it was about code quality rules!

### 9. Time-Saving Tips

1. **Always check what query suite is being used** in GitHub workflows
2. **Run specific queries first** before running full suites
3. **Distinguish security from quality** - they're reported differently
4. **Don't assume all alerts are security issues**
5. **Use SARIF format** and jq for analysis, not CSV

### 10. For Log Injection Specifically

Just use inline `.Replace(Environment.NewLine, "")` on string parameters in log statements. Don't overcomplicate it with wrapper functions.