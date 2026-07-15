#!/usr/bin/env pwsh
#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.0.0' }

<#
    Pester tests for the gh-issue-tracking-init skill's operation scripts.

    These cover the mockable helper logic in common.ps1 and per-script contracts
    (parsing, -DryRun surface, and -Repo validation). Full end-to-end behavior is
    exercised by the user-run smoke test against a throwaway repo (see README.md),
    because gh is a native command and the write paths mutate a real repo.

    Run:  Invoke-Pester -Path .agents/skills/gh-issue-tracking-init/scripts/tests
#>

$ghitDir = Split-Path -Parent $PSScriptRoot
$opScripts = @(
    'ensure-labels.ps1', 'ensure-project.ps1', 'ensure-issue.ps1',
    'link-sub-issue.ps1', 'set-dependency.ps1', 'set-project-fields.ps1'
)
$allScripts = @('common.ps1') + $opScripts

$repoValidationCases = @(
    @{ Name = 'ensure-labels.ps1'; Splat = @{ Repo = 'no-slash' } }
    @{ Name = 'ensure-project.ps1'; Splat = @{ Owner = 'o'; Repo = 'no-slash' } }
    @{ Name = 'ensure-issue.ps1'; Splat = @{ Repo = 'no-slash'; Title = 't' } }
    @{ Name = 'link-sub-issue.ps1'; Splat = @{ Repo = 'no-slash'; ParentNumber = 1; ChildNumber = 2 } }
    @{ Name = 'set-dependency.ps1'; Splat = @{ Repo = 'no-slash'; IssueNumber = 1; BlockedByNumber = 2 } }
    @{ Name = 'set-project-fields.ps1'; Splat = @{ Owner = 'o'; ProjectNumber = 1; Repo = 'no-slash'; IssueNumber = 2 } }
)

BeforeAll {
    $script:GhitDir = Split-Path -Parent $PSScriptRoot
    . (Join-Path $script:GhitDir 'common.ps1')
}

Describe 'common.ps1 helpers' {

    Context 'Get-RepoParts' {
        It 'splits an owner/repo string' {
            $p = Get-RepoParts -Repo 'nam20485/SupportAssistant'
            $p.Owner | Should -Be 'nam20485'
            $p.Name | Should -Be 'SupportAssistant'
        }
        It 'throws on a malformed repo' {
            { Get-RepoParts -Repo 'not-a-repo' } | Should -Throw
        }
    }

    Context 'Find-IssueNumberByTitle' {
        It 'returns the number for an exact title match (ignoring partial matches)' {
            Mock Invoke-GhJson {
                @(
                    [pscustomobject]@{ number = 6; title = 'Epic 1: Foundations (extended)' }
                    [pscustomobject]@{ number = 5; title = 'Epic 1: Foundations' }
                )
            }
            Find-IssueNumberByTitle -Repo 'o/r' -Title 'Epic 1: Foundations' | Should -Be 5
        }
        It 'returns null when only partial matches exist' {
            Mock Invoke-GhJson { @([pscustomobject]@{ number = 6; title = 'Epic 1: Foundations (extended)' }) }
            Find-IssueNumberByTitle -Repo 'o/r' -Title 'Epic 1: Foundations' | Should -BeNullOrEmpty
        }
        It 'returns null when the search is empty' {
            Mock Invoke-GhJson { $null }
            Find-IssueNumberByTitle -Repo 'o/r' -Title 'Anything' | Should -BeNullOrEmpty
        }
    }

    Context 'Get-IssueDbId' {
        It 'returns the numeric database id as an int' {
            Mock Invoke-Gh { '246813' }
            $id = Get-IssueDbId -Repo 'o/r' -Number 7
            $id | Should -Be 246813
            $id | Should -BeOfType [int]
        }
    }

    Context 'Invoke-GhJson' {
        It 'parses JSON stdout into objects' {
            Mock Invoke-Gh { '{"number":42,"title":"x"}' }
            (Invoke-GhJson api 'repos/o/r/issues/42').number | Should -Be 42
        }
    }
}

Describe 'label taxonomy (labels.json)' {
    It 'is valid JSON and contains the canonical taxonomy' {
        $labels = Get-Content (Join-Path $GhitDir 'labels.json') -Raw | ConvertFrom-Json
        $names = @($labels.name)
        foreach ($expected in @('plan', 'epic', 'story', 'task', 'P0', 'P1', 'P2', 'P3', 'blocked', 'needs-review', 'wontfix')) {
            $names | Should -Contain $expected
        }
    }
    It 'does not include workflow-state labels (those live in the Project Status field)' {
        $labels = Get-Content (Join-Path $GhitDir 'labels.json') -Raw | ConvertFrom-Json
        $names = @($labels.name)
        $names | Should -Not -Contain 'in-progress'
        $names | Should -Not -Contain 'done'
    }
}

Describe 'operation script contracts' {

    It 'parses without errors: <_>' -ForEach $allScripts {
        $path = Join-Path $GhitDir $_
        $errors = $null
        [System.Management.Automation.Language.Parser]::ParseFile($path, [ref]$null, [ref]$errors) | Out-Null
        $errors | Should -BeNullOrEmpty
    }

    It 'exposes a -DryRun switch: <_>' -ForEach $opScripts {
        $path = Join-Path $GhitDir $_
        (Get-Command $path).Parameters.ContainsKey('DryRun') | Should -BeTrue
    }

    It 'rejects a malformed -Repo: <Name>' -ForEach $repoValidationCases {
        $path = Join-Path $GhitDir $Name
        { & $path @Splat } | Should -Throw
    }
}

Describe 'self-containment' {
    It 'vendors common-auth.ps1, import-labels.ps1, and create-milestones.ps1 locally' {
        foreach ($f in @('common-auth.ps1', 'import-labels.ps1', 'create-milestones.ps1')) {
            Test-Path -LiteralPath (Join-Path $GhitDir $f) | Should -BeTrue
        }
    }
}
