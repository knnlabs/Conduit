#Requires -Version 7.0
<#
.SYNOPSIS
    Common utility functions for Conduit development scripts.

.DESCRIPTION
    This module provides shared functionality for PowerShell development scripts including:
    - Color-coded logging
    - Cross-platform utilities
    - Docker helpers
    - Environment variable handling
#>

# Strict mode for better error handling
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

#region Logging Functions

function Write-Info {
    <#
    .SYNOPSIS
        Write an informational message with green prefix.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position = 0, ValueFromRemainingArguments)]
        [string[]]$Message
    )
    Write-Host "[INFO] " -ForegroundColor Green -NoNewline
    Write-Host ($Message -join ' ')
}

function Write-Success {
    <#
    .SYNOPSIS
        Write a success message with green checkmark.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position = 0, ValueFromRemainingArguments)]
        [string[]]$Message
    )
    Write-Host "OK " -ForegroundColor Green -NoNewline
    Write-Host ($Message -join ' ')
}

function Write-Warn {
    <#
    .SYNOPSIS
        Write a warning message with yellow prefix.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position = 0, ValueFromRemainingArguments)]
        [string[]]$Message
    )
    Write-Host "[WARN] " -ForegroundColor Yellow -NoNewline
    Write-Host ($Message -join ' ')
}

function Write-Err {
    <#
    .SYNOPSIS
        Write an error message with red prefix.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position = 0, ValueFromRemainingArguments)]
        [string[]]$Message
    )
    Write-Host "[ERROR] " -ForegroundColor Red -NoNewline
    Write-Host ($Message -join ' ')
}

function Write-Task {
    <#
    .SYNOPSIS
        Write a task message with cyan prefix.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position = 0, ValueFromRemainingArguments)]
        [string[]]$Message
    )
    Write-Host "[TASK] " -ForegroundColor Cyan -NoNewline
    Write-Host ($Message -join ' ')
}

function Write-Stats {
    <#
    .SYNOPSIS
        Write a statistics message with cyan prefix.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position = 0, ValueFromRemainingArguments)]
        [string[]]$Message
    )
    Write-Host "[STATS] " -ForegroundColor Cyan -NoNewline
    Write-Host ($Message -join ' ')
}

function Write-SectionHeader {
    <#
    .SYNOPSIS
        Write a boxed section header.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Title
    )

    $headerLength = $Title.Length
    $totalWidth = $headerLength + 6
    $border = [string]::new([char]0x2550, $totalWidth)  # ═
    $padding = "   "

    Write-Host ""
    Write-Host "$([char]0x2554)$border$([char]0x2557)"  # ╔...╗
    Write-Host "$([char]0x2551)$padding$Title$padding$([char]0x2551)"  # ║ Title ║
    Write-Host "$([char]0x255A)$border$([char]0x255D)"  # ╚...╝
}

#endregion

#region Cross-Platform Utilities

function Get-CrossPlatformTempPath {
    <#
    .SYNOPSIS
        Get the cross-platform temporary directory path.
    #>
    [CmdletBinding()]
    param()
    return [System.IO.Path]::GetTempPath()
}

function Get-ScriptDirectory {
    <#
    .SYNOPSIS
        Get the directory containing the calling script.
    #>
    [CmdletBinding()]
    param()

    # Try various methods to get the script path
    if ($PSScriptRoot) {
        return $PSScriptRoot
    }
    elseif ($MyInvocation.PSScriptRoot) {
        return $MyInvocation.PSScriptRoot
    }
    elseif ($MyInvocation.MyCommand.Path) {
        return Split-Path -Parent $MyInvocation.MyCommand.Path
    }
    else {
        return (Get-Location).Path
    }
}

