<#
.SYNOPSIS
    Builds Battery Buddy into build\out\BatteryBuddy.exe.
.EXAMPLE
    .\build\build.ps1              # test + publish Release, with the rolling 5-minute logs\diag.log
    .\build\build.ps1 -SkipTests   # publish only
    .\build\build.ps1 -SelfContained   # bundle the .NET runtime so the exe runs without .NET installed; no diag.log
    .\build\build.ps1 -AutoClose -AutoLaunch   # stop any running Battery Buddy, build, then start the new one
.PARAMETER AutoClose
    Force-stops every running Battery Buddy (wherever it runs from) once the tests pass, instead of refusing
    to build over a copy running from build\out. A failed test run leaves it running.
.PARAMETER AutoLaunch
    Starts build\out\BatteryBuddy.exe after a successful build.
#>
param(
    [string]$Configuration = "Release",
    [switch]$SkipTests,
    [switch]$SelfContained,
    [switch]$AutoClose,
    [switch]$AutoLaunch
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $PSScriptRoot "out"

Push-Location $root
try {
    if (-not $SkipTests) {
        dotnet test tests/BatteryBuddy.Tests -c $Configuration --nologo
        if ($LASTEXITCODE -ne 0) { throw "Tests failed; nothing was published." }
    }

    if ($AutoClose) {
        $running = @(Get-Process BatteryBuddy -ErrorAction SilentlyContinue)
        if ($running) {
            Write-Host "Closing $($running.Count) running Battery Buddy..."
            $running | Stop-Process -Force
            # Wait so build\out's files are unlocked before they're deleted.
            $running | ForEach-Object { $_.WaitForExit(10000) | Out-Null }
        }
    }
    else {
        $running = Get-Process BatteryBuddy -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$out*" }
        if ($running) { throw "Battery Buddy is running from build\out. Quit it (tray icon > Quit), or pass -AutoClose, and build again." }
    }

    # Start from a clean out\ so removed files don't linger; keep the placeholder that keeps the folder in git.
    if (Test-Path $out) { Get-ChildItem $out -Exclude .gitkeep | Remove-Item -Recurse -Force }

    $publishArgs = @("src/BatteryBuddy.App", "-c", $Configuration, "-r", "win-x64", "-p:PublishSingleFile=true", "-o", $out, "--nologo")
    if ($SelfContained) {
        # The runtime and WPF make the exe much larger; compression keeps it manageable.
        $publishArgs += "--self-contained", "true", "-p:IncludeNativeLibrariesForSelfExtract=true", "-p:EnableCompressionInSingleFile=true"
    }
    else {
        $publishArgs += "--self-contained", "false", "-p:DiagLog=true"
    }
    dotnet publish @publishArgs
    if ($LASTEXITCODE -ne 0) { throw "Publish failed." }

    Write-Host ""
    Write-Host "Built: $out\BatteryBuddy.exe" -ForegroundColor Green

    if ($AutoLaunch) {
        # Only one copy runs at a time: with another still running, the new one would quietly exit.
        if (Get-Process BatteryBuddy -ErrorAction SilentlyContinue) {
            Write-Warning "Battery Buddy is already running, so the new build wasn't started. Pass -AutoClose to replace it."
        }
        else {
            Start-Process (Join-Path $out "BatteryBuddy.exe") -WorkingDirectory $out
            Write-Host "Launched." -ForegroundColor Green
        }
    }
}
finally {
    Pop-Location
}
