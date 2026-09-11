function Get-DataverseTableRowCount {
    <#
.EXTERNALHELP PSDataverse.PowerShell-Help.xml
#>
    [CmdletBinding()]
    param (
        [Parameter(Mandatory=$true)]
        [String]$TableName,
        [Parameter(Mandatory=$false)]
        [string]$Filter = ""
    )
    Write-Progress -Activity "Counting rows"
    $meta = Send-DataverseOperation "EntityDefinitions(LogicalName='$TableName')?`$select=LogicalCollectionName,PrimaryIdAttribute" | Select-Object -ExpandProperty Content | ConvertFrom-Json
    $uri = $meta | Select-Object -ExpandProperty LogicalCollectionName
    $primaryAttr = $meta | Select-Object -ExpandProperty PrimaryIdAttribute
    $uri += "?`$count=true&`$top=1&`$select=$primaryAttr"
    if ($Filter -ne "") { $uri += "&`$filter=$($Filter)" }
    $resp = Send-DataverseOperation $uri | Select-Object -ExpandProperty Content | ConvertFrom-Json
    $count = $resp | Select-Object -ExpandProperty "@odata.count"
    Write-Progress -Activity "Counting rows" -Completed
    return $count
}
