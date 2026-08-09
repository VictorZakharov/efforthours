param(
    [string] $InputRoot = (Join-Path $PSScriptRoot '../../artifacts/host-review-m8'),
    [string] $CorpusPath = (Join-Path $PSScriptRoot '../../calibration/corpora/public-expansion/0.1.0.corpus.json'),
    [string] $CliDll = (Join-Path $PSScriptRoot '../../src/EffortHours.Cli/bin/Release/net10.0/efforthours.dll'),
    [string] $OutputRoot = (Join-Path $PSScriptRoot 'public-expansion/0.1.0')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$InputRoot = [IO.Path]::GetFullPath($InputRoot)
$CorpusPath = [IO.Path]::GetFullPath($CorpusPath)
$CliDll = [IO.Path]::GetFullPath($CliDll)
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
$LedgerRoot = Join-Path $InputRoot 'ledgers'
$MeasurementRoot = Join-Path $OutputRoot 'measurements'

[IO.Directory]::CreateDirectory($LedgerRoot) | Out-Null
[IO.Directory]::CreateDirectory($MeasurementRoot) | Out-Null

$Corpus = Get-Content -Raw -LiteralPath $CorpusPath | ConvertFrom-Json

$Samples = @(
    [ordered]@{
        Name = 'mitt-3.0.1'
        Subject = 'public-sample-a'
        Repository = 'mitt-source/mitt-3.0.1'
        QueryTarget = 'work:unit-tests:875ec9f8d2f4b23af140'
    },
    [ordered]@{
        Name = 'cliwrap-3.10.4'
        Subject = 'public-sample-b'
        Repository = 'cliwrap-source/CliWrap-3.10.4'
        QueryTarget = 'work:dotnet-source-backbone:0e62be7c6d2d8617652b'
    },
    [ordered]@{
        Name = 'nanostores-1.4.2'
        Subject = 'public-sample-c'
        Repository = 'nanostores-source/nanostores-1.4.2'
        QueryTarget = 'work:javascript-source-backbone:2f5795bda39a0bae4a33'
    }
)

$CompactReplacements = @{
    'mitt-3.0.1' = @{
        'work:unit-tests:875ec9f8d2f4b23af140' = @(3.5, 6, 9)
        'work:build-tooling:c2c91d07e063088d401a' = @(1.75, 3, 5)
        'work:ci-infrastructure:bee22f5b9a094661eb75' = @(1.25, 2.5, 4.5)
        'work:architecture-design:1301c834bf26665d0695' = @(0.75, 1.25, 2.25)
    }
    'cliwrap-3.10.4' = @{
        'work:dotnet-source-backbone:0e62be7c6d2d8617652b' = @(38, 65, 105)
        'work:unit-tests:a6ad88961efa60e9f48e' = @(25, 44, 70)
        'work:dotnet-source-backbone:82c6f462b95679cc72e3' = @(4, 8, 14)
        'work:dotnet-source-backbone:bff334a3ff0bfc0ff2f6' = @(3, 6, 11)
        'work:documentation:bebbc838ff439c1b4e49' = @(6, 10, 16)
        'work:manual-validation:760419422c64b12f8328' = @(0.5, 1, 2)
        'work:manual-validation:8080ab37fe1b4f3b2481' = @(0.5, 1, 2)
        'work:manual-validation:2f30231effeda20fa0c4' = @(0.5, 1, 2)
        'work:manual-validation:13a1d5c2bbf0fa759582' = @(0.75, 1.5, 3)
    }
    'nanostores-1.4.2' = @{
        'work:javascript-source-backbone:2f5795bda39a0bae4a33' = @(40, 80, 145)
        'work:unit-tests:875ec9f8d2f4b23af140' = @(30, 50, 78)
        'work:documentation:bebbc838ff439c1b4e49' = @(7, 12, 18)
        'work:manual-validation:a0df1126ba2f8dbcb19b' = @(0.5, 1, 2)
    }
}

function Write-Json([object] $Value, [string] $Path) {
    $Json = $Value | ConvertTo-Json -Depth 100 -Compress
    [IO.File]::WriteAllText($Path, $Json, [Text.UTF8Encoding]::new($false))
}

function Normalize-JsonFile([string] $Path) {
    $Json = [IO.File]::ReadAllText($Path).TrimEnd([char[]] @("`r", "`n"))
    [IO.File]::WriteAllText($Path, $Json, [Text.UTF8Encoding]::new($false))
}

function New-Range([object[]] $Values) {
    [ordered]@{
        low = [decimal] $Values[0]
        expected = [decimal] $Values[1]
        high = [decimal] $Values[2]
    }
}

function Test-RangeEqual([object] $Left, [object] $Right) {
    return [decimal] $Left.low -eq [decimal] $Right.low -and
        [decimal] $Left.expected -eq [decimal] $Right.expected -and
        [decimal] $Left.high -eq [decimal] $Right.high
}

function New-Affirmation([object] $Candidate, [string] $Reason) {
    [ordered]@{
        targetId = $Candidate.capability.id
        decision = 'affirm'
        originalHours = $Candidate.capability.hours
        evidenceIds = @($Candidate.evidenceIds[0])
        reason = $Reason
    }
}

function New-Replacement(
    [object] $Candidate,
    [string] $Category,
    [object] $Hours,
    [string] $Reason
) {
    [ordered]@{
        targetId = $Candidate.capability.id
        decision = 'replace'
        originalHours = $Candidate.capability.hours
        replacement = [ordered]@{
            category = $Category
            hours = $Hours
            confidence = [decimal] $Candidate.capability.confidence
            assumptions = @($Candidate.assumptions)
            exclusions = @($Candidate.exclusions)
            uncertaintyReasons = @($Candidate.uncertaintyReasons)
        }
        evidenceIds = @($Candidate.evidenceIds[0])
        reason = $Reason
    }
}

function New-CompactLedger([object] $Packet, [string] $Name) {
    $Replacements = $CompactReplacements[$Name]
    $Adjustments = foreach ($Candidate in $Packet.candidates) {
        $Id = $Candidate.capability.id
        if ($Replacements.ContainsKey($Id)) {
            New-Replacement `
                $Candidate `
                $Candidate.capability.category `
                (New-Range $Replacements[$Id]) `
                'Compact host review replaced this range from packet evidence and one bounded capability query.'
        }
        else {
            New-Affirmation $Candidate 'Compact host review affirmed this packet range.'
        }
    }

    [ordered]@{
        schemaVersion = '1.0.0'
        protocolVersion = $Packet.protocolVersion
        inputDigest = $Packet.inputDigest
        reviewerModel = [ordered]@{
            isAvailable = $true
            provider = 'openai'
            model = 'codex'
            version = 'session-build-undisclosed'
        }
        adjustments = @($Adjustments)
        notes = @('Packet-only review authored for the Milestone 8 measurement checkpoint.')
    }
}

function Get-ReferenceRange([object[]] $Targets) {
    [ordered]@{
        low = [decimal] (($Targets | ForEach-Object { $_.hours.low } | Measure-Object -Sum).Sum)
        expected = [decimal] (($Targets | ForEach-Object { $_.hours.expected } | Measure-Object -Sum).Sum)
        high = [decimal] (($Targets | ForEach-Object { $_.hours.high } | Measure-Object -Sum).Sum)
    }
}

function New-ReferenceLedger([object] $Packet, [object] $Record) {
    $Reviewer = $Record.review.reviewers[0]
    $Adjustments = foreach ($Candidate in $Packet.candidates) {
        $Id = $Candidate.capability.id
        $Targets = @($Record.targets | Where-Object {
            @($_.sourceWorkItemIds | Where-Object {
                $_ -eq $Id -or $_.StartsWith($Id + ':part-', [StringComparison]::Ordinal)
            }).Count -gt 0
        })
        if ($Targets.Count -eq 0) {
            throw "No frozen reference target maps to '$Id'."
        }

        $Categories = @($Targets.category | Sort-Object -Unique)
        if ($Categories.Count -ne 1) {
            throw "Reference targets for '$Id' do not have exactly one category."
        }

        $Hours = Get-ReferenceRange $Targets
        if ($Categories[0] -eq $Candidate.capability.category -and
            (Test-RangeEqual $Hours $Candidate.capability.hours)) {
            New-Affirmation $Candidate 'Frozen broader-source teacher review affirmed this baseline range.'
        }
        else {
            New-Replacement `
                $Candidate `
                $Categories[0] `
                $Hours `
                'Frozen broader-source teacher targets were reconciled to this packet capability.'
        }
    }

    [ordered]@{
        schemaVersion = '1.0.0'
        protocolVersion = $Packet.protocolVersion
        inputDigest = $Packet.inputDigest
        reviewerModel = [ordered]@{
            isAvailable = $true
            model = $Reviewer.modelId
            version = $Reviewer.modelVersion
        }
        adjustments = @($Adjustments)
        notes = @('Derived exactly from the frozen public-expansion/0.1.0 teacher targets.')
    }
}

function Invoke-EffortHours([string[]] $Arguments) {
    & dotnet $CliDll @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "EffortHours exited with code $LASTEXITCODE for: $($Arguments -join ' ')"
    }
}

