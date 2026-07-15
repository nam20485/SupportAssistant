#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Shared helpers for the gh-issue-tracking-init operation scripts.

.DESCRIPTION
    Dot-source this file from each operation script:

        . (Join-Path $PSScriptRoot 'common.ps1')

    It provides GitHub CLI auth bootstrap, a single wrapper around `gh`
    (so calls are mockable in Pester), owner/repo parsing, and the id
    lookups required by the sub-issues and issue-dependencies REST APIs
    (both of which key off the issue's numeric database id, not its number).

    This skill's scripts/ directory is self-contained: common-auth.ps1,
    import-labels.ps1, and create-milestones.ps1 are vendored here (copied
    from the repository root's scripts/) so the skill has no dependency on
    the parent scripts/ directory.

.NOTES
    Requires: PowerShell 7+, GitHub CLI (gh) authenticated against the target repo.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# common-auth.ps1 is vendored alongside this file so the skill is self-contained.
$commonAuth = Join-Path $PSScriptRoot 'common-auth.ps1'
if (Test-Path -LiteralPath $commonAuth) { . $commonAuth }

function Assert-GhCli {
    <#.SYNOPSIS Throw unless the gh CLI is on PATH.#>
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        throw "Required command 'gh' not found in PATH. Install the GitHub CLI first."
    }
}

function Initialize-Auth {
    <#.SYNOPSIS Ensure the gh CLI is authenticated (delegates to Initialize-GitHubAuth when available).#>
    [CmdletBinding()]
    param([switch]$DryRun)
    Assert-GhCli
    if (Get-Command Initialize-GitHubAuth -ErrorAction SilentlyContinue) {
        Initialize-GitHubAuth -DryRun:$DryRun
        return
    }
    & gh auth status 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Warning 'GitHub CLI is not authenticated. Run: gh auth login'
    }
}

function Invoke-Gh {
    <#.SYNOPSIS Central wrapper around `gh` so every call is mockable in tests. Throws on non-zero exit.#>
    [CmdletBinding()]
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$GhArgs)
    # stdout is captured for the caller; stderr flows to the console so it is not
    # merged into (and corrupting) JSON output.
    $output = & gh @GhArgs
    if ($LASTEXITCODE -ne 0) {
        throw "gh $($GhArgs -join ' ') failed (exit $LASTEXITCODE). See gh output above."
    }
    return $output
}

function Invoke-GhJson {
    <#.SYNOPSIS Invoke-Gh and parse JSON stdout into objects.#>
    [CmdletBinding()]
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$GhArgs)
    $raw = Invoke-Gh @GhArgs
    if (-not $raw) { return $null }
    return ($raw | Out-String | ConvertFrom-Json)
}

function Get-RepoParts {
    <#.SYNOPSIS Split an 'owner/repo' string into Owner/Name.#>
    param([Parameter(Mandatory = $true)][string]$Repo)
    if ($Repo -notmatch '^[^/]+/[^/]+$') {
        throw "Repo must be in 'owner/repo' form; got: $Repo"
    }
    $parts = $Repo.Split('/', 2)
    return [pscustomobject]@{ Owner = $parts[0]; Name = $parts[1] }
}

function Get-IssueDbId {
    <#.SYNOPSIS Return an issue's numeric database id (required by sub-issues + dependencies APIs), not its number.#>
    param(
        [Parameter(Mandatory = $true)][string]$Repo,
        [Parameter(Mandatory = $true)][int]$Number
    )
    return [int](Invoke-Gh api "repos/$Repo/issues/$Number" --jq '.id')
}

function Find-IssueNumberByTitle {
    <#.SYNOPSIS Exact-title lookup across open+closed issues (for idempotent create/update). Returns [int] or $null.#>
    param(
        [Parameter(Mandatory = $true)][string]$Repo,
        [Parameter(Mandatory = $true)][string]$Title
    )
    $items = Invoke-GhJson issue list --repo $Repo --state all --search "in:title `"$Title`"" --json 'number,title' --limit 200
    if (-not $items) { return $null }
    $match = @($items | Where-Object { $_.title -eq $Title }) | Select-Object -First 1
    if ($match) { return [int]$match.number }
    return $null
}

function Write-Step { param([string]$Message) Write-Host $Message -ForegroundColor Cyan }
function Write-DryRun { param([string]$Message) Write-Host "[dry-run] $Message" -ForegroundColor Yellow }
function Write-Ok { param([string]$Message) Write-Host $Message -ForegroundColor Green }
function Write-Skip { param([string]$Message) Write-Host $Message -ForegroundColor DarkGray }
