@echo off
REM Conduit TUI Launcher Script for Windows
REM This script provides a convenient way to launch the Conduit TUI application on Windows

REM Change to the script's directory
cd /d "%~dp0"

REM Check if the TUI project exists
if not exist "ConduitLLM.TUI" (
    echo Error: ConduitLLM.TUI directory not found!
    echo Please ensure you're running this script from the Conduit repository root.
    exit /b 1
)

REM Check if .NET is installed
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo Error: .NET SDK is not installed or not in PATH
    echo Please install .NET 9.0 or later from https://dotnet.microsoft.com/download
    exit /b 1
)

REM Check if we need to build
set BUILD_FLAG=0
if "%1"=="--build" set BUILD_FLAG=1

REM Build the project if it hasn't been built yet or if --build flag is passed
if %BUILD_FLAG%==1 goto :build
if not exist "ConduitLLM.TUI\bin\Debug\net9.0\conduit-tui.dll" goto :build
goto :run

:build
echo Building Conduit TUI...
dotnet build ConduitLLM.TUI\ConduitLLM.TUI.csproj -c Debug
if errorlevel 1 (
    echo Build failed!
    exit /b 1
)
REM If --build was the first argument, shift the arguments
if %BUILD_FLAG%==1 shift

:run
REM Run the TUI application with all passed arguments
dotnet run --project ConduitLLM.TUI\ConduitLLM.TUI.csproj --no-build -- %*