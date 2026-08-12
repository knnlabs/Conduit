param(
    [string]$ArtifactDirectory,
    [switch]$NoRestore
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "../..")
if ([string]::IsNullOrWhiteSpace($ArtifactDirectory)) {
    $ArtifactDirectory = Join-Path $repoRoot "artifacts/native-aot"
}
$artifactRoot = [IO.Path]::GetFullPath($ArtifactDirectory)
$runtimeRoot = Join-Path $artifactRoot "runtime"
$symbolsRoot = Join-Path $artifactRoot "symbols"
$reportsRoot = Join-Path $artifactRoot "reports"
New-Item -ItemType Directory -Force -Path $runtimeRoot, $symbolsRoot, $reportsRoot | Out-Null

$metrics = [Collections.Generic.List[object]]::new()
foreach ($service in @("Admin", "Gateway")) {
    $serviceSlug = $service.ToLowerInvariant()
    $project = Join-Path $repoRoot "Services/ConduitLLM.$service/ConduitLLM.$service.csproj"
    $publishDirectory = Join-Path $runtimeRoot $serviceSlug
    $symbolDirectory = Join-Path $symbolsRoot $serviceSlug
    $logPath = Join-Path $reportsRoot "$serviceSlug-publish.log"
    New-Item -ItemType Directory -Force -Path $publishDirectory, $symbolDirectory | Out-Null

    $arguments = @(
        "publish"
        $project
        "--configuration", "Release"
        "--runtime", "linux-x64"
        "--self-contained", "true"
        "--output", $publishDirectory
        "--nologo"
        "--tl:off"
        "--verbosity", "minimal"
        "-p:PublishAot=true"
        "-p:ConduitAotAudit=true"
        "-p:StripSymbols=true"
        "-p:UseSharedCompilation=false"
    )
    if ($NoRestore) {
        $arguments += "--no-restore"
    }

    Write-Host "Publishing ConduitLLM.$service for linux-x64 NativeAOT..."
    $stopwatch = [Diagnostics.Stopwatch]::StartNew()
    & dotnet @arguments 2>&1 | Tee-Object -FilePath $logPath
    $publishExitCode = $LASTEXITCODE
    $stopwatch.Stop()
    if ($publishExitCode -ne 0) {
        throw "$service NativeAOT publish failed with exit code $publishExitCode."
    }

    Get-ChildItem -LiteralPath $publishDirectory -File |
        Where-Object { $_.Extension -in @(".dbg", ".pdb") } |
        Move-Item -Destination $symbolDirectory

    $executable = Join-Path $publishDirectory "ConduitLLM.$service"
    if (!(Test-Path -LiteralPath $executable -PathType Leaf)) {
        throw "$service native executable was not produced: $executable"
    }

    $runtimeBytes = (
        Get-ChildItem -LiteralPath $publishDirectory -Recurse -File |
            Measure-Object -Property Length -Sum
    ).Sum
    $metrics.Add([ordered]@{
        service = $service
        rid = "linux-x64"
        publishDurationMilliseconds = $stopwatch.ElapsedMilliseconds
        executableBytes = (Get-Item -LiteralPath $executable).Length
        runtimeArtifactBytes = [long]$runtimeBytes
    })
}

[ordered]@{
    schemaVersion = 1
    generatedAtUtc = [DateTime]::UtcNow.ToString("O")
    services = $metrics
} | ConvertTo-Json -Depth 5 |
    Set-Content -LiteralPath (Join-Path $reportsRoot "native-baselines.json") -Encoding utf8

Write-Host "Native runtime artifacts: $runtimeRoot"
Write-Host "Native symbols: $symbolsRoot"
