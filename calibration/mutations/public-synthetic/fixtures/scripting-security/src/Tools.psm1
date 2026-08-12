function Convert-Value {
    param([string] $ApiToken)
    if ($ApiToken) { return ConvertTo-SecureString $ApiToken -AsPlainText -Force }
}
Export-ModuleMember -Function Convert-Value
