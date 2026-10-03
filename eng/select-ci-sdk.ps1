Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $false
if (-not $env:GITHUB_OUTPUT) { throw "GITHUB_OUTPUT is required." }
$available = $false
if (Get-Command dotnet -ErrorAction SilentlyContinue) {
    $version = & dotnet --version 2>$null
    $available = $LASTEXITCODE -eq 0
    if ($available) { Write-Output "Using installed SDK selected by global.json: $version" }
}
$packages = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else {
    [IO.Path]::Combine([Environment]::GetFolderPath("UserProfile"), ".nuget", "packages")
}
"available=$($available.ToString().ToLowerInvariant())" | Out-File -LiteralPath $env:GITHUB_OUTPUT -Encoding utf8 -Append
"nuget_cache_path=$($packages.TrimEnd('/', '\'))$([IO.Path]::DirectorySeparatorChar)" | Out-File -LiteralPath $env:GITHUB_OUTPUT -Encoding utf8 -Append
$global:LASTEXITCODE = 0
