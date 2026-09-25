<#
.SYNOPSIS
    Builds Battery Buddy into build\out\BatteryBuddy.exe.
.EXAMPLE
    .\build\build.ps1              # test + publish Release, with the rolling 5-minute logs\diag.log
    .\build\build.ps1 -SkipTests   # publish only
    .\build\build.ps1 -SelfContained   # bundle the .NET runtime so the exe runs without .NET installed; no diag.log
    .\build\build.ps1 -AutoClose -AutoLaunch   # stop any running Battery Buddy, build, then start the new one
    .\build\build.ps1 -Branch main   # build main without asking
    .\build\build.ps1 -Package   # test, then zip both downloads into build\release
    .\build\build.ps1 -Release 1.2.0   # release: -Package, then bump the version, commit and tag v1.2.0
.PARAMETER Branch
    The local branch to build. Without it, a repo with more than one branch asks which one (Enter: the current
    one), showing how many uncommitted changes each checked-out branch has. A branch checked out in this or any
    other worktree builds that worktree's files as they are, uncommitted changes included; any other branch
    builds its last commit in a temporary git worktree, leaving your checkouts alone. Either way the exe lands
    in the main checkout's build\out, even when this script runs from another worktree.
.PARAMETER AutoClose
    Force-stops every running Battery Buddy (wherever it runs from) once the tests pass, instead of refusing
    to build over a copy running from build\out. A failed test run leaves it running.
.PARAMETER AutoLaunch
    Starts build\out\BatteryBuddy.exe after a successful build.
.PARAMETER Package
    Builds both downloads and zips them into build\release (emptied first), under names that stay the same from
    release to release so the README can link to the latest one:
      BatteryBuddy-standalone-win-x64.zip  self-contained, runs on any 64-bit Windows 10/11 (also left in build\out)
      BatteryBuddy-dotnet8-win-x64.zip     much smaller, needs the .NET 8 Desktop Runtime
    Neither has the diag.log. -SelfContained is implied.
.PARAMETER Release
    Prepares release <version> (major.minor.patch, above every existing v* tag) from a clean main: runs
    -Package with that version, then sets <Version> in Directory.Build.props, commits that and tags v<version>.
    It never pushes; 'git push origin main --follow-tags' does, and GitHub Actions then builds the tag the same
    way and attaches both zips to the GitHub release.
#>
param(
    [string]$Branch,
    [string]$Configuration = "Release",
    [switch]$SkipTests,
    [switch]$SelfContained,
    [switch]$AutoClose,
    [switch]$AutoLaunch,
    [switch]$Package,
    [string]$Release
)

