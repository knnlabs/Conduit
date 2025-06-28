# Conduit TUI Launcher Script for Windows
# This script provides a convenient way to launch the Conduit TUI application on Windows

# Get the script's directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptDir

# Check if the TUI project exists
if (-not (Test-Path "ConduitLLM.TUI")) {
    Write-Host "Error: ConduitLLM.TUI directory not found!" -ForegroundColor Red
    Write-Host "Please ensure you're running this script from the Conduit repository root."
    exit 1
}

# Check if .NET is installed
try {
    $null = dotnet --version
} catch {
    Write-Host "Error: .NET SDK is not installed or not in PATH" -ForegroundColor Red
    Write-Host "Please install .NET 9.0 or later from https://dotnet.microsoft.com/download"
    exit 1
}

# Check if we need to build
$buildFlag = $false
if ($args.Count -gt 0 -and $args[0] -eq "--build") {
    $buildFlag = $true
}

# Build the project if it hasn't been built yet or if --build flag is passed
$dllPath = "ConduitLLM.TUI\bin\Debug\net9.0\conduit-tui.dll"
if ($buildFlag -or -not (Test-Path $dllPath)) {
    Write-Host "Building Conduit TUI..." -ForegroundColor Cyan
    dotnet build ConduitLLM.TUI\ConduitLLM.TUI.csproj -c Debug
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed!" -ForegroundColor Red
        exit 1
    }
    # If --build was the first argument, remove it before passing args to the app
    if ($buildFlag) {
        $args = $args[1..($args.Length - 1)]
    }
}

# Run the TUI application with all passed arguments
dotnet run --project ConduitLLM.TUI\ConduitLLM.TUI.csproj --no-build -- $args