[CmdletBinding()]
param(
    [Parameter()]
    [string] $Commit = "HEAD"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Invoke-Git {
    param(
        [Parameter(Mandatory = $true)]
        [string[]] $Arguments
    )

    $output = @(& git @Arguments 2>&1)
    if ($LASTEXITCODE -ne 0) {
        $details = $output -join [Environment]::NewLine
        throw "git $($Arguments -join ' ') failed.$([Environment]::NewLine)$details"
    }

    return ($output -join [Environment]::NewLine).Trim()
}

$mergeCommit = Invoke-Git -Arguments @(
    "rev-parse",
    "--verify",
    "$Commit`^{commit}"
)
$parentLine = Invoke-Git -Arguments @(
    "show",
    "--no-patch",
    "--format=%P",
    $mergeCommit
)
$parents = @([regex]::Split($parentLine.Trim(), "\s+") | Where-Object { $_ })

if ($parents.Count -ne 2) {
    [Console]::Error.WriteLine(
        "Release commit '$mergeCommit' must have exactly two parents; found $($parents.Count).")
    exit 2
}

$pullRequestHead = $parents[1]
$mergeTree = Invoke-Git -Arguments @(
    "rev-parse",
    "$mergeCommit`^{tree}"
)
$pullRequestTree = Invoke-Git -Arguments @(
    "rev-parse",
    "$pullRequestHead`^{tree}"
)

$treeMatchesPullRequestHead = $mergeTree -eq $pullRequestTree
$validationCommit = if ($treeMatchesPullRequestHead) {
    $pullRequestHead
}
else {
    $mergeCommit
}

[Console]::Out.WriteLine("merge_commit=$mergeCommit")
[Console]::Out.WriteLine("pr_head=$pullRequestHead")
[Console]::Out.WriteLine("tree=$mergeTree")
[Console]::Out.WriteLine(
    "tree_matches_pr_head=$($treeMatchesPullRequestHead.ToString().ToLowerInvariant())")
[Console]::Out.WriteLine("validation_commit=$validationCommit")