function Get-ProjectRoot {
    <#
    .SYNOPSIS
        Get the Conduit project root directory.
    #>
    [CmdletBinding()]
    param(
        [Parameter()]
        [string]$FromPath
    )

    $searchPath = if ($FromPath) { $FromPath } else { Get-ScriptDirectory }

    # Walk up the directory tree looking for Conduit.sln
    $current = $searchPath
    while ($current -and -not (Test-Path (Join-Path $current "Conduit.sln"))) {
        $parent = Split-Path -Parent $current
        if ($parent -eq $current) {
            # Reached root without finding Conduit.sln
            throw "Could not find Conduit.sln in parent directories of $searchPath"
        }
        $current = $parent
    }

    return $current
}

function Test-IsWindows {
    <#
    .SYNOPSIS
        Check if running on Windows.
    #>
    [CmdletBinding()]
    param()
    return $IsWindows -or ($PSVersionTable.PSEdition -eq 'Desktop')
}

function Test-IsLinux {
    <#
    .SYNOPSIS
        Check if running on Linux.
    #>
    [CmdletBinding()]
    param()
    return $IsLinux
}

function Test-IsMacOS {
    <#
    .SYNOPSIS
        Check if running on macOS.
    #>
    [CmdletBinding()]
    param()
    return $IsMacOS
}

function Get-DockerUserIds {
    <#
    .SYNOPSIS
        Get user and group IDs for Docker volume mapping.
    .DESCRIPTION
        Returns a hashtable with UserId and GroupId.
        On Windows, returns 1000:1000 as a common default.
        On Linux/macOS, returns the actual user/group IDs.
    #>
    [CmdletBinding()]
    param()

    if (Test-IsWindows) {
        # Windows doesn't have Unix UIDs, use common defaults
        return @{
            UserId = 1000
            GroupId = 1000
        }
    }
    else {
        # Get actual Unix user/group IDs
        $userId = & id -u 2>$null
        $groupId = & id -g 2>$null

        return @{
            UserId = [int]$userId
            GroupId = [int]$groupId
        }
    }
}

#endregion

#region Environment Variable Utilities

function Import-DotEnv {
    <#
    .SYNOPSIS
        Load environment variables from a .env file.
    .DESCRIPTION
        Parses a .env file and sets environment variables for the current session.
        Supports comments (#) and basic KEY=VALUE format.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter()]
        [switch]$Export
    )

    if (-not (Test-Path $Path)) {
        throw "Environment file not found: $Path"
    }

    $content = Get-Content $Path -ErrorAction Stop

    foreach ($line in $content) {
        # Skip empty lines and comments
        $trimmedLine = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmedLine) -or $trimmedLine.StartsWith('#')) {
            continue
        }

        # Parse KEY=VALUE
        $equalIndex = $trimmedLine.IndexOf('=')
        if ($equalIndex -gt 0) {
            $key = $trimmedLine.Substring(0, $equalIndex).Trim()
            $value = $trimmedLine.Substring($equalIndex + 1).Trim()

            # Remove surrounding quotes if present
            if (($value.StartsWith('"') -and $value.EndsWith('"')) -or
                ($value.StartsWith("'") -and $value.EndsWith("'"))) {
                $value = $value.Substring(1, $value.Length - 2)
            }

            # Set environment variable
            [Environment]::SetEnvironmentVariable($key, $value, [EnvironmentVariableTarget]::Process)
        }
    }
}

function Get-RequiredEnvVar {
    <#
    .SYNOPSIS
        Get a required environment variable or throw an error.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter()]
        [string]$ErrorMessage
    )

    $value = [Environment]::GetEnvironmentVariable($Name)

    if ([string]::IsNullOrWhiteSpace($value)) {
        $msg = if ($ErrorMessage) { $ErrorMessage } else { "Required environment variable '$Name' is not set" }
        throw $msg
    }

    return $value
}

#endregion

#region Docker Utilities

function Test-DockerRunning {
    <#
    .SYNOPSIS
        Check if Docker daemon is running.
    #>
    [CmdletBinding()]
    param()

    try {
        $null = docker info 2>&1
        return $LASTEXITCODE -eq 0
    }
    catch {
        return $false
    }
}

