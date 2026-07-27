param(
    [switch]$NoRestore
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "../..")
$project = Join-Path $repoRoot "Services/ConduitLLM.Admin/ConduitLLM.Admin.csproj"
$generatedRoot = Join-Path `
    $repoRoot `
    "artifacts/conduit-admin-rdg-$PID-$([Guid]::NewGuid().ToString('N'))"

try {
    $arguments = @(
        "build"
        $project
        "--configuration", "Release"
        "-t:Rebuild"
        "-p:EmitCompilerGeneratedFiles=true"
        "-p:CompilerGeneratedFilesOutputPath=$generatedRoot"
        "-p:UseSharedCompilation=false"
    )
    if ($NoRestore) {
        $arguments += "--no-restore"
    }

    & dotnet @arguments
    $buildExitCode = $LASTEXITCODE

    if ($buildExitCode -ne 0) {
        throw "Admin Release build failed with exit code $buildExitCode."
    }

    $routeSources = @(
        Get-ChildItem `
            -Path $generatedRoot `
            -Recurse `
            -Filter "GeneratedRouteBuilderExtensions.g.cs" `
            -File
    )
    if ($routeSources.Count -eq 0) {
        throw "RequestDelegateGenerator did not emit GeneratedRouteBuilderExtensions.g.cs."
    }

    $routeSource = ($routeSources | ForEach-Object { Get-Content $_.FullName -Raw }) `
        -join [Environment]::NewLine
    $mediaHandlers = [Regex]::Matches(
        $routeSource,
        "var handler = Cast\(del,[^\r\n]*MediaEndpoints\.MediaEndpointLog")

    if ($mediaHandlers.Count -ne 9) {
        throw "Expected 9 generated media handlers using MediaEndpointLog; found $($mediaHandlers.Count)."
    }

    Write-Host "Admin RDG guard passed: all 9 affected media handlers were generated at compile time."
}
finally {
    if (Test-Path -LiteralPath $generatedRoot) {
        Remove-Item -LiteralPath $generatedRoot -Recurse -Force
    }
}
