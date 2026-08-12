# Equivalent layout with an ordinary comment.
function Convert-Value
{
    param(
        [string] $Value
    )
    if ($Value)
    {
        return $Value.Trim()
    }
}
Export-ModuleMember -Function Convert-Value