function Get-DockerComposeCommand {
    <#
    .SYNOPSIS
        Get the appropriate docker compose command.
    .DESCRIPTION
        Returns 'docker compose' (v2) or 'docker-compose' (v1) depending on what's available.
    #>
    [CmdletBinding()]
    param()

    # Try docker compose (v2) first
    try {
        $null = docker compose version 2>&1
        if ($LASTEXITCODE -eq 0) {
            return 'docker compose'
        }
    }
    catch { }

    # Fall back to docker-compose (v1)
    try {
        $null = docker-compose version 2>&1
        if ($LASTEXITCODE -eq 0) {
            return 'docker-compose'
        }
    }
    catch { }

    throw "Neither 'docker compose' nor 'docker-compose' is available"
}

function Invoke-DockerCompose {
    <#
    .SYNOPSIS
        Execute a docker compose command with proper file references.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments,

        [Parameter()]
        [string]$WorkingDirectory,

        [Parameter()]
        [switch]$UseDev
    )

    $composeCmd = Get-DockerComposeCommand

    $composeFiles = @('-f', 'docker-compose.yml')
    if ($UseDev) {
        $composeFiles += @('-f', 'docker-compose.dev.yml')
    }

    $allArgs = $composeFiles + $Arguments

    $startInfo = @{
        FilePath = 'docker'
        ArgumentList = @('compose') + $allArgs
        NoNewWindow = $true
        Wait = $true
    }

    if ($WorkingDirectory) {
        $startInfo.WorkingDirectory = $WorkingDirectory
    }

    # Execute using native command for better output handling
    if ($composeCmd -eq 'docker compose') {
        if ($WorkingDirectory) {
            Push-Location $WorkingDirectory
        }
        try {
            & docker compose @composeFiles @Arguments
            return $LASTEXITCODE
        }
        finally {
            if ($WorkingDirectory) {
                Pop-Location
            }
        }
    }
    else {
        if ($WorkingDirectory) {
            Push-Location $WorkingDirectory
        }
        try {
            & docker-compose @composeFiles @Arguments
            return $LASTEXITCODE
        }
        finally {
            if ($WorkingDirectory) {
                Pop-Location
            }
        }
    }
}

function Test-ContainerRunning {
    <#
    .SYNOPSIS
        Check if a container is running by name pattern.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$NamePattern
    )

    $containers = docker ps --filter "name=$NamePattern" --format "{{.Names}}" 2>&1
    return ($LASTEXITCODE -eq 0) -and (-not [string]::IsNullOrWhiteSpace($containers))
}

function Get-ContainerHealth {
    <#
    .SYNOPSIS
        Get the health status of a container.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$ContainerName
    )

    $health = docker inspect --format='{{.State.Health.Status}}' $ContainerName 2>&1
    if ($LASTEXITCODE -eq 0) {
        return $health.Trim()
    }
    return $null
}

#endregion

#region Port Utilities

function Test-PortInUse {
    <#
    .SYNOPSIS
        Check if a TCP port is actively in use (excludes TIME_WAIT and other transitional states).
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [int]$Port
    )

    if (Test-IsWindows) {
        # Use Get-NetTCPConnection on Windows
        try {
            # Only listening sockets prevent Docker from publishing a host port.
            $blockingStates = @('Listen')
            $connections = Get-NetTCPConnection -LocalPort $Port -ErrorAction SilentlyContinue |
                Where-Object { $blockingStates -contains $_.State }
            return $null -ne $connections -and @($connections).Count -gt 0
        }
        catch {
            # Fallback to netstat - filter out TIME_WAIT
            $result = netstat -an | Select-String ":$Port\s" | Where-Object { $_ -notmatch 'TIME_WAIT' }
            return $null -ne $result
        }
    }
    else {
        # Use ss on Linux, lsof on macOS
        if (Test-IsLinux) {
            $result = & ss -tuln 2>&1 | Select-String ":$Port\s"
        }
        else {
            $result = & lsof -i ":$Port" 2>&1
        }
        return $null -ne $result -and $result.Count -gt 0
    }
}

