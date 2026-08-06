[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
)

$ErrorActionPreference = "Stop"

function Test-IsPureLogAndRethrow {
    param(
        [Microsoft.CodeAnalysis.CSharp.Syntax.CatchClauseSyntax]$CatchClause
    )

    if ($CatchClause.Block.Statements.Count -ne 2) {
        return $false
    }

    $logStatement = $CatchClause.Block.Statements[0]
    $throwStatement = $CatchClause.Block.Statements[1]
    if ($logStatement -isnot [Microsoft.CodeAnalysis.CSharp.Syntax.ExpressionStatementSyntax] -or
        $logStatement.Expression -isnot [Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax] -or
        $logStatement.Expression.Expression -isnot [Microsoft.CodeAnalysis.CSharp.Syntax.MemberAccessExpressionSyntax] -or
        $throwStatement -isnot [Microsoft.CodeAnalysis.CSharp.Syntax.ThrowStatementSyntax] -or
        $null -ne $throwStatement.Expression) {
        return $false
    }

    $methodName = $logStatement.Expression.Expression.Name.Identifier.ValueText
    return $methodName -in @(
        "LogCritical",
        "LogError",
        "LogErrorSecure",
        "LogWarning",
        "LogInformation",
        "LogDebug"
    )
}

$sourceFiles = & git -C $RepositoryRoot ls-files -- "*.cs"
if ($LASTEXITCODE -ne 0) {
    throw "Unable to enumerate tracked C# source files."
}

$violations = foreach ($relativePath in $sourceFiles) {
    $fullPath = Join-Path $RepositoryRoot $relativePath
    if (-not (Test-Path -LiteralPath $fullPath)) {
        continue
    }

    $source = [IO.File]::ReadAllText($fullPath)
    $root = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText($source).GetRoot()
    foreach ($node in $root.DescendantNodes()) {
        if ($node -is [Microsoft.CodeAnalysis.CSharp.Syntax.CatchClauseSyntax] -and
            (Test-IsPureLogAndRethrow -CatchClause $node)) {
            $line = $node.GetLocation().GetLineSpan().StartLinePosition.Line + 1
            "${relativePath}:${line}"
        }
    }
}

if ($violations.Count -gt 0) {
    Write-Error @"
Pure catch-log-rethrow wrappers are not allowed. Log exceptions at the boundary
that decides the response, retry, dead-letter, degradation, or shutdown outcome.
See docs/decisions/0005-exception-logging-ownership.md.

$($violations -join [Environment]::NewLine)
"@
}

Write-Host "Exception logging ownership guard passed."
