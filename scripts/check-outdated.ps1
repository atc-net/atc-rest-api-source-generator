<#
.SYNOPSIS
    Lists every outdated dependency in the repository - NuGet, npm and GitHub Actions - and changes nothing.

.DESCRIPTION
    The PowerShell twin of check-outdated.sh. Run it before a release: take the minor and patch updates, and either
    take each major or write down why it stays pinned (an `ignore` with a comment in .github/dependabot.yml).
    `npm outdated` and `npm update` stay inside each package.json range (^6.1.0 never shows 7.x), so the npm part uses
    npm-check-updates, which lists the majors too.

.PARAMETER Area
    All (the default), NuGet, Npm or Actions.

.EXAMPLE
    ./scripts/check-outdated.ps1

.EXAMPLE
    ./scripts/check-outdated.ps1 Npm
#>
[CmdletBinding()]
param(
    [ValidateSet('All', 'NuGet', 'Npm', 'Actions')]
    [string] $Area = 'All'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

# The npm folders Dependabot watches (.github/dependabot.yml)
$npmDirectories = @(
    'test/NodeWorkspace'
    'test/Showcase.E2ETests'
    'sample/Showcase/Showcase.ReactApp.Fetch'
    'sample/Showcase/Showcase.ReactApp.Axios'
)

function Get-Major([string] $version) {
    ($version.TrimStart('v', '^', '~') -split '\.')[0]
}

if ($Area -in 'All', 'NuGet') {
    Write-Host '=== NuGet' -ForegroundColor Cyan

    # One line per package (the resolved version and the latest, in how many projects), majors marked
    $updates = dotnet list (Join-Path $root 'Atc.Rest.Api.SourceGenerator.slnx') package --outdated 2>&1 |
        ForEach-Object { "$_".Trim() } |
        Where-Object { $_.StartsWith('> ') } |
        ForEach-Object {
            $fields = $_ -split '\s+'
            [pscustomobject]@{ Package = $fields[1]; Resolved = $fields[-2]; Latest = $fields[-1] }
        } |
        Group-Object Package, Resolved, Latest |
        ForEach-Object {
            $first = $_.Group[0]
            [pscustomobject]@{
                Package  = $first.Package
                Resolved = $first.Resolved
                Latest   = $first.Latest
                Projects = $_.Count
                Major    = if ((Get-Major $first.Resolved) -ne (Get-Major $first.Latest)) { 'MAJOR' } else { '' }
            }
        } |
        Sort-Object Package, Resolved

    if ($updates) { $updates | Format-Table -AutoSize | Out-Host } else { Write-Host '  up to date' }
}

if ($Area -in 'All', 'Npm') {
    foreach ($directory in $npmDirectories) {
        Write-Host "=== npm: $directory" -ForegroundColor Cyan
        Push-Location (Join-Path $root $directory)
        try {
            npx --yes npm-check-updates --format group 2>&1 |
                ForEach-Object { "$_" } |
                Where-Object { $_ -and $_ -notmatch '^(Checking|Run ncu|Run npx)' } |
                Out-Host
        }
        finally {
            Pop-Location
        }

        Write-Host
    }
}

if ($Area -in 'All', 'Actions') {
    Write-Host '=== GitHub Actions (pinned SHA, # tag) against the latest release' -ForegroundColor Cyan
    $pattern = 'uses: (?<repo>[\w.-]+/[\w.-]+)@[0-9a-f]{40} # (?<tag>v[\d.]+)'
    $actions = Get-ChildItem (Join-Path $root '.github/workflows') -Filter *.yml |
        Select-String -Pattern $pattern -AllMatches |
        ForEach-Object { $_.Matches } |
        ForEach-Object { [pscustomobject]@{ Repo = $_.Groups['repo'].Value; Tag = $_.Groups['tag'].Value } } |
        Sort-Object Repo, Tag -Unique

    $outdated = foreach ($action in $actions) {
        $latest = gh api "repos/$($action.Repo)/releases/latest" --jq .tag_name 2>$null
        if (-not $latest) { $latest = '?' }
        if ($latest -ne $action.Tag) {
            [pscustomobject]@{
                Action = $action.Repo
                Pinned = $action.Tag
                Latest = $latest
                Major  = if ((Get-Major $action.Tag) -ne (Get-Major $latest)) { 'MAJOR' } else { '' }
            }
        }
    }

    if ($outdated) { $outdated | Format-Table -AutoSize | Out-Host } else { Write-Host '  up to date' }
}
