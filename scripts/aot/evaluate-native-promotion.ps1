[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('admin', 'gateway')]
    [string] $Service,

    [Parameter(Mandatory)]
    [string] $BenchmarkPath,

    [Parameter(Mandatory)]
    [string] $CanaryEvidencePath,

    [string] $PolicyPath = 'deploy/native-canary/promotion-policy.json',

    [switch] $ReportOnly
)

$ErrorActionPreference = 'Stop'
$policy = Get-Content -LiteralPath $PolicyPath -Raw | ConvertFrom-Json
$benchmark = Get-Content -LiteralPath $BenchmarkPath -Raw | ConvertFrom-Json
$evidence = Get-Content -LiteralPath $CanaryEvidencePath -Raw | ConvertFrom-Json
$failures = [System.Collections.Generic.List[string]]::new()

if ($policy.status -ne 'open') {
    foreach ($blocker in $policy.blockedBy) { $failures.Add("promotion blocked: $blocker") }
}
if ($benchmark.service -ne $Service) { $failures.Add('benchmark service does not match the requested promotion') }
if ($evidence.service -ne $Service) { $failures.Add('canary evidence service does not match the requested promotion') }
if ($evidence.soakHours -lt $policy.minimumSoakHours.$Service) { $failures.Add('minimum soak duration was not met') }
if ($evidence.healthyWindows -lt $policy.requiredConsecutiveHealthyWindows) { $failures.Add('required consecutive healthy windows were not met') }
if ($evidence.errorRate -gt $policy.thresholds.maximumErrorRate) { $failures.Add('canary error rate exceeds the threshold') }
if (-not $evidence.fullFeatureMatrixPassed) { $failures.Add('full supported feature matrix did not pass') }
if (-not $evidence.automaticRollbackTested) { $failures.Add('automatic rollback was not tested') }
if (-not $evidence.manualRollbackTested) { $failures.Add('manual rollback was not tested') }

$comparison = $benchmark.comparison
if ($comparison.imageSizeReductionPercent -lt $policy.thresholds.minimumImageSizeReductionPercent) { $failures.Add('image-size improvement is below threshold') }
if ($comparison.coldStartReductionPercent -lt $policy.thresholds.minimumColdStartReductionPercent) { $failures.Add('cold-start improvement is below threshold') }
if ($comparison.idleMemoryReductionPercent -lt $policy.thresholds.minimumIdleMemoryReductionPercent) { $failures.Add('idle-memory improvement is below threshold') }
if ($comparison.p99LatencyRegressionPercent -gt $policy.thresholds.maximumP99LatencyRegressionPercent) { $failures.Add('p99 latency regression exceeds threshold') }
if ($comparison.throughputImprovementPercent -lt $policy.thresholds.minimumThroughputImprovementPercent) { $failures.Add('throughput improvement is below threshold') }

$decision = if ($failures.Count -eq 0) { 'promote' } else { 'hold' }
[pscustomobject]@{
    service = $Service
    decision = $decision
    evaluatedAt = [DateTimeOffset]::UtcNow.ToString('O')
    failures = @($failures)
} | ConvertTo-Json -Depth 5

if ($decision -ne 'promote' -and -not $ReportOnly) { exit 1 }
