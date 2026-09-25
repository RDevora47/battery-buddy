<#
.SYNOPSIS
    Builds Battery Buddy into build\out\BatteryBuddy.exe.
.EXAMPLE
    .\build\build.ps1              # test + publish Release, with the rolling 5-minute logs\diag.log
    .\build\build.ps1 -SkipTests   # publish only
    .\build\build.ps1 -SelfContained   # bundle the .NET runtime so the exe runs without .NET installed; no diag.log
    .\build\build.ps1 -AutoClose -AutoLaunch   # stop any running Battery Buddy, build, then start the new one
    .\build\build.ps1 -Branch main   # build main without asking
.PARAMETER Branch
    The local branch to build. Without it, a repo with more than one branch asks which one (Enter: the current
    one). The current branch builds the working tree as it is, uncommitted changes included; any other branch
    builds its last commit in a temporary git worktree, leaving your checkout alone. Either way the exe lands
    in this checkout's build\out.
.PARAMETER AutoClose
    Force-stops every running Battery Buddy (wherever it runs from) once the tests pass, instead of refusing
    to build over a copy running from build\out. A failed test run leaves it running.
.PARAMETER AutoLaunch
    Starts build\out\BatteryBuddy.exe after a successful build.
#>
param(
    [string]$Branch,
    [string]$Configuration = "Release",
    [switch]$SkipTests,
    [switch]$SelfContained,
    [switch]$AutoClose,
    [switch]$AutoLaunch
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $PSScriptRoot "out"

function Select-Branch {
    $branches = @(git -C $root branch --format="%(refname:short)")
    if ($LASTEXITCODE -ne 0) { throw "Couldn't list the git branches." }
    $current = git -C $root branch --show-current   # empty on a detached HEAD
    if ($Branch) {
        if ($branches -notcontains $Branch) { throw "No local branch '$Branch'. Branches: $($branches -join ', ')" }
        return $Branch
    }
    if ($branches.Count -le 1) { return $current }

    Write-Host "Branches:"
    for ($i = 0; $i -lt $branches.Count; $i++) {
        $mark = if ($branches[$i] -eq $current) { " (current, working tree)" } else { "" }
        Write-Host ("  {0}. {1}{2}" -f ($i + 1), $branches[$i], $mark)
    }
    while ($true) {
        $answer = (Read-Host "Build which branch? [number or name, Enter = current]").Trim()
        if (-not $answer -and $current) { return $current }
        if ($answer -match '^\d+$' -and [int]$answer -ge 1 -and [int]$answer -le $branches.Count) { return $branches[[int]$answer - 1] }
        if ($branches -contains $answer) { return $answer }
        Write-Host "Not a branch: '$answer'" -ForegroundColor Yellow
    }
}

$selected = Select-Branch
$source = $root
$worktree = $null
if ($selected -and $selected -ne (git -C $root branch --show-current)) {
    # Another branch: build its last commit on the side, so the checkout and any uncommitted work stay put.
    $worktree = Join-Path ([IO.Path]::GetTempPath()) "BatteryBuddy-build-$([guid]::NewGuid().ToString('N').Substring(0, 8))"
    git -C $root worktree add --detach --quiet $worktree $selected
    if ($LASTEXITCODE -ne 0) { throw "Couldn't check out '$selected' into a temporary worktree." }
    $source = $worktree
}
if ($selected) { Write-Host "Building $selected$(if ($worktree) { ' (last commit)' } else { ' (working tree)' })" -ForegroundColor Cyan }

Push-Location $source
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
    if ($worktree) {
        git -C $root worktree remove --force $worktree
        if ($LASTEXITCODE -ne 0) { Write-Warning "Couldn't remove the temporary worktree $worktree; 'git worktree prune' cleans it up once it's deleted." }
    }
}
