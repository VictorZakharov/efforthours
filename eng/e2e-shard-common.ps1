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
