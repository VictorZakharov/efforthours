[CmdletBinding()]
param(
    [ValidateRange(0, 7)] [int] $Index,
    [ValidateRange(1, 8)] [int] $Count,
    [Parameter(Mandatory)] [string] $OutputDirectory
)
. "$PSScriptRoot/e2e-shard-common.ps1"
if ($Index -ge $Count) { throw "Shard index must be below shard count." }
$project = "tests/EffortHours.EndToEndTests/EffortHours.EndToEndTests.csproj"
$arguments = @("test", $project, "--no-build", "--no-restore", "--configuration", "Release")
$listing = @(& dotnet @arguments --list-tests)
if ($LASTEXITCODE -ne 0) { throw "E2E discovery failed." }
$inventory = @($listing | ForEach-Object { $_.Trim() } |
    Where-Object { $_.StartsWith("EffortHours.EndToEndTests.", [StringComparison]::Ordinal) })
if ($inventory.Count -eq 0) { throw "No E2E tests were discovered." }
Assert-E2eCases -Expected $inventory -Actual $inventory
$expected = @($inventory | Where-Object { (Get-E2eShard -Case $_ -Count $Count) -eq $Index })
if ($expected.Count -eq 0) { throw "Empty E2E shard." }
$methods = @($expected | ForEach-Object { ($_ -split "\(", 2)[0] } | Sort-Object -Unique)
$filter = ($methods | ForEach-Object { "FullyQualifiedName=$_" }) -join "|"
$null = New-Item -ItemType Directory -Force -Path $OutputDirectory
& dotnet @arguments --filter $filter --logger "trx;LogFileName=results.trx" --results-directory $OutputDirectory
$testExit = $LASTEXITCODE
$path = Join-Path $OutputDirectory "results.trx"
if (-not (Test-Path -LiteralPath $path)) { throw "E2E shard emitted no result." }
[xml] $trx = Get-Content -LiteralPath $path -Raw -Encoding utf8
$rows = @($trx.TestRun.Results.UnitTestResult)
$results = @($rows | ForEach-Object {
    [ordered] @{ name = [string] $_.testName; outcome = [string] $_.outcome; duration = [string] $_.duration }
})
$head = if ($env:GITHUB_SHA) { $env:GITHUB_SHA } else { (& git rev-parse HEAD).Trim() }
$receipt = [ordered] @{
    schemaVersion = "e2e-shard/1.0.0"; head = $head; index = $Index; count = $Count
    inventory = $inventory; results = $results
}
$receipt | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutputDirectory "receipt.json") -Encoding utf8
Assert-E2eCases -Expected $expected -Actual @($results | ForEach-Object { $_.name })
if ($testExit -ne 0 -or @($results | Where-Object { $_.outcome -ne "Passed" }).Count -ne 0) {
    throw "E2E shard failed or skipped tests."
}
Write-Output "E2E shard $Index/$Count passed $($results.Count) of $($inventory.Count) discovered cases."
