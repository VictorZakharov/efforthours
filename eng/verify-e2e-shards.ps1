[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Directory,
    [Parameter(Mandatory)] [ValidateRange(1, 8)] [int] $Count,
    [Parameter(Mandatory)] [string] $Head
)
. "$PSScriptRoot/e2e-shard-common.ps1"
$files = @(Get-ChildItem -LiteralPath $Directory -Filter receipt.json -Recurse -File)
if ($files.Count -ne $Count) { throw "Missing or extra E2E shard receipts." }
$receipts = @($files | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw -Encoding utf8 | ConvertFrom-Json })
$indices = [Collections.Generic.HashSet[int]]::new()
$inventory = @($receipts[0].inventory)
if ($inventory.Count -eq 0) { throw "Empty E2E inventory." }
$all = [Collections.Generic.List[string]]::new()
foreach ($receipt in $receipts) {
    if ($receipt.schemaVersion -ne "e2e-shard/1.0.0" -or $receipt.head -cne $Head -or $receipt.count -ne $Count) {
        throw "E2E receipt has incompatible schema, head or shard count."
    }
    if ($receipt.index -lt 0 -or $receipt.index -ge $Count -or -not $indices.Add($receipt.index)) {
        throw "Invalid or duplicate E2E shard index."
    }
    Assert-E2eCases -Expected $inventory -Actual @($receipt.inventory)
    $expected = @($inventory | Where-Object { (Get-E2eShard -Case $_ -Count $Count) -eq $receipt.index })
    if ($expected.Count -eq 0) { throw "Empty E2E shard." }
    $actual = @($receipt.results | ForEach-Object {
        if ($_.outcome -cne "Passed") { throw "E2E receipt contains failed or skipped cases." }
        $all.Add([string] $_.name)
        [string] $_.name
    })
    Assert-E2eCases -Expected $expected -Actual $actual
}
Assert-E2eCases -Expected $inventory -Actual @($all)
Write-Output "Verified exact once-only coverage of $($all.Count) E2E cases across $Count current-head shards."
