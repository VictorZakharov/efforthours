function Invoke-RestMethod { param($Value) return $Value }
function Convert-Value {
    param([string] $Value)
    if ($Value) { return Invoke-RestMethod $Value }
}
Export-ModuleMember -Function Convert-Value
