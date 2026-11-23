[CmdletBinding()]
param(
    # Directory containing downloaded `e2e-<run>-<attempt>-<os>-<shard>` receipt artifacts
    # from one or more complete CI runs, for example from
    # `gh run download <run> --pattern "e2e-*" --dir <directory>`.
    [Parameter(Mandatory)] [string] $Directory,
    [string] $OutputPath = (Join-Path $PSScriptRoot "e2e-shard-weights.json"),
    [ValidateRange(1, 60)] [int] $MinimumSeconds = 2
)
. "$PSScriptRoot/e2e-shard-common.ps1"
$files = @(Get-ChildItem -LiteralPath $Directory -Filter receipt.json -Recurse -File)
if ($files.Count -eq 0) { throw "No E2E shard receipts found." }
# A method runs in exactly one shard per run and OS, so the maximum per-receipt method
# total is the slowest observed platform duration across all supplied runs.
$seconds = [Collections.Generic.Dictionary[string, double]]::new([StringComparer]::Ordinal)
foreach ($file in $files) {
    $receipt = Get-Content -LiteralPath $file.FullName -Raw -Encoding utf8 | ConvertFrom-Json
    if ($receipt.schemaVersion -cne "e2e-shard/1.0.0") { throw "Unsupported E2E receipt: $($file.FullName)" }
    $methodTotals = [Collections.Generic.Dictionary[string, double]]::new([StringComparer]::Ordinal)
    foreach ($result in @($receipt.results)) {
        $method = Get-E2eMethod -Case ([string] $result.name)
        $duration = [TimeSpan]::Parse([string] $result.duration, [Globalization.CultureInfo]::InvariantCulture).TotalSeconds
        $previous = 0.0
        if ($methodTotals.ContainsKey($method)) { $previous = $methodTotals[$method] }
        $methodTotals[$method] = $previous + $duration
    }
    foreach ($method in $methodTotals.Keys) {
        if (-not $seconds.ContainsKey($method) -or $methodTotals[$method] -gt $seconds[$method]) {
            $seconds[$method] = $methodTotals[$method]
        }
    }
}
$names = [Collections.Generic.List[string]]::new()
foreach ($method in $seconds.Keys) {
    if ([Math]::Ceiling($seconds[$method]) -ge $MinimumSeconds) { $names.Add($method) }
}
$names.Sort([StringComparer]::Ordinal)
$lines = [Collections.Generic.List[string]]::new()
$lines.Add("{")
$lines.Add('  "schemaVersion": "e2e-shard-weights/1.0.0",')
$lines.Add('  "methods": {')
for ($index = 0; $index -lt $names.Count; $index++) {
    $separator = if ($index -lt $names.Count - 1) { "," } else { "" }
    $lines.Add("    `"$($names[$index])`": $([int] [Math]::Ceiling($seconds[$names[$index]]))$separator")
}
$lines.Add("  }")
$lines.Add("}")
[IO.File]::WriteAllText($OutputPath, ($lines -join "`n") + "`n", [Text.UTF8Encoding]::new($false))
Write-Output "Wrote $($names.Count) E2E method weights from $($files.Count) receipts to $OutputPath."
