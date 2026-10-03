Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-E2eShard {
    param([string] $Case, [int] $Count)
    if ($Case -notmatch '^(EffortHours\.EndToEndTests\.[A-Za-z0-9_]+\.[A-Za-z0-9_]+)(?:\(|$)') {
        throw "Unrecognized E2E case identity: $Case"
    }
    $hash = [System.Security.Cryptography.SHA256]::Create()
    try { $bytes = $hash.ComputeHash([Text.Encoding]::UTF8.GetBytes($Matches[1])) }
    finally { $hash.Dispose() }
    return [int] ($bytes[0] % $Count)
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