$ErrorActionPreference = "Stop"
function Normalize([string]$path) { [IO.Path]::GetFullPath($path).TrimEnd('\') }

$root = Normalize (Split-Path $PSScriptRoot -Parent)   # the worktree this script runs from

# Every existing worktree with the branch checked out in it (none when detached); git lists the main one first.
function Get-Worktrees {
    $lines = @(git -C $root worktree list --porcelain) + ""
    if ($LASTEXITCODE -ne 0) { throw "Couldn't list the git worktrees." }
    $entry = $null
    foreach ($line in $lines) {
        if ($line -like "worktree *") { $entry = [pscustomobject]@{ Path = Normalize $line.Substring(9); Branch = $null } }
        elseif ($line -like "branch refs/heads/*" -and $entry) { $entry.Branch = $line.Substring(18) }
        elseif (-not $line -and $entry) {
            if (Test-Path $entry.Path) { $entry }   # a deleted worktree lingers until 'git worktree prune'
            $entry = $null
        }
    }
}

$worktrees = @(Get-Worktrees)
$out = Join-Path $worktrees[0].Path "build\out"
$current = ($worktrees | Where-Object Path -eq $root).Branch   # empty on a detached HEAD

if ($Release) {
    # Check everything before building, so a refused release leaves nothing behind.
    if ($Release -notmatch '^\d+\.\d+\.\d+$') { throw "-Release takes a version like 1.2.0, not '$Release'." }
    if ($SkipTests) { throw "-Release always runs the tests; drop -SkipTests." }
    if ($Branch -and $Branch -ne "main") { throw "-Release builds main, not '$Branch'." }
    if ($current -ne "main") { throw "-Release commits and tags here, so check out main first (this checkout is on '$current')." }
    if (@(git -C $root status --porcelain).Count) { throw "main has uncommitted changes; commit or stash them first." }
    $tags = @(git -C $root tag --list "v*")
    if ($tags -contains "v$Release") { throw "Tag v$Release already exists." }
    $newest = $tags | Where-Object { $_ -match '^v\d+\.\d+\.\d+$' } | ForEach-Object { [version]$_.Substring(1) } |
        Sort-Object | Select-Object -Last 1
    if ($newest -and [version]$Release -le $newest) { throw "v$Release isn't newer than the last release, v$newest." }
    $Branch = "main"
    $Package = $true
}
if ($Package) { $SelfContained = $true }   # build\out gets the standalone build

# Where a branch builds from: the worktree it's checked out in (its files as they are), else its last commit.
function Get-BuildSource([string]$name) {
    $checkout = $worktrees | Where-Object Branch -eq $name | Select-Object -First 1
    if (-not $checkout) { return [pscustomobject]@{ Path = $null; Label = "last commit" } }
    $changes = @(git -C $checkout.Path status --porcelain).Count
    $where = if ($checkout.Path -eq $root) { "current" } else { "worktree $($checkout.Path)" }
    $dirty = switch ($changes) { 0 { "" } 1 { ", 1 uncommitted change" } default { ", $changes uncommitted changes" } }
    [pscustomobject]@{ Path = $checkout.Path; Label = "$where, working tree$dirty" }
}

function Select-Branch {
    $branches = @(git -C $root branch --format="%(refname:short)")
    if ($LASTEXITCODE -ne 0) { throw "Couldn't list the git branches." }
    if ($Branch) {
        if ($branches -notcontains $Branch) { throw "No local branch '$Branch'. Branches: $($branches -join ', ')" }
        return $Branch
    }
    if ($branches.Count -le 1) { return $current }

    Write-Host "Branches:"
    for ($i = 0; $i -lt $branches.Count; $i++) {
        Write-Host ("  {0}. {1} ({2})" -f ($i + 1), $branches[$i], (Get-BuildSource $branches[$i]).Label)
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
$tempWorktree = $null
if ($selected) {
    $from = Get-BuildSource $selected
    if ($from.Path) { $source = $from.Path }
    else {
        # Not checked out anywhere: build its last commit on the side, so every checkout stays put.
        $tempWorktree = Join-Path ([IO.Path]::GetTempPath()) "BatteryBuddy-build-$([guid]::NewGuid().ToString('N').Substring(0, 8))"
        git -C $root worktree add --detach --quiet $tempWorktree $selected
        if ($LASTEXITCODE -ne 0) { throw "Couldn't check out '$selected' into a temporary worktree." }
        $source = $tempWorktree
    }
    Write-Host "Building $selected ($($from.Label))" -ForegroundColor Cyan
}
if ($source -ne $worktrees[0].Path) { Write-Host "Output: $out" -ForegroundColor Cyan }

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

    # A framework-dependent build gets the diag.log unless it's a download.
    function Publish([string]$dir, [bool]$standalone) {
        $publishArgs = @("src/BatteryBuddy.App", "-c", $Configuration, "-r", "win-x64", "-p:PublishSingleFile=true", "-o", $dir, "--nologo")
        if ($standalone) {
            # The runtime and WPF make the exe much larger; compression keeps it manageable.
            $publishArgs += "--self-contained", "true", "-p:IncludeNativeLibrariesForSelfExtract=true", "-p:EnableCompressionInSingleFile=true"
        }
        else {
            $publishArgs += "--self-contained", "false"
            if (-not $Package) { $publishArgs += "-p:DiagLog=true" }
        }
        if ($Release) { $publishArgs += "-p:Version=$Release" }
        dotnet publish @publishArgs
        if ($LASTEXITCODE -ne 0) { throw "Publish failed." }
    }

    Publish $out $SelfContained
    Write-Host ""
    Write-Host "Built: $out\BatteryBuddy.exe" -ForegroundColor Green

    if ($Package) {
        $releaseDir = Join-Path $worktrees[0].Path "build\release"
        if (Test-Path $releaseDir) { Remove-Item $releaseDir -Recurse -Force }
        $small = Join-Path $releaseDir "dotnet8"
        Publish $small $false
        $zips = @(
            @{ From = $out; To = Join-Path $releaseDir "BatteryBuddy-standalone-win-x64.zip" },
            @{ From = $small; To = Join-Path $releaseDir "BatteryBuddy-dotnet8-win-x64.zip" }
        )
        foreach ($zip in $zips) {
            # The exe only: the .pdb is for debugging, and .gitkeep holds build\out in git.
            Get-ChildItem $zip.From -Exclude .gitkeep, *.pdb | Compress-Archive -DestinationPath $zip.To
        }
        Remove-Item $small -Recurse -Force
        Write-Host ""
        foreach ($zip in $zips) { Write-Host ("Packaged: {0} ({1:N0} MB)" -f $zip.To, ((Get-Item $zip.To).Length / 1MB)) -ForegroundColor Green }
    }

    if ($Release) {
        # Only now that it built: record the version, commit it and tag that commit.
        $props = Join-Path $root "Directory.Build.props"
        $text = [IO.File]::ReadAllText($props)
        [IO.File]::WriteAllText($props, ($text -replace '<Version>[^<]*</Version>', "<Version>$Release</Version>"))
        git -C $root add Directory.Build.props
        git -C $root diff --cached --quiet
        if ($LASTEXITCODE -ne 0) {
            git -C $root commit --quiet -m "chore: release v$Release"
            if ($LASTEXITCODE -ne 0) { throw "Couldn't commit the version bump." }
        }
        git -C $root tag -a "v$Release" -m "Battery Buddy v$Release"
        if ($LASTEXITCODE -ne 0) { throw "Couldn't create tag v$Release." }

        Write-Host "Release v$Release is committed and tagged." -ForegroundColor Green
        Write-Host "To publish it:  git push origin main --follow-tags" -ForegroundColor Cyan
        Write-Host "(GitHub Actions then builds the tag and creates the GitHub release with both zips.)"
    }

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
    if ($tempWorktree) {
        git -C $root worktree remove --force $tempWorktree
        if ($LASTEXITCODE -ne 0) { Write-Warning "Couldn't remove the temporary worktree $tempWorktree; 'git worktree prune' cleans it up once it's deleted." }
    }
}