$MeasurementPaths = @()
foreach ($Sample in $Samples) {
    $RepositoryPath = Join-Path $InputRoot $Sample.Repository
    $PacketPath = Join-Path $InputRoot ($Sample.Name + '.packet.json')
    $QueryPath = Join-Path $InputRoot ($Sample.Name + '.query.json')
    Invoke-EffortHours @(
        'review', 'packet', $RepositoryPath,
        '--profile', 'implementation',
        '--compact',
        '--output', $PacketPath
    )
    Normalize-JsonFile $PacketPath
    $Packet = Get-Content -Raw -LiteralPath $PacketPath | ConvertFrom-Json
    Invoke-EffortHours @(
        'review', 'query', $RepositoryPath,
        '--profile', 'implementation',
        '--input-digest', $Packet.inputDigest,
        '--capability', $Sample.QueryTarget,
        '--reason', 'Inspect the dominant compact-review capability without source.',
        '--compact',
        '--output', $QueryPath
    )
    Normalize-JsonFile $QueryPath
    $Record = @($Corpus.records | Where-Object {
        $_.repository.sourceDigest -eq $Packet.repository.sourceDigest -and
        $_.profile -eq $Packet.profile
    })
    if ($Record.Count -ne 1) {
        throw "Expected one public corpus record for '$($Sample.Name)', found $($Record.Count)."
    }

    $CompactLedgerPath = Join-Path $LedgerRoot ($Sample.Name + '.compact-adjustment.json')
    $ReferenceLedgerPath = Join-Path $LedgerRoot ($Sample.Name + '.source-adjustment.json')
    Write-Json (New-CompactLedger $Packet $Sample.Name) $CompactLedgerPath
    Write-Json (New-ReferenceLedger $Packet $Record[0]) $ReferenceLedgerPath

    Invoke-EffortHours @('review', 'validate', $PacketPath, $CompactLedgerPath, '--compact')
    Invoke-EffortHours @('review', 'validate', $PacketPath, $ReferenceLedgerPath, '--compact')

    $CompactMeasurementPath = Join-Path $MeasurementRoot ($Sample.Subject + '.compact.json')
    $SourceMeasurementPath = Join-Path $MeasurementRoot ($Sample.Subject + '.broader-source.json')
    Invoke-EffortHours @(
        'review', 'measure', $PacketPath, $CompactLedgerPath,
        '--subject', $Sample.Subject,
        '--session', ('m8-compact-' + $Sample.Subject.Substring($Sample.Subject.Length - 1)),
        '--context', 'compact',
        '--query-result', $QueryPath,
        '--source-seen-before',
        '--reference-seen-before',
        '--note', 'This was not a blind context-isolation run; broader source and aggregate reference results were available beforehand.',
        '--compact',
        '--output', $CompactMeasurementPath
    )
    Normalize-JsonFile $CompactMeasurementPath
    Invoke-EffortHours @(
        'review', 'measure', $PacketPath, $ReferenceLedgerPath,
        '--subject', $Sample.Subject,
        '--session', ('m8-source-' + $Sample.Subject.Substring($Sample.Subject.Length - 1)),
        '--context', 'broader-source',
        '--independent-reviewer',
        '--note', 'Frozen public teacher targets predate the paired compact review; provider usage, duration, and exact additional-context size were not recorded.',
        '--compact',
        '--output', $SourceMeasurementPath
    )
    Normalize-JsonFile $SourceMeasurementPath
    $MeasurementPaths += $CompactMeasurementPath
    $MeasurementPaths += $SourceMeasurementPath
}

$BenchmarkPath = Join-Path $OutputRoot '0.1.0.benchmark.json'
Invoke-EffortHours (@('review', 'benchmark') + $MeasurementPaths + @(
    '--compact',
    '--output', $BenchmarkPath
))
Normalize-JsonFile $BenchmarkPath

Write-Output "Generated $BenchmarkPath"
