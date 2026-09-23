<#
.SYNOPSIS
    Builds Battery Buddy into build\out\BatteryBuddy.exe.
.EXAMPLE
    .\build\build.ps1              # test + publish Release
    .\build\build.ps1 -SkipTests   # publish only
#>
param(
    [string]$Configuration = "Release",
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $PSScriptRoot "out"

Push-Location $root
try {
    if (-not $SkipTests) {
        dotnet test tests/BatteryBuddy.Core.Tests -c $Configuration --nologo
        if ($LASTEXITCODE -ne 0) { throw "Tests failed; nothing was published." }
    }

    $running = Get-Process BatteryBuddy -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$out*" }
    if ($running) { throw "Battery Buddy is running from build\out. Quit it (tray icon > Quit) and build again." }

    # Start from a clean out\ so removed files don't linger; keep the placeholder that keeps the folder in git.
    if (Test-Path $out) { Get-ChildItem $out -Exclude .gitkeep | Remove-Item -Recurse -Force }

    dotnet publish src/BatteryBuddy.App -c $Configuration -r win-x64 --self-contained false `
        -p:PublishSingleFile=true -o $out --nologo
    if ($LASTEXITCODE -ne 0) { throw "Publish failed." }

    Write-Host ""
    Write-Host "Built: $out\BatteryBuddy.exe" -ForegroundColor Green
}
finally {
    Pop-Location
}
