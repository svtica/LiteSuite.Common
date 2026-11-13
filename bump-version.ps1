<#
.SYNOPSIS
    Bumps the LiteSuite.Common package version, commits, tags and pushes to publish it.

.DESCRIPTION
    Updates <Version> in src\LiteSuite.Common\LiteSuite.Common.csproj, commits
    "Bump version to X" on main, tags vX and pushes both.

    Locally, pushing the tag triggers the "Build" workflow, which publishes the package. The
    "Release" workflow (Actions tab > Release > Run workflow) runs this script with -Bump or
    -Version and then calls the build itself, because a tag pushed from a workflow does not
    trigger other workflows.

.PARAMETER Version
    The new version, 3 parts (e.g. "1.0.1").

.PARAMETER Bump
    Instead of -Version: increment the current version (patch 1.0.0 -> 1.0.1,
    minor -> 1.1.0, major -> 2.0.0).

.EXAMPLE
    .\bump-version.ps1 -Bump patch
.EXAMPLE
    .\bump-version.ps1 -Version "1.1.0"
#>
[CmdletBinding(DefaultParameterSetName = 'Explicit')]
param(
    [Parameter(Mandatory = $true, ParameterSetName = 'Explicit')]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,

    [Parameter(Mandatory = $true, ParameterSetName = 'Bump')]
    [ValidateSet('patch', 'minor', 'major')]
    [string]$Bump
)

$ErrorActionPreference = 'Stop'

if (git status --porcelain) { throw 'Working tree has uncommitted changes; commit or stash them first.' }
$branch = (git rev-parse --abbrev-ref HEAD).Trim()
if ($branch -ne 'main') { throw "Releases are tagged from main (current branch: $branch)." }
git pull --ff-only origin main
if ($LASTEXITCODE -ne 0) { throw 'git pull --ff-only failed; resolve it before bumping the version.' }

$projectFile = Join-Path $PSScriptRoot 'src\LiteSuite.Common\LiteSuite.Common.csproj'
$proj = Get-Content $projectFile -Raw
$m = [regex]::Match($proj, '<Version>(\d+)\.(\d+)\.(\d+)</Version>')
if (-not $m.Success) { throw "<Version> not found in $projectFile" }
$current = "$($m.Groups[1].Value).$($m.Groups[2].Value).$($m.Groups[3].Value)"

if ($PSCmdlet.ParameterSetName -eq 'Bump') {
    $major = [int]$m.Groups[1].Value; $minor = [int]$m.Groups[2].Value; $patch = [int]$m.Groups[3].Value
    switch ($Bump) {
        'major' { $major++; $minor = 0; $patch = 0 }
        'minor' { $minor++; $patch = 0 }
        'patch' { $patch++ }
    }
    $Version = "$major.$minor.$patch"
}

$tag = "v$Version"
if ([version]$Version -le [version]$current) { throw "New version $Version must be greater than the current $current." }
git fetch --tags --quiet origin
if (git tag --list $tag) { throw "Tag $tag already exists." }

Write-Host "Bumping $current -> $Version (tag $tag)"
$proj = $proj -replace '<Version>[^<]+</Version>', "<Version>$Version</Version>"
Set-Content $projectFile $proj -NoNewline

git add $projectFile
git commit -m "Bump version to $Version"
if ($LASTEXITCODE -ne 0) { throw 'git commit failed.' }
git tag $tag
git push origin HEAD
if ($LASTEXITCODE -ne 0) { throw 'git push of main failed (branch protection?).' }
git push origin $tag
if ($LASTEXITCODE -ne 0) { throw "git push of $tag failed." }

if ($env:GITHUB_OUTPUT) {
    Add-Content -Path $env:GITHUB_OUTPUT -Value "version=$Version"
    Add-Content -Path $env:GITHUB_OUTPUT -Value "tag=$tag"
}

Write-Host "`nDone! $tag pushed."
