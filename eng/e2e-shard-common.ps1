Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Matches MaxParallelThreads in e2e-shard.runsettings.
$script:E2eShardThreads = 6

function Get-E2eMethod {
    param([string] $Case)
    if ($Case -notmatch '^(EffortHours\.EndToEndTests\.[A-Za-z0-9_]+\.[A-Za-z0-9_]+)(?:\(|$)') {
        throw "Unrecognized E2E case identity: $Case"
    }
    return $Matches[1]
}

function Read-E2eShardWeights {
    param([Parameter(Mandatory)] [string] $Path)
    $document = Get-Content -LiteralPath $Path -Raw -Encoding utf8 | ConvertFrom-Json
    if ($document.schemaVersion -cne "e2e-shard-weights/1.0.0") { throw "Unsupported E2E shard weight schema." }
    $weights = [Collections.Generic.Dictionary[string, int]]::new([StringComparer]::Ordinal)
    foreach ($property in $document.methods.PSObject.Properties) {
        $null = Get-E2eMethod -Case $property.Name
        $seconds = [int] $property.Value
        if ($seconds -lt 1 -or $seconds -gt 3600) { throw "Invalid E2E shard weight: $($property.Name)" }
        $weights[$property.Name] = $seconds
    }
    return , $weights
}

# Assigns whole test methods to shards from measured weights (unlisted methods weigh one
# second). Methods of one class share an xUnit collection and run serially, so a shard's
# estimate is its longest class chain or its total divided by the runner's parallel
# threads, whichever is larger. Heaviest methods are placed first, each on the shard with
# the smallest resulting estimate, then total, then index. Every input is ordered
# ordinally, so each shard and the aggregate verifier compute the same assignment.
function Get-E2eShardAssignment {
    param(
        [Parameter(Mandatory)] [string[]] $Cases,
        [Parameter(Mandatory)] [int] $Count,
        [string] $WeightsPath = (Join-Path $PSScriptRoot "e2e-shard-weights.json")
    )
    $weights = Read-E2eShardWeights -Path $WeightsPath
    $methods = [Collections.Generic.SortedSet[string]]::new([StringComparer]::Ordinal)
    foreach ($case in $Cases) { $null = $methods.Add((Get-E2eMethod -Case $case)) }
    $items = @(foreach ($method in $methods) {
        $weight = 1
        if ($weights.ContainsKey($method)) { $weight = $weights[$method] }
        [pscustomobject] @{ Method = $method; Weight = $weight }
    })
    $ordered = @($items | Sort-Object -Property @{ Expression = "Weight"; Descending = $true } -Stable)
    $chains = @(for ($index = 0; $index -lt $Count; $index++) {
        , [Collections.Generic.Dictionary[string, int]]::new([StringComparer]::Ordinal)
    })
    $longest = [int[]]::new($Count)
    $totals = [int[]]::new($Count)
    $assignment = [Collections.Generic.Dictionary[string, int]]::new([StringComparer]::Ordinal)
    foreach ($item in $ordered) {
        $class = $item.Method.Substring(0, $item.Method.LastIndexOf('.'))
        $best = -1
        $bestEstimate = 0
        $bestTotal = 0
        $bestChain = 0
        for ($index = 0; $index -lt $Count; $index++) {
            $chain = $item.Weight
            if ($chains[$index].ContainsKey($class)) { $chain += $chains[$index][$class] }
            $total = $totals[$index] + $item.Weight
            $estimate = [Math]::Max([Math]::Max($chain, $longest[$index]) * $script:E2eShardThreads, $total)
            if ($best -lt 0 -or $estimate -lt $bestEstimate -or ($estimate -eq $bestEstimate -and $total -lt $bestTotal)) {
                $best = $index
                $bestEstimate = $estimate
                $bestTotal = $total
                $bestChain = $chain
            }
        }
        $chains[$best][$class] = $bestChain
        $longest[$best] = [Math]::Max($longest[$best], $bestChain)
        $totals[$best] = $bestTotal
        $assignment[$item.Method] = $best
    }
    return , $assignment
}

function Get-E2eShardCases {
    param([Parameter(Mandatory)] [string[]] $Cases, [Parameter(Mandatory)] [int] $Count, [Parameter(Mandatory)] [int] $Index)
    $assignment = Get-E2eShardAssignment -Cases $Cases -Count $Count
    return @($Cases | Where-Object { $assignment[(Get-E2eMethod -Case $_)] -eq $Index })
}

function Assert-E2eCases {
    param([string[]] $Expected, [string[]] $Actual)
    $expectedSet = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $actualSet = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($name in $Expected) {
        if (-not $expectedSet.Add($name)) { throw "Duplicate discovered E2E case: $name" }
    }
    foreach ($name in $Actual) {
        if (-not $actualSet.Add($name)) { throw "Duplicate executed E2E case: $name" }
    }
    if (-not $expectedSet.SetEquals($actualSet)) {
        throw "E2E coverage mismatch: expected $($Expected.Count), executed $($Actual.Count)."
    }
}

function Test-E2ePeersCompleted {
    param([object[]] $Jobs, [string] $OperatingSystem, [int] $Count)
    $complete = $true
    for ($index = 1; $index -lt $Count; $index++) {
        $name = "E2E shard ($OperatingSystem, $index)"
        $matches = @($Jobs | Where-Object { $_.name -ceq $name })
        if ($matches.Count -gt 1) { throw "Duplicate E2E peer job: $name" }
        if ($matches.Count -eq 0 -or $matches[0].status -cne "completed") {
            $complete = $false
            continue
        }
        if ($matches[0].conclusion -cne "success") { throw "E2E peer did not pass: $name" }
    }
    return $complete
}
