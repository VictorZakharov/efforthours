[CmdletBinding()]
param(
    [Parameter(Mandatory)] [ValidateSet("windows-latest", "ubuntu-latest", "macos-latest")] [string] $OperatingSystem,
    [Parameter(Mandatory)] [ValidateRange(1, 8)] [int] $Count,
    [Parameter(Mandatory)] [string] $Directory,
    [Parameter(Mandatory)] [string] $Head
)
. "$PSScriptRoot/e2e-shard-common.ps1"
foreach ($name in @("GITHUB_REPOSITORY", "GITHUB_RUN_ID", "GITHUB_RUN_ATTEMPT", "GH_TOKEN")) {
    if (-not [Environment]::GetEnvironmentVariable($name)) { throw "Missing coordinator environment: $name" }
}
$endpoint = "repos/$env:GITHUB_REPOSITORY/actions/runs/$env:GITHUB_RUN_ID/attempts/$env:GITHUB_RUN_ATTEMPT/jobs?per_page=100"
$deadline = [DateTime]::UtcNow.AddMinutes(10)
do {
    $response = & gh api $endpoint
    if ($LASTEXITCODE -ne 0) { throw "Unable to read current-attempt E2E peer status." }
    $jobs = ($response | ConvertFrom-Json).jobs
    if (Test-E2ePeersCompleted -Jobs $jobs -OperatingSystem $OperatingSystem -Count $Count) { break }
    if ([DateTime]::UtcNow -ge $deadline) { throw "Timed out waiting for complete E2E peer jobs." }
    Start-Sleep -Seconds 5
} while ($true)
$pattern = "e2e-$env:GITHUB_RUN_ID-$env:GITHUB_RUN_ATTEMPT-$OperatingSystem-*"
& gh run download $env:GITHUB_RUN_ID --repo $env:GITHUB_REPOSITORY --pattern $pattern --dir $Directory
if ($LASTEXITCODE -ne 0) { throw "Unable to download exact-attempt E2E coverage receipts." }
& "$PSScriptRoot/verify-e2e-shards.ps1" -Directory $Directory -Count $Count -Head $Head
