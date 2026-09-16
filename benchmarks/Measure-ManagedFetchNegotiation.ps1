# Explicit local checkpoint; ordinary CI does not gate wall-clock measurements.
# Synthetic fixture and harness are provided under the repository MIT license.
param(
    [ValidateRange(1, 20)] [int] $Iterations = 5,
    [ValidateRange(1, 256)] [int] $BlobCount = 64,
    [ValidateRange(1, 1024)] [int] $BlobKiB = 256,
    [string] $OutputRoot = (Join-Path $PSScriptRoot '../artifacts/managed-fetch-benchmark')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$runRoot = Join-Path ([IO.Path]::GetFullPath($OutputRoot)) ([Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($runRoot) | Out-Null
$emptyConfig = Join-Path $runRoot 'empty.gitconfig'
[IO.File]::WriteAllText($emptyConfig, '')
$emptyHooks = Join-Path $runRoot 'hooks'
[IO.Directory]::CreateDirectory($emptyHooks) | Out-Null

function Invoke-Git([string] $Directory, [string[]] $Arguments) {
    $start = [Diagnostics.ProcessStartInfo]::new('git')
    $start.WorkingDirectory = $Directory
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.Environment['GIT_CONFIG_NOSYSTEM'] = '1'
    $start.Environment['GIT_CONFIG_GLOBAL'] = $emptyConfig
    $start.Environment['GIT_TERMINAL_PROMPT'] = '0'
    $start.Environment['LC_ALL'] = 'C'
    $start.Environment['GIT_AUTHOR_DATE'] = '2026-09-16T12:00:00Z'
    $start.Environment['GIT_COMMITTER_DATE'] = '2026-09-16T12:00:00Z'
    foreach ($argument in @('-c', "core.hooksPath=$emptyHooks", '-c', 'gc.auto=0', '-c', 'maintenance.auto=false') + $Arguments) {
        $start.ArgumentList.Add($argument)
    }
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $process = [Diagnostics.Process]::Start($start)
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $process.WaitForExit()
        $output = $stdout.GetAwaiter().GetResult()
        $errorOutput = $stderr.GetAwaiter().GetResult()
        $timer.Stop()
        if ($process.ExitCode -ne 0) { throw "Git failed: $errorOutput" }
        return [pscustomobject]@{ Output = $output; Error = $errorOutput; Seconds = $timer.Elapsed.TotalSeconds }
    }
    finally { $process.Dispose() }
}

$source = Join-Path $runRoot 'source'
[IO.Directory]::CreateDirectory($source) | Out-Null
$null = Invoke-Git $source @('init', '--initial-branch=main')
$null = Invoke-Git $source @('config', 'user.name', 'Synthetic Benchmark')
$null = Invoke-Git $source @('config', 'user.email', 'synthetic@example.test')
$random = [Random]::new(1729)
$bytes = [byte[]]::new($BlobKiB * 1024)
for ($index = 0; $index -lt $BlobCount; $index++) {
    $random.NextBytes($bytes)
    [IO.File]::WriteAllBytes((Join-Path $source "payload-$index.bin"), $bytes)
}
$null = Invoke-Git $source @('add', '--all')
$null = Invoke-Git $source @('-c', 'commit.gpgSign=false', 'commit', '-m', 'synthetic base')
$base = (Invoke-Git $source @('rev-parse', 'HEAD')).Output.Trim()
[IO.File]::WriteAllText((Join-Path $source 'Feature.cs'), "namespace Demo; public sealed class Feature { }`n")
$null = Invoke-Git $source @('add', '--all')
$null = Invoke-Git $source @('-c', 'commit.gpgSign=false', 'commit', '-m', 'synthetic advance')
$head = (Invoke-Git $source @('rev-parse', 'HEAD')).Output.Trim()
$expectedGraph = (Invoke-Git $source @('rev-list', '--objects', $head)).Output
$uri = [Uri]::new($source + [IO.Path]::DirectorySeparatorChar).AbsoluteUri
$fetch = @('-c', 'credential.helper=', '-c', 'credential.helper=!gh auth git-credential',
    'fetch', '--no-tags', '--no-write-fetch-head', '--no-recurse-submodules', '--progress')

# Every timed command starts from an independent ref-free cache holding the base.
# Prepare all caches before timing; run one discarded pair to warm executable/OS caches.
$cases = @()
for ($iteration = 0; $iteration -le $Iterations; $iteration++) {
    $order = if ($iteration % 2 -eq 0) { @('before', 'after') } else { @('after', 'before') }
    foreach ($variant in $order) {
        $cache = Join-Path $runRoot "$iteration-$variant.git"
        [IO.Directory]::CreateDirectory($cache) | Out-Null
        $null = Invoke-Git $cache @('init', '--bare')
        $null = Invoke-Git $cache ($fetch + @($uri, $base))
        $cases += [pscustomobject]@{ Iteration = $iteration; Variant = $variant; Cache = $cache }
    }
}

$samples = @()
$warmup = @()
foreach ($case in $cases) {
    $tips = if ($case.Variant -eq 'after') { @("--negotiation-tip=$base") } else { @() }
    $result = Invoke-Git $case.Cache ($fetch + $tips + @($uri, 'refs/heads/main'))
    $sent = [regex]::Match($result.Error, 'Total (\d+)')
    if (-not $sent.Success) { throw 'Sender object total missing from Git progress.' }
    $expectedCount = if ($case.Variant -eq 'after') { 3 } else { $BlobCount + 5 }
    if ([int]$sent.Groups[1].Value -ne $expectedCount) { throw 'Unexpected transferred object count.' }
    if ((Invoke-Git $case.Cache @('rev-list', '--objects', $head)).Output -cne $expectedGraph) {
        throw 'Acquired immutable graph differs from source.'
    }
    if ((Invoke-Git $case.Cache @('for-each-ref', '--format=%(refname)')).Output.Trim() -ne '' -or
        (Test-Path (Join-Path $case.Cache 'FETCH_HEAD')) -or (Test-Path (Join-Path $case.Cache 'index')) -or
        (Invoke-Git $case.Cache @('rev-parse', '--is-bare-repository')).Output.Trim() -ne 'true') {
        throw 'Fetch created mutable selection state.'
    }
    $sample = [ordered]@{
        iteration = $case.Iteration
        variant = $case.Variant
        seconds = [Math]::Round($result.Seconds, 6)
        sentObjects = [int]$sent.Groups[1].Value
        graphEqual = $true
        localRefs = 0
    }
    if ($case.Iteration -eq 0) { $warmup += $sample } else { $samples += $sample }
}

$summary = foreach ($variant in @('before', 'after')) {
    $times = @($samples | Where-Object { $_.variant -eq $variant } | ForEach-Object { $_.seconds } | Sort-Object)
    $middle = [int][Math]::Floor($times.Count / 2)
    $median = if ($times.Count % 2 -eq 0) { ($times[$middle - 1] + $times[$middle]) / 2 } else { $times[$middle] }
    [ordered]@{ variant = $variant; medianSeconds = $median; minSeconds = $times[0]; maxSeconds = $times[-1] }
}
$report = [ordered]@{
    checkpoint = 'managed-fetch-negotiation/1.0.0'
    measuredAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
    gitVersion = (Invoke-Git $source @('--version')).Output.Trim()
    powershellVersion = $PSVersionTable.PSVersion.ToString()
    os = [Environment]::OSVersion.VersionString
    logicalProcessors = [Environment]::ProcessorCount
    transport = 'file://'
    payloadBytes = $BlobCount * $BlobKiB * 1024
    baseObjects = $BlobCount + 2
    newObjects = 3
    timerScope = 'Git process startup through completion; excludes fixture setup, cache preparation, verification, and application wrapper/analysis'
    discardedWarmup = $warmup
    samples = $samples
    summary = @($summary)
}
$json = $report | ConvertTo-Json -Depth 8
[IO.File]::WriteAllText((Join-Path $runRoot 'result.json'), $json + "`n")
$json