function Get-PortProcess {
    <#
    .SYNOPSIS
        Get the process using a specific port.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [int]$Port
    )

    if (Test-IsWindows) {
        try {
            $connection = Get-NetTCPConnection -LocalPort $Port -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($connection) {
                $process = Get-Process -Id $connection.OwningProcess -ErrorAction SilentlyContinue
                return @{
                    ProcessId = $connection.OwningProcess
                    ProcessName = $process.ProcessName
                }
            }
        }
        catch { }
    }
    else {
        # Parse lsof output on Unix
        $output = & lsof -i ":$Port" -t 2>&1 | Select-Object -First 1
        if ($output) {
            return @{
                ProcessId = [int]$output
                ProcessName = (& ps -p $output -o comm= 2>&1).Trim()
            }
        }
    }

    return $null
}

#endregion

#region HTTP Utilities

function Invoke-ApiRequest {
    <#
    .SYNOPSIS
        Make an HTTP API request with proper error handling.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Uri,

        [Parameter()]
        [ValidateSet('GET', 'POST', 'PUT', 'DELETE', 'PATCH')]
        [string]$Method = 'GET',

        [Parameter()]
        [hashtable]$Headers = @{},

        [Parameter()]
        [object]$Body,

        [Parameter()]
        [string]$ContentType = 'application/json'
    )

    $requestParams = @{
        Uri = $Uri
        Method = $Method
        Headers = $Headers
        ContentType = $ContentType
        ErrorAction = 'Stop'
    }

    if ($Body) {
        if ($Body -is [string]) {
            $requestParams.Body = $Body
        }
        else {
            $requestParams.Body = $Body | ConvertTo-Json -Depth 10
        }
    }

    try {
        $response = Invoke-RestMethod @requestParams
        return @{
            Success = $true
            Data = $response
            Error = $null
        }
    }
    catch {
        return @{
            Success = $false
            Data = $null
            Error = $_.Exception.Message
        }
    }
}

#endregion

#region File Utilities

function Test-WriteAccess {
    <#
    .SYNOPSIS
        Test if a directory is writable.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    if (-not (Test-Path $Path -PathType Container)) {
        return $false
    }

    $testFile = Join-Path $Path ".write-test-$PID"

    try {
        $null = New-Item -Path $testFile -ItemType File -Force -ErrorAction Stop
        Remove-Item -Path $testFile -Force -ErrorAction SilentlyContinue
        return $true
    }
    catch {
        return $false
    }
}

function Remove-DirectoryContents {
    <#
    .SYNOPSIS
        Remove all contents of a directory without removing the directory itself.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter()]
        [switch]$Force
    )

    if (Test-Path $Path) {
        Get-ChildItem -Path $Path -Force:$Force | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    }
}

#endregion

# Export all functions
Export-ModuleMember -Function @(
    # Logging
    'Write-Info'
    'Write-Success'
    'Write-Warn'
    'Write-Err'
    'Write-Task'
    'Write-Stats'
    'Write-SectionHeader'

    # Cross-platform
    'Get-CrossPlatformTempPath'
    'Get-ScriptDirectory'
    'Get-ProjectRoot'
    'Test-IsWindows'
    'Test-IsLinux'
    'Test-IsMacOS'
    'Get-DockerUserIds'

    # Environment
    'Import-DotEnv'
    'Get-RequiredEnvVar'

    # Docker
    'Test-DockerRunning'
    'Get-DockerComposeCommand'
    'Invoke-DockerCompose'
    'Test-ContainerRunning'
    'Get-ContainerHealth'

    # Ports
    'Test-PortInUse'
    'Get-PortProcess'

    # HTTP
    'Invoke-ApiRequest'

    # Files
    'Test-WriteAccess'
    'Remove-DirectoryContents'
)
